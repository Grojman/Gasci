using Gasci.Core;
using Gasci.Variables;

namespace Gasci.Layout;

/// <summary>A character ready to be drawn: glyph, colour and typewriter timing.</summary>
public readonly record struct TextToken(int Glyph, Color Color, int DelayMs, int PauseMs)
{
    public const int NewLineGlyph = -1;
    public bool IsNewLine => Glyph == NewLineGlyph;
}

/// <summary>
/// Parsing of the inline codes of the texts and word wrap, shared by the text boxes, the text
/// blocks and the text metrics. Codes:
///   {c:Red} / {c:#ff0000}  colour ({c} resets)     {d:80}  ms per character ({d} resets)
///   {p:600}                pause before the next character
///   {v:game.player_name}   value of a variable
///   {k:map.interact}       name of the (primary) key bound to an input action
/// </summary>
public static class TextLayout
{
    public static List<TextToken> Parse(string text, Color foreground, int delayMs)
    {
        var tokens = new List<TextToken>();
        CharMap map = CharMap.Current;
        int spaceGlyph = map.Glyph(' ');
        Color color = foreground;
        int delay = delayMs, pause = 0;

        void Add(char c)
        {
            tokens.Add(c == '\n' ? new TextToken(TextToken.NewLineGlyph, color, 0, 0) : new TextToken(c == ' ' ? spaceGlyph : map.Glyph(c), color, delay, pause));
            if (c != '\n') pause = 0;
        }

        for (int i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (c == '{' && text.IndexOf('}', i) is var end && end > i)
            {
                string[] tag = text[(i + 1)..end].Split(':', 2);
                string arg = tag.Length > 1 ? tag[1] : "";
                bool known = true;
                switch (tag[0])
                {
                    case "c": color = arg == "" ? foreground : ColorParser.Parse(arg, foreground); break;
                    case "d": delay = int.TryParse(arg, out int d) ? d : delayMs; break;
                    case "p": pause += int.TryParse(arg, out int p) ? p : 0; break;
                    case "v": foreach (char vc in VariableRegistry.Get(arg).ToString()) Add(vc); break;
                    case "k": foreach (char kc in KeyText(arg)) Add(kc); break;
                    default: known = false; break;
                }
                if (known) { i = end; continue; }
            }
            Add(c);
        }
        return tokens;
    }

    /// <summary>Width reserved for a {k:...} code when measuring (key names are short: "Esc", "Space", "↑").</summary>
    public const int KeyCodeWidth = 6;

    public static string KeyText(string action) =>
        Input.InputMap.KeyAt(action, 0) is { } key ? Input.InputMap.KeyName(key) : action == Input.InputMap.Confirm ? "Enter" : "-";

    /// <summary>Text without inline codes; {v:...} codes become <paramref name="variableWidth"/> placeholder characters.</summary>
    public static string Strip(string text, Func<string, int>? variableWidth = null)
    {
        var result = new System.Text.StringBuilder(text.Length);
        for (int i = 0; i < text.Length; i++)
        {
            if (text[i] == '{' && text.IndexOf('}', i) is var end && end > i)
            {
                string[] tag = text[(i + 1)..end].Split(':', 2);
                if (tag[0] is "c" or "d" or "p" or "v" or "k")
                {
                    if (tag[0] == "v") result.Append('x', variableWidth?.Invoke(tag.Length > 1 ? tag[1] : "") ?? 0);
                    if (tag[0] == "k") result.Append('x', KeyCodeWidth);
                    i = end;
                    continue;
                }
            }
            result.Append(text[i]);
        }
        return result.ToString();
    }

    /// <summary>Word wrap: breaks lines at spaces, hard-breaks words longer than the width.</summary>
    public static List<List<TextToken>> Wrap(IReadOnlyList<TextToken> tokens, int width, int spaceGlyph)
    {
        var lines = new List<List<TextToken>>();
        var line = new List<TextToken>();
        var word = new List<TextToken>();
        width = Math.Max(1, width);

        void FlushWord()
        {
            if (word.Count == 0) return;
            if (line.Count > 0 && line.Count + word.Count > width)
            {
                TrimEnd(line, spaceGlyph);
                lines.Add(line);
                line = new List<TextToken>();
            }
            foreach (TextToken t in word)
            {
                if (line.Count >= width) { lines.Add(line); line = new List<TextToken>(); }
                line.Add(t);
            }
            word.Clear();
        }

        foreach (TextToken t in tokens)
        {
            if (t.IsNewLine)
            {
                FlushWord();
                lines.Add(line);
                line = new List<TextToken>();
            }
            else if (t.Glyph == spaceGlyph)
            {
                FlushWord();
                if (line.Count > 0 && line.Count < width) line.Add(t);
            }
            else word.Add(t);
        }
        FlushWord();
        if (line.Count > 0) { TrimEnd(line, spaceGlyph); lines.Add(line); }
        return lines;
    }

    private static void TrimEnd(List<TextToken> line, int spaceGlyph)
    {
        while (line.Count > 0 && line[^1].Glyph == spaceGlyph) line.RemoveAt(line.Count - 1);
    }

    /// <summary>Number of lines a plain text (no codes) takes when wrapped at a width.</summary>
    public static int CountLines(string plain, int width)
    {
        var tokens = plain.Select(c => new TextToken(c == '\n' ? TextToken.NewLineGlyph : c, Color.White, 0, 0)).ToList();
        return Math.Max(1, Wrap(tokens, width, ' ').Count);
    }
}
