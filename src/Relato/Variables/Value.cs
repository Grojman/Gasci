using System.Text.Json;

namespace Relato.Variables;

[System.Text.Json.Serialization.JsonConverter(typeof(System.Text.Json.Serialization.JsonStringEnumConverter))]
public enum VarType { Int, Bool, String }

public enum VarScope { Game, Settings, Session, System }

/// <summary>A typed value of a variable. Bools are stored as 0/1 in <see cref="Int"/>.</summary>
public readonly struct Value : IEquatable<Value>
{
    public VarType Type { get; }
    public int Int { get; }
    public string Str { get; }

    private Value(VarType type, int i, string s)
    {
        Type = type;
        Int = i;
        Str = s;
    }

    public bool Bool => Int != 0;

    public static Value Of(int value) => new(VarType.Int, value, "");
    public static Value Of(bool value) => new(VarType.Bool, value ? 1 : 0, "");
    public static Value Of(string value) => new(VarType.String, 0, value);

    public static Value Default(VarType type) => type switch
    {
        VarType.Int => Of(0),
        VarType.Bool => Of(false),
        _ => Of(""),
    };

    /// <summary>Reads a JSON literal as a value of the given type. Returns false if the JSON has another type.</summary>
    public static bool TryFromJson(JsonElement json, VarType type, out Value value)
    {
        value = Default(type);
        switch (type)
        {
            case VarType.Int when json.ValueKind == JsonValueKind.Number && json.TryGetInt32(out int i):
                value = Of(i);
                return true;
            case VarType.Bool when json.ValueKind is JsonValueKind.True or JsonValueKind.False:
                value = Of(json.GetBoolean());
                return true;
            case VarType.String when json.ValueKind == JsonValueKind.String:
                value = Of(json.GetString()!);
                return true;
            default:
                return false;
        }
    }

    /// <summary>Type of a JSON literal, if it is one the variables can hold.</summary>
    public static VarType? JsonType(JsonElement json) => json.ValueKind switch
    {
        JsonValueKind.Number => VarType.Int,
        JsonValueKind.True or JsonValueKind.False => VarType.Bool,
        JsonValueKind.String => VarType.String,
        _ => null,
    };

    public object ToJsonObject() => Type switch
    {
        VarType.Int => Int,
        VarType.Bool => Bool,
        _ => Str,
    };

    public bool Equals(Value other) => Type == other.Type && Int == other.Int && Str == other.Str;
    public override bool Equals(object? obj) => obj is Value v && Equals(v);
    public override int GetHashCode() => HashCode.Combine(Type, Int, Str);
    public static bool operator ==(Value a, Value b) => a.Equals(b);
    public static bool operator !=(Value a, Value b) => !a.Equals(b);

    public override string ToString() => Type switch
    {
        VarType.Int => Int.ToString(System.Globalization.CultureInfo.InvariantCulture),
        VarType.Bool => Bool ? "true" : "false",
        _ => Str,
    };

    public static string TypeName(VarType type) => type.ToString().ToLowerInvariant();
}
