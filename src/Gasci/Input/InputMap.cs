using Gasci.Core;
using Gasci.Variables;
using SadConsole.Input;

namespace Gasci.Input;

/// <summary>One entry of assets/data/input/actions.json.</summary>
public sealed class InputActionDefinition
{
    /// <summary>Text key shown on the controls page.</summary>
    public string? Label { get; set; }
    /// <summary>Default keys (SadConsole key names: "Up", "W", "Space", "Escape"...), primary first.</summary>
    public List<string> Keys { get; set; } = new();
    /// <summary>
    /// Actions of the same context can be active at the same time, so they cannot share a key.
    /// "global" conflicts with every context.
    /// </summary>
    public string Context { get; set; } = "global";
    /// <summary>The action reports "held" while its key is down (map walking).</summary>
    public bool Repeat { get; set; }
    public bool Rebindable { get; set; } = true;
}

/// <summary>
/// Turns keys into actions. Blocks and scenes never read keys: they ask whether an action was
/// pressed or is held. Enter is fixed: it always means ui.confirm, cannot be bound, and is what
/// starts and cancels listening for a new key on the controls page. Raw keys are only read by text
/// fields and key bind controls, which capture the keyboard while they edit/listen.
/// </summary>
public static class InputMap
{
    public const string Confirm = "ui.confirm";
    public const string Back = "ui.back";
    public const string Up = "ui.up", Down = "ui.down", Left = "ui.left", Right = "ui.right";
    public const string PersistenceKey = "keybindings";
    /// <summary>Actions the engine reacts to; they must be declared.</summary>
    public static readonly string[] Required = [Confirm, Back, Up, Down, Left, Right, "map.up", "map.down", "map.left", "map.right", "map.interact"];
    public static readonly Keys FixedConfirmKey = Keys.Enter;

    private static readonly Dictionary<string, InputActionDefinition> Defs = new();
    private static readonly Dictionary<string, Keys?[]> Bound = new();
    private static readonly List<string> PressedThisFrame = new();
    private static readonly HashSet<string> HeldThisFrame = new();
    private static object? _captureOwner;
    /// <summary>Keys that were down when input was reset; ignored until they are seen released.</summary>
    private static readonly HashSet<Keys> Ignored = new();
    private static bool _ignoreHeld;

    public static int MaxKeys { get; private set; } = 2;
    public static IReadOnlyDictionary<string, InputActionDefinition> Definitions => Defs;
    /// <summary>Actions pressed this frame, in declaration order.</summary>
    public static IReadOnlyList<string> Pressed => PressedThisFrame;
    public static Keyboard? Keyboard { get; private set; }
    public static bool IsCaptured => _captureOwner is not null;

    // ---- Loading -------------------------------------------------------------------------

    public static void Load(string actionsFile, int maxKeys)
    {
        MaxKeys = Math.Max(1, maxKeys);
        Defs.Clear();
        Bound.Clear();
        var problems = new List<string>();
        foreach (var (id, def) in Json.Load<Dictionary<string, InputActionDefinition>>(actionsFile))
        {
            Defs[id] = def;
            if (def.Keys.Count > MaxKeys) problems.Add($"'{id}' has {def.Keys.Count} keys, the maximum is {MaxKeys}");
            foreach (string key in def.Keys)
            {
                if (!TryParseKey(key, out Keys k)) problems.Add($"'{id}': unknown key '{key}'");
                else if (k == FixedConfirmKey) problems.Add($"'{id}': Enter is fixed to ui.confirm and cannot be bound");
            }
        }
        foreach (string required in Required)
            if (!Defs.ContainsKey(required)) problems.Add($"missing engine action '{required}'");
        if (problems.Count > 0) throw new ContentException("actions.json:\n  " + string.Join("\n  ", problems));

        foreach (string id in Defs.Keys) Bound[id] = Defaults(id);
        foreach (string conflict in Conflicts()) problems.Add(conflict);
        if (problems.Count > 0) throw new ContentException("actions.json:\n  " + string.Join("\n  ", problems));
    }

    public static bool TryParseKey(string name, out Keys key) =>
        Enum.TryParse(name, ignoreCase: true, out key) && Enum.IsDefined(key) && key != Keys.None;

