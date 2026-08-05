using System;
using System.Linq;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Looks up OpenFeature context attributes for the conditions that match on them.
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
    /// </remarks>
    public static bool IsOneOf(EvaluationContext? context, string key, string[] values)
        => context is not null && context.AsDictionary().Any(entry =>
            entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            && entry.Value.AsString is { } attribute
            && values.Contains(attribute, StringComparer.OrdinalIgnoreCase));
}
