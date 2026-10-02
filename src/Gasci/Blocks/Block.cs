using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Gasci.Actions;
using Gasci.Blocks.Controls;
using Gasci.Config;
using Gasci.Core;
using Gasci.Expressions;
using Gasci.Layout;
using Gasci.Scenes;
using Gasci.UI;
using Size = Gasci.Layout.Size;

namespace Gasci.Blocks;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum Backdrop { None, Dim, Opaque }

/// <summary>
/// A piece of a scene: containers, texts, images, maps, menus... Blocks are read from the scene files
/// (the "type" property picks the class) and live as long as the game: their ids are unique in the
/// whole game, so any block can be addressed from anywhere (events, focus changes, map messages).
///
/// Layout is done in two passes: <see cref="Measure"/> (how much space the block wants, given the
/// space available) and <see cref="Arrange"/> (the rectangle it finally gets, in grid cells).
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type", UnknownDerivedTypeHandling = JsonUnknownDerivedTypeHandling.FailSerialization)]
[JsonDerivedType(typeof(StackBlock), "stack")]
[JsonDerivedType(typeof(OverlayBlock), "overlay")]
[JsonDerivedType(typeof(MenuBlock), "menu")]
[JsonDerivedType(typeof(BranchBlock), "branch")]
[JsonDerivedType(typeof(TextBlock), "text")]
[JsonDerivedType(typeof(TextBoxBlock), "textbox")]
[JsonDerivedType(typeof(ImageBlock), "image")]
[JsonDerivedType(typeof(MapBlock), "map")]
[JsonDerivedType(typeof(ConversationBlock), "conversation")]
[JsonDerivedType(typeof(ChoicesBlock), "choices")]
[JsonDerivedType(typeof(CardBlock), "card")]
[JsonDerivedType(typeof(BarBlock), "bar")]
[JsonDerivedType(typeof(EffectBlock), "effect")]
[JsonDerivedType(typeof(TimerBlock), "timer")]
[JsonDerivedType(typeof(SpacerBlock), "spacer")]
[JsonDerivedType(typeof(ButtonControl), "button")]
[JsonDerivedType(typeof(SwitchControl), "switch")]
[JsonDerivedType(typeof(SliderControl), "slider")]
[JsonDerivedType(typeof(ComboBoxControl), "combobox")]
[JsonDerivedType(typeof(NumberFieldControl), "numberfield")]
[JsonDerivedType(typeof(TextFieldControl), "textfield")]
[JsonDerivedType(typeof(KeyBindControl), "keybind")]
public abstract class Block
{
    // ---- Properties read from JSON ---------------------------------------------------------

    public string? Id { get; set; }
    public Size Width { get; set; } = Size.Auto;
    public Size Height { get; set; } = Size.Auto;
    public Size? MinWidth { get; set; }
    public Size? MaxWidth { get; set; }
    public Size? MinHeight { get; set; }
    public Size? MaxHeight { get; set; }
    /// <summary>Horizontal anchor inside the parent (the cross axis in a vertical stack).</summary>
    public Anchor? X { get; set; }
    /// <summary>Vertical anchor inside the parent.</summary>
    public Anchor? Y { get; set; }
    /// <summary>Cells to move the block after anchoring: [x, y].</summary>
    public int[]? Offset { get; set; }
    public string? VisibleIf { get; set; }
    public string? EnabledIf { get; set; }
    /// <summary>Initial visibility (the engine shows and hides some blocks, e.g. message boxes).</summary>
    public bool Visible { get; set; } = true;
    /// <summary>Theme colour name or literal colour of the block's content.</summary>
    public string? Style { get; set; }
    /// <summary>Background colour of the block's area (opaque backgrounds hide what is below).</summary>
    public string? Background { get; set; }
    /// <summary>Initially focused (receives input when the scene opens).</summary>
    public bool Focus { get; set; }
    /// <summary>Layer drawn under the block over the whole scene, for popups inside a scene.</summary>
    public Backdrop Backdrop { get; set; }
    /// <summary>Action lists run when the block has focus and an input action is pressed.</summary>
    public Dictionary<string, List<ActionDefinition>>? Inputs { get; set; }
    /// <summary>The block notifies the event engine of its input and changes, and lets events run while it is focused and idle.</summary>
    public bool RaiseEvents { get; set; }
    /// <summary>React to input without consuming it (lower focused blocks also receive it).</summary>
    public bool PassInput { get; set; }

