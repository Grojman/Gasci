using Gasci.Core;
using Gasci.Layout;
using Gasci.UI;
using Gasci.Variables;

namespace Gasci.Blocks;

/// <summary>
/// A static text: a localisation key (<see cref="Text"/>) or the value of a variable
/// (<see cref="TextVar"/>, translated when the value is a text key). Measured with the widest
/// translation, so it keeps its size when the language changes.
/// </summary>
public sealed class TextBlock : Block
{
    public string? Text { get; set; }
    public string? TextVar { get; set; }
    /// <summary>Font scale (2 = big text). The block's size in normal cells is a multiple of it.</summary>
    public int Scale { get; set; } = 1;
    /// <summary>Alignment of the lines inside the block.</summary>
    public Anchor Align { get; set; } = Anchor.Center;
    public bool Wrap { get; set; } = true;

    private string _rendered = "";

    protected override void OnAttach()
    {
        if (Text is null && TextVar is null) throw new ContentException($"{Where}: a text needs 'text' or 'textVar'");
        if (TextVar is not null) VariableRegistry.Definition(TextVar);
        if (Scale is < 1 or > 4) throw new ContentException($"{Where}: scale must be 1 to 4");
    }

    private string CurrentText()
    {
        if (TextVar is not null)
        {
            string value = VariableRegistry.Get(TextVar).ToString();
            return GameServices.Loc.Has(value) ? GameServices.T(value) : value;
        }
        return GameServices.T(Resolve(Text)!);
    }

    /// <summary>The texts this block can show (all languages, all values), to measure the widest.</summary>
    private IEnumerable<string> PossibleTexts()
    {
        Localization loc = GameServices.Loc;
        IEnumerable<string> keys;
        if (TextVar is not null)
        {
            VariableDefinition def = VariableRegistry.Definition(TextVar);
            keys = def.AllowedValues ?? [VariableRegistry.Read(def).ToString()];
        }
        else keys = [Resolve(Text)!];

        foreach (string key in keys)
        {
            if (!loc.Has(key)) { yield return key; continue; }
            foreach (string language in loc.Languages)
                if (loc.HasLanguage(key, language)) yield return loc.Get(key, language);
        }
    }

    protected override Point MeasureContent(Point available)
    {
        var plains = PossibleTexts().Select(t => TextLayout.Strip(t, n => VariableRegistry.Find(n)?.MaxTextWidth ?? 0)).ToList();
        if (plains.Count == 0) return Point.Zero;
        int natural = plains.Max(p => TextMetrics.Measure(p).Natural);
        int width = Wrap ? Math.Min(natural, Math.Max(1, available.X / Scale)) : natural;
        int lines = plains.Max(p => TextLayout.CountLines(p, width));
        return new Point(width * Scale, lines * Scale);
    }

    protected override void OnArrange()
    {
        EnsureSurface(BackgroundColor, Scale);
        _rendered = "";
        Draw();
    }

    public override void Update(TimeSpan delta) => Draw();

    private void Draw()
    {
        if (Surface is null || !IsShown) return;
        string text = CurrentText();
        Color color = IsEnabled ? ContentColor() : Config.Theme.Current.Disabled;
        string key = text + color.PackedValue;
        if (key == _rendered) return;
        _rendered = key;

        ICellSurface s = Surface.Surface;
        s.Clear();
        int spaceGlyph = CharMap.Current.Glyph(' ');
        List<List<TextToken>> lines = TextLayout.Wrap(TextLayout.Parse(text, color, 0), Wrap ? s.Width : int.MaxValue / 2, spaceGlyph);
        int top = LayoutMath.Place(Y ?? Anchor.Start, s.Height, Math.Min(lines.Count, s.Height));
        for (int y = 0; y < lines.Count && y < s.Height; y++)
        {
            int x = LayoutMath.Place(Align, s.Width, lines[y].Count);
            for (int i = 0; i < lines[y].Count; i++)
                if (x + i >= 0 && x + i < s.Width) s.SetGlyph(x + i, top + y, lines[y][i].Glyph, lines[y][i].Color);
        }
    }
}

