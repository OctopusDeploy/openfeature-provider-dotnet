using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class ContextAttributeIsOneOfConditionResourceTests
{
    [Fact]
    public void MatchesWhenTheAttributeValueIsListed()
    {
        var condition = new ContextAttributeIsOneOfConditionResource("user-id", ["1234", "5678"]);

        using var scope = new AssertionScope();
        condition.Matches(Contexts.ForRules(attributes: ("user-id", "5678"))).Should().BeTrue();
        condition.Matches(Contexts.ForRules(attributes: ("user-id", "9999"))).Should().BeFalse();
        condition.Matches(Contexts.ForRules()).Should().BeFalse("a missing attribute is not one of the values");
    }

    [Fact]
    public void TheKeyAndValueAreCaseInsensitive()
    {
        var condition = new ContextAttributeIsOneOfConditionResource("Region", ["EU", "US"]);

        using var scope = new AssertionScope();
        condition.Matches(Contexts.ForRules(attributes: ("region", "eu"))).Should().BeTrue();
        condition.Matches(Contexts.ForRules(attributes: ("REGION", "Us"))).Should().BeTrue();
    }

    [Theory]
    [InlineData("Plan", "free", "plan", "pro")]
    [InlineData("plan", "pro", "Plan", "free")]
    public void EveryEntryWhoseKeyMatchesIsChecked(string firstKey, string firstValue, string secondKey, string secondValue)
    {
        // A context can carry several case variants of the same key. Every one of them has to be
        // considered: AsDictionary returns an immutable dictionary ordered by key hash, and .NET
        // randomises string hashing per process, so checking only the first matching entry made the
        // same flag, context and rule evaluate differently from one process to the next.
        var condition = new ContextAttributeIsOneOfConditionResource("plan", ["pro"]);

        var context = Contexts.ForRules(attributes: [(firstKey, firstValue), (secondKey, secondValue)]);

        condition.Matches(context)
            .Should().BeTrue("one of the 'plan' entries is 'pro', whichever order they are iterated in");
    }

    [Fact]
    public void ANonStringValueIsTreatedAsAbsent()
    {
        // OpenFeature's Value.AsString is null for a non-string, and v3 segment matching skips those
        // entries too, so a numeric attribute never matches a string value.
        var context = new ClientSideEvaluationContext(Contexts.EvaluationKey,
            EvaluationContext.Builder().Set("user-id", 1234).Build());

        new ContextAttributeIsOneOfConditionResource("user-id", ["1234"]).Matches(context).Should().BeFalse();
    }

    [Fact]
    public void ANullOpenFeatureContextDoesNotMatch()
    {
        var context = new ClientSideEvaluationContext(Contexts.EvaluationKey, openFeatureContext: null);

        new ContextAttributeIsOneOfConditionResource("plan", ["pro"]).Matches(context).Should().BeFalse();
    }

    [Fact]
    public void AKeyAndAtLeastOneValue_IsWellFormed()
    {
        new ContextAttributeIsOneOfConditionResource("plan", ["pro"]).Validate().Should().BeNull();
    }

    // The declared types are non-nullable, so these shapes only arrive off the wire. Both attribute
    // conditions share this validation; ContextAttributeIsNotOneOfConditionResourceTests checks that it
    // is wired up there too.
    [Theory]
    [InlineData(null, new[] { "pro" }, "a context-attribute condition with no key")]
    [InlineData("plan", null, "a context-attribute condition on 'plan' with no values")]
    [InlineData("plan", new string[0], "a context-attribute condition on 'plan' with no values")]
    public void AMissingKeyOrValues_IsMalformed(string? key, string[]? values, string expectedProblem)
    {
        new ContextAttributeIsOneOfConditionResource(key!, values!).Validate().Should().Be(expectedProblem);
    }

    [Fact]
    public void AMissingValueInTheList_IsMalformed()
    {
        var values = new[] { "pro", null! };

        new ContextAttributeIsOneOfConditionResource("plan", values).Validate()
            .Should().Be("a context-attribute condition on 'plan' with a missing value");
    }

    [Fact]
    public void MatchingAMalformedCondition_DoesNotThrow()
    {
        // Validation rejects the flag before evaluation reaches here, but matching still has to be total.
        var match = () => new ContextAttributeIsOneOfConditionResource(null!, null!)
            .Matches(Contexts.ForRules(attributes: ("plan", "pro")));

        using var scope = new AssertionScope();
        match.Should().NotThrow();
        match().Should().BeFalse();
    }
}
