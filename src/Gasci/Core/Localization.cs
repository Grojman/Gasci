using System.Text;

namespace Gasci.Core;

/// <summary>
/// Text service: every text box receives a key and asks this service for the real text in the
/// current language. All texts live in assets/data/dialogs.csv (columns: key, es, en, ...).
/// </summary>
public sealed class Localization
{
    private readonly Dictionary<string, Dictionary<string, string>> _texts = new();
    private readonly List<string> _languages = new();

    public IReadOnlyList<string> Languages => _languages;
    public IEnumerable<string> Keys => _texts.Keys;
    public string Language { get; set; } = "";
    public string DefaultLanguage { get; set; } = "";

    public static Localization Load(string path, string defaultLanguage)
    {
        var loc = new Localization { DefaultLanguage = defaultLanguage, Language = defaultLanguage };
        List<List<string>> rows = ParseCsv(File.ReadAllText(path, Encoding.UTF8));
        if (rows.Count == 0) throw new InvalidDataException("dialogs.csv is empty");

        List<string> header = rows[0];
        loc._languages.AddRange(header.Skip(1).Select(h => h.Trim()));

        for (int r = 1; r < rows.Count; r++)
        {
            List<string> row = rows[r];
            if (row.Count == 0 || string.IsNullOrWhiteSpace(row[0]) || row[0].StartsWith('#')) continue;

            var byLanguage = new Dictionary<string, string>();
            for (int c = 1; c < row.Count && c < header.Count; c++)
                if (!string.IsNullOrEmpty(row[c]))
                    byLanguage[header[c].Trim()] = row[c].Replace("\\n", "\n");

            if (!loc._texts.TryAdd(row[0].Trim(), byLanguage))
                throw new InvalidDataException($"dialogs.csv: duplicated key '{row[0]}' (line {r + 1})");
        }
        return loc;
    }

    public bool Has(string key) => _texts.ContainsKey(key);

    /// <summary>Text of a key in a specific language (no fallback).</summary>
    public string Get(string key, string language) =>
        _texts.TryGetValue(key, out var t) && t.TryGetValue(language, out string? text) ? text : "";

    public bool HasLanguage(string key, string language) =>
        _texts.TryGetValue(key, out var t) && t.ContainsKey(language);

    /// <summary>Returns the text in the current language, falling back to the default language and then to the key itself.</summary>
    public string Get(string key)
    {
        if (!_texts.TryGetValue(key, out var byLanguage)) return $"[{key}]";
        if (byLanguage.TryGetValue(Language, out string? text)) return text;
        if (byLanguage.TryGetValue(DefaultLanguage, out text)) return text;
        return byLanguage.Values.FirstOrDefault() ?? $"[{key}]";
    }

    /// <summary>RFC 4180 CSV: comma separated, quotes for fields containing commas, quotes or new lines.</summary>
    private static List<List<string>> ParseCsv(string content)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;

        for (int i = 0; i < content.Length; i++)
        {
            char c = content[i];
            if (quoted)
            {
                if (c == '"' && i + 1 < content.Length && content[i + 1] == '"') { field.Append('"'); i++; }
                else if (c == '"') quoted = false;
                else field.Append(c);
                continue;
            }

            switch (c)
            {
                case '"': quoted = true; break;
                case ',': row.Add(field.ToString()); field.Clear(); break;
                case '\r': break;
                case '\n':
                    row.Add(field.ToString()); field.Clear();
                    rows.Add(row); row = new List<string>();
                    break;
                default: field.Append(c); break;
            }
        }

        if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString()); rows.Add(row); }
        return rows;
    }
}
