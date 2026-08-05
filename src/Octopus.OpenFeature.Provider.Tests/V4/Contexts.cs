using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// Builds the contexts the v4 evaluation tests run against.
/// </summary>
static class Contexts
{
    // OctopusFeatureContext.GetNormalizedNumber("evaluation-key", "targeting-key") == 13, so a
    // targeting key of "targeting-key" is inside a >=13% rollout and outside a <13% one. The pair of
    // rollout tests either side of the bucket pins that value, so it cannot drift unnoticed.
    public const string Slug = "my-feature";
    public const string EvaluationKey = "evaluation-key";
    public const string TargetingKey = "targeting-key";
    public const int TargetingKeyBucket = 13;

    /// <summary>An OpenFeature context with the given targeting key and string attributes.</summary>
    public static EvaluationContext OpenFeature(string? targetingKey = null, params (string key, string value)[] attributes)
    {
        var builder = EvaluationContext.Builder();
        foreach (var (key, value) in attributes)
        {
            builder.Set(key, value);
        }

        if (targetingKey is not null)
        {
            builder.SetTargetingKey(targetingKey);
        }

        return builder.Build();
    }

    /// <summary>
    /// What a rule or condition is evaluated against: an OpenFeature context paired with the slug and
    /// evaluation key of the flag being evaluated, as <see cref="ServerSideEvaluation.Evaluate"/> pairs them.
    /// </summary>
    public static ClientSideEvaluationContext ForRules(string? targetingKey = null, params (string key, string value)[] attributes)
        => new(EvaluationKey, OpenFeature(targetingKey, attributes));

    /// <summary>A rule context whose caller supplied no OpenFeature context at all.</summary>
    public static ClientSideEvaluationContext WithoutOpenFeatureContext()
        => new(EvaluationKey, openFeatureContext: null);
}
