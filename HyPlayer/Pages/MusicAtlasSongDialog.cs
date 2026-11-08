using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HyPlayer.Classes;
using HyPlayer.HyPlayControl;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Microsoft.Web.WebView2.Core;
using WebView2 = Microsoft.UI.Xaml.Controls.WebView2;

namespace HyPlayer.Pages;

internal sealed class MusicAtlasSongDialog : ContentDialog
{
    private const string Url = "https://music-atlas.hyplayer.local/index.html";
    private readonly WebView2 _web = new() { MinWidth = 260, MinHeight = 300, HorizontalAlignment = HorizontalAlignment.Stretch };
    private readonly MusicAtlasGenres _genres = new();
    private readonly CancellationTokenSource _lifetime = new();
    private readonly NCSong _song;
    private bool _closed;
    private bool _loaded;
    private bool _fetchFailed;
    private static bool _showing;

    public static async Task ShowCurrentAsync()
    {
        if (_showing) return;
        var item = HyPlayList.NowPlayingItem;
        if (item?.PlayItem == null || item.ItemType != HyPlayItemType.Netease)
        {
            await new ContentDialog { Title = "曲目分类", Content = "目前支持网易云歌曲的分类编辑。", CloseButtonText = "关闭" }.ShowAsync();
            return;
        }
        _showing = true;
        try { await new MusicAtlasSongDialog(item.ToNCSong()).ShowAsync(); }
        finally { _showing = false; }
    }

    private MusicAtlasSongDialog(NCSong song)
    {
        _song = song; // Frozen identity: playback may advance while this dialog remains open.
        Title = "曲目分类";
        CloseButtonText = "完成";
        DefaultButton = ContentDialogButton.Close;
        var bounds = Windows.UI.ViewManagement.ApplicationView.GetForCurrentView().VisibleBounds;
        Content = new Grid { Width = Math.Max(260, Math.Min(500, bounds.Width - 90)), Height = Math.Max(300, Math.Min(520, bounds.Height - 180)), Children = { _web } };
        _web.Loaded += Load;
        Closed += (_, __) =>
        {
            _closed = true;
            _lifetime.Cancel();
            MusicAtlasState.Changed -= SendState;
            _genres.Dispose();
            _web.Close();
        };
    }

    private async void Load(object sender, RoutedEventArgs e)
    {
        if (_loaded || _closed) return;
        _loaded = true;
        try
        {
            await _web.EnsureCoreWebView2Async();
            if (_closed) return;
            var core = _web.CoreWebView2;
            core.SetVirtualHostNameToFolderMapping("music-atlas.hyplayer.local", Path.Combine(Package.Current.InstalledLocation.Path, "Assets", "MusicAtlas"), CoreWebView2HostResourceAccessKind.Deny);
            core.NavigationStarting += (_, args) => args.Cancel = args.Uri != Url;
            core.NewWindowRequested += (_, args) => args.Handled = true;
            core.WebMessageReceived += OnMessage;
            MusicAtlasState.Changed += SendState;
            await MusicAtlasState.ReadAsync();
            await _genres.InitializeAsync();
            if (_closed) return;
            core.Navigate(Url);
            await RefreshAsync();
        }
        catch (Exception error) { if (!_closed) Title = "分类加载失败：" + error.Message; }
    }

    private async Task RefreshAsync()
    {
        if (_genres.IsFresh(_song.sid)) return;
        try
        {
            _fetchFailed = !await _genres.FetchAsync(_song.sid, _lifetime.Token);
            await _genres.SaveAsync();
            SendSong();
        }
        catch (OperationCanceledException) { }
    }

    private void SendSong()
    {
        if (_closed) return;
        var track = new JsonObject
        {
            ["id"] = JsonValue.CreateStringValue("ncm-song-" + _song.sid),
            ["title"] = JsonValue.CreateStringValue(_song.songname ?? ""),
            ["artist"] = JsonValue.CreateStringValue(string.Join(" / ", _song.Artist.Select(x => x.name))),
            ["album"] = JsonValue.CreateStringValue(_song.Album?.name ?? ""),
            ["styles"] = _genres.Styles(_song.sid), ["emotions"] = _genres.Emotions(_song.sid),
            ["genreState"] = JsonValue.CreateStringValue(_fetchFailed ? "failed" : _genres.Has(_song.sid) ? "known" : "pending"),
            ["emotionState"] = JsonValue.CreateStringValue(_fetchFailed ? "failed" : _genres.HasEmotions(_song.sid) ? "known" : "pending")
        };
        _web.CoreWebView2.PostWebMessageAsJson(new JsonObject
        {
            ["type"] = JsonValue.CreateStringValue("playlist"), ["editor"] = JsonValue.CreateBooleanValue(true),
            ["playlistId"] = JsonValue.CreateStringValue("song:" + _song.sid), ["name"] = JsonValue.CreateStringValue(_song.songname ?? ""),
            ["tracks"] = new JsonArray { track }
        }.Stringify());
    }

    private void SendState(string state)
    {
        if (_closed || !_loaded || state == null) return;
        _web.CoreWebView2.PostWebMessageAsJson(new JsonObject { ["type"] = JsonValue.CreateStringValue("configuration"), ["state"] = JsonObject.Parse(state) }.Stringify());
    }

    private async void OnMessage(CoreWebView2 sender, CoreWebView2WebMessageReceivedEventArgs args)
    {
        if (_closed || args.Source != Url) return;
        try
        {
            var message = JsonObject.Parse(args.WebMessageAsJson);
            switch (message.GetNamedString("type", ""))
            {
                case "ready": SendState(await MusicAtlasState.ReadAsync()); SendSong(); break;
                case "saveState": await MusicAtlasState.WriteAsync(message.GetNamedObject("state")); break;
                case "retryTags": await RefreshAsync(); break;
            }
        }
        catch (Exception error) { if (!_closed) Title = "分类保存失败：" + error.Message; }
    }
}
