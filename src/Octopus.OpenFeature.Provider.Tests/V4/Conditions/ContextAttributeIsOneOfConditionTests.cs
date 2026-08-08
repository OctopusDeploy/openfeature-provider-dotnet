using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class ContextAttributeIsOneOfConditionTests
{
    [Fact]
    public void MatchesWhenTheAttributeValueIsListed()
    {
        var condition = new ContextAttributeIsOneOfCondition("user-id", ["1234", "5678"]);

        using var scope = new AssertionScope();
        condition.Matches(Contexts.ForRules(attributes: ("user-id", "5678"))).Should().BeTrue();
        condition.Matches(Contexts.ForRules(attributes: ("user-id", "9999"))).Should().BeFalse();
        condition.Matches(Contexts.ForRules()).Should().BeFalse("a missing attribute is not one of the values");
    }

    [Fact]
    public void TheKeyAndValueAreCaseInsensitive()
    {
        var condition = new ContextAttributeIsOneOfCondition("Region", ["EU", "US"]);

        using var scope = new AssertionScope();
        condition.Matches(Contexts.ForRules(attributes: ("region", "eu"))).Should().BeTrue();
        condition.Matches(Contexts.ForRules(attributes: ("REGION", "Us"))).Should().BeTrue();
    }

    [Theory]
    [InlineData("Plan", "free", "plan", "pro")]
    [InlineData("plan", "pro", "Plan", "free")]
    public void EveryEntryWhoseKeyMatchesIsChecked(string firstKey, string firstValue, string secondKey, string secondValue)
    {
        // AsDictionary is ordered by key hash and .NET randomises string hashing per process, so
        // checking only the first matching entry evaluated differently from one run to the next.
        var condition = new ContextAttributeIsOneOfCondition("plan", ["pro"]);

        var context = Contexts.ForRules(attributes: [(firstKey, firstValue), (secondKey, secondValue)]);

        condition.Matches(context)
            .Should().BeTrue("one of the 'plan' entries is 'pro', whichever order they are iterated in");
    }

    [Fact]
    public void ANonStringValueIsTreatedAsAbsent()
    {
        // OpenFeature's Value.AsString is null for a non-string, so a numeric attribute never matches a string value.
        var context = new ClientSideEvaluationContext(Contexts.EvaluationKey,
            EvaluationContext.Builder().Set("user-id", 1234).Build());

        new ContextAttributeIsOneOfCondition("user-id", ["1234"]).Matches(context).Should().BeFalse();
    }

    [Fact]
    public void ANullOpenFeatureContextDoesNotMatch()
    {
        new ContextAttributeIsOneOfCondition("plan", ["pro"])
            .Matches(Contexts.WithoutOpenFeatureContext()).Should().BeFalse();
    }

    // Key and Values are declared non-nullable, so these shapes only arrive off the wire. A condition
    // with nothing to match on has no defensible answer, so it fails the evaluation.
    [Theory]
    [InlineData(null, new[] { "pro" }, "A condition is missing a key.")]
    [InlineData("plan", null, "A condition is missing values.")]
    [InlineData("plan", new string[0], "A condition is missing values.")]
    public void AMissingKeyOrValues_ThrowsAParseError(string? key, string[]? values, string expectedProblem)
    {
        var matches = () => new ContextAttributeIsOneOfCondition(key!, values!)
            .Matches(Contexts.ForRules(attributes: ("plan", "pro")));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be(expectedProblem);
    }

    [Fact]
    public void AMissingValueInTheList_ThrowsAParseError()
    {
        var values = new[] { "pro", null! };

        var matches = () => new ContextAttributeIsOneOfCondition("plan", values)
            .Matches(Contexts.ForRules(attributes: ("plan", "pro")));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be(
                "A condition is missing a value.");
    }
}
