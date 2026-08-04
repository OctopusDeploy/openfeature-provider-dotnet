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

        // A deferred flag is enabled when any rule matches. A well-formed deferred flag always carries
        // an evaluation key and rules; anything else falls through to "did not match any rules".
        if (flag.EvaluationKey is not null && flag.Rules is not null)
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

    // A rule matches when every one of its conditions matches. A rule with no conditions matches
    // everything.
    static bool Matches(ClientSideRuleResource rule, string evaluationKey, EvaluationContext? context)
        => rule.Conditions.All(condition => Matches(condition, evaluationKey, context));

    static bool Matches(ClientSideConditionResource condition, string evaluationKey, EvaluationContext? context)
        => condition switch
        {
            PercentageByContextConditionResource percentage => WithinRollout(percentage, evaluationKey, context),
            ContextAttributeIsOneOfConditionResource isOneOf => AttributeIsOneOf(isOneOf.Key, isOneOf.Values, context),
            ContextAttributeIsNotOneOfConditionResource isNotOneOf => !AttributeIsOneOf(isNotOneOf.Key, isNotOneOf.Values, context),
            // An unknown condition (a type introduced by a newer server) can never match.
            _ => false
        };

    static bool WithinRollout(PercentageByContextConditionResource condition, string evaluationKey, EvaluationContext? context)
    {
        var targetingKey = context?.TargetingKey;

        // Without a targeting key there is nothing to bucket, so only a full rollout matches.
        if (string.IsNullOrEmpty(targetingKey))
        {
            return condition.Percentage >= 100;
        }

        // Reuse the shared bucketing so v4 client-side rollout stays consistent with v3 and the other
        // provider libraries.
        return OctopusFeatureContext.GetNormalizedNumber(evaluationKey, targetingKey!) <= condition.Percentage;
    }

    static bool AttributeIsOneOf(string key, string[] values, EvaluationContext? context)
    {
        var attribute = GetAttribute(context, key);

        return attribute is not null && values.Contains(attribute, StringComparer.OrdinalIgnoreCase);
    }

    // Looks up a context attribute by key (case-insensitively, as v3 segment matching does) and
    // returns its string value, or null if it is absent or not a string.
    static string? GetAttribute(EvaluationContext? context, string key)
    {
        if (context is null)
        {
            return null;
        }

        foreach (var entry in context.AsDictionary())
        {
            if (entry.Key.Equals(key, StringComparison.OrdinalIgnoreCase))
            {
                return entry.Value.AsString;
            }
        }

        return null;
    }
}
