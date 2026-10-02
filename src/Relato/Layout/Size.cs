using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relato.Layout;

public enum SizeKind { Auto, Percent, Cells }

/// <summary>
/// A width or height as the author writes it: "auto" (fit the content), "40%" (of the space the parent
/// gives), "fill" (= "100%") or a number of cells (discouraged: the grid depends on the screen).
/// </summary>
[JsonConverter(typeof(SizeConverter))]
public readonly record struct Size(SizeKind Kind, double Amount)
{
    public static readonly Size Auto = new(SizeKind.Auto, 0);
    public static readonly Size Fill = new(SizeKind.Percent, 100);
    public static Size Percent(double p) => new(SizeKind.Percent, p);
    public static Size Cells(int n) => new(SizeKind.Cells, n);

    public bool IsAuto => Kind == SizeKind.Auto;
    public bool IsPercent => Kind == SizeKind.Percent;

    /// <summary>Size in cells for an available space, using <paramref name="measured"/> for "auto".</summary>
    public int Resolve(int available, int measured) => Kind switch
    {
        SizeKind.Percent => (int)Math.Round(available * Amount / 100.0, MidpointRounding.AwayFromZero),
        SizeKind.Cells => (int)Amount,
        _ => measured,
    };

    public static Size Parse(string text)
    {
        text = text.Trim();
        if (text.Equals("auto", StringComparison.OrdinalIgnoreCase)) return Auto;
        if (text.Equals("fill", StringComparison.OrdinalIgnoreCase)) return Fill;
        if (text.EndsWith('%') && double.TryParse(text[..^1], NumberStyles.Float, CultureInfo.InvariantCulture, out double p) && p >= 0)
            return Percent(p);
        if (int.TryParse(text, out int cells) && cells >= 0) return Cells(cells);
        throw new FormatException($"invalid size '{text}' (use \"auto\", \"fill\", \"40%\" or a number of cells)");
    }

    public override string ToString() => Kind switch
    {
        SizeKind.Auto => "auto",
        SizeKind.Percent => Amount.ToString(CultureInfo.InvariantCulture) + "%",
        _ => ((int)Amount).ToString(),
    };
}

public sealed class SizeConverter : JsonConverter<Size>
{
    public override Size Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        try
        {
            return reader.TokenType switch
            {
                JsonTokenType.Number => Size.Cells(reader.GetInt32()),
                JsonTokenType.String => Size.Parse(reader.GetString()!),
                _ => throw new JsonException("a size must be a string or a number"),
            };
        }
        catch (FormatException e) { throw new JsonException(e.Message); }
    }

    public override void Write(Utf8JsonWriter writer, Size value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString());
}

/// <summary>Position on one axis: left/top, center, right/bottom.</summary>
[JsonConverter(typeof(AnchorConverter))]
public enum Anchor { Start, Center, End }

public sealed class AnchorConverter : JsonConverter<Anchor>
{
    public override Anchor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        reader.GetString()?.ToLowerInvariant() switch
        {
            "left" or "top" or "start" => Anchor.Start,
            "center" or "centre" or "middle" => Anchor.Center,
            "right" or "bottom" or "end" => Anchor.End,
            var other => throw new JsonException($"invalid anchor '{other}' (left, center, right / top, center, bottom)"),
        };

    public override void Write(Utf8JsonWriter writer, Anchor value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString().ToLowerInvariant());
}

public static class LayoutMath
{
    /// <summary>Offset of a child of size <paramref name="size"/> anchored inside <paramref name="space"/>.</summary>
    public static int Place(Anchor anchor, int space, int size) => anchor switch
    {
        Anchor.Center => (space - size) / 2,
        Anchor.End => space - size,
        _ => 0,
    };

    /// <summary>
    /// Splits <paramref name="total"/> cells between percentages. Each element takes the whole cells of
    /// its share and passes the fraction left over to the next one (element i ends at
    /// floor(total·Σp≤i)); when the percentages add up to 100 the last element absorbs whatever is
    /// left, so the cells always fill the container exactly.
    /// </summary>
    public static int[] Distribute(int total, IReadOnlyList<double> percents)
    {
        var sizes = new int[percents.Count];
        bool fills = percents.Sum() >= 99.5;
        double cumulative = 0;
        int start = 0;
        for (int i = 0; i < percents.Count; i++)
        {
            cumulative += percents[i];
            int end = fills && i == percents.Count - 1
                ? total
                : (int)Math.Floor(total * Math.Min(cumulative, 100) / 100.0 + 1e-4); // 1e-4 absorbs floating point noise
            sizes[i] = Math.Max(0, end - start);
            start = Math.Max(start, end);
        }
        return sizes;
    }
}
