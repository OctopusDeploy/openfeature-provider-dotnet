namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Matches when the OpenFeature targeting key falls within the <see cref="Percentage"/>% rollout.
/// </summary>
internal sealed class PercentageByContextConditionResource : ClientSideConditionResource
{
    public PercentageByContextConditionResource(int? percentage)
    {
        Percentage = percentage;
    }

    /// <summary>
    /// The rollout percentage, 0–100. Nullable so an absent <c>percentage</c> stays distinguishable
    /// from an explicit <c>0</c>: the first is a malformed response, the second turns the flag off.
    /// </summary>
    public int? Percentage { get; }

    public override bool Matches(ClientSideEvaluationContext context)
    {
        // Only a malformed response has no percentage, and the flag is rejected before evaluation
        // reaches here. Guarded anyway so the condition never throws on a payload nothing enforces.
        if (Percentage is not { } percentage)
        {
            return false;
        }

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
            return percentage >= 100;
        }

        // Reuse the shared bucketing so v4 client-side rollout stays consistent with v3 and the other
        // provider libraries.
        return OctopusFeatureContext.GetNormalizedNumber(context.EvaluationKey, targetingKey!) <= percentage;
    }

    /// <summary>
    /// A percentage outside 0–100 is rejected rather than clamped: the server cannot produce one, so
    /// reading 101 as "everyone" would turn a flag on off the back of a bad payload.
    /// </summary>
    public override string? Validate()
        => Percentage switch
        {
            null => "a percentage-by-context condition with no percentage",
            < 0 or > 100 => $"a percentage-by-context condition with a percentage of {Percentage}",
            _ => null
        };
}