/// <summary>
/// Typewriter text with pages (▼ when there is more, ► at the end). Used for map messages, dialog
/// lines and cards. <see cref="Show"/> queues keys; while it shows them it takes the focus of the
/// scene ("move") and gives it back ("release") when the queue ends.
/// </summary>
public sealed class TextBoxBlock : Block
{
    /// <summary>Hidden while it has nothing to show.</summary>
    public bool AutoHide { get; set; } = true;
    public bool Border { get; set; } = true;
    public int Scale { get; set; } = 1;
    public bool CenterLines { get; set; }
    public int CharDelayMs { get; set; } = 22;
    public bool IndicateEnd { get; set; } = true;
    public bool TypeSound { get; set; } = true;
    /// <summary>Takes the focus while showing a queue (map messages). Composite blocks that drive the box set it to false.</summary>
    public bool TakeFocus { get; set; } = true;
    /// <summary>Text key shown when the scene opens (static boxes).</summary>
    public string? Text { get; set; }

    private TextBox? _box;
    private readonly Queue<string> _queue = new();
    private Action? _done;
    private bool _active;

    [System.Text.Json.Serialization.JsonIgnore] public bool IsShowing => _active;
    [System.Text.Json.Serialization.JsonIgnore] public bool IsFinished => _box is null || _box.IsFinished;
    [System.Text.Json.Serialization.JsonIgnore] public TextBox? Widget => _box;

    protected override void OnAttach()
    {
        if (AutoHide) Visible = Text is not null;
    }

    public override void OnOpen(bool restoring)
    {
        _queue.Clear();
        _active = false;
        _done = null;
        _box?.Clear();
        if (Text is not null) ShowRaw(GameServices.T(Resolve(Text)!));
        else if (AutoHide) SetVisible(false);
    }

    protected override Point MeasureContent(Point available) =>
        new(Math.Min(available.X, 40), (Border ? 2 : 0) + 3 * Scale);

    protected override void OnArrange()
    {
        int w = Math.Max(1, Bounds.Width / Scale), h = Math.Max(1, Bounds.Height / Scale);
        if (_box is not null && _box.Surface.Width == w && _box.Surface.Height == h && _box.FontSize == Display.CellSize * Scale)
        {
            _box.Position = Bounds.Position * Display.CellSize;
            return;
        }
        string? previous = _box?.RawText;
        int page = _box?.Page ?? 0;
        _box = new TextBox(w, h, new TextStyle
        {
            Border = Border,
            Scale = Scale,
            CenterLines = CenterLines,
            CharDelayMs = CharDelayMs,
            TypeSound = TypeSound,
            Foreground = ContentColor(),
            Background = Background is null ? Color.Black : BackgroundColor,
            BorderColor = Config.Theme.Current.Border,
        })
        {
            Position = Bounds.Position * Display.CellSize,
            IndicateEnd = IndicateEnd,
        };
        if (previous is not null) _box.ShowFinished(previous, page);
    }

    public override IEnumerable<IScreenObject> Surfaces() =>
        _box is null ? base.Surfaces() : base.Surfaces().Append(_box);

    /// <summary>Shows texts one after another; <paramref name="done"/> runs when the player has read the last one.</summary>
    public void Show(IReadOnlyList<string> keys, Action? done)
    {
        foreach (string key in keys) _queue.Enqueue(key);
        _done = done;
        if (_queue.Count == 0) { Close(); return; }
        if (!_active)
        {
            _active = true;
            SetVisible(true);
            if (TakeFocus) Scene.Focus.Move(null, this);
        }
        Scene.EnsureLaidOut();
        _box!.Show(_queue.Dequeue());
    }

    /// <summary>Shows a text without queue or focus handling (driven by composite blocks).</summary>
    public void ShowRaw(string text)
    {
        SetVisible(true);
        Scene.EnsureLaidOut();
        _box?.ShowRaw(text);
    }

    public void ShowKey(string key) => ShowRaw(GameServices.T(key));

    /// <summary>Finishes the page being typed or moves to the next; true when everything has been read.</summary>
    public bool Advance() => _box?.Advance() ?? true;

    public void Clear()
    {
        _box?.Clear();
        if (AutoHide) SetVisible(false);
    }

    public void SetIndicateEnd(bool value)
    {
        if (_box is not null) _box.IndicateEnd = value;
    }

    public override bool HandleAction(string action)
    {
        if (!_active || action != Input.InputMap.Confirm) return false;
        if (!Advance()) return true;
        if (_queue.Count > 0) { _box!.Show(_queue.Dequeue()); return true; }
        Close();
        return true;
    }

    private void Close()
    {
        _active = false;
        _box?.Clear();
        if (AutoHide) SetVisible(false);
        if (TakeFocus) Scene.Focus.Release(this);
        Action? done = _done;
        _done = null;
        done?.Invoke();
    }

    [System.Text.Json.Serialization.JsonIgnore] public override bool IsIdle => !_active;
}
