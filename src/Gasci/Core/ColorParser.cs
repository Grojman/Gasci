using System.Globalization;
using System.Reflection;

namespace Gasci.Core;

/// <summary>Parses colours written in the JSON assets: a SadRogue colour name ("DarkRed") or "#RRGGBB".</summary>
public static class ColorParser
{
    private static readonly Dictionary<string, Color> Cache = new(StringComparer.OrdinalIgnoreCase);

    public static bool TryParse(string? text, out Color color)
    {
        color = Color.White;
        if (string.IsNullOrWhiteSpace(text)) return false;
        if (Cache.TryGetValue(text, out color)) return true;

        if (text.StartsWith('#') && text.Length == 7 &&
            int.TryParse(text.AsSpan(1), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int rgb))
        {
            color = new Color((rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
        }
        else
        {
            FieldInfo? field = typeof(Color).GetField(text,
                BindingFlags.Public | BindingFlags.Static | BindingFlags.IgnoreCase);
            if (field?.GetValue(null) is not Color named) return false;
            color = named;
        }

        Cache[text] = color;
        return true;
    }

    public static Color Parse(string? text, Color fallback) => TryParse(text, out Color c) ? c : fallback;

    public static Color Scale(Color c, float factor) =>
        new((int)(c.R * factor), (int)(c.G * factor), (int)(c.B * factor), c.A);
}
