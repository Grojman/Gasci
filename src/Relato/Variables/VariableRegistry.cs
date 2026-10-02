using System.Text.Json;
using Relato.Core;

namespace Relato.Variables;

/// <summary>
/// Every variable of the game, kept in memory. Names carry their scope as a prefix:
///   game.*     declared in variables/game.json, stored in the save, reset by New game.
///   settings.* declared in variables/settings.json, stored in settings.json (debounced).
///   session.*  declared in variables/session.json, never stored.
///   system.*   read-only values computed by the engine.
/// Writes are checked here, in one place: ints are clamped to min/max, invalid strings are rejected,
/// and both cases are logged. Values loaded from files that break the rules reset to the default.
/// </summary>
public static class VariableRegistry
{
    public const string SettingsPersistenceKey = "settings";

    private static readonly Dictionary<string, VariableDefinition> Defs = new();
    private static Value[] _values = [];

    /// <summary>Raised after a variable changes (not for system values).</summary>
    public static event Action<VariableDefinition>? Changed;

    public static IEnumerable<VariableDefinition> Definitions => Defs.Values;

    public static string Prefix(VarScope scope) => scope.ToString().ToLowerInvariant();

    public static VarScope? ScopeOf(string name)
    {
        int dot = name.IndexOf('.');
        if (dot <= 0) return null;
        return Enum.TryParse(name[..dot], ignoreCase: false, out VarScope s) && Prefix(s) == name[..dot] ? s : null;
    }

    // ---- Loading -------------------------------------------------------------------------

    /// <summary>
    /// Loads the three template files from <paramref name="folder"/>. Every problem is collected and
    /// reported in one <see cref="ContentException"/>: the game must not start with a broken template.
    /// </summary>
    public static void Load(string folder, IReadOnlyList<string> languages)
    {
        Defs.Clear();
        var problems = new List<string>();
        foreach (VarScope scope in new[] { VarScope.Game, VarScope.Settings, VarScope.Session })
        {
            string file = Path.Combine(folder, Prefix(scope) + ".json");
            if (!File.Exists(file)) continue;
            Dictionary<string, VariableDefinition> declared;
            try { declared = Json.Load<Dictionary<string, VariableDefinition>>(file); }
            catch (Exception e) { problems.Add($"{Path.GetFileName(file)}: {e.Message}"); continue; }

            foreach (var (shortName, def) in declared)
            {
                def.Name = $"{Prefix(scope)}.{shortName}";
                def.Scope = scope;
                Prepare(def, languages, problems);
                Defs[def.Name] = def;
            }
        }
        RegisterSystem();
        if (problems.Count > 0)
            throw new ContentException("Variable templates:\n  " + string.Join("\n  ", problems));

        _values = new Value[Defs.Count];
        int slot = 0;
        foreach (VariableDefinition def in Defs.Values)
        {
            def.Slot = slot++;
            _values[def.Slot] = def.DefaultValue;
        }
    }

    private static void Prepare(VariableDefinition def, IReadOnlyList<string> languages, List<string> problems)
    {
        string where = def.Name;
        if (def.OptionsFrom is not null)
        {
            if (def.OptionsFrom == "languages") def.AllowedValues = languages;
            else problems.Add($"{where}: unknown optionsFrom '{def.OptionsFrom}' (known: languages)");
        }
        else if (def.Options is not null) def.AllowedValues = def.Options;
        if ((def.Options is not null || def.OptionsFrom is not null || def.MaxLength is not null) && def.Type != VarType.String)
            problems.Add($"{where}: options and maxLength only apply to string variables");
        if ((def.Min is not null || def.Max is not null) && def.Type != VarType.Int)
            problems.Add($"{where}: min and max only apply to int variables");
        if (def.Min > def.Max) problems.Add($"{where}: min is bigger than max");

        if (def.Default is not { } json || json.ValueKind == JsonValueKind.Undefined)
        {
            problems.Add($"{where}: missing 'default' (every variable needs one)");
            return;
        }
        if (!Value.TryFromJson(json, def.Type, out Value value))
        {
            problems.Add($"{where}: default {json.GetRawText()} is not a {Value.TypeName(def.Type)}");
            return;
        }
        if (!def.Accepts(value, out string reason)) problems.Add($"{where}: default breaks its own rules ({reason})");
        def.DefaultValue = value;

        if (def.Bind is not null)
        {
            if (!Bindings.Known.TryGetValue(def.Bind, out VarType bindType))
                problems.Add($"{where}: unknown binding '{def.Bind}' (known: {string.Join(", ", Bindings.Known.Keys)})");
            else if (bindType != def.Type)
                problems.Add($"{where}: binding '{def.Bind}' needs a {Value.TypeName(bindType)} variable");
            else if (def.Scope != VarScope.Settings)
                problems.Add($"{where}: bindings are only allowed on settings variables");
            else if (Defs.Values.Any(d => d.Bind == def.Bind))
                problems.Add($"{where}: binding '{def.Bind}' is already used by another variable");
        }
    }

    private static readonly Dictionary<string, (VarType type, Func<Value> getter)> SystemValues = new();

    /// <summary>Registers a read-only engine value, e.g. system.save_exists. Call before <see cref="Load"/>.</summary>
    public static void DefineSystem(string shortName, VarType type, Func<Value> getter) =>
        SystemValues[shortName] = (type, getter);

    /// <summary>Names of the system values the engine provides (declared even when no getter is installed, for validation).</summary>
    public static readonly (string name, VarType type)[] BuiltInSystem =
    [
        ("save_exists", VarType.Bool), ("can_save", VarType.Bool), ("in_game", VarType.Bool),
        ("display_mode", VarType.String), ("language", VarType.String),
    ];

