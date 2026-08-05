using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Evaluates the client-side portion of a v4 flag response, returning the resolved value and a
/// reason.
///
/// A flag the server already resolved carries a <see cref="EvaluationResource.Value"/> and reason,
/// which are surfaced unchanged. A flag deferred to the client carries rules: the flag is enabled
/// when any rule matches, and a rule matches when all of its conditions match.
///
/// The response is assumed to be well-formed. A flag in neither shape, or one whose rules cannot be
/// read, throws the parse error described by <see cref="MalformedEvaluation"/> rather than being
/// evaluated as far as it can be — a bad payload should not quietly decide a flag either way. The one
/// thing that is not malformed is a condition type this client does not recognise: that is a newer
/// server capability, so it simply never matches and fails only its own rule.
/// </summary>
internal static class ClientSideEvaluator
{
    /// <summary>
    /// The caller's default value is not a parameter: when this throws, the OpenFeature SDK is the one
    /// that hands it back.
    /// </summary>
    public static ResolutionDetails<bool> Evaluate(EvaluationResource flag, EvaluationContext? context)
    {
        // The server resolved the flag; surface its value and reason unchanged.
        if (flag.Value is { } value)
        {
            if (flag.Reason is null)
            {
                throw Malformed(flag, "the server resolved the flag but sent no reason");
            }

            if (flag.EvaluationKey is not null || flag.Rules is not null)
            {
                throw Malformed(flag, "the flag carries both a server-resolved value and client-side rules");
            }

            return Resolved(flag, value, flag.Reason);
        }

        if (flag.Rules is null)
        {
            throw Malformed(flag, "the flag has neither a value nor rules");
        }

        if (flag.EvaluationKey is null)
        {
            throw Malformed(flag, "the flag defers to the client but has no evaluation key");
        }

        if (flag.Rules.Length == 0)
        {
            throw Malformed(flag, "the flag defers to the client with no rules");
        }

        // A deferred flag is enabled when any rule matches. Rules are read in order and stop at the
        // first match, so the reason names the rule that decided it.
        var ruleContext = new ClientSideEvaluationContext(flag.Slug, flag.EvaluationKey, context);

        foreach (var rule in flag.Rules)
        {
            if (rule is null)
            {
                throw Malformed(flag, "the flag has a missing rule");
            }

            if (rule.Matches(ruleContext))
            {
                return Resolved(flag, true, EvaluationReasons.MatchedRule(rule.Name));
            }
        }

        return Resolved(flag, false, EvaluationReasons.DidNotMatchAnyRules());
    }

    static ResolutionDetails<bool> Resolved(EvaluationResource flag, bool value, string reason)
        => new(flag.Slug, value, reason: reason);

    static ParseErrorException Malformed(EvaluationResource flag, string problem)
        => MalformedEvaluation.ParseError(flag.Slug, problem);
}
