using System;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Data.Json;
using Windows.Storage;

namespace HyPlayer.Pages;

// Public song metadata only; deliberately never attach the player's login cookies.
internal sealed class MusicAtlasGenres : IDisposable
{
    private static readonly SemaphoreSlim CacheGate = new(1, 1);
    private readonly HttpClient _http = new(new HttpClientHandler { UseCookies = false });
    private JsonObject _cache = new();
    private const string CacheName = "music-atlas-genres-v1.json";

    public async Task InitializeAsync()
    {
        _http.Timeout = TimeSpan.FromSeconds(20);
        try
        {
            var seed = await Package.Current.InstalledLocation.GetFileAsync("Assets\\MusicAtlas\\genres.json");
            _cache = JsonObject.Parse(await FileIO.ReadTextAsync(seed));
        }
        catch { /* A missing seed must not prevent live lookup. */ }
        try
        {
            var file = await ApplicationData.Current.LocalFolder.GetFileAsync(CacheName);
            var saved = JsonObject.Parse(await FileIO.ReadTextAsync(file));
            // Iterate keys rather than projected KeyValuePair structs on .NET Native.
            foreach (var key in saved.Keys)
                if (!_cache.ContainsKey(key) || Date(saved[key].GetObject()) >= Date(_cache[key].GetObject()))
                    _cache[key] = saved[key];
        }
        catch (Exception error)
        {
            // Keep diagnostics local; no song content or account credentials are included.
            try
            {
                var log = await ApplicationData.Current.LocalFolder.CreateFileAsync("music-atlas-genre-cache-error.txt", CreationCollisionOption.ReplaceExisting);
                await FileIO.WriteTextAsync(log, error.GetType().FullName + "\n" + error.StackTrace);
            }
            catch { }
        }
    }

    private static DateTimeOffset Date(JsonObject entry) =>
        DateTimeOffset.TryParse(entry.GetNamedString("checkedAt", ""), out var date) ? date : DateTimeOffset.MinValue;

    public bool Has(string id) => _cache.ContainsKey(id);
    public bool HasEmotions(string id) => Has(id) && _cache[id].GetObject().ContainsKey("emotions");
    public bool IsFresh(string id) => HasEmotions(id) && DateTimeOffset.UtcNow - Date(_cache[id].GetObject()) < TimeSpan.FromDays(30);
    public JsonArray Styles(string id) => Has(id) ? _cache[id].GetObject().GetNamedArray("styles", new JsonArray()) : new JsonArray();
    public JsonArray Emotions(string id) => Has(id) ? _cache[id].GetObject().GetNamedArray("emotions", new JsonArray()) : new JsonArray();
    private static readonly string[] MoodAllowlist = { "放松", "治愈", "悲伤", "平静", "活力", "思念", "浪漫", "孤独", "欢快", "苦情", "狂欢", "热血励志", "自信", "抑郁", "甜蜜" };

    public async Task<bool> FetchAsync(string id, CancellationToken cancellation)
    {
        if (!long.TryParse(id, out var numeric) || numeric <= 0) return false;
        try
        {
            using var response = await _http.GetAsync("https://music.163.com/api/song/play/about/block/page?songId=" + id, cancellation);
            response.EnsureSuccessStatusCode();
            var body = JsonObject.Parse(await response.Content.ReadAsStringAsync());
            if (body.GetNamedNumber("code", 0) != 200) return false;
            var data = body.GetNamedObject("data", new JsonObject());
            // Missing data is a failed request, not proof that a song has no genres.
            if (!data.ContainsKey("blocks")) return false;
            var styles = new JsonArray();
            var emotions = new JsonArray();
            foreach (var b in data.GetNamedArray("blocks"))
            {
                var block = b.GetObject();
                if (block.GetNamedString("code", "") != "SONG_PLAY_ABOUT_SONG_BASIC") continue;
                foreach (var c in block.GetNamedArray("creatives", new JsonArray()))
                {
                    var creative = c.GetObject();
                    var kind = creative.GetNamedString("creativeType", "");
                    if (kind != "songTag" && kind != "songBizTag") continue;
                    foreach (var r in creative.GetNamedArray("resources", new JsonArray()))
                    {
                        var title = r.GetObject().GetNamedObject("uiElement", new JsonObject())
                            .GetNamedObject("mainTitle", new JsonObject()).GetNamedString("title", "");
                        var target = kind == "songTag" ? styles : emotions;
                        if (kind == "songBizTag" && !MoodAllowlist.Contains(title)) continue;
                        if (!string.IsNullOrWhiteSpace(title) && !target.Any(x => x.GetString() == title))
                            target.Add(JsonValue.CreateStringValue(title));
                    }
                }
            }
            _cache[id] = new JsonObject
            {
                ["styles"] = styles,
                ["emotions"] = emotions,
                ["checkedAt"] = JsonValue.CreateStringValue(DateTimeOffset.UtcNow.ToString("O"))
            };
            return true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
        catch { return false; }
    }

    public async Task SaveAsync()
    {
        await CacheGate.WaitAsync();
        try
        {
            var folder = ApplicationData.Current.LocalFolder;
            // Map and song dialog may be alive together; merge instead of overwriting newer entries.
            try
            {
                var existing = JsonObject.Parse(await FileIO.ReadTextAsync(await folder.GetFileAsync(CacheName)));
                foreach (var key in existing.Keys)
                    if (!_cache.ContainsKey(key) || Date(existing[key].GetObject()) > Date(_cache[key].GetObject())) _cache[key] = existing[key];
            }
            catch { }
            var temp = await folder.CreateFileAsync(CacheName + ".tmp", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(temp, _cache.Stringify());
            await temp.RenameAsync(CacheName, NameCollisionOption.ReplaceExisting);
        }
        catch { /* Cache write failure must not affect playback. */ }
        finally { CacheGate.Release(); }
    }

    public void Dispose() => _http.Dispose();
}
