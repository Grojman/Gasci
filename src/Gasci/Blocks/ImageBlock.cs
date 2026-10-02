using Gasci.Core;
using Gasci.Input;
using Gasci.Layout;
using Gasci.Rendering;
using Gasci.UI;

namespace Gasci.Blocks;

/// <summary>
/// An ASCII image, drawn by a <see cref="DrawCursor"/> and fitted to the block as CSS object-fit does
/// (contain, cover, fill, none, scale-down) at a position ("center", "top left"...). The image file
/// only holds the drawing: how it is shown is decided here.
/// </summary>
public sealed class ImageBlock : Block
{
    /// <summary>Image key ("general/title") or a scene parameter ("$image"). Empty = nothing.</summary>
    public string? Image { get; set; }
    public string Fit { get; set; } = "contain";
    /// <summary>Where the image sits in the block: "center", "top", "bottom left"...</summary>
    public string Position { get; set; } = "center";
    public string DrawMode { get; set; } = "Random";
    public string CursorMode { get; set; } = "Multiple";
    public string? Color { get; set; }
    /// <summary>Seconds the whole drawing takes (wins over <see cref="Speed"/>).</summary>
    public double Duration { get; set; }
    /// <summary>Normal cells drawn per second.</summary>
    public double Speed { get; set; } = 700;
    /// <summary>ui.confirm / ui.back complete the drawing first, whatever has the focus.</summary>
    public bool Skippable { get; set; } = true;
    /// <summary>Image characters per cell side for fit "none" (default: game.json images.detail).</summary>
    public int? Detail { get; set; }

    private ScreenSurface? _canvas;
    private DrawCursor? _cursor;
    private AsciiImage? _image;
    private Color _color;
    private DrawMode _drawMode;
    private CursorMode _cursorMode;
    private bool _warnedCut, _animatePending;
    private Rectangle _drawnBounds;
    private Point _drawnCell;

    [System.Text.Json.Serialization.JsonIgnore] public bool IsFinished => _cursor?.IsFinished ?? true;
    [System.Text.Json.Serialization.JsonIgnore] public bool IsDrawing => _cursor is { IsFinished: false };

    /// <summary>Smallest image character, in pixels, at the current font scale.</summary>
    public static Point MinCharSize
    {
        get
        {
            double[] min = GameServices.Game.Images.MinCharSize;
            int scale = Display.FontScale;
            return new Point(Math.Max(1, (int)Math.Round(min[0] * scale)), Math.Max(1, (int)Math.Round(min[1] * scale)));
        }
    }

    protected override void OnAttach()
    {
        if (ParseFit(Fit) is null) throw new ContentException($"{Where}: unknown fit '{Fit}' (contain, cover, fill, none, scale-down)");
        if (!Enum.TryParse<DrawMode>(DrawMode, true, out _)) throw new ContentException($"{Where}: unknown drawMode '{DrawMode}'");
        if (!Enum.TryParse<CursorMode>(CursorMode, true, out _)) throw new ContentException($"{Where}: unknown cursorMode '{CursorMode}'");
    }

    public static ImageFit? ParseFit(string fit) => fit.ToLowerInvariant() switch
    {
        "contain" => ImageFit.Contain,
        "cover" => ImageFit.Cover,
        "fill" => ImageFit.Fill,
        "none" => ImageFit.None,
        "scale-down" or "scaledown" => ImageFit.ScaleDown,
        _ => null,
    };

    private Point DetailCharSize
    {
        get
        {
            int detail = Math.Max(1, Detail ?? GameServices.Game.Images.Detail);
            return new Point(Math.Max(1, Display.CellSize.X / detail), Math.Max(1, Display.CellSize.Y / detail));
        }
    }

    public override void OnOpen(bool restoring)
    {
        if (Image is null) return; // driven by a composite block (conversation portrait, card image)
        string? key = Resolve(Image);
        if (string.IsNullOrEmpty(key)) { Clear(); return; }
        Show(GameServices.Images.Get(key), DrawMode, CursorMode, Color, animate: !restoring);
    }

    protected override Point MeasureContent(Point available)
    {
        if (_image is null && Resolve(Image) is { Length: > 0 } key && GameServices.Images.Exists(key)) _image = GameServices.Images.Get(key);
        if (_image is null) return Point.Zero;
        // Natural size: the image at the default detail ("none"), in normal cells.
        Point detail = DetailCharSize;
        return new Point(
            (int)Math.Ceiling(_image.Width * detail.X / (double)Display.CellSize.X),
            (int)Math.Ceiling(_image.Height * detail.Y / (double)Display.CellSize.Y));
    }

