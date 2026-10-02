using Relato.Actions;
using Relato.Core;
using Relato.Expressions;
using Relato.Variables;

namespace Relato.Logic;

/// <summary>What the event engine needs from the game.</summary>
public interface IEventHost
{
    /// <summary>
    /// True when the top scene has a focused, idle block with raiseEvents (the map, when the player
    /// is free; an inventory menu...). Events wait until then.
    /// </summary>
    bool CanRunEvents { get; }
}

/// <summary>
/// Runs the events of events.json. Events are indexed by what triggers them, so a trigger only looks
/// at its own events, and their condition is checked at that moment (no re-evaluation of every event
/// on every variable change). Immediate events have no trigger: they are re-checked only when a
/// variable their condition reads changes (or every time, if it reads system.* values).
/// </summary>
public sealed class EventEngine
{
    private readonly List<EventDefinition> _definitions;
    private readonly Dictionary<string, List<EventDefinition>> _byTrigger = new();
    private readonly List<EventDefinition> _immediate = new();
    private readonly Dictionary<string, List<EventDefinition>> _immediateByVariable = new();
    private readonly List<EventDefinition> _volatileImmediate = new();
    private readonly HashSet<EventDefinition> _dirty = new();
    private readonly List<EventDefinition> _ready = new();
    private readonly HashSet<string> _completed = new();
    private EventDefinition? _running;

    public IEventHost? Host { get; set; }

    public IReadOnlyList<EventDefinition> Definitions => _definitions;
    public IReadOnlyCollection<string> Completed => _completed;
    public IEnumerable<string> ReadyIds => _ready.Select(e => e.Id);
    public bool IsBusy => _running is not null;

    public EventEngine(List<EventDefinition> definitions)
    {
        _definitions = definitions;
        foreach (EventDefinition e in definitions)
        {
            if (e.Trigger.Type == TriggerTypes.Immediate)
            {
                _immediate.Add(e);
                IReadOnlySet<string> reads = e.If is null ? new HashSet<string>() : Expression.Condition(e.If).Variables;
                if (reads.Any(v => v.StartsWith("system."))) _volatileImmediate.Add(e);
                foreach (string variable in reads)
                {
                    if (!_immediateByVariable.TryGetValue(variable, out var list)) _immediateByVariable[variable] = list = new();
                    list.Add(e);
                }
            }
            else
            {
                string key = Key(e.Trigger);
                if (!_byTrigger.TryGetValue(key, out var list)) _byTrigger[key] = list = new();
                list.Add(e);
            }
        }
        VariableRegistry.Changed += OnVariableChanged;
        _dirty.UnionWith(_immediate);
    }

    private static string Key(EventTrigger t) => t.Type switch
    {
        TriggerTypes.EnterMap => $"enter:{t.Map}",
        TriggerTypes.Step or TriggerTypes.Halfway => $"map:{t.Map}",
        TriggerTypes.Input => $"input:{t.Block ?? "*"}",
        TriggerTypes.Change => $"change:{t.Block ?? "*"}",
        _ => t.Type,
    };

    private void OnVariableChanged(VariableDefinition def)
    {
        if (_immediateByVariable.TryGetValue(def.Name, out var events)) _dirty.UnionWith(events);
    }

    // ---- State ---------------------------------------------------------------------------

    public void Reset()
    {
        _completed.Clear();
        _ready.Clear();
        _running = null;
        _dirty.UnionWith(_immediate);
    }

    public void Restore(IEnumerable<string> completed, IEnumerable<string> ready)
    {
        Reset();
        _completed.UnionWith(completed);
        foreach (string id in ready)
            if (_definitions.FirstOrDefault(d => d.Id == id) is { } def) _ready.Add(def);
    }

    // ---- Triggers ------------------------------------------------------------------------

    public void NotifyEnterMap(string map) => Fire($"enter:{map}", _ => true);

    public void NotifyStep(string map, Point position, int sectionX, int previousLocalX, int localX, int sectionWidth)
    {
        int half = sectionWidth / 2;
        bool crossedHalf = previousLocalX < half && localX >= half;
        Fire($"map:{map}", e => e.Trigger.Type switch
        {
            TriggerTypes.Step => position.X >= e.Trigger.X && position.X < e.Trigger.X + e.Trigger.W
                              && position.Y >= e.Trigger.Y && position.Y < e.Trigger.Y + e.Trigger.H,
            TriggerTypes.Halfway => crossedHalf && (e.Trigger.Section is null || e.Trigger.Section == sectionX),
            _ => false,
        });
    }

    public void NotifyInput(string? block, string action)
    {
        bool Matches(EventDefinition e) => e.Trigger.Action is null || e.Trigger.Action == action;
        if (block is not null) Fire($"input:{block}", Matches);
        Fire("input:*", Matches);
    }

    public void NotifyChange(string? block)
    {
        if (block is not null) Fire($"change:{block}", _ => true);
        Fire("change:*", _ => true);
    }

    private void Fire(string key, Func<EventDefinition, bool> matches)
    {
        if (!_byTrigger.TryGetValue(key, out var events)) return;
        foreach (EventDefinition e in events)
            if (!_completed.Contains(e.Id) && e != _running && !_ready.Contains(e) && matches(e) && Expression.IsTrue(e.If))
                _ready.Add(e);
    }

    // ---- Execution -----------------------------------------------------------------------

    public void Update()
    {
        if (IsBusy || Host is not { CanRunEvents: true }) return;

        _dirty.UnionWith(_volatileImmediate);
        foreach (EventDefinition e in _immediate)
        {
            if (!_dirty.Remove(e) || _completed.Contains(e.Id) || _ready.Contains(e)) continue;
            if (Expression.IsTrue(e.If)) _ready.Add(e);
        }

        // An event could have become impossible between its trigger and now (another event ran first).
        while (_ready.Count > 0)
        {
            EventDefinition next = _ready[0];
            _ready.RemoveAt(0);
            if (_completed.Contains(next.Id) || !Expression.IsTrue(next.If)) continue;
            Start(next);
            return;
        }
    }

    private void Start(EventDefinition e)
    {
        _running = e;
        ActionRunner.Run(e.Actions, ActionContext.Event(e.Id), () => Finish(e));
    }

    private void Finish(EventDefinition finished)
    {
        if (_running != finished) return; // the game was reset while it ran (endGame, return to title)
        _running = null;
        if (finished.Once || finished.Trigger.Type == TriggerTypes.Immediate) _completed.Add(finished.Id);
    }

    /// <summary>Stops the running event without completing it (the game is being reset).</summary>
    public void Abort() => _running = null;
}