    private static void RegisterSystem()
    {
        foreach (var (name, type) in BuiltInSystem)
            if (!SystemValues.ContainsKey(name)) SystemValues[name] = (type, () => Value.Default(type));
        foreach (var (shortName, (type, getter)) in SystemValues)
        {
            var def = new VariableDefinition { Type = type, Name = "system." + shortName, Scope = VarScope.System, Getter = getter };
            def.DefaultValue = Value.Default(type);
            Defs[def.Name] = def;
        }
    }

    // ---- Reading -------------------------------------------------------------------------

    public static bool IsDeclared(string name) => Defs.ContainsKey(name);

    public static VariableDefinition? Find(string name) => Defs.GetValueOrDefault(name);

    /// <summary>The definition of a variable; undeclared names are a content error.</summary>
    public static VariableDefinition Definition(string name) =>
        Defs.TryGetValue(name, out VariableDefinition? def) ? def : throw new ContentException(Undeclared(name));

    public static string Undeclared(string name) =>
        ScopeOf(name) is null
            ? $"Variable '{name}' has no scope prefix (game., settings., session. or system.)"
            : $"Variable '{name}' is not declared in assets/data/variables/{name[..name.IndexOf('.')]}.json";

    public static Value Get(string name) => Read(Definition(name));

    public static Value Read(VariableDefinition def) => def.Getter is { } getter ? getter() : _values[def.Slot];

    public static int GetInt(string name) => Get(name).Int;
    public static bool GetBool(string name) => Get(name).Bool;
    public static string GetString(string name) => Get(name).Str;

    // ---- Writing -------------------------------------------------------------------------

    /// <summary>
    /// Writes a variable. Ints out of range are clamped and strings that break their rules are
    /// rejected; both are logged as warnings. Returns true if the stored value changed.
    /// </summary>
    public static bool Set(string name, Value value) => Set(Definition(name), value);

    public static bool Set(VariableDefinition def, Value value)
    {
        if (def.IsReadOnly) throw new ContentException($"{def.Name} is read-only");
        if (value.Type != def.Type)
        {
            Log.Error($"{def.Name}: cannot store a {Value.TypeName(value.Type)} in a {Value.TypeName(def.Type)} variable; ignored");
            return false;
        }
        if (!def.Accepts(value, out string reason))
        {
            if (def.Type == VarType.Int)
            {
                int clamped = Math.Clamp(value.Int, def.Min ?? int.MinValue, def.Max ?? int.MaxValue);
                Log.Warn($"{def.Name}: {reason}; clamped to {clamped}");
                value = Value.Of(clamped);
            }
            else
            {
                Log.Warn($"{def.Name}: {reason}; the value was not changed");
                return false;
            }
        }
        if (_values[def.Slot] == value) return false;
        _values[def.Slot] = value;
        OnChanged(def, value);
        return true;
    }

    public static bool Add(string name, int delta)
    {
        VariableDefinition def = Definition(name);
        if (def.Type != VarType.Int) throw new ContentException($"'add' needs an int variable, {name} is {Value.TypeName(def.Type)}");
        return Set(def, Value.Of(Read(def).Int + delta));
    }

    private static void OnChanged(VariableDefinition def, Value value)
    {
        if (def.Scope == VarScope.Settings) PersistenceScheduler.MarkDirty(SettingsPersistenceKey);
        Bindings.Apply(def, value);
        Changed?.Invoke(def);
    }

    // ---- Scopes and files ----------------------------------------------------------------

    /// <summary>Sets every variable of a scope back to its default (New game resets the game scope).</summary>
    public static void Reset(VarScope scope)
    {
        foreach (VariableDefinition def in Defs.Values.Where(d => d.Scope == scope))
        {
            if (_values[def.Slot] == def.DefaultValue) continue;
            _values[def.Slot] = def.DefaultValue;
            OnChanged(def, def.DefaultValue);
        }
    }

    /// <summary>
    /// Loads stored values of a scope. Unknown names are dropped, and values with the wrong type or
    /// outside the rules reset to the default; every case is logged as a warning.
    /// </summary>
    public static void LoadValues(VarScope scope, IReadOnlyDictionary<string, JsonElement>? values, string source)
    {
        Reset(scope);
        if (values is null) return;
        string prefix = Prefix(scope) + ".";
        foreach (var (shortName, json) in values)
        {
            string name = prefix + shortName;
            if (!Defs.TryGetValue(name, out VariableDefinition? def))
            {
                Log.Warn($"{source}: '{name}' is no longer declared; its value was dropped");
                continue;
            }
            if (!Value.TryFromJson(json, def.Type, out Value value))
            {
                Log.Warn($"{source}: {name} = {json.GetRawText()} is not a {Value.TypeName(def.Type)}; using the default {def.DefaultValue}");
                continue;
            }
            if (!def.Accepts(value, out string reason))
            {
                Log.Warn($"{source}: {name}: {reason}; using the default {def.DefaultValue}");
                continue;
            }
            if (_values[def.Slot] == value) continue;
            _values[def.Slot] = value;
            Bindings.Apply(def, value);
            Changed?.Invoke(def);
        }
    }

    /// <summary>Values of a scope keyed by their short name, ready to be serialised.</summary>
    public static Dictionary<string, object> Snapshot(VarScope scope)
    {
        string prefix = Prefix(scope) + ".";
        return Defs.Values.Where(d => d.Scope == scope)
            .ToDictionary(d => d.Name[prefix.Length..], d => _values[d.Slot].ToJsonObject());
    }

    /// <summary>Calls every binding with the current value (after loading the settings).</summary>
    public static void ApplyBindings()
    {
        foreach (VariableDefinition def in Defs.Values.Where(d => d.Bind is not null))
            Bindings.Apply(def, _values[def.Slot]);
    }
}
