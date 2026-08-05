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

    [Fact]
    public void ValidationMatchesTheIsOneOfCondition()
    {
        // Both conditions carry the same fields, so they are malformed in the same ways. The cases are
        // enumerated in ContextAttributeIsOneOfConditionResourceTests; this pins that the shared
        // validation is wired up here as well, rather than an exclusion silently skipping it.
        using var scope = new AssertionScope();
        new ContextAttributeIsNotOneOfConditionResource("region", ["eu"]).Validate().Should().BeNull();
        new ContextAttributeIsNotOneOfConditionResource("region", []).Validate()
            .Should().Be("a context-attribute condition on 'region' with no values");
    }
}
