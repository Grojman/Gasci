using Relato.Actions;
using Relato.Blocks;
using Relato.Core;
using Relato.Input;
using Relato.Layout;
using Relato.UI;
using Relato.Variables;

namespace Relato.Scenes;

/// <summary>
/// One screen of the game (title, game, dialog, pause...), built from the blocks of its scene file.
/// Every scene is created once when the game loads. Only the top scene of the
/// <see cref="SceneStack"/> is updated and receives input; the ones below are frozen.
/// </summary>
public sealed class Scene : ScreenObject
{
    private bool _layoutInvalid = true;
    private ScreenSurface? _backdrop;

    public string Id { get; }
    public SceneDefinition Definition { get; }
    public Block Root => Definition.Root;
    public Dictionary<string, Value> Params { get; private set; } = new();
    public FocusManager Focus { get; }
    /// <summary>Every block of the scene, in drawing order.</summary>
    public IReadOnlyList<Block> Blocks { get; }
    public bool IsTop => SceneStack.Instance?.Top == this;

    public Scene(string id, SceneDefinition definition)
    {
        Id = id;
        Definition = definition;
        Focus = new FocusManager(this);
        if (definition.Root is null) throw new ContentException($"scene '{id}': missing 'root'");
        foreach (var (name, type) in definition.Params)
            if (type is not ("int" or "bool" or "string")) throw new ContentException($"scene '{id}': parameter '{name}' has an unknown type '{type}'");
        Root.Attach(this, null);
        Blocks = Root.Descendants().ToList();
        for (int i = 0; i < Blocks.Count; i++) Blocks[i].ZIndex = i;
    }

    // ---- Lifecycle -----------------------------------------------------------------------

    public void Open(Dictionary<string, Value> parameters, bool restoring = false)
    {
        foreach (var (name, type) in Definition.Params)
        {
            if (!parameters.TryGetValue(name, out Value value))
                throw new ContentException($"scene '{Id}' needs the parameter '{name}' ({type})");
            if (Value.TypeName(value.Type) != type)
                throw new ContentException($"scene '{Id}': parameter '{name}' must be {type}, got {Value.TypeName(value.Type)} {value}");
        }
        foreach (string name in parameters.Keys)
            if (!Definition.Params.ContainsKey(name)) throw new ContentException($"scene '{Id}' has no parameter '{name}'");
        Params = parameters;

        if (Definition.Music is { } music) GameServices.Audio?.PlayMusic(music);
        RefreshVisibility();
        // Children first: composite blocks (conversation, card) drive their slots after the slots reset themselves.
        for (int i = Blocks.Count - 1; i >= 0; i--) Blocks[i].OnOpen(restoring);
        InvalidateLayout();
        EnsureLaidOut();
        Focus.Reset();
        if (!restoring) ActionRunner.Run(Definition.OnEnter, new ActionContext(this, null, $"scene '{Id}' onEnter"));
    }

    public void Close()
    {
        foreach (Block block in Blocks) block.OnClose();
        ActionRunner.Run(Definition.OnLeave, new ActionContext(this, null, $"scene '{Id}' onLeave"));
    }

    public void Resume()
    {
        foreach (Block block in Blocks) block.OnResume();
        Focus.Validate();
        ActionRunner.Run(Definition.OnResume, new ActionContext(this, null, $"scene '{Id}' onResume"));
    }

    /// <summary>Text of a property that may reference a parameter: "$map" → the value of the parameter "map".</summary>
    public string? Resolve(string? raw)
    {
        if (raw is null || !raw.StartsWith('$')) return raw;
        string name = raw[1..];
        if (!Definition.Params.ContainsKey(name)) throw new ContentException($"scene '{Id}': '{raw}' refers to an undeclared parameter");
        return Params.TryGetValue(name, out Value v) ? v.ToString() : "";
    }

    // ---- Layout --------------------------------------------------------------------------

    public void InvalidateLayout() => _layoutInvalid = true;

    /// <summary>Re-evaluates visibility and lays the scene out if something changed.</summary>
    public void EnsureLaidOut()
    {
        RefreshVisibility();
        if (_layoutInvalid) Layout();
    }

    private void RefreshVisibility()
    {
        foreach (Block block in Blocks)
        {
            if (block is BranchBlock branch) branch.UpdateCases();
            if (block.RefreshShown()) _layoutInvalid = true;
        }
    }

