using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Matches when the OpenFeature targeting key falls within the <see cref="Percentage"/>% rollout.
/// </summary>
internal sealed class PercentageByContextCondition : ClientSideCondition
{
    public PercentageByContextCondition(int? percentage)
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
        // The server always sends a percentage in 0–100. An absent one is not read as "nobody", and an
        // out-of-range one is not clamped: reading 101 as "everyone" would turn a flag on off the back
        // of a bad payload.
        if (Percentage is not { } percentage)
        {
            throw new ParseErrorException("A condition has no percentage.");
        }

        if (percentage is < 0 or > 100)
        {
            throw new ParseErrorException($"A condition has a percentage of {percentage}.");
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
}
