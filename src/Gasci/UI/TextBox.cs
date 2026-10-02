using Gasci.Config;
using Gasci.Core;
using Gasci.Layout;

namespace Gasci.UI;

public sealed class TextStyle
{
    public Color Foreground { get; init; } = Theme.Current.Text;
    /// <summary>Default delay between characters, in milliseconds (0 = instant).</summary>
    public int CharDelayMs { get; init; } = 22;
    public bool Border { get; init; } = true;
    public Color BorderColor { get; init; } = Theme.Current.Border;
    public Color Background { get; init; } = Color.Black;
    public bool CenterLines { get; init; }
    public bool TypeSound { get; init; } = true;
    /// <summary>Font size of the whole box, in normal cells. SadConsole cannot mix sizes inside one surface, so size is per box.</summary>
    public int Scale { get; init; } = 1;
}

/// <summary>
/// Text box that receives a localisation key, types the text with a typewriter effect, and
/// splits it into pages when it does not fit. When a page is full a ▼ icon blinks; the owner
/// must call <see cref="Advance"/> to clear the box and show the next page. Inline codes: see
/// <see cref="TextLayout"/>.
/// </summary>
public sealed class TextBox : ScreenSurface
{
    private readonly TextStyle _style;
    private List<List<List<TextToken>>> _pages = new();
    private int _page;
    private int _typed;          // tokens already written on the current page
    private double _timer;
    private double _blink;
    private int _pausedAt = -1;  // token whose {p:...} pause has already been waited

    public TextStyle Style => _style;
    public bool IsTyping { get; private set; }
    public bool HasMorePages => _page < _pages.Count - 1;
    /// <summary>All the text has been written and there are no more pages.</summary>
    public bool IsFinished => !IsTyping && !HasMorePages;
    public bool IsEmpty => _pages.Count == 0;
    /// <summary>Also blink an indicator when the last page is complete (the owner is waiting for a key).</summary>
    public bool IndicateEnd { get; set; }
    /// <summary>Text shown (already translated), to re-paginate after a resize.</summary>
    public string? RawText { get; private set; }

    private int InnerX => _style.Border ? 2 : 0;
    private int InnerY => _style.Border ? 1 : 0;
    private int InnerWidth => Surface.Width - InnerX * 2;
    private int InnerHeight => Surface.Height - InnerY * 2;

    /// <param name="width">Width in cells of the box's own font (normal cells ÷ scale).</param>
    public TextBox(int width, int height, TextStyle? style = null) : base(Math.Max(1, width), Math.Max(1, height))
    {
        _style = style ?? new TextStyle();
        FontSize = Display.CellSize * _style.Scale;
        UsePixelPositioning = true;
        Surface.DefaultBackground = _style.Background;
        DrawEmpty();
    }

    /// <summary>Shows the text associated with a key of the text service.</summary>
    public void Show(string key) => ShowRaw(GameServices.T(key));

    public void ShowRaw(string text)
    {
        RawText = text;
        _pages = Paginate(TextLayout.Wrap(TextLayout.Parse(text, _style.Foreground, _style.CharDelayMs), InnerWidth, CharMap.Current.Glyph(' ')));
        _page = 0;
        StartPage();
    }

    /// <summary>Shows a text already fully written (after a resize), keeping the page if possible.</summary>
    public void ShowFinished(string text, int page)
    {
        ShowRaw(text);
        _page = Math.Clamp(page, 0, _pages.Count - 1);
        StartPage();
        if (IsTyping) FinishPage();
    }

    public int Page => _page;

    public void Clear()
    {
        _pages.Clear();
        RawText = null;
        IsTyping = false;
        DrawEmpty();
    }

    /// <summary>
    /// Player pressed "continue": finishes the page being typed, or clears the box and shows the
    /// next page. Returns true when there was nothing left to show.
    /// </summary>
    public bool Advance()
    {
        if (IsTyping) { FinishPage(); return false; }
        if (HasMorePages)
        {
            _page++;
            StartPage();
            GameServices.Audio?.PlaySfx("page");
            return false;
        }
        return true;
    }

    public override void Update(TimeSpan delta)
    {
        base.Update(delta);
        if (IsTyping) Type(delta.TotalMilliseconds);
        else if (HasMorePages || (IndicateEnd && !IsEmpty))
        {
            _blink += delta.TotalSeconds;
            bool on = _blink % 0.8 < 0.5;
            CharMap map = CharMap.Current;
            int glyph = map.Glyph(HasMorePages ? '▼' : '►');
            int off = _style.Border ? map.Glyph('─') : map.Glyph(' ');
            Surface.SetGlyph(Surface.Width - 3, Surface.Height - 1, on ? glyph : off, on ? Theme.Current.Highlight : _style.BorderColor);
        }
    }

    private void Type(double elapsedMs)
    {
        _timer -= elapsedMs;
        List<List<TextToken>> page = _pages[_page];
        while (_timer <= 0 && IsTyping)
        {
            (int line, int col) = Locate(page, _typed);
            if (line < 0) { IsTyping = false; break; }

            TextToken token = page[line][col];
            if (token.PauseMs > 0 && _pausedAt != _typed)
            {
                _pausedAt = _typed;
                _timer += token.PauseMs;
                continue;
            }

            WriteToken(page, line, col);
            _typed++;
            if (_style.TypeSound && !CharMap.Current.IsBlank(token.Glyph)) GameServices.Audio?.PlayCursor();
            _timer += token.DelayMs;
            if (Locate(page, _typed).line < 0) IsTyping = false;
        }
    }

    private void StartPage()
    {
        DrawEmpty();
        _typed = 0;
        _timer = 0;
        _pausedAt = -1;
        IsTyping = _pages.Count > 0;
        if (IsTyping && _pages[_page].All(l => l.All(t => t.DelayMs == 0 && t.PauseMs == 0))) FinishPage();
    }

    private void FinishPage()
    {
        List<List<TextToken>> page = _pages[_page];
        for (int line = 0; line < page.Count; line++)
            for (int col = 0; col < page[line].Count; col++)
                WriteToken(page, line, col);
        IsTyping = false;
    }

    private void WriteToken(List<List<TextToken>> page, int line, int col)
    {
        int offset = _style.CenterLines ? (InnerWidth - page[line].Count) / 2 : 0;
        TextToken t = page[line][col];
        Surface.SetGlyph(InnerX + offset + col, InnerY + line, t.Glyph, t.Color);
    }

    private static (int line, int col) Locate(List<List<TextToken>> page, int index)
    {
        for (int line = 0; line < page.Count; line++)
        {
            if (index < page[line].Count) return (line, index);
            index -= page[line].Count;
        }
        return (-1, -1);
    }

    private void DrawEmpty()
    {
        Surface.Clear();
        if (_style.Border)
            Frame.Box(Surface, Surface.Area, _style.BorderColor, _style.Background);
        _blink = 0;
    }

    private List<List<List<TextToken>>> Paginate(List<List<TextToken>> lines)
    {
        var pages = new List<List<List<TextToken>>>();
        int height = Math.Max(1, InnerHeight);
        for (int i = 0; i < lines.Count; i += height)
            pages.Add(lines.Skip(i).Take(height).ToList());
        if (pages.Count == 0) pages.Add(new List<List<TextToken>>());
        return pages;
    }
}
