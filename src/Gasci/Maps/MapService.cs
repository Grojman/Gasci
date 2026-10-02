using Gasci.Core;
using Gasci.Expressions;

namespace Gasci.Maps;

/// <summary>Loads maps from assets/data/maps: &lt;id&gt;.txt (drawing) + &lt;id&gt;_info.json (meaning of the characters).</summary>
public sealed class MapService
{
    private readonly string _root;
    private readonly Dictionary<string, MapInfo> _infos = new();

    public MapService(string root) => _root = root;

    public IEnumerable<string> AvailableMaps =>
        Directory.EnumerateFiles(_root, "*_info.json").Select(f => Path.GetFileName(f)[..^"_info.json".Length]);

    public MapInfo Info(string id)
    {
        if (_infos.TryGetValue(id, out MapInfo? info)) return info;
        return _infos[id] = File.Exists(Path.Combine(_root, id + "_info.json"))
            ? Json.Load<MapInfo>(Path.Combine(_root, id + "_info.json"))
            : throw new ContentException($"Map '{id}' does not exist (no {id}_info.json)");
    }

    /// <summary>Loads a map choosing the drawing variant that matches the current variables.</summary>
    public MapData Load(string id)
    {
        MapInfo info = Info(id);
        string file = info.Variants.FirstOrDefault(v => Expression.IsTrue(v.If))?.File ?? id;
        return LoadFile(id, file, info);
    }

    public MapData LoadFile(string id, string file, MapInfo info)
    {
        string path = Path.Combine(_root, file + ".txt");
        List<string> lines = File.ReadAllLines(path).Select(l => l.TrimEnd('\r')).ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        return new MapData(id, file, info, lines);
    }

    /// <summary>Name of the variant drawing that would be used now (to know if the map must be reloaded).</summary>
    public string ResolveFile(string id) =>
        Info(id).Variants.FirstOrDefault(v => Expression.IsTrue(v.If))?.File ?? id;
}
