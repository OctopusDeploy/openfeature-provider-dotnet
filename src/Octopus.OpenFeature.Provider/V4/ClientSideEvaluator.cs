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

    // A rule matches when every one of its conditions matches.
    //
    // A rule carrying no conditions does not match. The server never emits one — it only defers a rule
    // that has at least one client-side condition — so this only arises from a malformed response,
    // where a rule with nothing to check is meaningless rather than universally true. Treating it as
    // unmet keeps it consistent with an unknown condition: a rule the client cannot make sense of
    // never turns a flag on. The rule and its conditions are null-checked for the same reason — the
    // declared types are non-nullable, but nothing enforces that on a deserialised payload.
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

    // Matches when *any* context entry whose key matches (case-insensitively, as v3 segment matching
    // does) holds one of the values. Checking every matching entry rather than just the first matters
    // because a context can carry several case variants of the same key ("Plan" and "plan"), and
    // EvaluationContext.AsDictionary is an immutable dictionary whose iteration order is arbitrary —
    // picking the first would make the outcome depend on that order. This mirrors the Any-based
    // matching in OctopusFeatureContext.MatchesSegment.
    //
    // A non-string value (Value.AsString is null) is treated as absent, again matching v3.
    static bool AttributeIsOneOf(string key, string[] values, EvaluationContext? context)
        => context is not null && context.AsDictionary().Any(entry =>
            entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase)
            && entry.Value.AsString is { } attribute
            && values.Contains(attribute, StringComparer.OrdinalIgnoreCase));
}
