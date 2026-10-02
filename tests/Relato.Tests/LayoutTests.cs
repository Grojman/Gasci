using Relato.Layout;

namespace Relato.Tests;

public sealed class LayoutTests
{
    [Fact]
    public void RemainderIsCarriedToTheNextElement()
    {
        Assert.Equal([12, 12, 12], LayoutMath.Distribute(36, [33.3333, 33.3333, 33.3334]));
        Assert.Equal([33, 33, 34], LayoutMath.Distribute(100, [33.3333, 33.3333, 33.3334]));
        Assert.Equal([1, 1, 1], LayoutMath.Distribute(3, [33.3333, 33.3333, 33.3334]));
        Assert.Equal([3, 3, 4], LayoutMath.Distribute(10, [33.33, 33.33, 33.33])); // 99.99 %: the last one absorbs the rest
    }

    [Fact]
    public void CellsAlwaysAddUp()
    {
        int[] sizes = LayoutMath.Distribute(37, [25, 25, 25, 25]);
        Assert.Equal(37, sizes.Sum());
    }

    [Fact]
    public void LessThanHundredLeavesFreeSpace() => Assert.Equal(50, LayoutMath.Distribute(100, [25, 25]).Sum());

    [Theory]
    [InlineData("auto", SizeKind.Auto, 0)]
    [InlineData("fill", SizeKind.Percent, 100)]
    [InlineData("40%", SizeKind.Percent, 40)]
    [InlineData("12", SizeKind.Cells, 12)]
    public void SizesParse(string text, SizeKind kind, double amount) => Assert.Equal(new Size(kind, amount), Size.Parse(text));

    [Fact]
    public void SizeResolves()
    {
        Assert.Equal(40, Size.Percent(40).Resolve(100, 7));
        Assert.Equal(7, Size.Auto.Resolve(100, 7));
        Assert.Equal(12, Size.Cells(12).Resolve(100, 7));
    }

    [Fact]
    public void Anchors()
    {
        Assert.Equal(0, LayoutMath.Place(Anchor.Start, 10, 4));
        Assert.Equal(3, LayoutMath.Place(Anchor.Center, 10, 4));
        Assert.Equal(6, LayoutMath.Place(Anchor.End, 10, 4));
    }

    [Fact]
    public void TextMetricsIgnoreCodes()
    {
        TextMetric m = TextMetrics.Measure("{c:Red}Hola{c} mundo{p:300} cruel\nadiós");
        Assert.Equal(16, m.Natural); // "Hola mundo cruel"
        Assert.Equal(5, m.Min);      // "mundo", "cruel", "adiós"
        Assert.Equal(2, m.Lines);
    }

    [Fact]
    public void WrapCountsLines() => Assert.Equal(2, TextLayout.CountLines("one two three", 8));
}
