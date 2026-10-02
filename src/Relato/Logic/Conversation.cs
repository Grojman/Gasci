using System.Text.Json;
using Relato.Core;

namespace Relato.Logic;

/// <summary>
/// A conversation from assets/data/conversations.json. Nodes hold text keys (dialogs.csv),
/// the portrait frame to draw and the options the player can answer with.
/// </summary>
public sealed class Conversation
{
    /// <summary>Folder in assets/images/characters with the numbered portrait frames.</summary>
    public string Character { get; set; } = "";
    /// <summary>Text key with the name shown over the portrait.</summary>
    public string? Name { get; set; }
    public string DrawMode { get; set; } = "Borders";
    public string CursorMode { get; set; } = "Multiple";
    public string? Color { get; set; }
    public string? Music { get; set; }
    public string Start { get; set; } = "start";
    public Dictionary<string, ConversationNode> Nodes { get; set; } = new();
}

public sealed class ConversationNode
{
    public string? Text { get; set; }
    /// <summary>Portrait frame to show (1.txt, 2.txt...). Null keeps the current one.</summary>
    public int? Image { get; set; }
    public string? DrawMode { get; set; }
    public string? CursorMode { get; set; }
    /// <summary>Changes the portrait while the player is still reading / deciding.</summary>
    public ImageChange? ImageAfter { get; set; }
    /// <summary>Variables to write: { "game.met_shadow": true }.</summary>
    public Dictionary<string, JsonElement>? Set { get; set; }
    public Dictionary<string, int>? Add { get; set; }
    public string? Sfx { get; set; }
    public List<DialogOption>? Options { get; set; }
    /// <summary>Conditional jumps checked in order before <see cref="Next"/>.</summary>
    public List<Branch>? Branch { get; set; }
    public string? Next { get; set; }
}

public sealed class DialogOption
{
    public string Text { get; set; } = "";
    public string? Next { get; set; }
    /// <summary>Condition for the option to be listed.</summary>
    public string? If { get; set; }
    /// <summary>Condition under which the option is shown but cannot be chosen.</summary>
    public string? LockedIf { get; set; }
    /// <summary>The option becomes locked after these seconds of hesitation.</summary>
    public double? LockAfter { get; set; }
    /// <summary>Variables to write: { "game.met_shadow": true }.</summary>
    public Dictionary<string, JsonElement>? Set { get; set; }
    public Dictionary<string, int>? Add { get; set; }
    public string? Sfx { get; set; }
}

public sealed class ImageChange
{
    public double Seconds { get; set; }
    public int Image { get; set; }
    public string? DrawMode { get; set; }
    public string? CursorMode { get; set; }
}

public sealed class Branch
{
    public string? If { get; set; }
    public string Next { get; set; } = "";
}

public sealed class ConversationRepository
{
    private readonly Dictionary<string, Conversation> _conversations;

    private ConversationRepository(Dictionary<string, Conversation> conversations) => _conversations = conversations;

    public IReadOnlyDictionary<string, Conversation> All => _conversations;

    public static ConversationRepository Load(string path) => new(Json.Load<Dictionary<string, Conversation>>(path));

    public Conversation Get(string id) =>
        _conversations.TryGetValue(id, out Conversation? c) ? c : throw new ContentException($"Conversation '{id}' not found");
}
