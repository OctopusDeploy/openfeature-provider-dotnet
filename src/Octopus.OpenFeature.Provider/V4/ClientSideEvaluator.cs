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

        // A deferred flag is enabled when any rule matches. A null rule is only reachable from a
        // malformed response, and cannot match.
        if (flag.Rules is not null)
        {
            var ruleContext = new ClientSideEvaluationContext(flag.EvaluationKey, context);

            var matchedRule = flag.Rules.FirstOrDefault(rule => rule?.Matches(ruleContext) is true);
            if (matchedRule is not null)
            {
                return Resolved(flag, true, EvaluationReasons.MatchedRule(matchedRule.Name));
            }
        }

        return Resolved(flag, false, EvaluationReasons.DidNotMatchAnyRules());
    }

    static ResolutionDetails<bool> Resolved(EvaluationResource flag, bool value, string? reason)
        => new(flag.Slug, value, reason: reason);
}
