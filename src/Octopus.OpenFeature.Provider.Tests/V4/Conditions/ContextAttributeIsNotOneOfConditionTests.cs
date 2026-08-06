using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class ContextAttributeIsNotOneOfConditionTests
{
    [Fact]
    public void MatchesUnlessTheAttributeValueIsListed()
    {
        var condition = new ContextAttributeIsNotOneOfCondition("region", ["eu"]);

        using var scope = new AssertionScope();
        condition.Matches(Contexts.ForRules(attributes: ("region", "us"))).Should().BeTrue();
        condition.Matches(Contexts.ForRules(attributes: ("region", "eu"))).Should().BeFalse();
        condition.Matches(Contexts.ForRules()).Should().BeTrue("a missing attribute is not one of the values");
    }

    [Fact]
    public void TheKeyAndValueAreCaseInsensitive()
    {
        var condition = new ContextAttributeIsNotOneOfCondition("Region", ["EU"]);

        condition.Matches(Contexts.ForRules(attributes: ("region", "eu"))).Should().BeFalse();
    }

    [Fact]
    public void ANonStringValueIsTreatedAsAbsent()
    {
        // Absent means "not one of", so the condition matches.
        var context = new ClientSideEvaluationContext(Contexts.EvaluationKey,
            EvaluationContext.Builder().Set("user-id", 1234).Build());

        new ContextAttributeIsNotOneOfCondition("user-id", ["1234"]).Matches(context).Should().BeTrue();
    }

    [Fact]
    public void ANullOpenFeatureContextMatches()
    {
        new ContextAttributeIsNotOneOfCondition("region", ["eu"])
            .Matches(Contexts.WithoutOpenFeatureContext()).Should().BeTrue();
    }

    [Theory]
    [InlineData(null, new[] { "eu" }, "A condition is missing a key.")]
    [InlineData("region", null, "A condition is missing values.")]
    [InlineData("region", new string[0], "A condition is missing values.")]
    public void AMissingKeyOrValues_ThrowsAParseError(string? key, string[]? values, string expectedMessage)
    {
        var matches = () => new ContextAttributeIsNotOneOfCondition(key!, values!)
            .Matches(Contexts.ForRules(attributes: ("region", "us")));

        matches.Should().Throw<ParseErrorException>().Which.Message.Should().Be(expectedMessage);
    }

    [Fact]
    public void AMissingValueInTheList_ThrowsAParseError()
    {
        var values = new[] { "eu", null! };

        var matches = () => new ContextAttributeIsNotOneOfCondition("region", values)
            .Matches(Contexts.ForRules(attributes: ("region", "us")));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be("A condition is missing a value.");
    }
}
