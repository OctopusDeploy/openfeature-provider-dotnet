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
/// Properties that do not apply to the returned shape are omitted from the JSON. A flag in neither
/// shape is a malformed response; see <see cref="ClientSideEvaluator"/>.
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
}