    private void Layout()
    {
        _layoutInvalid = false;
        Point grid = Display.Grid;
        ScenePresentation p = Definition.Presentation;
        Point measured = Root.Measure(grid);
        int w = Math.Min(grid.X, p.Width.IsAuto ? measured.X : Root.ClampWidth(p.Width.Resolve(grid.X, measured.X), grid.X));
        int h = Math.Min(grid.Y, p.Height.IsAuto ? measured.Y : Root.ClampHeight(p.Height.Resolve(grid.Y, measured.Y), grid.Y));
        Root.Arrange(new Rectangle(LayoutMath.Place(p.X, grid.X, w), LayoutMath.Place(p.Y, grid.Y, h), w, h));
        Cull();
        RebuildSurfaces();
        Focus.Validate();
    }

    /// <summary>Blocks completely covered by an opaque block above them are not drawn.</summary>
    private void Cull()
    {
        var shown = Blocks.Where(b => b.IsShown).ToList();
        foreach (Block b in shown) b.IsCulled = false;
        foreach (Block top in shown.Where(b => b.IsOpaque && b.Bounds.Width > 0))
            foreach (Block below in shown)
            {
                if (below.ZIndex >= top.ZIndex || IsAncestor(below, top)) continue;
                if (below.Bounds.Width > 0 && top.Bounds.Contains(below.Bounds)) below.IsCulled = true;
            }
    }

    private static bool IsAncestor(Block candidate, Block of)
    {
        for (Block? p = of.Parent; p is not null; p = p.Parent)
            if (p == candidate) return true;
        return false;
    }

    /// <summary>Rebuilds the list of surfaces in drawing order (backdrop, then blocks from bottom to top).</summary>
    public void RebuildSurfaces()
    {
        Children.Clear();
        Backdrop backdrop = Definition.Presentation.Backdrop;
        if (backdrop != Backdrop.None)
        {
            if (_backdrop is null || _backdrop.Surface.Width != Display.Grid.X || _backdrop.Surface.Height != Display.Grid.Y || _backdrop.FontSize != Display.CellSize)
                _backdrop = Frame.CreateSurface(Display.Grid.X, Display.Grid.Y, Block.BackdropColor(backdrop));
            Children.Add(_backdrop);
        }
        foreach (Block block in Blocks)
        {
            if (!block.IsShown) continue;
            foreach (IScreenObject surface in block.Surfaces())
            {
                surface.IsVisible = !block.IsCulled;
                if (!Children.Contains(surface)) Children.Add(surface);
            }
        }
    }

    // ---- Update and input ----------------------------------------------------------------

    public override void Update(TimeSpan delta)
    {
        if (!IsEnabled) return;
        EnsureLaidOut();
        foreach (Block block in Blocks) block.Update(delta);
        base.Update(delta);
        EnsureLaidOut();
    }

    /// <summary>Delivers this frame's input actions: image skips, then focused blocks (topmost first), then the scene handlers.</summary>
    public void HandleInput()
    {
        List<string> actions = InputMap.Pressed.ToList();
        if (actions.Count == 0) return;
        List<Block> focused = Focus.Ordered();
        foreach (string action in actions)
        {
            if (Blocks.OfType<ImageBlock>().Where(i => i.IsShown).Aggregate(false, (skipped, image) => image.TrySkip(action) | skipped)) continue;

            bool consumed = false;
            foreach (Block block in focused)
            {
                if (!Focus.Has(block) || !block.IsShown) continue;
                bool handled = block.HandleAction(action);
                if (!handled && block.Inputs?.TryGetValue(action, out var list) == true)
                {
                    ActionRunner.Run(list, new ActionContext(this, block, $"{block.Where} input '{action}'"));
                    handled = true;
                }
                if (block.RaiseEvents) GameServices.Events.NotifyInput(block.Id, action);
                if (handled && !block.PassInput) { consumed = true; break; }
            }
            if (!consumed && Definition.Inputs?.TryGetValue(action, out var handlers) == true)
                ActionRunner.Run(handlers, new ActionContext(this, null, $"scene '{Id}' input '{action}'"));
            if (!IsTop) return; // an action opened another scene: the rest of the input is not for this one
        }
    }

    public void NotifyChange(Block block)
    {
        if (block.RaiseEvents) GameServices.Events.NotifyChange(block.Id);
    }

    /// <summary>A focused block with raiseEvents is idle: events may run.</summary>
    public bool CanRunEvents => Focus.Focused.Any(b => b.RaiseEvents && b.IsShown && b.IsIdle);
}
