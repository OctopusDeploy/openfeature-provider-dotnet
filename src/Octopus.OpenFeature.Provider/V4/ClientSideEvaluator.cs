using System.Linq;
using OpenFeature.Constant;
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
/// A flag in neither shape is malformed. It resolves to the caller's default value with
/// <see cref="ErrorType.ParseError"/>, as v3's evaluation path does, rather than being evaluated as
/// far as it can be — a bad payload should not quietly decide a flag either way. The one thing that is
/// not malformed is a condition type this client does not recognise: that is a newer server capability,
/// so it simply never matches and fails only its own rule. See <see cref="EvaluationResource.Validate"/>.
/// </summary>
internal static class ClientSideEvaluator
{
    public static ResolutionDetails<bool> Evaluate(EvaluationResource flag, bool defaultValue, EvaluationContext? context)
    {
        if (flag.Validate() is { } problem)
        {
            return Malformed(flag, defaultValue, problem);
        }

        // The server resolved the flag; surface its value and reason unchanged.
        if (flag.Value is { } value)
        {
            return Resolved(flag, value, flag.Reason);
        }

        // A deferred flag is enabled when any rule matches. Validation has already established that
        // there is at least one rule and that every rule is evaluable.
        var ruleContext = new ClientSideEvaluationContext(flag.EvaluationKey, context);

        var matchedRule = flag.Rules!.FirstOrDefault(rule => rule.Matches(ruleContext));

        return matchedRule is not null
            ? Resolved(flag, true, EvaluationReasons.MatchedRule(matchedRule.Name))
            : Resolved(flag, false, EvaluationReasons.DidNotMatchAnyRules());
    }

    static ResolutionDetails<bool> Resolved(EvaluationResource flag, bool value, string? reason)
        => new(flag.Slug, value, reason: reason);

    /// <summary>
    /// The reason carries the same sentence v3 returns for an unevaluable flag; the specific problem
    /// goes in the error message, where it is available to whoever has to work out what the server
    /// sent.
    /// </summary>
    static ResolutionDetails<bool> Malformed(EvaluationResource flag, bool defaultValue, string problem)
        => new(flag.Slug,
            defaultValue,
            ErrorType.ParseError,
            reason: EvaluationReasons.MalformedEvaluation(flag.Slug),
            errorMessage: $"Feature toggle {flag.Slug} could not be evaluated because {problem}.");
}
