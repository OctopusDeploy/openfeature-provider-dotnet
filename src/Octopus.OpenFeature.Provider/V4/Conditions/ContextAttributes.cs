using System;
using System.Linq;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Looks up OpenFeature context attributes for the conditions that match on them. Shared by both
/// attribute conditions, which carry the same fields and so read them the same way.
/// </summary>
internal static class ContextAttributes
{
    /// <summary>
    /// Whether the context holds an attribute named <paramref name="key"/> with one of
    /// <paramref name="values"/>.
    /// </summary>
    /// <remarks>
    /// Mirrors v3 segment matching (OctopusFeatureContext.MatchesSegment): keys and values compare
    /// case-insensitively and a non-string value counts as absent. *Every* entry whose key matches is
    /// checked — a context can hold several case variants of one key, and AsDictionary's order is
    /// unstable, so taking the first evaluated inconsistently from one process to the next.
    ///
    /// <paramref name="key"/> and <paramref name="values"/> are declared non-nullable but nothing
    /// enforces that on a deserialised payload. A condition the server could not have sent is a
    /// malformed response, so it fails the evaluation rather than being matched against as far as it can
    /// be — an attribute condition with nothing to match on has no defensible answer.
    /// </remarks>
    public static bool IsOneOf(ClientSideEvaluationContext context, string? key, string[]? values)
    {
        if (key is null)
        {
            throw context.ParseError("a context-attribute condition with no key");
        }

        if (values is not { Length: > 0 })
        {
            throw context.ParseError($"a context-attribute condition on '{key}' with no values");
        }

        if (values.Any(value => value is null))
        {
            throw context.ParseError($"a context-attribute condition on '{key}' with a missing value");
        }

        return context.OpenFeatureContext is { } openFeatureContext
               && openFeatureContext.AsDictionary().Any(entry =>
                   entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
                   && entry.Value.AsString is { } attribute
                   && values.Contains(attribute, StringComparer.OrdinalIgnoreCase));
    }
}
