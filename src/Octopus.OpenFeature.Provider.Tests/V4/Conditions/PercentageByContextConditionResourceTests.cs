using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class PercentageByContextConditionResourceTests
{
    [Fact]
    public void TargetingKeyInsideTheRollout_Matches()
    {
        new PercentageByContextConditionResource(Contexts.TargetingKeyBucket)
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeTrue();
    }

    [Fact]
    public void TargetingKeyOutsideTheRollout_DoesNotMatch()
    {
        new PercentageByContextConditionResource(Contexts.TargetingKeyBucket - 1)
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    [Fact]
    public void WithoutATargetingKey_OnlyAFullRolloutMatches()
    {
        using var scope = new AssertionScope();
        new PercentageByContextConditionResource(100).Matches(Contexts.ForRules())
            .Should().BeTrue("a 100% rollout matches even without a targeting key");
        new PercentageByContextConditionResource(99).Matches(Contexts.ForRules())
            .Should().BeFalse("a partial rollout cannot bucket without a targeting key");
        new PercentageByContextConditionResource(50).Matches(Contexts.ForRules(targetingKey: ""))
            .Should().BeFalse("an empty targeting key is treated the same as none");
    }

    [Fact]
    public void AtZeroPercent_NothingMatches()
    {
        // The lowest bucket is 1, so nothing is included at 0%.
        new PercentageByContextConditionResource(0)
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    [Fact]
    public void WithoutAnEvaluationKey_NothingMatches()
    {
        // There is no key to bucket against, so the condition is unmet rather than assumed.
        using var scope = new AssertionScope();
        new PercentageByContextConditionResource(100)
            .Matches(Contexts.WithoutEvaluationKey(Contexts.TargetingKey)).Should().BeFalse();
        new PercentageByContextConditionResource(100)
            .Matches(Contexts.WithoutEvaluationKey()).Should().BeFalse();
    }

    [Fact]
    public void WithANullOpenFeatureContext_OnlyAFullRolloutMatches()
    {
        using var scope = new AssertionScope();
        var context = new ClientSideEvaluationContext(Contexts.EvaluationKey, openFeatureContext: null);
        new PercentageByContextConditionResource(100).Matches(context).Should().BeTrue();
        new PercentageByContextConditionResource(99).Matches(context).Should().BeFalse();
    }
}
