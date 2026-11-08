using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using HyPlayer.Classes;
using Windows.Data.Json;
using Windows.Storage;

namespace HyPlayer.Pages;

internal static class MusicAtlasPlaylistCache
{
    internal sealed class Entry
    {
        public List<NCSong> Songs = new();
        public bool Fresh;
    }
    private static string Name(string id)
    {
        if (!long.TryParse(id, out var number) || number <= 0) throw new ArgumentException("Invalid playlist ID");
        return "music-atlas-playlist-" + id + ".json";
    }
    private static JsonValue S(string s) => JsonValue.CreateStringValue(s ?? "");
    public static async Task<Entry> ReadAsync(string id)
    {
        try
        {
            var file = await ApplicationData.Current.LocalFolder.GetFileAsync(Name(id));
            var root = JsonObject.Parse(await FileIO.ReadTextAsync(file));
            if (root.GetNamedNumber("version", 0) != 1) return new Entry();
            var songs = root.GetNamedArray("songs").Select(v =>
            {
                var x = v.GetObject(); var al = x.GetNamedObject("album");
                return new NCSong
                {
                    sid = x.GetNamedString("id"), songname = x.GetNamedString("name"),
                    Type = HyPlayItemType.Netease, alias = x.GetNamedString("alias"), transname = x.GetNamedString("translation"),
                    CDName = x.GetNamedString("cd"), TrackId = (int)x.GetNamedNumber("track"),
                    LengthInMilliseconds = x.GetNamedNumber("duration"), IsVip = x.GetNamedBoolean("vip"),
                    IsCloud = x.GetNamedBoolean("cloud"), IsAvailable = x.GetNamedBoolean("available"),
                    Album = new NCAlbum { id = al.GetNamedString("id"), name = al.GetNamedString("name"), cover = al.GetNamedString("cover"), AlbumType = HyPlayItemType.Netease },
                    Artist = x.GetNamedArray("artists").Select(a => new NCArtist { id = a.GetObject().GetNamedString("id"), name = a.GetObject().GetNamedString("name"), Type = HyPlayItemType.Netease }).ToList()
                };
            }).ToList();
            return new Entry { Songs = songs, Fresh = DateTimeOffset.TryParse(root.GetNamedString("savedAt"), out var at) && DateTimeOffset.UtcNow - at < TimeSpan.FromDays(7) };
        }
        catch { return new Entry(); }
    }
    public static async Task SaveAsync(string id, IEnumerable<NCSong> songs, bool refreshTimestamp)
    {
        try
        {
            var array = new JsonArray();
            foreach (var s in songs)
            {
                var artists = new JsonArray();
                foreach (var a in s.Artist) artists.Add(new JsonObject { ["id"] = S(a.id), ["name"] = S(a.name) });
                array.Add(new JsonObject
                {
                    ["id"] = S(s.sid), ["name"] = S(s.songname), ["alias"] = S(s.alias), ["translation"] = S(s.transname),
                    ["cd"] = S(s.CDName), ["track"] = JsonValue.CreateNumberValue(s.TrackId), ["duration"] = JsonValue.CreateNumberValue(s.LengthInMilliseconds),
                    ["vip"] = JsonValue.CreateBooleanValue(s.IsVip), ["cloud"] = JsonValue.CreateBooleanValue(s.IsCloud), ["available"] = JsonValue.CreateBooleanValue(s.IsAvailable),
                    ["album"] = new JsonObject { ["id"] = S(s.Album?.id), ["name"] = S(s.Album?.name), ["cover"] = S(s.Album?.cover) }, ["artists"] = artists
                });
            }
            var folder = ApplicationData.Current.LocalFolder;
            var timestamp = DateTimeOffset.UtcNow.ToString("O");
            if (!refreshTimestamp)
            {
                try { timestamp = JsonObject.Parse(await FileIO.ReadTextAsync(await folder.GetFileAsync(Name(id)))).GetNamedString("savedAt"); }
                catch { }
            }
            var root = new JsonObject { ["version"] = JsonValue.CreateNumberValue(1), ["savedAt"] = S(timestamp), ["songs"] = array };
            var temp = await folder.CreateFileAsync(Name(id) + ".tmp", CreationCollisionOption.ReplaceExisting);
            await FileIO.WriteTextAsync(temp, root.Stringify());
            await temp.RenameAsync(Name(id), NameCollisionOption.ReplaceExisting);
        }
        catch { /* Optional cache must never block playback. */ }
    }
}