    // ---- Runtime -----------------------------------------------------------------------------

    [JsonIgnore] public Scene Scene { get; private set; } = null!;
    [JsonIgnore] public Block? Parent { get; private set; }
    /// <summary>Rectangle in grid cells, relative to the screen.</summary>
    [JsonIgnore] public Rectangle Bounds { get; private set; }
    /// <summary>Order of the block in the scene (later = drawn on top).</summary>
    [JsonIgnore] public int ZIndex { get; internal set; }
    [JsonIgnore] public bool IsShown { get; private set; }
    /// <summary>Hidden because an opaque block above covers it completely.</summary>
    [JsonIgnore] public bool IsCulled { get; internal set; }
    /// <summary>Set by a branch: false when its case is not the one shown.</summary>
    [JsonIgnore] internal bool BranchActive { get; set; } = true;
    [JsonIgnore] public string Name => Id ?? $"<{GetType().Name.Replace("Block", "").Replace("Control", "").ToLowerInvariant()}>";
    [JsonIgnore] public string Where => $"scene '{Scene?.Id}', block {Name}";

    private Expression? _visibleIf, _enabledIf;
    private ScreenSurface? _backdropSurface;
    protected ScreenSurface? Surface;

    [JsonIgnore] public virtual IEnumerable<Block> Children => [];

    /// <summary>Every block of the subtree, parents before children (= drawing order).</summary>
    public IEnumerable<Block> Descendants()
    {
        yield return this;
        foreach (Block child in Children)
            foreach (Block b in child.Descendants()) yield return b;
    }

    internal void Attach(Scene scene, Block? parent)
    {
        Scene = scene;
        Parent = parent;
        _visibleIf = Compile(VisibleIf);
        _enabledIf = Compile(EnabledIf);
        OnAttach();
        foreach (Block child in Children) child.Attach(scene, this);
    }

    protected Expression? Compile(string? condition)
    {
        if (string.IsNullOrWhiteSpace(condition)) return null;
        try { return Expression.Condition(condition); }
        catch (ExpressionException e) { throw new ContentException($"{Where}: {e.Message}"); }
    }

    /// <summary>Called once when the scenes are loaded (check properties, compile expressions).</summary>
    protected virtual void OnAttach() { }

    /// <summary>Re-evaluates visibility; returns true if it changed (the scene then re-lays out).</summary>
    internal bool RefreshShown()
    {
        bool shown = Visible && BranchActive && (Parent?.IsShown ?? true) && (_visibleIf?.EvalBool() ?? true);
        bool changed = shown != IsShown;
        IsShown = shown;
        return changed;
    }

    /// <summary>Visibility for the validator, which measures scenes without opening them.</summary>
    internal void RefreshShownForValidation() => RefreshShown();

    [JsonIgnore] public bool IsEnabled => (_enabledIf?.EvalBool() ?? true) && (Parent?.IsEnabled ?? true);

    /// <summary>Shows or hides the block (re-layout happens on the next update).</summary>
    public void SetVisible(bool visible)
    {
        if (Visible == visible) return;
        Visible = visible;
        Scene?.InvalidateLayout();
    }

    // ---- Layout ------------------------------------------------------------------------------

    /// <summary>Size the block wants in a space, after applying its width/height and min/max.</summary>
    public Point Measure(Point available)
    {
        Point content = MeasureContent(available);
        int w = Width.IsAuto ? content.X : Width.Resolve(available.X, content.X);
        int h = Height.IsAuto ? content.Y : Height.Resolve(available.Y, content.Y);
        return new Point(ClampWidth(w, available.X), ClampHeight(h, available.Y));
    }

