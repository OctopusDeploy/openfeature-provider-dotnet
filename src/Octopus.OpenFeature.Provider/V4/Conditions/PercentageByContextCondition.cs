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
    /// from an explicit <c>0</c>, which is a legitimate "nobody".
    /// </summary>
    public int? Percentage { get; }

    public override bool Matches(ClientSideEvaluationContext context)
    {
        if (Percentage is not { } percentage)
        {
            throw new ParseErrorException("A condition is missing a percentage value.");
        }

        // Rejected rather than clamped: reading 101 as "everyone" would turn a flag on off the back of a
        // bad payload.
        if (percentage is < 0 or > 100)
        {
            throw new ParseErrorException($"A condition has a percentage of {percentage}.");
        }

        var targetingKey = context.OpenFeatureContext?.TargetingKey;

        // Nothing to bucket, so only a full rollout matches — as the server treats an untenanted caller.
        if (string.IsNullOrEmpty(targetingKey))
        {
            return percentage >= 100;
        }

        return PercentageRollout.Includes(context.EvaluationKey, targetingKey!, percentage);
    }
}
