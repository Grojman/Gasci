using System.Text.Json.Serialization;
using Relato.Core;
using Relato.Layout;
using Relato.UI;

namespace Relato.Blocks;

/// <summary>Common part of the blocks that hold other blocks: border, padding, background and title.</summary>
public abstract class ContainerBlock : Block
{
    [JsonPropertyName("children")]
    public List<Block> Items { get; set; } = new();
    public bool Border { get; set; }
    /// <summary>Space between the border and the children: [horizontal, vertical] or one number for both.</summary>
    public int[]? Padding { get; set; }
    /// <summary>Text key printed on the top border.</summary>
    public string? Title { get; set; }
    public string? BorderColor { get; set; }

    [System.Text.Json.Serialization.JsonIgnore] public override IEnumerable<Block> Children => Items;

    protected IEnumerable<Block> ShownChildren => Items.Where(c => c.IsShown);

    private Point PaddingSize => Padding switch
    {
        [var both] => new Point(both, both),
        [var x, var y, ..] => new Point(x, y),
        _ => Point.Zero,
    };

    /// <summary>Cells taken by border and padding on each side.</summary>
    protected Point Chrome => PaddingSize + (Border ? new Point(1, 1) : Point.Zero);

    protected Rectangle Inner => new(Bounds.X + Chrome.X, Bounds.Y + Chrome.Y,
        Math.Max(0, Bounds.Width - Chrome.X * 2), Math.Max(0, Bounds.Height - Chrome.Y * 2));

    protected Point InnerAvailable(Point available) =>
        new(Math.Max(0, available.X - Chrome.X * 2), Math.Max(0, available.Y - Chrome.Y * 2));

    protected bool DrawsChrome => Border || Background is not null || Title is not null;

    protected override void OnArrange()
    {
        if (!DrawsChrome) { Surface = null; ArrangeChildren(); return; }
        ScreenSurface s = EnsureSurface(BackgroundColor);
        s.Surface.Clear();
        if (Border) Frame.Box(s.Surface, s.Surface.Area, Theme.Color(BorderColor, "border"), Background is null ? null : BackgroundColor);
        if (Title is not null) Frame.PrintCentered(s.Surface, 0, $" {GameServices.T(Title)} ", ContentColor("highlight"));
        ArrangeChildren();
    }

    private static Config.Theme Theme => Config.Theme.Current;

    protected abstract void ArrangeChildren();
}

/// <summary>
/// Children flow one after another (vertical or horizontal). A child's anchor only applies on the
/// cross axis. Percentage children share the main axis with the remainder carried to the next one.
/// </summary>
public class StackBlock : ContainerBlock
{
    public string Direction { get; set; } = "vertical";
    public int Spacing { get; set; }
    /// <summary>Default cross-axis anchor of the children.</summary>
    public Anchor Align { get; set; } = Anchor.Center;
    /// <summary>Where the children go on the main axis when they do not fill it.</summary>
    public Anchor Justify { get; set; } = Anchor.Start;

    protected bool Vertical => Direction != "horizontal";

    private static int MainOf(Point p, bool vertical) => vertical ? p.Y : p.X;
    private static int CrossOf(Point p, bool vertical) => vertical ? p.X : p.Y;
    private static Point Make(int main, int cross, bool vertical) => vertical ? new Point(cross, main) : new Point(main, cross);

    protected override Point MeasureContent(Point available)
    {
        Point inner = InnerAvailable(available);
        var children = ShownChildren.ToList();
        int main = 0, cross = 0;
        foreach (Block child in children)
        {
            Point size = child.Measure(inner);
            main += MainOf(size, Vertical);
            cross = Math.Max(cross, CrossOf(size, Vertical));
        }
        main += Spacing * Math.Max(0, children.Count - 1);
        return Make(main, cross, Vertical) + Chrome * 2;
    }

    protected override void ArrangeChildren()
    {
        var children = ShownChildren.ToList();
        if (children.Count == 0) return;
        Rectangle inner = Inner;
        Point innerSize = new(inner.Width, inner.Height);
        int mainSpace = MainOf(innerSize, Vertical) - Spacing * (children.Count - 1);
        int crossSpace = CrossOf(innerSize, Vertical);

        // Fixed and auto children first; percentage children share what their percentages ask for.
        var sizes = new int[children.Count];
        var percentIndexes = new List<int>();
        int used = 0;
        for (int i = 0; i < children.Count; i++)
        {
            Size spec = Vertical ? children[i].Height : children[i].Width;
            if (spec.IsPercent) { percentIndexes.Add(i); continue; }
            sizes[i] = MainOf(children[i].Measure(innerSize), Vertical);
            used += sizes[i];
        }
        if (percentIndexes.Count > 0)
        {
            var percents = percentIndexes.Select(i => (Vertical ? children[i].Height : children[i].Width).Amount).ToList();
            int[] shares = LayoutMath.Distribute(Math.Max(0, mainSpace), percents);
            int wanted = shares.Sum(), left = Math.Max(0, mainSpace - used);
            if (wanted > left) // not enough room: percentage children shrink (proportionally) before anything else
            {
                double sum = percents.Sum();
                shares = LayoutMath.Distribute(left, percents.Select(p => p * 100.0 / sum).ToList());
            }
            for (int k = 0; k < percentIndexes.Count; k++)
            {
                Block child = children[percentIndexes[k]];
                sizes[percentIndexes[k]] = Vertical ? child.ClampHeight(shares[k], mainSpace) : child.ClampWidth(shares[k], mainSpace);
            }
        }

        int total = sizes.Sum() + Spacing * (children.Count - 1);
        if (total > MainOf(innerSize, Vertical))
            Log.Warn($"{Where}: the children need {total} cells but only {MainOf(innerSize, Vertical)} are available; the last ones are cut");
        int position = LayoutMath.Place(Justify, MainOf(innerSize, Vertical), Math.Min(total, MainOf(innerSize, Vertical)));

        for (int i = 0; i < children.Count; i++)
        {
            Block child = children[i];
            Point measured = child.Measure(innerSize);
            int cross = Math.Min(CrossOf(measured, Vertical), crossSpace);
            Anchor anchor = (Vertical ? child.X : child.Y) ?? Align;
            int crossPos = LayoutMath.Place(anchor, crossSpace, cross);
            int mainSize = Math.Max(0, Math.Min(sizes[i], MainOf(innerSize, Vertical) - position));
            Point offset = child.Offset is [var ox, var oy, ..] ? new Point(ox, oy) : Point.Zero;
            Point pos = Make(position, crossPos, Vertical) + inner.Position + offset;
            Point size = Make(mainSize, cross, Vertical);
            child.Arrange(new Rectangle(pos.X, pos.Y, size.X, size.Y));
            position += sizes[i] + Spacing;
        }
    }
}

