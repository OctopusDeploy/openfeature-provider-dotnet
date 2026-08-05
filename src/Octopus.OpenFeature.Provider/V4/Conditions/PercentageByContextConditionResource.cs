namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Matches when the OpenFeature targeting key falls within the <see cref="Percentage"/>% rollout.
/// </summary>
internal sealed class PercentageByContextConditionResource : ClientSideConditionResource
{
    public PercentageByContextConditionResource(int percentage)
    {
        Percentage = percentage;
    }

    public int Percentage { get; }

    public override bool Matches(ClientSideEvaluationContext context)
    {
        // Without an evaluation key the bucket cannot be computed at all, so the condition is unmet —
        // the same safe degradation as an unknown condition. Only a malformed response gets here.
        if (context.EvaluationKey is null)
        {
            return false;
        }

        var targetingKey = context.OpenFeatureContext?.TargetingKey;

        // Without a targeting key there is nothing to bucket, so only a full rollout matches. This
        // mirrors the server's percentage-by-tenant handling of an untenanted caller.
        if (string.IsNullOrEmpty(targetingKey))
        {
            return Percentage >= 100;
        }

        // Reuse the shared bucketing so v4 client-side rollout stays consistent with v3 and the other
        // provider libraries.
        return OctopusFeatureContext.GetNormalizedNumber(context.EvaluationKey, targetingKey!) <= Percentage;
    }
}
