using Relato.Core;

namespace Relato.Expressions;

/// <summary>An expression that cannot be compiled (syntax, unknown name, types) or that failed when evaluated.</summary>
public sealed class ExpressionException : ContentException
{
    public string ExpressionText { get; }

    public ExpressionException(string source, int position, string message)
        : base($"{message} at position {position + 1} in \"{source}\"") => ExpressionText = source;

    public ExpressionException(string source, string message) : base($"{message} in \"{source}\"") => ExpressionText = source;
}
