using System.Text.Json;

namespace Relato.Actions;

/// <summary>
/// One step of an action list. The same shape is used by events (events.json), buttons, control
/// changes, scene and block input handlers, scene hooks and timers. Only the fields of its
/// <see cref="Type"/> are used (see <see cref="ActionTypes"/>).
/// </summary>
public sealed class ActionDefinition
{
    public string Type { get; set; } = "";
    /// <summary>Optional condition: the action is skipped when it is false.</summary>
    public string? If { get; set; }

    // set / add
    public string? Var { get; set; }
    public JsonElement? Value { get; set; }
    public string? Expr { get; set; }

    // sfx / music / dialog (id), message (keys / key + target), black (key, image...), teleport
    public string? Id { get; set; }
    public string? Key { get; set; }
    public List<string>? Keys { get; set; }
    public string? Image { get; set; }
    public string? DrawMode { get; set; }
    public string? CursorMode { get; set; }
    public double Seconds { get; set; }
    public bool Big { get; set; }
    public string? Map { get; set; }
    public int X { get; set; }
    public int Y { get; set; }

    // scenes and focus
    public string? Scene { get; set; }
    public Dictionary<string, JsonElement>? Params { get; set; }
    public bool Wait { get; set; }
    /// <summary>Block id: textbox of a message, target of a focus change.</summary>
    public string? Target { get; set; }
    /// <summary>Focus mode: move (default), share, release.</summary>
    public string? Mode { get; set; }

    // save
    public List<ActionDefinition>? OnSuccess { get; set; }
    public List<ActionDefinition>? OnFail { get; set; }

    // resetBinding
    public string? Action { get; set; }
}

public static class ActionTypes
{
    public const string Set = "set";                     // var, value | expr
    public const string Add = "add";                     // var, value | expr (int)
    public const string Sfx = "sfx";                     // id
    public const string Music = "music";                 // id ("" or missing = stop)
    public const string Open = "open";                   // scene, params?, wait?
    public const string Goto = "goto";                   // scene, params? (replaces the current scene)
    public const string Back = "back";                   // closes the current scene
    public const string CloseAll = "closeAll";           // closes every scene above the base one
    public const string Focus = "focus";                 // target?, mode: move | share | release
    public const string Message = "message";             // target (textbox id), keys | key
    public const string Dialog = "dialog";               // id: conversation (opens the conversation scene and waits)
    public const string Black = "black";                 // key, image?, seconds?, big?, drawMode?, cursorMode? (card scene params: key, image, ms, big, drawMode, cursorMode)
    public const string Teleport = "teleport";           // map, x, y (the player map)
    public const string NewGame = "newGame";
    public const string Continue = "continue";
    public const string Save = "save";                   // onSuccess?, onFail?
    public const string ReturnToTitle = "returnToTitle";
    public const string Quit = "quit";                   // opens the quit scene (exit effect) or closes
    public const string Exit = "exit";                   // closes the game at once
    public const string EndGame = "endGame";             // deletes the save and returns to the title
    public const string ResetBinding = "resetBinding";   // action: input action id
    public const string ResetAllBindings = "resetAllBindings";

    public static readonly string[] All =
    [
        Set, Add, Sfx, Music, Open, Goto, Back, CloseAll, Focus, Message, Dialog, Black, Teleport,
        NewGame, Continue, Save, ReturnToTitle, Quit, Exit, EndGame, ResetBinding, ResetAllBindings,
    ];
}
