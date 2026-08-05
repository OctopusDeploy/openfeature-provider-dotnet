using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class ContextAttributeIsNotOneOfConditionResourceTests
{
    [Fact]
    public void MatchesUnlessTheAttributeValueIsListed()
    {
        var condition = new ContextAttributeIsNotOneOfConditionResource("region", ["eu"]);

        using var scope = new AssertionScope();
        condition.Matches(Contexts.ForRules(attributes: ("region", "us"))).Should().BeTrue();
        condition.Matches(Contexts.ForRules(attributes: ("region", "eu"))).Should().BeFalse();
        condition.Matches(Contexts.ForRules()).Should().BeTrue("a missing attribute is not one of the values");
    }

    [Fact]
    public void TheKeyAndValueAreCaseInsensitive()
    {
        var condition = new ContextAttributeIsNotOneOfConditionResource("Region", ["EU"]);

        condition.Matches(Contexts.ForRules(attributes: ("region", "eu"))).Should().BeFalse();
    }

    [Fact]
    public void ANonStringValueIsTreatedAsAbsent()
    {
        // Absent means "not one of", so the condition matches.
        var context = new ClientSideEvaluationContext(Contexts.EvaluationKey,
            EvaluationContext.Builder().Set("user-id", 1234).Build());

        new ContextAttributeIsNotOneOfConditionResource("user-id", ["1234"]).Matches(context).Should().BeTrue();
    }

    [Fact]
    public void ANullOpenFeatureContextMatches()
    {
        var context = new ClientSideEvaluationContext(Contexts.EvaluationKey, openFeatureContext: null);

        new ContextAttributeIsNotOneOfConditionResource("region", ["eu"]).Matches(context).Should().BeTrue();
    }
}
