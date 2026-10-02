using Gasci.Core;

namespace Gasci.Rendering;

/// <summary>An ASCII drawing loaded from a text file, already converted to font glyphs.</summary>
public sealed class AsciiImage
{
    private readonly int[,] _glyphs;

    public int Width { get; }
    public int Height { get; }

    public AsciiImage(IReadOnlyList<string> lines)
    {
        Height = lines.Count;
        Width = lines.Count == 0 ? 0 : lines.Max(l => l.Length);
        _glyphs = new int[Math.Max(Width, 1), Math.Max(Height, 1)];

        for (int y = 0; y < Height; y++)
            for (int x = 0; x < Width; x++)
                _glyphs[x, y] = CharMap.Current.Glyph(x < lines[y].Length ? lines[y][x] : ' ');
    }

    public static AsciiImage FromFile(string path) =>
        new(File.ReadAllLines(path).Select(l => l.TrimEnd()).ToList());

    /// <summary>Glyph at a position; outside the image returns a space.</summary>
    public int GlyphAt(int x, int y) =>
        x >= 0 && y >= 0 && x < Width && y < Height ? _glyphs[x, y] : CharMap.Current.Glyph(' ');

    /// <summary>Horror-ASCII shading: light shade characters are drawn darker than solid ones.</summary>
    public static Color Shade(int glyph, Color baseColor)
    {
        CharMap map = CharMap.Current;
        if (glyph == map.Glyph('░')) return ColorParser.Scale(baseColor, 0.45f);
        if (glyph == map.Glyph('▒')) return ColorParser.Scale(baseColor, 0.65f);
        if (glyph == map.Glyph('▓')) return ColorParser.Scale(baseColor, 0.85f);
        foreach (char c in ".,'`:·∙")
            if (glyph == map.Glyph(c)) return ColorParser.Scale(baseColor, 0.55f);
        return baseColor;
    }
}
