using System;
using System.Threading;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Storage;

namespace HyPlayer.Pages;

// One shared document for both map and editor, independent of a WebView's lifetime.
internal static class MusicAtlasState
{
    private const string FileName = "music-atlas-state-v4.json";
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static string _state;
    private static bool _loaded;
    public static event Action<string> Changed;

    public static async Task<string> ReadAsync()
    {
        await Gate.WaitAsync();
        try
        {
            if (!_loaded)
            {
                try
                {
                    _state = await FileIO.ReadTextAsync(await ApplicationData.Current.LocalFolder.GetFileAsync(FileName));
                    JsonObject.Parse(_state);
                }
                catch { _state = null; }
                _loaded = true;
            }
            return _state;
        }
        finally { Gate.Release(); }
    }

    public static async Task WriteAsync(JsonObject state)
    {
        if (state.GetNamedNumber("schema", 0) != 4 || !state.ContainsKey("categories") || !state.ContainsKey("overrides"))
            throw new InvalidOperationException("分类配置格式不正确");
        var text = state.Stringify();
        if (text.Length > 4000000) throw new InvalidOperationException("分类配置过大");
        await Gate.WaitAsync();
        try
        {
            if (text == _state) return;
            var folder = ApplicationData.Current.LocalFolder;
            var temp = await folder.CreateFileAsync(FileName + ".tmp", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(temp, text);
            await temp.RenameAsync(FileName, NameCollisionOption.ReplaceExisting);
            _state = text;
            _loaded = true;
            Changed?.Invoke(text);
        }
        finally { Gate.Release(); }
    }
}
