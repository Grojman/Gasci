using Gasci.Actions;

namespace Gasci.Logic;

/// <summary>
/// An event from assets/data/events.json: when its trigger happens and its condition is true (and it
/// has not been completed), its actions run.
/// </summary>
public sealed class EventDefinition
{
    public string Id { get; set; } = "";
    public string? If { get; set; }
    public EventTrigger Trigger { get; set; } = new();
    /// <summary>Once events are completed after running; the others can run again on their next trigger.</summary>
    public bool Once { get; set; } = true;
    public List<ActionDefinition> Actions { get; set; } = new();
}

public static class TriggerTypes
{
    /// <summary>Runs as soon as its condition is true and the game can run events.</summary>
    public const string Immediate = "immediate";
    /// <summary>Runs when the player enters <see cref="EventTrigger.Map"/>.</summary>
    public const string EnterMap = "enterMap";
    /// <summary>Runs when the player crosses the middle of a section from left to right.</summary>
    public const string Halfway = "halfway";
    /// <summary>Runs when the player steps inside the area X,Y,W,H.</summary>
    public const string Step = "step";
    /// <summary>Runs when a block with raiseEvents receives an input action (block?, action?).</summary>
    public const string Input = "input";
    /// <summary>Runs when a control with raiseEvents changes its value (block?).</summary>
    public const string Change = "change";

    public static readonly string[] All = [Immediate, EnterMap, Halfway, Step, Input, Change];
}

public sealed class EventTrigger
{
    public string Type { get; set; } = TriggerTypes.Immediate;
    public string? Map { get; set; }
    /// <summary>Horizontal section index for "halfway" (null = any section).</summary>
    public int? Section { get; set; }
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; } = 1;
    public int H { get; set; } = 1;
    /// <summary>Block id for input/change triggers (null = any block with raiseEvents).</summary>
    public string? Block { get; set; }
    /// <summary>Input action id for input triggers (null = any action).</summary>
    public string? Action { get; set; }
}
