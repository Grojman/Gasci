using Gasci.Core;

namespace Gasci.Rendering;

/// <summary>Order in which a <see cref="DrawCursor"/> fills the cells of an image.</summary>
public enum DrawMode
{
    /// <summary>Random cells, no order.</summary>
    Random,
    /// <summary>From the edges of the area towards the centre.</summary>
    Borders,
    /// <summary>Line by line, from the top to the bottom.</summary>
    TopToBottom,
    /// <summary>Alternates one random cell of the top half with one of the bottom half.</summary>
    ZigZag,
    /// <summary>Reverse of Borders: from the centre outwards.</summary>
    Inside,
}

/// <summary>How many cursors a <see cref="DrawCursor"/> shows at once.</summary>
public enum CursorMode
{
    /// <summary>Several cells are being written at the same time, each with its own cursor.</summary>
    Multiple,
    /// <summary>One cursor travels through every cell: a cell is written as soon as the cursor moves on.</summary>
    Single,
}

/// <summary>
/// "Pointer" that draws an ASCII image into a surface character by character. Each cell first
/// shows a console cursor (█) and then the real character, playing the cursor sound.
/// Cells that already contain the right character are skipped, so switching between two frames
/// of the same character only rewrites what changed.
/// </summary>
public sealed class DrawCursor
{
    private readonly ICellSurface _target;
    private readonly List<Cell> _cells;
    private readonly List<Head> _heads = new();
    private int _next;
    private double _spawnBudget;

    private readonly record struct Cell(int X, int Y, int Glyph, Color Foreground);
    private sealed class Head(Cell cell) { public Cell Cell = cell; public double Age; }

    /// <summary>How many new cells start being written per second.</summary>
    public double CellsPerSecond { get; set; } = 700;
    /// <summary>How long the cursor glyph stays in a cell before the real character appears.</summary>
    public double CursorSeconds { get; set; } = 0.045;
    public Color CursorColor { get; set; } = new(200, 200, 200);
    public CursorMode CursorMode { get; set; } = CursorMode.Multiple;

    public bool IsFinished => _next >= _cells.Count && _heads.Count == 0;
    public int ChangedCells => _cells.Count;

    public event Action? Finished;

    /// <param name="target">Surface to draw on.</param>
    /// <param name="area">Region of the surface managed by the cursor. Cells of the area not covered by the image are cleared.</param>
    /// <param name="image">Image to draw, or null to erase the area.</param>
    /// <param name="imageOffset">Top-left corner of the image relative to the area.</param>
    public DrawCursor(ICellSurface target, Rectangle area, AsciiImage? image, Point imageOffset, Color color, DrawMode mode,
        CursorMode cursorMode = CursorMode.Multiple)
    {
        _target = target;
        CursorMode = cursorMode;
        var cells = new List<Cell>();

        for (int y = area.Y; y < area.MaxExtentY + 1; y++)
            for (int x = area.X; x < area.MaxExtentX + 1; x++)
            {
                int glyph = image?.GlyphAt(x - area.X - imageOffset.X, y - area.Y - imageOffset.Y) ?? CharMap.Current.Glyph(' ');
                Color fg = AsciiImage.Shade(glyph, color);
                ColoredGlyphBase current = target[x, y];

                bool same = CharMap.Current.IsBlank(glyph)
                    ? CharMap.Current.IsBlank(current.Glyph)
                    : current.Glyph == glyph && current.Foreground == fg;
                if (!same) cells.Add(new Cell(x, y, glyph, fg));
            }

        _cells = Order(cells, area, mode);
    }

    /// <summary>Convenience: centres the image inside the area.</summary>
    public static DrawCursor Centered(ICellSurface target, Rectangle area, AsciiImage? image, Color color, DrawMode mode,
        CursorMode cursorMode = CursorMode.Multiple)
    {
        Point offset = image is null
            ? Point.None
            : new Point((area.Width - image.Width) / 2, (area.Height - image.Height) / 2);
        return new DrawCursor(target, area, image, offset, color, mode, cursorMode);
    }

    public void Update(TimeSpan delta)
    {
        if (IsFinished) return;
        double dt = delta.TotalSeconds;

        for (int i = _heads.Count - 1; i >= 0; i--)
        {
            Head head = _heads[i];
            head.Age += dt;
            if (head.Age >= CursorSeconds)
            {
                Commit(head.Cell);
                _heads.RemoveAt(i);
            }
        }

        _spawnBudget += dt * CellsPerSecond;
        while (_spawnBudget >= 1 && _next < _cells.Count)
        {
            _spawnBudget -= 1;
            if (CursorMode == CursorMode.Single)
            {
                foreach (Head previous in _heads) Commit(previous.Cell);
                _heads.Clear();
            }
            Cell cell = _cells[_next++];
            _target.SetGlyph(cell.X, cell.Y, CharMap.Current.Glyph('█'), CursorColor);
            _heads.Add(new Head(cell));
        }
        if (_next >= _cells.Count) _spawnBudget = 0;

        if (IsFinished) Finished?.Invoke();
    }

    /// <summary>Sets the speed so that the whole drawing takes <paramref name="seconds"/>, whatever its size.</summary>
    public void SetDuration(double seconds)
    {
        if (seconds > 0) CellsPerSecond = Math.Max(1, _cells.Count / seconds);
    }

    /// <summary>Skips the animation and writes every remaining cell.</summary>
    public void Complete()
    {
        if (IsFinished) return;
        foreach (Head head in _heads) Commit(head.Cell, playSound: false);
        _heads.Clear();
        for (; _next < _cells.Count; _next++) Commit(_cells[_next], playSound: false);
        Finished?.Invoke();
    }

    private void Commit(Cell cell, bool playSound = true)
    {
        _target.SetGlyph(cell.X, cell.Y, cell.Glyph, cell.Foreground);
        if (playSound && !CharMap.Current.IsBlank(cell.Glyph)) GameServices.Audio?.PlayCursor();
    }

    private static List<Cell> Order(List<Cell> cells, Rectangle area, DrawMode mode)
    {
        Random rng = GameServices.Rng;
        // Random tie-breaker so cells of the same "ring"/"half" are not drawn in reading order.
        Dictionary<Cell, int> noise = cells.ToDictionary(c => c, _ => rng.Next());

        int Ring(Cell c)
        {
            int dx = c.X - area.X, dy = c.Y - area.Y;
            return Math.Min(Math.Min(dx, area.Width - 1 - dx), Math.Min(dy, area.Height - 1 - dy));
        }

        switch (mode)
        {
            case DrawMode.TopToBottom:
                return cells.OrderBy(c => c.Y).ThenBy(c => c.X).ToList();
            case DrawMode.Borders:
                return cells.OrderBy(Ring).ThenBy(c => noise[c]).ToList();
            case DrawMode.Inside:
                return cells.OrderByDescending(Ring).ThenBy(c => noise[c]).ToList();
            case DrawMode.ZigZag:
            {
                int middle = area.Y + area.Height / 2;
                var top = cells.Where(c => c.Y < middle).OrderBy(c => noise[c]).ToList();
                var bottom = cells.Where(c => c.Y >= middle).OrderBy(c => noise[c]).ToList();
                var result = new List<Cell>(cells.Count);
                for (int i = 0; i < Math.Max(top.Count, bottom.Count); i++)
                {
                    if (i < top.Count) result.Add(top[i]);
                    if (i < bottom.Count) result.Add(bottom[i]);
                }
                return result;
            }
            default:
                return cells.OrderBy(c => noise[c]).ToList();
        }
    }
}
