using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using HyPlayer.Classes;
using HyPlayer.HyPlayControl;
using HyPlayer.NeteaseApi.ApiContracts;
using HyPlayer.NeteaseApi.ApiContracts.Playlist;
using HyPlayer.NeteaseApi.ApiContracts.Song;
using Microsoft.Web.WebView2.Core;

namespace HyPlayer.Pages;

// Only metadata and validated song IDs cross this bridge. Playback stays in HyPlayer.
public sealed partial class MusicAtlasPage : Page
{
    private const string AtlasUrl = "https://music-atlas.hyplayer.local/index.html";
    private readonly CancellationTokenSource _lifetime = new();
    private readonly Dictionary<string, NCSong> _songs = new(StringComparer.Ordinal);
    private NCPlayList? _playlist;
    private JsonObject? _snapshot;
    private bool _initialized;
    private bool _loading;
    private bool _playing;
    private bool _closed;
    private readonly MusicAtlasGenres _genres = new();
    private bool _genreLoading;
    private string _phase = "initialize";

    public MusicAtlasPage() => InitializeComponent();

    protected override void OnNavigatedTo(NavigationEventArgs e)
    {
        base.OnNavigatedTo(e);
        _playlist = e.Parameter as NCPlayList;
    }

    private async void Atlas_Loaded(object sender, RoutedEventArgs e)
    {
        if (_initialized || _closed) return;
        try
        {
            await Atlas.EnsureCoreWebView2Async();
            if (_closed) return;
            var core = Atlas.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("music-atlas.hyplayer.local",
                Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "MusicAtlas"),
                CoreWebView2HostResourceAccessKind.Deny);
            core.NavigationStarting += (_, args) => args.Cancel = args.Uri != AtlasUrl;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.WebMessageReceived += OnMessage;
            core.NavigationCompleted += (_, args) =>
            {
                if (_closed) return;
                if (!args.IsSuccess) ShowError("地图页面加载失败：" + args.WebErrorStatus);
                else SendSnapshot();
            };
            _initialized = true;
            MusicAtlasState.Changed += SendState;
            await MusicAtlasState.ReadAsync();
            await _genres.InitializeAsync();
            core.Navigate(AtlasUrl);
            await LoadPlaylistAsync();
        }
        catch (Exception ex) { if (!_closed) ReportError(ex); }
    }

    private async Task LoadPlaylistAsync()
    {
        if (_loading || _closed) return;
        _loading = true;
        Retry.Visibility = Visibility.Collapsed;
        Status.Text = "正在加载完整歌单…";
        try
        {
            if (_playlist == null || string.IsNullOrEmpty(_playlist.plid))
                throw new InvalidOperationException("请从歌单页打开音乐地图。");
            _phase = "playlist-cache";
            var cached = await MusicAtlasPlaylistCache.ReadAsync(_playlist.plid);
            cached ??= new MusicAtlasPlaylistCache.Entry();
            cached.Songs ??= new List<NCSong>();
            if (_closed) return;
            if (cached.Songs.Count > 0)
            {
                foreach (var song in cached.Songs) _songs["ncm-song-" + song.sid] = song;
                Publish(cached.Songs);
                Status.Text = $"{_playlist.name} · {cached.Songs.Count} 首 · 已显示缓存，正在检查歌单变化…";
            }
            _phase = "playlist-index";
            var detail = await Common.NeteaseAPI.RequestAsync(NeteaseApis.PlaylistTracksGetApi,
                new PlaylistTracksGetRequest { Id = _playlist.plid }, _lifetime.Token);
            if (detail.IsError) throw new InvalidOperationException(detail.Error?.Message ?? "加载歌单失败");
            var ids = detail.Value?.Playlist?.TrackIds?.Select(x => x.Id).ToList();
            if (ids == null) throw new InvalidOperationException("未返回完整歌单目录");
            var songs = new Dictionary<string, NCSong>(StringComparer.Ordinal);
            var wanted = new HashSet<string>(ids.Select(x => "ncm-song-" + x));
            if (cached.Fresh) foreach (var item in _songs.Where(x => wanted.Contains(x.Key))) songs[item.Key] = item.Value;
            var fetchIds = ids.Where(id => !songs.ContainsKey("ncm-song-" + id)).ToList();
            var omitted = 0;
            for (var offset = 0; offset < fetchIds.Count; offset += 500)
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                var batch = fetchIds.Skip(offset).Take(500).ToList();
                _phase = "song-details";
                var result = await Common.NeteaseAPI.RequestAsync(NeteaseApis.SongDetailApi,
                    new SongDetailRequest { IdList = batch }, _lifetime.Token);
                if (result.IsError) throw new InvalidOperationException(result.Error?.Message ?? "加载歌曲详情失败");
                if (result.Value?.Songs == null) throw new InvalidOperationException("歌曲详情未返回，请重试");
                var mapped = result.Value.Songs.Select(x => x.MapToNcSong()).ToDictionary(x => x.sid);
                foreach (var id in batch)
                {
                    // Keep playlist order even if the API returns details in a different order.
                    if (!mapped.TryGetValue(id, out var song)) { omitted++; continue; }
                    var key = "ncm-song-" + song.sid;
                    if (songs.ContainsKey(key)) continue;
                    songs.Add(key, song);
                }
                Status.Text = $"{_playlist.name} · 已读取 {Math.Min(offset + 500, fetchIds.Count)} / {fetchIds.Count} 首新详情…";
                if (offset + 500 < fetchIds.Count) await Task.Delay(350, _lifetime.Token);
            }
            _lifetime.Token.ThrowIfCancellationRequested();
            _songs.Clear();
            foreach (var pair in songs) _songs.Add(pair.Key, pair.Value);
            var ordered = ids.Select(id => "ncm-song-" + id).Where(songs.ContainsKey).Distinct().Select(id => songs[id]).ToList();
            _phase = "publish";
            if (_snapshot == null || !cached.Fresh || !cached.Songs.Select(x => x.sid).SequenceEqual(ordered.Select(x => x.sid))) Publish(ordered);
            Status.Text = $"{_playlist.name} · {songs.Count} 首" + (omitted > 0 ? $"（另有 {omitted} 首未返回详情）" : "");
            await MusicAtlasPlaylistCache.SaveAsync(_playlist.plid, ordered, !cached.Fresh);
            await LoadGenresAsync();
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { if (!_closed) ReportError(ex); }
        finally { _loading = false; }
    }

    private void SendSnapshot()
    {
        if (!_closed && _snapshot is not null) Atlas.CoreWebView2.PostWebMessageAsJson(_snapshot.Stringify());
    }

    private void Publish(IEnumerable<NCSong> songs)
    {
        var metadata = new JsonArray();
        foreach (var song in songs) metadata.Add(new JsonObject
        {
            ["id"] = JsonValue.CreateStringValue("ncm-song-" + song.sid),
            ["title"] = JsonValue.CreateStringValue(song.songname ?? ""),
            ["artist"] = JsonValue.CreateStringValue(string.Join(" / ", song.Artist.Select(x => x.name))),
            ["album"] = JsonValue.CreateStringValue(song.Album?.name ?? ""),
            ["styles"] = _genres.Styles(song.sid),
            ["emotions"] = _genres.Emotions(song.sid),
            ["emotionState"] = JsonValue.CreateStringValue(_genres.HasEmotions(song.sid) ? "known" : "pending"),
            ["genreState"] = JsonValue.CreateStringValue(_genres.Has(song.sid) ? "known" : "pending")
        });
        _snapshot = new JsonObject
        {
            ["type"] = JsonValue.CreateStringValue("playlist"), ["playlistId"] = JsonValue.CreateStringValue(_playlist.plid),
            ["name"] = JsonValue.CreateStringValue(_playlist.name ?? "歌单"), ["tracks"] = metadata
        };
        SendSnapshot();
    }

    private async Task LoadGenresAsync()
    {
        if (_genreLoading || _closed || _snapshot == null) return;
        _genreLoading = true;
        var failed = 0;
        try
        {
            var records = _snapshot.GetNamedArray("tracks").Select(x => x.GetObject()).ToList();
            var pending = records.Where(x => !_genres.IsFresh(x.GetNamedString("id").Substring(9))).ToList();
            for (var offset = 0; offset < pending.Count; offset += 3)
            {
                _lifetime.Token.ThrowIfCancellationRequested();
                var batch = pending.Skip(offset).Take(3).ToArray();
                var results = await Task.WhenAll(batch.Select(x => _genres.FetchAsync(x.GetNamedString("id").Substring(9), _lifetime.Token)));
                var updates = new JsonArray();
                for (var i = 0; i < batch.Length; i++)
                {
                    if (!results[i]) failed++;
                    var item = batch[i];
                    item["styles"] = _genres.Styles(item.GetNamedString("id").Substring(9));
                    item["emotions"] = _genres.Emotions(item.GetNamedString("id").Substring(9));
                    item["emotionState"] = JsonValue.CreateStringValue(results[i] ? "known" : "failed");
                    item["genreState"] = JsonValue.CreateStringValue(results[i] ? "known" : "failed");
                    updates.Add(item);
                }
                if (_closed) return;
                Atlas.CoreWebView2.PostWebMessageAsJson(new JsonObject
                {
                    ["type"] = JsonValue.CreateStringValue("genres"),
                    ["playlistId"] = JsonValue.CreateStringValue(_playlist.plid),
                    ["tracks"] = updates
                }.Stringify());
                Status.Text = $"{_playlist.name} · {_songs.Count} 首 · 查询曲风 {Math.Min(offset + 3, pending.Count)}/{pending.Count}";
                if (offset % 30 == 0) await _genres.SaveAsync();
                await Task.Delay(350, _lifetime.Token);
            }
            await _genres.SaveAsync();
            if (!_closed) Status.Text = $"{_playlist.name} · {_songs.Count} 首 · 网易云曲风" + (failed > 0 ? $"（{failed} 首查询失败，可重试）" : "");
            if (!_closed) Retry.Visibility = failed > 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        finally { _genreLoading = false; }
    }

    private async void OnMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (_closed || args.Source != AtlasUrl) return;
        var ownsPlayback = false;
        try
        {
            var message = JsonObject.Parse(args.WebMessageAsJson);
            var type = message.GetNamedString("type", "");
            if (type == "ready") { SendState(await MusicAtlasState.ReadAsync()); SendSnapshot(); return; }
            if (type == "saveState") { await MusicAtlasState.WriteAsync(message.GetNamedObject("state")); return; }
            if (type != "play" || _playing) return;
            if (message.GetNamedString("playlistId", "") != _playlist?.plid) return;
            var ids = message.GetNamedArray("ids").Select(x => x.GetString()).Distinct().ToArray();
            if (ids.Length == 0 || ids.Length > _songs.Count || ids.Any(x => !_songs.ContainsKey(x)))
                throw new InvalidOperationException("播放列表包含当前歌单以外的歌曲。");
            var first = message.GetNamedString("startId");
            if (!ids.Contains(first)) throw new InvalidOperationException("起始歌曲不在所选风格中。");
            _playing = true;
            ownsPlayback = true;
            var queue = ids.Select(x => _songs[x]).ToList();
            HyPlayList.AppendNcSongs(queue);
            HyPlayList.PlaySourceId = "content";
            var startIndex = HyPlayList.List.FindIndex(x => x.PlayItem.Id == _songs[first].sid);
            if (startIndex < 0) throw new InvalidOperationException("未能建立所选风格播放队列");
            HyPlayList.SongMoveTo(startIndex);
            if (!_closed) sender.PostWebMessageAsJson(new JsonObject
            {
                ["type"] = JsonValue.CreateStringValue("accepted"),
                ["title"] = JsonValue.CreateStringValue(_songs[first].songname)
            }.Stringify());
        }
        catch (Exception ex)
        {
            if (!_closed) sender.PostWebMessageAsJson(new JsonObject
            {
                ["type"] = JsonValue.CreateStringValue("error"),
                ["message"] = JsonValue.CreateStringValue(ex.Message)
            }.Stringify());
        }
        finally { if (ownsPlayback) _playing = false; }
    }

    private void ShowError(string message)
    {
        Status.Text = "加载失败：" + message;
        Retry.Visibility = Visibility.Visible;
    }

    private void SendState(string state)
    {
        if (_closed || !_initialized || state == null) return;
        Atlas.CoreWebView2.PostWebMessageAsJson(new JsonObject
        {
            ["type"] = JsonValue.CreateStringValue("configuration"), ["state"] = JsonObject.Parse(state)
        }.Stringify());
    }

    private async void ReportError(Exception error)
    {
        ShowError(error.Message);
        try
        {
            var file = await Windows.Storage.ApplicationData.Current.LocalFolder.CreateFileAsync("music-atlas-error.txt", Windows.Storage.CreationCollisionOption.ReplaceExisting);
            await Windows.Storage.FileIO.WriteTextAsync(file, _phase + "\n" + error.GetType().FullName + "\n" + error.StackTrace);
        }
        catch { }
    }

    private async void Retry_Click(object sender, RoutedEventArgs e)
    {
        if (!_initialized) Atlas_Loaded(sender, e);
        else { Atlas.CoreWebView2.Navigate(AtlasUrl); await LoadPlaylistAsync(); }
    }

    private void Back_Click(object sender, RoutedEventArgs e) { if (Frame.CanGoBack) Frame.GoBack(); }

    protected override void OnNavigatedFrom(NavigationEventArgs e)
    {
        _closed = true;
        _lifetime.Cancel();
        _genres.Dispose();
        MusicAtlasState.Changed -= SendState;
        if (_initialized) Atlas.CoreWebView2.WebMessageReceived -= OnMessage;
        Atlas.Close();
        base.OnNavigatedFrom(e);
    }
}