    public int ClampWidth(int w, int available) =>
        Math.Max(0, Math.Min(Math.Max(w, MinWidth?.Resolve(available, 0) ?? 0), MaxWidth?.Resolve(available, int.MaxValue) ?? int.MaxValue));

    public int ClampHeight(int h, int available) =>
        Math.Max(0, Math.Min(Math.Max(h, MinHeight?.Resolve(available, 0) ?? 0), MaxHeight?.Resolve(available, int.MaxValue) ?? int.MaxValue));

    /// <summary>Natural size of the content (what "auto" means for this block).</summary>
    protected abstract Point MeasureContent(Point available);

    public void Arrange(Rectangle bounds)
    {
        Bounds = bounds;
        OnArrange();
    }

    /// <summary>Called with the final <see cref="Bounds"/>: (re)create surfaces, lay out children.</summary>
    protected virtual void OnArrange() { }

    /// <summary>Surfaces of this block only (not of its children), bottom first.</summary>
    public virtual IEnumerable<IScreenObject> Surfaces()
    {
        if (Backdrop != Backdrop.None && IsShown)
        {
            _backdropSurface ??= Frame.CreateSurface(1, 1, Color.Transparent);
            if (_backdropSurface.Surface.Width != Display.Grid.X || _backdropSurface.Surface.Height != Display.Grid.Y || _backdropSurface.FontSize != Display.CellSize)
            {
                _backdropSurface = Frame.CreateSurface(Display.Grid.X, Display.Grid.Y, BackdropColor(Backdrop));
            }
            yield return _backdropSurface;
        }
        if (Surface is not null) yield return Surface;
    }

    public static Color BackdropColor(Backdrop backdrop) => backdrop switch
    {
        Backdrop.Dim => new Color(0, 0, 0, 170),
        Backdrop.Opaque => Color.Black,
        _ => Color.Transparent,
    };

    /// <summary>True when the block paints every cell of its bounds with an opaque colour.</summary>
    [JsonIgnore] public virtual bool IsOpaque => Background is not null && Theme.Current.Color(Background).A == 255;

    /// <summary>A surface covering <see cref="Bounds"/>, recreated only if its size or cell size changed.</summary>
    protected ScreenSurface EnsureSurface(Color background, int scale = 1)
    {
        int w = Math.Max(1, Bounds.Width / scale), h = Math.Max(1, Bounds.Height / scale);
        if (Surface is null || Surface.Surface.Width != w || Surface.Surface.Height != h || Surface.FontSize != Display.CellSize * scale)
            Surface = Frame.CreateSurface(w, h, background, scale);
        Surface.Surface.DefaultBackground = background;
        Surface.Position = Bounds.Position * Display.CellSize;
        return Surface;
    }

    protected Color BackgroundColor => Background is null ? Color.Transparent : Theme.Current.Color(Background);
    protected Color ContentColor(string fallback = "text") => Theme.Current.Color(Style, fallback);

    // ---- Behaviour ---------------------------------------------------------------------------

    /// <summary>The scene was opened (with new parameters) or restored from a save.</summary>
    public virtual void OnOpen(bool restoring) { }
    public virtual void OnClose() { }
    public virtual void OnResume() { }
    /// <summary>Called every frame while the scene is the top one, for every block (shown or not).</summary>
    public virtual void Update(TimeSpan delta) { }
    /// <summary>An input action while the block is focused. Return true to consume it.</summary>
    public virtual bool HandleAction(string action) => false;
    /// <summary>Whether events may run while this (focused, raiseEvents) block is in its current state.</summary>
    [JsonIgnore] public virtual bool IsIdle => true;
    /// <summary>State stored in the save file (null = nothing to store).</summary>
    public virtual JsonNode? SaveState() => null;
    public virtual void RestoreState(JsonNode state) { }

    /// <summary>Text of a property that may reference a scene parameter ("$map").</summary>
    protected string? Resolve(string? raw) => Scene.Resolve(raw);

    /// <summary>A control changed its value: tell the event engine if this block raises events.</summary>
    protected void NotifyChanged() => Scene.NotifyChange(this);
}
