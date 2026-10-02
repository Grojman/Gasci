using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relato.Variables;

/// <summary>One entry of a template file (assets/data/variables/&lt;scope&gt;.json).</summary>
public sealed class VariableDefinition
{
    public VarType Type { get; set; }
    /// <summary>Mandatory: a variable without a default is a design error and stops the game from loading.</summary>
    public JsonElement? Default { get; set; }
    public int? Min { get; set; }
    public int? Max { get; set; }
    public int? MaxLength { get; set; }
    /// <summary>Allowed values of a string variable.</summary>
    public List<string>? Options { get; set; }
    /// <summary>Allowed values taken from an engine list: "languages".</summary>
    public string? OptionsFrom { get; set; }
    /// <summary>Engine side effect called when the value changes (see <see cref="Bindings"/>).</summary>
    public string? Bind { get; set; }

    /// <summary>Full name including the scope prefix ("game.courage").</summary>
    [JsonIgnore] public string Name { get; internal set; } = "";
    [JsonIgnore] public VarScope Scope { get; internal set; }
    [JsonIgnore] public Value DefaultValue { get; internal set; }
    [JsonIgnore] internal int Slot { get; set; } = -1;
    /// <summary>Read-only engine value (system scope).</summary>
    [JsonIgnore] internal Func<Value>? Getter { get; set; }
    /// <summary>Allowed string values once <see cref="OptionsFrom"/> is resolved.</summary>
    [JsonIgnore] public IReadOnlyList<string>? AllowedValues { get; internal set; }

    public bool IsReadOnly => Scope == VarScope.System;

    /// <summary>Widest text this variable can print (used to measure texts with {v:...} codes).</summary>
    public int MaxTextWidth => Type switch
    {
        VarType.Bool => 5,
        VarType.Int => Math.Max((Min ?? -99999).ToString().Length, (Max ?? 999999).ToString().Length),
        _ => AllowedValues?.Max(v => v.Length) ?? MaxLength ?? 0,
    };

    /// <summary>Whether the value follows the rules of this variable; <paramref name="reason"/> says why not.</summary>
    public bool Accepts(Value value, out string reason)
    {
        reason = "";
        if (value.Type != Type) { reason = $"expects {Value.TypeName(Type)}, got {Value.TypeName(value.Type)}"; return false; }
        if (Type == VarType.Int)
        {
            if (Min is { } min && value.Int < min) { reason = $"{value.Int} is below the minimum {min}"; return false; }
            if (Max is { } max && value.Int > max) { reason = $"{value.Int} is above the maximum {max}"; return false; }
        }
        if (Type == VarType.String)
        {
            if (MaxLength is { } length && value.Str.Length > length) { reason = $"\"{value.Str}\" is longer than {length} characters"; return false; }
            if (AllowedValues is { } allowed && !allowed.Contains(value.Str)) { reason = $"\"{value.Str}\" is not one of: {string.Join(", ", allowed)}"; return false; }
        }
        return true;
    }
}
