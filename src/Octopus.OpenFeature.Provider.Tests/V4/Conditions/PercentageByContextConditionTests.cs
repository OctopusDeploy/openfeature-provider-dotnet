using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class PercentageByContextConditionTests
{
    [Fact]
    public void TargetingKeyInsideTheRollout_Matches()
    {
        new PercentageByContextCondition(Contexts.TargetingKeyBucket)
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeTrue();
    }

    [Fact]
    public void TargetingKeyOutsideTheRollout_DoesNotMatch()
    {
        new PercentageByContextCondition(Contexts.TargetingKeyBucket - 1)
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    [Fact]
    public void WithoutATargetingKey_OnlyAFullRolloutMatches()
    {
        using var scope = new AssertionScope();
        new PercentageByContextCondition(100).Matches(Contexts.ForRules())
            .Should().BeTrue("a 100% rollout matches even without a targeting key");
        new PercentageByContextCondition(99).Matches(Contexts.ForRules())
            .Should().BeFalse("a partial rollout cannot bucket without a targeting key");
        new PercentageByContextCondition(50).Matches(Contexts.ForRules(targetingKey: ""))
            .Should().BeFalse("an empty targeting key is treated the same as none");
    }

    [Fact]
    public void AtZeroPercent_NothingMatches()
    {
        // The lowest bucket is 1, so nothing is included at 0%. An explicit 0 is a legitimate "nobody",
        // which is why it has to stay distinguishable from an absent percentage.
        new PercentageByContextCondition(0)
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    [Fact]
    public void WithANullOpenFeatureContext_OnlyAFullRolloutMatches()
    {
        using var scope = new AssertionScope();
        var context = Contexts.WithoutOpenFeatureContext();
        new PercentageByContextCondition(100).Matches(context).Should().BeTrue();
        new PercentageByContextCondition(99).Matches(context).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "A condition has no percentage.")]
    [InlineData(101, "A condition has a percentage of 101.")]
    [InlineData(-1, "A condition has a percentage of -1.")]
    public void AnAbsentOrOutOfRangePercentage_ThrowsAParseError(int? percentage, string expectedProblem)
    {
        // An out-of-range percentage is rejected rather than clamped, so a bad payload cannot roll a flag
        // out to everyone, and an absent one is not read as a rollout to nobody.
        var matches = () => new PercentageByContextCondition(percentage)
            .Matches(Contexts.ForRules(Contexts.TargetingKey));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be(expectedProblem);
    }
}
