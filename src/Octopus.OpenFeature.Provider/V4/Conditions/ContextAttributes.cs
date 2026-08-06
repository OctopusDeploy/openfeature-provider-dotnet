using System;
using System.Linq;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// The attribute lookup shared by both attribute conditions.
/// </summary>
internal static class ContextAttributes
{
    /// <summary>
    /// Whether the context holds an attribute named <paramref name="key"/> with one of
    /// <paramref name="values"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors v3 segment matching: keys and values compare case-insensitively and a non-string value
    /// counts as absent. Every entry whose key matches is checked, not just the first — a context can
    /// hold several case variants of one key, and AsDictionary's order is unstable across processes.
    /// </remarks>
    public static bool IsOneOf(ClientSideEvaluationContext context, string? key, string[]? values)
    {
        if (key is null)
        {
            throw new ParseErrorException("A condition is missing a key.");
        }

        if (values is not { Length: > 0 })
        {
            throw new ParseErrorException("A condition is missing values.");
        }

        if (values.Any(value => value is null))
        {
            throw new ParseErrorException("A condition is missing a value.");
        }

        return context.OpenFeatureContext is { } openFeatureContext
               && openFeatureContext.AsDictionary().Any(entry =>
                   entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                   && entry.Value.AsString is { } attribute
                   && values.Contains(attribute, StringComparer.OrdinalIgnoreCase));
    }
}
