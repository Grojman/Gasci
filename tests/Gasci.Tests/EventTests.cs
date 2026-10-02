using Gasci.Logic;
using Gasci.Variables;

namespace Gasci.Tests;

[Collection("registry")]
public sealed class EventTests
{
    private sealed class Host : IEventHost
    {
        public bool CanRunEvents => true;
    }

    public EventTests() => TestContent.LoadVariables(TestContent.Game);

    private static EventEngine Engine(params EventDefinition[] events) => new(events.ToList()) { Host = new Host() };

    [Fact]
    public void TriggeredEventsCheckTheirConditionAtTriggerTime()
    {
        var e = new EventDefinition { Id = "enter", If = "game.courage >= 1", Trigger = new() { Type = TriggerTypes.EnterMap, Map = "home" } };
        EventEngine engine = Engine(e);
        engine.NotifyEnterMap("home");
        Assert.Empty(engine.ReadyIds);
        VariableRegistry.Set("game.courage", Value.Of(1));
        engine.NotifyEnterMap("street");
        Assert.Empty(engine.ReadyIds);
        engine.NotifyEnterMap("home");
        Assert.Equal(["enter"], engine.ReadyIds);
    }

    [Fact]
    public void StepTriggersOnlyInsideTheirArea()
    {
        var e = new EventDefinition { Id = "bench", Trigger = new() { Type = TriggerTypes.Step, Map = "park", X = 10, Y = 2, W = 3, H = 1 } };
        EventEngine engine = Engine(e);
        engine.NotifyStep("park", new Point(9, 2), 0, 0, 0, 98);
        Assert.Empty(engine.ReadyIds);
        engine.NotifyStep("park", new Point(12, 2), 0, 0, 0, 98);
        Assert.Equal(["bench"], engine.ReadyIds);
    }

    [Fact]
    public void InputTriggersMatchBlockAndAction()
    {
        var e = new EventDefinition { Id = "use", Trigger = new() { Type = TriggerTypes.Input, Block = "inventory", Action = "ui.confirm" } };
        EventEngine engine = Engine(e);
        engine.NotifyInput("inventory", "ui.back");
        engine.NotifyInput("other", "ui.confirm");
        Assert.Empty(engine.ReadyIds);
        engine.NotifyInput("inventory", "ui.confirm");
        Assert.Equal(["use"], engine.ReadyIds);
    }

    [Fact]
    public void CompletedEventsDoNotTriggerAgain()
    {
        var e = new EventDefinition { Id = "once", Trigger = new() { Type = TriggerTypes.EnterMap, Map = "home" } };
        EventEngine engine = Engine(e);
        engine.Restore(["once"], []);
        engine.NotifyEnterMap("home");
        Assert.Empty(engine.ReadyIds);
    }
}
