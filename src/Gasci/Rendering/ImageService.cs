namespace Gasci.Rendering;

/// <summary>
/// Returns images by key. Keys are paths relative to assets/images without extension:
///   "general/title", "characters/mother/2".
/// Character folders hold numbered frames (1.txt, 2.txt...).
/// </summary>
public sealed class ImageService
{
    private readonly string _root;
    private readonly Dictionary<string, AsciiImage> _cache = new();

    public ImageService(string root) => _root = root;

    public static string CharacterKey(string character, int frame) => $"characters/{character}/{frame}";

    public bool Exists(string key) => File.Exists(PathOf(key));

    /// <summary>Keys of every image file on disk, e.g. "characters/mother/2".</summary>
    public IEnumerable<string> AllKeys() =>
        Directory.Exists(_root)
            ? Directory.EnumerateFiles(_root, "*.txt", SearchOption.AllDirectories)
                .Select(f => Path.ChangeExtension(Path.GetRelativePath(_root, f), null).Replace(Path.DirectorySeparatorChar, '/'))
                .Order()
            : [];

    public AsciiImage Get(string key)
    {
        if (_cache.TryGetValue(key, out AsciiImage? image)) return image;
        string path = PathOf(key);
        if (!File.Exists(path)) throw new FileNotFoundException($"Image '{key}' not found", path);
        return _cache[key] = AsciiImage.FromFile(path);
    }

    public AsciiImage Character(string character, int frame) => Get(CharacterKey(character, frame));

    private string PathOf(string key) => Path.Combine(_root, key.Replace('/', Path.DirectorySeparatorChar) + ".txt");
}