/// <summary>Children are placed by their own anchors and may overlap; later children are drawn on top.</summary>
public sealed class OverlayBlock : ContainerBlock
{
    protected override Point MeasureContent(Point available)
    {
        Point inner = InnerAvailable(available);
        Point max = Point.Zero;
        foreach (Block child in ShownChildren)
        {
            Point size = child.Measure(inner);
            max = new Point(Math.Max(max.X, size.X), Math.Max(max.Y, size.Y));
        }
        return max + Chrome * 2;
    }

    protected override void ArrangeChildren() => ArrangeOverlay(Inner, ShownChildren);

    /// <summary>Places each child by its anchors inside an area (also used by composite blocks).</summary>
    public static void ArrangeOverlay(Rectangle inner, IEnumerable<Block> children)
    {
        var space = new Point(inner.Width, inner.Height);
        foreach (Block child in children)
        {
            Point size = child.Measure(space);
            size = new Point(Math.Min(size.X, space.X), Math.Min(size.Y, space.Y));
            Point offset = child.Offset is [var ox, var oy, ..] ? new Point(ox, oy) : Point.Zero;
            var pos = new Point(
                inner.X + LayoutMath.Place(child.X ?? Anchor.Start, space.X, size.X),
                inner.Y + LayoutMath.Place(child.Y ?? Anchor.Start, space.Y, size.Y)) + offset;
            child.Arrange(new Rectangle(pos.X, pos.Y, size.X, size.Y));
        }
    }
}

/// <summary>Shows the first case whose condition is true (mode "first"), or every true case (mode "all"), stacked.</summary>
public sealed class BranchBlock : Block
{
    public List<BranchCase> Cases { get; set; } = new();
    public Block? Else { get; set; }
    public string Mode { get; set; } = "first";
    public int Spacing { get; set; }

    private readonly List<Expressions.Expression?> _conditions = new();

    [System.Text.Json.Serialization.JsonIgnore] public override IEnumerable<Block> Children => Cases.Select(c => c.Content).Append(Else).OfType<Block>();

    protected override void OnAttach()
    {
        if (Mode is not ("first" or "all")) throw new ContentException($"{Where}: mode must be \"first\" or \"all\"");
        foreach (BranchCase c in Cases) _conditions.Add(Compile(c.If));
        UpdateCases();
    }

    /// <summary>Activates the right cases; called before visibility is refreshed.</summary>
    internal void UpdateCases()
    {
        bool any = false;
        for (int i = 0; i < Cases.Count; i++)
        {
            bool active = (Mode == "all" || !any) && (_conditions.Count <= i || (_conditions[i]?.EvalBool() ?? true));
            Cases[i].Content.BranchActive = active;
            any |= active;
        }
        if (Else is not null) Else.BranchActive = !any;
    }

    private IEnumerable<Block> Active => Children.Where(c => c.IsShown);

    protected override Point MeasureContent(Point available)
    {
        int w = 0, h = 0, n = 0;
        foreach (Block child in Active)
        {
            Point size = child.Measure(available);
            w = Math.Max(w, size.X);
            h += size.Y;
            n++;
        }
        return new Point(w, h + Spacing * Math.Max(0, n - 1));
    }

    protected override void OnArrange()
    {
        int y = Bounds.Y;
        foreach (Block child in Active)
        {
            Point size = child.Measure(new Point(Bounds.Width, Bounds.Height));
            int w = Math.Min(size.X, Bounds.Width);
            int x = Bounds.X + LayoutMath.Place(child.X ?? Anchor.Center, Bounds.Width, w);
            child.Arrange(new Rectangle(x, y, w, Math.Min(size.Y, Bounds.MaxExtentY + 1 - y)));
            y += size.Y + Spacing;
        }
    }
}

public sealed class BranchCase
{
    public string? If { get; set; }
    public Block Content { get; set; } = null!;
}

/// <summary>Empty space.</summary>
public sealed class SpacerBlock : Block
{
    protected override Point MeasureContent(Point available) => Point.Zero;
}