    protected override void OnArrange()
    {
        bool sameArea = Surface is not null && Bounds == _drawnBounds && Display.CellSize == _drawnCell;
        EnsureSurface(BackgroundColor);
        if (sameArea && !_animatePending) return; // re-layout elsewhere in the scene: keep the drawing (and its animation)
        Surface!.Surface.Clear();
        _drawnBounds = Bounds;
        _drawnCell = Display.CellSize;
        // A pending Show (or a drawing still in progress) animates; a resize of a finished image redraws it at once.
        if (_image is not null) Draw(animate: _animatePending || IsDrawing);
        _animatePending = false;
    }

    /// <summary>Shows an image; frames with the same size reuse the canvas, so only what changed is redrawn.</summary>
    public void Show(AsciiImage image, string? drawMode, string? cursorMode, string? color, bool animate = true)
    {
        _image = image;
        _drawMode = Enum.TryParse(drawMode, true, out DrawMode d) ? d : Enum.Parse<DrawMode>(DrawMode, true);
        _cursorMode = Enum.TryParse(cursorMode, true, out CursorMode c) ? c : Enum.Parse<CursorMode>(CursorMode, true);
        _color = color is null ? ContentColor() : Config.Theme.Current.Color(color, Style ?? "text");
        if (Surface is not null && Bounds.Width > 0) Draw(animate);
        else _animatePending = animate;
    }

    public void Clear()
    {
        _cursor?.Complete();
        _cursor = null;
        _image = null;
        if (_canvas is not null) Surface?.Children.Remove(_canvas);
        _canvas = null;
    }

    private void Draw(bool animate)
    {
        if (Surface is null || _image is null) return;
        _cursor?.Complete();
        var area = new Point(Bounds.Width * Display.CellSize.X, Bounds.Height * Display.CellSize.Y);
        ImageFit fit = ParseFit(Fit)!.Value;
        Point charSize = ImageCanvas.CharSizeFor(fit, area, _image, MinCharSize, DetailCharSize);

        if (_canvas is null || _canvas.FontSize != charSize || _canvas.Surface.Width != area.X / charSize.X || _canvas.Surface.Height != area.Y / charSize.Y)
        {
            if (_canvas is not null) Surface.Children.Remove(_canvas);
            _canvas = ImageCanvas.Create(Surface, new Rectangle(0, 0, area.X, area.Y), charSize);
        }

        ICellSurface canvas = _canvas.Surface;
        if (!_warnedCut && fit is ImageFit.Contain or ImageFit.ScaleDown && (_image.Width > canvas.Width || _image.Height > canvas.Height))
        {
            _warnedCut = true;
            Log.Warn($"{Where}: image is {_image.Width}x{_image.Height} but only {canvas.Width}x{canvas.Height} characters fit at the minimum character size; it is cut");
        }

        (Anchor ax, Anchor ay) = ParsePosition(Position);
        var offset = new Point(LayoutMath.Place(ax, canvas.Width, _image.Width), LayoutMath.Place(ay, canvas.Height, _image.Height));
        _cursor = ImageCanvas.Scaled(new DrawCursor(canvas, canvas.Area, _image, offset, _color, _drawMode, _cursorMode), _canvas);
        _cursor.CellsPerSecond = Speed * _cursor.CellsPerSecond / 700;
        if (Duration > 0) _cursor.SetDuration(Duration);
        if (!animate) _cursor.Complete();
    }

    public static (Anchor x, Anchor y) ParsePosition(string position)
    {
        Anchor x = Anchor.Center, y = Anchor.Center;
        foreach (string word in position.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            switch (word)
            {
                case "left": x = Anchor.Start; break;
                case "right": x = Anchor.End; break;
                case "top": y = Anchor.Start; break;
                case "bottom": y = Anchor.End; break;
            }
        return (x, y);
    }

    public void Complete() => _cursor?.Complete();

    public override void Update(TimeSpan delta) => _cursor?.Update(delta);

    /// <summary>Completes the drawing when the player presses confirm or back (called by the scene for every shown image).</summary>
    internal bool TrySkip(string action)
    {
        if (!Skippable || !IsDrawing || action is not (InputMap.Confirm or InputMap.Back)) return false;
        _cursor!.Complete();
        return true;
    }
}
