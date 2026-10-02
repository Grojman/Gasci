using System.Text.Json;
using System.Text.Json.Serialization;

namespace Relato.Core;

public static class Json
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        WriteIndented = true,
        // "type" does not need to be the first property of a polymorphic object (blocks, layouts).
        AllowOutOfOrderMetadataProperties = true,
    };

    public static T Load<T>(string path)
    {
        try
        {
            return JsonSerializer.Deserialize<T>(File.ReadAllText(path), Options)
                ?? throw new InvalidDataException("the file is empty");
        }
        catch (JsonException e)
        {
            throw new InvalidDataException($"{Path.GetFileName(path)}: {e.Message}", e);
        }
    }

    public static void Save<T>(string path, T value) =>
        File.WriteAllText(path, JsonSerializer.Serialize(value, Options));
}
