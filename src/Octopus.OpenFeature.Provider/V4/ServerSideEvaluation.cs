using System.Text.Json.Serialization;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// How far the OctoToggle v4 evaluations endpoint got with a single feature flag, and how the provider
/// library finishes the job. The endpoint returns an array of these.
///
/// A flag comes back in one of two shapes:
/// <list type="bullet">
/// <item>Resolved by the server — <see cref="Value"/> and <see cref="Reason"/> are populated, and
/// <see cref="Evaluate"/> surfaces them unchanged.</item>
/// <item>Deferred to the client — <see cref="EvaluationKey"/> and <see cref="Rules"/> are populated and
/// <see cref="Evaluate"/> evaluates the remaining client-side conditions.</item>
/// </list>
/// Properties that do not apply to the returned shape are omitted from the JSON.
/// </summary>
internal sealed class ServerSideEvaluation
{
    public ServerSideEvaluation(
        string slug,
        bool? value,
        string? reason,
        string? evaluationKey,
        ClientSideRule[]? rules)
    {
        Slug = slug;
        Value = value;
        Reason = reason;
        EvaluationKey = evaluationKey;
        Rules = rules;
    }

    public string Slug { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? Value { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Reason { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? EvaluationKey { get; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ClientSideRule[]? Rules { get; }

    /// <summary>
    /// Resolves the flag, evaluating the client-side portion of the response if the server left one. A
    /// deferred flag is enabled when any rule matches, and a rule matches when all of its conditions
    /// match.
    ///
    /// The response is assumed to be well-formed. A flag in neither shape, or one whose rules cannot be
    /// read, throws a <see cref="ParseErrorException"/> rather than being
    /// evaluated as far as it can be — a bad payload should not quietly decide a flag either way. The one
    /// thing that is not malformed is a condition type this client does not recognise: that is a newer
    /// server capability, so it simply never matches and fails only its own rule.
    ///
    /// The caller's default value is not a parameter: when this throws, the OpenFeature SDK is the one
    /// that hands it back.
    /// </summary>
    public ResolutionDetails<bool> Evaluate(EvaluationContext? context)
    {
        // The server resolved the flag; surface its value and reason unchanged.
        if (Value is { } value)
        {
            if (Reason is null)
            {
                throw new ParseErrorException("The flag was resolved by the server but has no reason.");
            }

            if (EvaluationKey is not null || Rules is not null)
            {
                throw new ParseErrorException("The flag has both a server-resolved value and client-side rules.");
            }

            return Resolved(value, Reason);
        }

        if (Rules is null)
        {
            throw new ParseErrorException("The flag has neither a value nor rules.");
        }

        if (EvaluationKey is null)
        {
            throw new ParseErrorException("The flag defers to the client but has no evaluation key.");
        }

        if (Rules.Length == 0)
        {
            throw new ParseErrorException("The flag defers to the client with no rules.");
        }

        // Rules are read in order and stop at the first match, so the reason names the rule that decided
        // it.
        var ruleContext = new ClientSideEvaluationContext(EvaluationKey, context);

        foreach (var rule in Rules)
        {
            if (rule is null)
            {
                throw new ParseErrorException("The flag has a missing rule.");
            }

            if (rule.Matches(ruleContext))
            {
                return Resolved(true, EvaluationReasons.MatchedRule(rule.Name));
            }
        }

        return Resolved(false, EvaluationReasons.DidNotMatchAnyRules());
    }

    ResolutionDetails<bool> Resolved(bool value, string reason) => new(Slug, value, reason: reason);

}
