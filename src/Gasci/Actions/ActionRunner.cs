using System.Text.Json;
using Gasci.Blocks;
using Gasci.Core;
using Gasci.Expressions;
using Gasci.Input;
using Gasci.Scenes;
using Gasci.Variables;

namespace Gasci.Actions;

/// <summary>Where an action list runs: the block and scene that started it (for back, focus...).</summary>
public sealed record ActionContext(Scene? Scene, Block? Origin, string Where)
{
    public static ActionContext Event(string id) => new(SceneStack.Instance?.Top, null, $"event '{id}'");
}

/// <summary>
/// Runs action lists in order. Actions that wait (a dialog, a scene opened with "wait", a black
/// screen, a message) suspend the list until they finish.
/// </summary>
public static class ActionRunner
{
    public static void Run(IReadOnlyList<ActionDefinition>? actions, ActionContext context, Action? done = null)
    {
        if (actions is null || actions.Count == 0) { done?.Invoke(); return; }
        new Sequence(actions, context, done).Next();
    }

    private sealed class Sequence(IReadOnlyList<ActionDefinition> actions, ActionContext context, Action? done)
    {
        private int _index;

        public void Next()
        {
            while (_index < actions.Count)
            {
                ActionDefinition action = actions[_index++];
                if (!Expression.IsTrue(action.If)) continue;

                bool returned = false, finishedEarly = false;
                Execute(action, context, () =>
                {
                    if (returned) Next();
                    else finishedEarly = true;
                });
                returned = true;
                if (!finishedEarly) return; // waiting: the callback continues the list later
            }
            done?.Invoke();
        }
    }

    private static void Execute(ActionDefinition a, ActionContext ctx, Action next)
    {
        SceneStack host = SceneStack.Instance ?? throw new InvalidOperationException("No scene stack");
        switch (a.Type)
        {
            case ActionTypes.Set:
                VariableRegistry.Set(a.Var!, ValueOf(a, VariableRegistry.Definition(a.Var!).Type, ctx));
                next();
                break;
            case ActionTypes.Add:
                VariableRegistry.Add(a.Var!, ValueOf(a, VarType.Int, ctx).Int);
                next();
                break;
            case ActionTypes.Sfx: GameServices.Audio?.PlaySfx(a.Id!); next(); break;
            case ActionTypes.Music: GameServices.Audio?.PlayMusic(a.Id); next(); break;
            case ActionTypes.Open:
                if (a.Wait) host.Open(a.Scene!, Params(a.Params), next);
                else { host.Open(a.Scene!, Params(a.Params)); next(); }
                break;
            case ActionTypes.Goto: host.Goto(ctx.Scene, a.Scene!, Params(a.Params)); next(); break;
            case ActionTypes.Back: host.Back(ctx.Scene); next(); break;
            case ActionTypes.CloseAll: host.CloseAll(); next(); break;
            case ActionTypes.Focus:
                (ctx.Origin?.Scene ?? ctx.Scene)?.Focus.Change(ctx.Origin, a.Target, a.Mode);
                next();
                break;
            case ActionTypes.Message:
                host.ShowMessage(a.Target, a.Keys ?? (a.Key is null ? [] : [a.Key]), next, ctx.Where);
                break;
            case ActionTypes.Dialog:
                host.Open(GameServices.Game.ConversationScene, new() { ["conversation"] = Value.Of(a.Id!) }, next);
                break;
            case ActionTypes.Black:
                host.Open(GameServices.Game.CardScene, new()
                {
                    ["key"] = Value.Of(a.Key ?? ""), ["image"] = Value.Of(a.Image ?? ""),
                    ["ms"] = Value.Of((int)Math.Round(a.Seconds * 1000)), ["big"] = Value.Of(a.Big),
                    ["drawMode"] = Value.Of(a.DrawMode ?? ""), ["cursorMode"] = Value.Of(a.CursorMode ?? ""),
                }, next);
                break;
            case ActionTypes.Teleport: host.Teleport(a.Map!, a.X, a.Y); next(); break;
            case ActionTypes.NewGame: host.NewGame(); next(); break;
            case ActionTypes.Continue: host.ContinueGame(); next(); break;
            case ActionTypes.Save:
                Run(host.SaveGame() ? a.OnSuccess : a.OnFail, ctx, next);
                break;
            case ActionTypes.ReturnToTitle: host.ReturnToTitle(); next(); break;
            case ActionTypes.Quit: host.Quit(); next(); break;
            case ActionTypes.Exit: host.Exit(); break;
            case ActionTypes.EndGame: host.EndGame(); break;
            case ActionTypes.ResetBinding: InputMap.Reset(a.Action!); next(); break;
            case ActionTypes.ResetAllBindings: InputMap.ResetAll(); next(); break;
            default: throw new ContentException($"{ctx.Where}: unknown action type '{a.Type}'");
        }
    }

    /// <summary>Value of a set/add action: the "value" literal or the "expr" expression.</summary>
    public static Value ValueOf(ActionDefinition a, VarType type, ActionContext ctx)
    {
        if (a.Expr is { } expr)
        {
            Expression e = Expression.Compile(expr);
            if (e.Type != type)
                throw new ContentException($"{ctx.Where}: '{expr}' is a {Value.TypeName(e.Type)}, {a.Var} needs a {Value.TypeName(type)}");
            return e.Evaluate();
        }
        if (a.Value is { } json && Value.TryFromJson(json, type, out Value v)) return v;
        throw new ContentException($"{ctx.Where}: '{a.Type}' on {a.Var} needs a {Value.TypeName(type)} 'value' or an 'expr'");
    }

    /// <summary>Applies the set / add dictionaries of conversation nodes and options.</summary>
    public static void Apply(Dictionary<string, JsonElement>? set, Dictionary<string, int>? add, string where)
    {
        if (set is not null)
            foreach (var (name, json) in set)
            {
                VariableDefinition def = VariableRegistry.Definition(name);
                if (!Value.TryFromJson(json, def.Type, out Value v))
                    throw new ContentException($"{where}: {name} needs a {Value.TypeName(def.Type)}, got {json.GetRawText()}");
                VariableRegistry.Set(def, v);
            }
        if (add is not null)
            foreach (var (name, delta) in add) VariableRegistry.Add(name, delta);
    }

    /// <summary>Converts JSON scene parameters to values.</summary>
    public static Dictionary<string, Value> Params(Dictionary<string, JsonElement>? json)
    {
        var result = new Dictionary<string, Value>();
        if (json is null) return result;
        foreach (var (name, element) in json)
            result[name] = Value.JsonType(element) is { } t && Value.TryFromJson(element, t, out Value v)
                ? v
                : throw new ContentException($"Scene parameter '{name}' must be a number, bool or string");
        return result;
    }
}
