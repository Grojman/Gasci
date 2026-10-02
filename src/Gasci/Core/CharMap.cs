using Gasci.Config;

namespace Gasci.Core;

/// <summary>
/// Converts text characters (UTF-8 in the assets) into glyph indices of the font in use. The
/// built-in font uses the IBM code page 437 layout; a custom font brings its own map (see
/// <see cref="FontConfigDefinition.Charmap"/>). Characters the font lacks are drawn as '?'.
/// </summary>
public sealed class CharMap
{
    private readonly Dictionary<char, int> _table;

    public int GlyphCount { get; }

    private CharMap(Dictionary<char, int> table, int glyphCount)
    {
        _table = table;
        GlyphCount = glyphCount;
    }

    /// <summary>The map in use. Defaults to CP437 so tools and tests work without loading a font.</summary>
    public static CharMap Current { get; set; } = Cp437();

    /// <summary>Characters the engine itself draws (frames, cursors, indicators). A custom font must have them.</summary>
    public const string RequiredCharacters = " ?█░▒▓─│┌┐└┘►◄▲▼↑↓←→·∙";

    public bool Has(char c) => c is '\0' or ' ' || _table.ContainsKey(c);

    public int Glyph(char c) => _table.TryGetValue(c, out int glyph) ? glyph : _table.GetValueOrDefault('?', '?');

    public bool IsBlank(int glyph) => glyph == 0 || glyph == _table.GetValueOrDefault(' ', 32) || glyph == _blankExtra;

    private int _blankExtra = -1;

    // ---- Built-in IBM layout ---------------------------------------------------------------

    private const string Low = "\0☺☻♥♦♣♠•◘○◙♂♀♪♫☼►◄↕‼¶§▬↨↑↓→←∟↔▲▼";
    private const string High =
        "ÇüéâäàåçêëèïîìÄÅÉæÆôöòûùÿÖÜ¢£¥₧ƒáíóúñÑªº¿⌐¬½¼¡«»░▒▓│┤╡╢╖╕╣║╗╝╜╛┐└┴┬├─┼╞╟╚╔╩╦╠═╬╧╨╤╥╙╘╒╓╫╪┘┌█▄▌▐▀αßΓπΣσµτΦΘΩδ∞φε∩≡±≥≤⌠⌡÷≈°∙·√ⁿ²■ ";

    public static CharMap Cp437()
    {
        var table = new Dictionary<char, int>();
        for (int i = 32; i < 127; i++) table[(char)i] = i;
        for (int i = 1; i < Low.Length; i++) table.TryAdd(Low[i], i);
        for (int i = 0; i < High.Length; i++) table.TryAdd(High[i], 128 + i);
        table['⌂'] = 127;
        AddFallbacks(table);
        return new CharMap(table, 256) { _blankExtra = 255 };
    }

    /// <summary>Characters that are drawn with a close glyph when the font lacks them.</summary>
    private static void AddFallbacks(Dictionary<char, int> table)
    {
        foreach (var (from, to) in new[]
        {
            ('Á', 'A'), ('À', 'A'), ('Í', 'I'), ('Ó', 'O'), ('Ú', 'U'), ('È', 'E'),
            ('—', '-'), ('–', '-'), ('“', '"'), ('”', '"'), ('‘', '\''), ('’', '\''), ('…', '.'),
            ('●', '•'), ('β', 'ß'),
        })
            if (!table.ContainsKey(from) && table.TryGetValue(to, out int g)) table[from] = g;
    }

    // ---- Custom fonts ------------------------------------------------------------------------

    /// <summary>Reads a character map file. Throws <see cref="ContentException"/> with every problem found.</summary>
    public static CharMap Load(string path, int glyphCount)
    {
        if (!File.Exists(path)) throw new ContentException($"Character map '{path}' does not exist");
        string text = File.ReadAllText(path).Replace("\r", "");
        string glyphs = text.Replace("\n", "");
        if (glyphs.Length > glyphCount)
            throw new ContentException($"Character map '{Path.GetFileName(path)}' has {glyphs.Length} characters but the font only has {glyphCount} glyphs");

        var table = new Dictionary<char, int>();
        for (int i = 0; i < glyphs.Length; i++) table.TryAdd(glyphs[i], i);
        AddFallbacks(table);
        var map = new CharMap(table, glyphCount);

        string missing = new(RequiredCharacters.Where(c => !map.Has(c)).ToArray());
        if (missing.Length > 0)
            throw new ContentException($"Character map '{Path.GetFileName(path)}' lacks characters the engine draws: \"{missing}\"");
        return map;
    }
}
