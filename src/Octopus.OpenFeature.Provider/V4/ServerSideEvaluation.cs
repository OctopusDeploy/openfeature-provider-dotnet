using System.Text.Json.Serialization;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// A single feature flag from the v4 evaluations endpoint, either resolved by the server
/// (<see cref="Value"/> and <see cref="Reason"/>) or deferred to the client
/// (<see cref="EvaluationKey"/> and <see cref="Rules"/>). Properties that do not apply to the returned
/// shape are omitted from the JSON.
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
    /// Resolves the flag, evaluating the client-side rules if the server left any: the flag is enabled
    /// when any rule matches.
    ///
    /// A response in neither shape throws <see cref="ParseErrorException"/>, which the OpenFeature SDK
    /// turns into the caller's default value.
    /// </summary>
    public ResolutionDetails<bool> Evaluate(EvaluationContext? context)
    {
        if (Value is { } value)
        {
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

    ResolutionDetails<bool> Resolved(bool value, string? reason) => new(Slug, value, reason: reason);
}
