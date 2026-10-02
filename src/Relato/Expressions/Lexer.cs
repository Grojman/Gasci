using System.Text;

namespace Relato.Expressions;

internal enum TokenKind { Int, String, Name, Op, End }

internal readonly record struct Token(TokenKind Kind, string Text, int Position);

internal static class Lexer
{
    private static readonly string[] TwoCharOps = ["&&", "||", "==", "!=", "<=", ">="];
    private const string OneCharOps = "+-*/%<>!()?:,";

    public static List<Token> Tokenize(string source)
    {
        var tokens = new List<Token>();
        int i = 0;
        while (i < source.Length)
        {
            char c = source[i];
            if (char.IsWhiteSpace(c)) { i++; continue; }
            int start = i;
            if (char.IsLetter(c) || c == '_')
            {
                while (i < source.Length && (char.IsLetterOrDigit(source[i]) || source[i] is '_' or '.')) i++;
                tokens.Add(new Token(TokenKind.Name, source[start..i], start));
            }
            else if (char.IsDigit(c))
            {
                while (i < source.Length && char.IsDigit(source[i])) i++;
                tokens.Add(new Token(TokenKind.Int, source[start..i], start));
            }
            else if (c == '\'')
            {
                var text = new StringBuilder();
                i++;
                while (true)
                {
                    if (i >= source.Length) throw new ExpressionException(source, start, "unterminated string");
                    char s = source[i++];
                    if (s == '\'') break;
                    if (s == '\\' && i < source.Length) s = source[i++];
                    text.Append(s);
                }
                tokens.Add(new Token(TokenKind.String, text.ToString(), start));
            }
            else if (i + 1 < source.Length && TwoCharOps.Contains(source.Substring(i, 2)))
            {
                tokens.Add(new Token(TokenKind.Op, source.Substring(i, 2), start));
                i += 2;
            }
            else if (OneCharOps.Contains(c))
            {
                tokens.Add(new Token(TokenKind.Op, c.ToString(), start));
                i++;
            }
            else throw new ExpressionException(source, i, $"invalid character '{c}'");
        }
        tokens.Add(new Token(TokenKind.End, "", source.Length));
        return tokens;
    }
}
