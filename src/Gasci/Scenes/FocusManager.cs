using Gasci.Blocks;
using Gasci.Core;

namespace Gasci.Scenes;

/// <summary>
/// The blocks of a scene that receive input. Focus changes:
///   move    — the origin loses focus and the target gains it (the previous set is remembered);
///   share   — the target gains focus and the origin keeps it;
///   release — the origin loses focus; if nothing is left focused, the last remembered set comes back.
/// Hidden or disabled blocks lose focus as if they released it.
/// </summary>
public sealed class FocusManager(Scene scene)
{
    private readonly List<Block> _focused = new();
    private readonly Stack<List<Block>> _history = new();

    public IReadOnlyList<Block> Focused => _focused;

    public bool Has(Block block) => _focused.Contains(block);

    public void Reset()
    {
        _focused.Clear();
        _history.Clear();
        _focused.AddRange(Defaults());
    }

    private IEnumerable<Block> Defaults() => scene.Blocks.Where(b => b.Focus && b.IsShown && b.IsEnabled);

    /// <summary>Applies a "focus" action: target block id (or none for release) and mode.</summary>
    public void Change(Block? origin, string? targetId, string? mode)
    {
        Block? target = targetId is null ? null : SceneStack.Instance.FindBlock(targetId);
        if (targetId is not null && target is null) throw new ContentException($"focus: there is no block '{targetId}'");
        if (target is not null && target.Scene != scene) throw new ContentException($"focus: block '{targetId}' is in scene '{target.Scene.Id}', not in '{scene.Id}'");
        switch (mode ?? "move")
        {
            case "move": Move(origin, target ?? throw new ContentException("focus move needs a 'target'")); break;
            case "share": Share(target ?? throw new ContentException("focus share needs a 'target'")); break;
            case "release": Release(origin ?? target ?? throw new ContentException("focus release needs an origin or a 'target'")); break;
            default: throw new ContentException($"unknown focus mode '{mode}' (move, share, release)");
        }
    }

    /// <summary>Moves the focus from <paramref name="origin"/> (or from every focused block, if null) to the target.</summary>
    public void Move(Block? origin, Block target)
    {
        _history.Push(_focused.ToList());
        if (origin is null) _focused.Clear();
        else _focused.Remove(origin);
        if (!_focused.Contains(target)) _focused.Add(target);
    }

    public void Share(Block target)
    {
        if (!_focused.Contains(target)) _focused.Add(target);
    }

    public void Release(Block origin)
    {
        _focused.Remove(origin);
        if (_focused.Count == 0) Restore();
    }

    private void Restore()
    {
        while (_history.Count > 0)
        {
            List<Block> previous = _history.Pop().Where(b => b.IsShown && b.IsEnabled).ToList();
            if (previous.Count == 0) continue;
            _focused.AddRange(previous);
            return;
        }
        _focused.AddRange(Defaults());
    }

    /// <summary>Drops blocks that became hidden or disabled (called after every layout/visibility change).</summary>
    public void Validate()
    {
        if (_focused.RemoveAll(b => !b.IsShown || !b.IsEnabled) > 0 && _focused.Count == 0) Restore();
    }

    /// <summary>Focused blocks, topmost first (the order in which they receive input).</summary>
    public List<Block> Ordered() => _focused.OrderByDescending(b => b.ZIndex).ToList();
}
