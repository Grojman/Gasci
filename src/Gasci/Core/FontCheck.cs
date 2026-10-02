using System.Text.Json;
using Gasci.Config;

namespace Gasci.Core;

/// <summary>
/// Checks a custom font (game.json → font) before it is used, both by --validate and at start-up:
/// the .font file and its tile sheet exist and make sense, and its character map covers every
/// character the engine draws.
/// </summary>
public static class FontCheck
{
    public sealed record Result(List<string> Problems, Point GlyphSize, int GlyphCount, string FontPath, CharMap? Map);

    public static Result Check(FontConfigDefinition font)
    {
        var problems = new List<string>();
        string fontPath = Path.Combine(Paths.Fonts, font.File);
        if (!File.Exists(fontPath)) return new([$"font file 'fonts/{font.File}' does not exist"], Point.Zero, 0, fontPath, null);

        JsonElement json;
        try { json = JsonDocument.Parse(File.ReadAllText(fontPath)).RootElement; }
        catch (JsonException e) { return new([$"'{font.File}' is not valid JSON: {e.Message}"], Point.Zero, 0, fontPath, null); }

        int Int(string name) => json.EnumerateObject().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.Number } p ? p.Value.GetInt32() : 0;
        string? Str(string name) => json.EnumerateObject().FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) is { Value.ValueKind: JsonValueKind.String } p ? p.Value.GetString() : null;

        int glyphW = Int("GlyphWidth"), glyphH = Int("GlyphHeight"), columns = Int("Columns"), padding = Int("GlyphPadding");
        if (glyphW < 4 || glyphH < 6) problems.Add($"'{font.File}': glyphs of {glyphW}x{glyphH} pixels are too small (minimum 4x6)");
        if (columns <= 0) problems.Add($"'{font.File}': 'Columns' must be positive");

        int glyphCount = 0;
        string? image = Str("FilePath");
        if (image is null) problems.Add($"'{font.File}': missing 'FilePath' (the tile sheet image)");
        else
        {
            string imagePath = Path.Combine(Path.GetDirectoryName(fontPath)!, image);
            if (!File.Exists(imagePath)) problems.Add($"'{font.File}': tile sheet '{image}' does not exist");
            else if (PngSize(imagePath) is not { } size) problems.Add($"'{font.File}': tile sheet '{image}' is not a PNG image");
            else if (columns > 0 && glyphW > 0 && glyphH > 0)
            {
                int rows = (size.Y + padding) / (glyphH + padding);
                glyphCount = columns * rows;
                if (columns * (glyphW + padding) - padding > size.X) problems.Add($"'{font.File}': {columns} columns of {glyphW} pixels do not fit in a {size.X} pixel wide image");
                if (glyphCount < 96) problems.Add($"'{font.File}': only {glyphCount} glyphs (at least 96 are needed for text)");
            }
        }

        CharMap? map = null;
        if (string.IsNullOrEmpty(font.Charmap)) problems.Add("a custom font needs a 'charmap' file (character → glyph)");
        else if (glyphCount > 0)
        {
            try { map = CharMap.Load(Path.Combine(Paths.Fonts, font.Charmap), glyphCount); }
            catch (ContentException e) { problems.Add(e.Message); }
        }
        return new(problems, new Point(glyphW, glyphH), glyphCount, fontPath, map);
    }

    /// <summary>Width and height from the IHDR chunk of a PNG file.</summary>
    private static Point? PngSize(string path)
    {
        using FileStream f = File.OpenRead(path);
        var header = new byte[24];
        if (f.Read(header, 0, 24) < 24 || header[1] != 'P' || header[2] != 'N' || header[3] != 'G') return null;
        int w = header[16] << 24 | header[17] << 16 | header[18] << 8 | header[19];
        int h = header[20] << 24 | header[21] << 16 | header[22] << 8 | header[23];
        return new Point(w, h);
    }
}
