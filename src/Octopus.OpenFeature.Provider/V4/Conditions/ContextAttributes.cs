using System;
using System.Linq;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Looks up OpenFeature context attributes for the conditions that match on them, and validates the
/// shape those conditions arrived in.
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
    /// enforces that on a deserialised payload, so both are guarded: a malformed condition is rejected
    /// before evaluation, and matching must not throw if one ever reaches here.
    /// </remarks>
    public static bool IsOneOf(EvaluationContext? context, string? key, string[]? values)
        => context is not null && key is not null && values is not null && context.AsDictionary().Any(entry =>
            entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            && entry.Value.AsString is { } attribute
            && values.Contains(attribute, StringComparer.OrdinalIgnoreCase));

    /// <summary>
    /// Describes why a condition matching on <paramref name="key"/> against <paramref name="values"/>
    /// is not well-formed, or returns <c>null</c> when it is. Shared by both attribute conditions,
    /// which carry the same fields and so are malformed in the same ways.
    /// </summary>
    public static string? Validate(string? key, string[]? values)
    {
        if (key is null)
        {
            return "a context-attribute condition with no key";
        }

        if (values is not { Length: > 0 })
        {
            return $"a context-attribute condition on '{key}' with no values";
        }

        return values.Any(value => value is null)
            ? $"a context-attribute condition on '{key}' with a missing value"
            : null;
    }
}
