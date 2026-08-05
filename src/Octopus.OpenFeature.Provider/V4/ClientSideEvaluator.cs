using System;
using System.Linq;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Evaluates the client-side portion of a v4 flag response, returning the resolved value and a
/// reason.
///
/// A flag the server already resolved carries a <see cref="EvaluationResource.Value"/> and reason,
/// which are surfaced unchanged. A flag deferred to the client carries rules: the flag is enabled
/// when any rule matches, and a rule matches when all of its conditions match.
/// </summary>
internal static class ClientSideEvaluator
{
    public static ResolutionDetails<bool> Evaluate(EvaluationResource flag, EvaluationContext? context)
    {
        // The server resolved the flag; surface its value and reason unchanged.
        if (flag.Value is { } value)
        {
            return Resolved(flag, value, flag.Reason);
        }

        // A deferred flag is enabled when any rule matches. Only percentage-by-context needs the
        // evaluation key, so a missing one is handled there rather than skipping rule evaluation
        // outright — an attribute-only rule can still be matched without it.
        if (flag.Rules is not null)
        {
            var matchedRule = flag.Rules.FirstOrDefault(rule => Matches(rule, flag.EvaluationKey, context));
            if (matchedRule is not null)
            {
                return Resolved(flag, true, EvaluationReasons.MatchedRule(matchedRule.Name));
            }
        }

        return Resolved(flag, false, EvaluationReasons.DidNotMatchAnyRules());
    }

    static ResolutionDetails<bool> Resolved(EvaluationResource flag, bool value, string? reason)
        => new(flag.Slug, value, reason: reason);

    // A rule with no conditions does not match: the server only defers rules carrying at least one, so
    // an empty or missing set is a malformed response and must not turn a flag on. The nullable
    // parameters are deliberate — a deserialised payload can hold nulls the declared types forbid.
    static bool Matches(ClientSideRuleResource? rule, string? evaluationKey, EvaluationContext? context)
        => rule?.Conditions is { Length: > 0 } conditions
           && conditions.All(condition => Matches(condition, evaluationKey, context));

    static bool Matches(ClientSideConditionResource? condition, string? evaluationKey, EvaluationContext? context)
        => condition switch
        {
            PercentageByContextConditionResource percentage => WithinRollout(percentage, evaluationKey, context),
            ContextAttributeIsOneOfConditionResource isOneOf => AttributeIsOneOf(isOneOf.Key, isOneOf.Values, context),
            ContextAttributeIsNotOneOfConditionResource isNotOneOf => !AttributeIsOneOf(isNotOneOf.Key, isNotOneOf.Values, context),
            // An unknown condition (a type introduced by a newer server) can never match. A null one,
            // only reachable from a malformed response, lands here too.
            _ => false
        };

    static bool WithinRollout(PercentageByContextConditionResource condition, string? evaluationKey, EvaluationContext? context)
    {
        // The server always sends an evaluation key alongside deferred rules, so this only happens for
        // a malformed response. Without it the bucket cannot be computed at all, so the condition is
        // treated as unmet — the same safe degradation as an unknown condition.
        if (evaluationKey is null)
        {
            return false;
        }

        var targetingKey = context?.TargetingKey;

        // Without a targeting key there is nothing to bucket, so only a full rollout matches. This
        // mirrors the server's percentage-by-tenant handling of an untenanted caller.
        if (string.IsNullOrEmpty(targetingKey))
        {
            return condition.Percentage >= 100;
        }

        // Reuse the shared bucketing so v4 client-side rollout stays consistent with v3 and the other
        // provider libraries.
        return OctopusFeatureContext.GetNormalizedNumber(evaluationKey, targetingKey!) <= condition.Percentage;
    }

    // Mirrors v3 segment matching (OctopusFeatureContext.MatchesSegment): keys and values compare
    // case-insensitively and a non-string value counts as absent. *Every* entry whose key matches is
    // checked — a context can hold several case variants of one key, and AsDictionary's order is
    // unstable, so taking the first evaluated inconsistently from one process to the next.
    static bool AttributeIsOneOf(string key, string[] values, EvaluationContext? context)
        => context is not null && context.AsDictionary().Any(entry =>
            entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            && entry.Value.AsString is { } attribute
            && values.Contains(attribute, StringComparer.OrdinalIgnoreCase));
}