    private static Keys?[] Defaults(string id)
    {
        var slots = new Keys?[MaxKeys];
        List<string> keys = Defs[id].Keys;
        for (int i = 0; i < keys.Count && i < MaxKeys; i++) slots[i] = TryParseKey(keys[i], out Keys k) ? k : null;
        return slots;
    }

    private static bool ContextsMeet(string a, string b) => a == b || a == "global" || b == "global";

    /// <summary>Pairs of actions that would react to the same key at the same time.</summary>
    public static IEnumerable<string> Conflicts()
    {
        var ids = Bound.Keys.ToList();
        for (int i = 0; i < ids.Count; i++)
            for (int j = i + 1; j < ids.Count; j++)
            {
                if (!ContextsMeet(Defs[ids[i]].Context, Defs[ids[j]].Context)) continue;
                foreach (Keys? key in Bound[ids[i]])
                    if (key is { } k && Bound[ids[j]].Contains(k))
                        yield return $"'{ids[i]}' and '{ids[j]}' both use {k} in context '{Defs[ids[i]].Context}'";
            }
    }

    /// <summary>Applies the player's overrides (keybindings.json). Broken or conflicting entries are dropped with a warning.</summary>
    public static void LoadOverrides(string file)
    {
        PersistenceScheduler.Register(PersistenceKey, () => SaveOverrides(file));
        if (!File.Exists(file)) return;
        Dictionary<string, List<string?>> overrides;
        try { overrides = Json.Load<Dictionary<string, List<string?>>>(file); }
        catch (Exception e) { Log.Warn($"keybindings.json could not be read, using the defaults: {e.Message}"); return; }

        foreach (var (id, keys) in overrides)
        {
            if (!Defs.TryGetValue(id, out var def) || !def.Rebindable) { Log.Warn($"keybindings.json: unknown or fixed action '{id}' ignored"); continue; }
            var slots = new Keys?[MaxKeys];
            bool valid = true;
            for (int i = 0; i < keys.Count && i < MaxKeys; i++)
            {
                if (keys[i] is null) continue;
                if (TryParseKey(keys[i]!, out Keys k) && k != FixedConfirmKey) slots[i] = k;
                else { valid = false; Log.Warn($"keybindings.json: '{id}' has an invalid key '{keys[i]}'"); }
            }
            if (!valid) continue;
            Keys?[] previous = Bound[id];
            Bound[id] = slots;
            if (Conflicts().Any())
            {
                Bound[id] = previous;
                Log.Warn($"keybindings.json: '{id}' conflicts with another action; using its default keys");
            }
        }
    }

    private static void SaveOverrides(string file)
    {
        var changed = Bound
            .Where(b => !b.Value.SequenceEqual(Defaults(b.Key)))
            .ToDictionary(b => b.Key, b => b.Value.Select(k => k?.ToString()).ToList());
        if (changed.Count == 0) { if (File.Exists(file)) File.Delete(file); return; }
        Json.Save(file, changed);
    }

    // ---- Per frame -----------------------------------------------------------------------

    public static void Update(Keyboard keyboard)
    {
        Keyboard = keyboard;
        PressedThisFrame.Clear();
        HeldThisFrame.Clear();
        if (_ignoreHeld)
        {
            _ignoreHeld = false;
            foreach (AsciiKey k in keyboard.KeysDown) Ignored.Add(k.Key);
        }
        Ignored.RemoveWhere(k => !keyboard.IsKeyDown(k));
        if (IsCaptured) return;

        foreach (var (id, keys) in Bound)
        {
            bool pressed = false, held = false;
            foreach (Keys? key in keys)
            {
                if (key is not { } k || Ignored.Contains(k)) continue;
                pressed |= keyboard.IsKeyPressed(k);
                held |= keyboard.IsKeyDown(k);
            }
            if (id == Confirm && !Ignored.Contains(FixedConfirmKey))
            {
                pressed |= keyboard.IsKeyPressed(FixedConfirmKey);
                held |= keyboard.IsKeyDown(FixedConfirmKey);
            }
            if (pressed) PressedThisFrame.Add(id);
            if (held) HeldThisFrame.Add(id);
        }
    }

