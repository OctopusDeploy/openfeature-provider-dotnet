using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// A single feature flag as returned by the OctoToggle v4 evaluations endpoint. The endpoint
/// returns an array of these.
///
/// A flag is returned in one of two shapes:
/// <list type="bullet">
/// <item>Resolved by the server — <see cref="Value"/> and <see cref="Reason"/> are populated.</item>
/// <item>Deferred to the client — <see cref="EvaluationKey"/> and <see cref="Rules"/> are populated
/// and the provider library must evaluate the remaining client-side conditions.</item>
/// </list>
/// Properties that do not apply to the returned shape are omitted from the JSON.
/// </summary>
internal sealed class EvaluationResource
{
    public EvaluationResource(
        string slug,
        bool? value,
        string? reason,
        string? evaluationKey,
        ClientSideRuleResource[]? rules)
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
    public ClientSideRuleResource[]? Rules { get; }

    /// <summary>
    /// Describes why this flag is not one of the two shapes above, or returns <c>null</c> when it is.
    ///
    /// Every check here mirrors a requirement of the shared fixture schema, so a response the
    /// specification calls malformed is one this library refuses to evaluate. The single exception is a
    /// condition naming a type this client does not recognise: that is a newer server's capability
    /// rather than a bad payload, and only fails its own rule.
    ///
    /// Validation is per flag, so a malformed flag costs its own evaluation and no others.
    /// </summary>
    public string? Validate()
    {
        if (Value is not null)
        {
            if (Reason is null)
            {
                return "the server resolved the flag but sent no reason";
            }

            return EvaluationKey is not null || Rules is not null
                ? "the flag carries both a server-resolved value and client-side rules"
                : null;
        }

        if (Rules is null)
        {
            return "the flag has neither a value nor rules";
        }

        if (EvaluationKey is null)
        {
            return "the flag defers to the client but has no evaluation key";
        }

        if (Rules.Length == 0)
        {
            return "the flag defers to the client with no rules";
        }

        foreach (var rule in Rules)
        {
            if (rule is null)
            {
                return "the flag has a missing rule";
            }

            if (rule.Validate() is { } problem)
            {
                return problem;
            }
        }

        return null;
    }
}
