using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;
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
        var context = new ClientSideEvaluationContext(Contexts.Slug, Contexts.EvaluationKey,
            EvaluationContext.Builder().Set("user-id", 1234).Build());

        new ContextAttributeIsNotOneOfConditionResource("user-id", ["1234"]).Matches(context).Should().BeTrue();
    }

    [Fact]
    public void ANullOpenFeatureContextMatches()
    {
        new ContextAttributeIsNotOneOfConditionResource("region", ["eu"])
            .Matches(Contexts.WithoutOpenFeatureContext()).Should().BeTrue();
    }

    [Fact]
    public void AMalformedConditionThrowsTheSameParseErrorAsIsOneOf()
    {
        // Both conditions carry the same fields, so they are malformed in the same ways. The cases are
        // enumerated in ContextAttributeIsOneOfConditionResourceTests; this pins that the shared reading
        // is wired up here as well, rather than a missing attribute quietly making this one match.
        var matches = () => new ContextAttributeIsNotOneOfConditionResource("region", [])
            .Matches(Contexts.ForRules(attributes: ("region", "us")));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be(
                Contexts.MalformedMessage("a context-attribute condition on 'region' with no values"));
    }
}