    /// <summary>
    /// Ignores every key held right now until it is released. Called when the window changes mode or
    /// regains focus: the window manager may swallow the key-up of the key that caused it, and
    /// SadConsole would then auto-repeat that key forever (a display combobox cycling every mode).
    /// </summary>
    public static void IgnoreHeldKeys() => _ignoreHeld = true;

    public static bool WasPressed(string action) => PressedThisFrame.Contains(action);
    public static bool IsHeld(string action) => HeldThisFrame.Contains(action);

    /// <summary>Gives the raw keyboard to one owner (a text field editing, a key bind listening).</summary>
    public static void Capture(object owner) => _captureOwner = owner;

    public static void ReleaseCapture(object owner)
    {
        if (_captureOwner == owner) _captureOwner = null;
    }

    // ---- Rebinding -----------------------------------------------------------------------

    public static Keys? KeyAt(string action, int slot) =>
        Bound.TryGetValue(action, out var keys) && slot < keys.Length ? keys[slot] : null;

    /// <summary>
    /// Binds a key to a slot of an action. If another action of a meeting context already uses the
    /// key, the two swap: that action receives the key this slot had. Returns the id of the action
    /// that was swapped, if any.
    /// </summary>
    public static string? Bind(string action, int slot, Keys key)
    {
        if (key == FixedConfirmKey) throw new ArgumentException("Enter cannot be bound");
        Keys?[] slots = Bound[action];
        Keys? old = slots[slot];
        if (old == key) return null;

        string? swapped = null;
        foreach (var (otherId, otherKeys) in Bound)
        {
            if (!ContextsMeet(Defs[action].Context, Defs[otherId].Context)) continue;
            for (int i = 0; i < otherKeys.Length; i++)
            {
                if (otherKeys[i] != key || (otherId == action && i == slot)) continue;
                otherKeys[i] = old; // swap (may leave the slot empty if this one was empty)
                swapped = otherId;
            }
        }
        slots[slot] = key;
        PersistenceScheduler.MarkDirty(PersistenceKey);
        return swapped;
    }

    public static void Reset(string action)
    {
        if (!Defs.ContainsKey(action)) throw new ContentException($"resetBinding: unknown input action '{action}'");
        Bound[action] = Defaults(action);
        // Restoring a default may take a key another action was given by a swap: give that one its default too.
        foreach (string other in Bound.Keys.ToList())
            if (other != action && ContextsMeet(Defs[action].Context, Defs[other].Context) && Bound[other].Any(k => k is { } key && Bound[action].Contains(key)))
                Bound[other] = Defaults(other);
        PersistenceScheduler.MarkDirty(PersistenceKey);
    }

    public static void ResetAll()
    {
        foreach (string id in Bound.Keys.ToList()) Bound[id] = Defaults(id);
        PersistenceScheduler.MarkDirty(PersistenceKey);
    }

    // ---- Names ---------------------------------------------------------------------------

    private static readonly Dictionary<Keys, string> ShortNames = new()
    {
        [Keys.Up] = "↑", [Keys.Down] = "↓", [Keys.Left] = "←", [Keys.Right] = "→",
        [Keys.Escape] = "Esc", [Keys.Space] = "Space", [Keys.Enter] = "Enter", [Keys.Back] = "Backspace",
        [Keys.LeftShift] = "LShift", [Keys.RightShift] = "RShift", [Keys.LeftControl] = "LCtrl", [Keys.RightControl] = "RCtrl",
        [Keys.LeftAlt] = "LAlt", [Keys.RightAlt] = "RAlt",
    };

    /// <summary>Name of a key for the screen: the text key "key.&lt;Name&gt;" if it exists, else a short default.</summary>
    public static string KeyName(Keys key)
    {
        string textKey = "key." + key;
        if (GameServices.Loc?.Has(textKey) == true) return GameServices.T(textKey);
        if (ShortNames.TryGetValue(key, out string? name)) return name;
        string s = key.ToString();
        return s.StartsWith('D') && s.Length == 2 && char.IsDigit(s[1]) ? s[1..] : s;
    }
}
