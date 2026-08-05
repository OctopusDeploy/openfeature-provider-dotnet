using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.Tests.V4;

public class ClientSideRuleTests
{
    static ClientSideRule Rule(params ClientSideCondition[] conditions)
        => new("Rule 1", conditions);

    [Fact]
    public void ASingleMatchingCondition_Matches()
    {
        Rule(new ContextAttributeIsOneOfCondition("plan", ["pro"]))
            .Matches(Contexts.ForRules(attributes: ("plan", "pro"))).Should().BeTrue();
    }

    [Fact]
    public void ConditionsAreCombinedWithAnd()
    {
        var rule = Rule(
            new PercentageByContextCondition(100),
            new ContextAttributeIsOneOfCondition("plan", ["pro"]));

        using var scope = new AssertionScope();
        rule.Matches(Contexts.ForRules(Contexts.TargetingKey, ("plan", "pro"))).Should().BeTrue("both conditions match");
        rule.Matches(Contexts.ForRules(Contexts.TargetingKey, ("plan", "free"))).Should().BeFalse("one condition fails");
    }

    [Fact]
    public void AMalformedConditionBehindAFailingOne_IsNeverRead()
    {
        // Conditions stop at the first one that does not match, so the rule has its answer without
        // reading the rest. A malformed condition only fails the flag if evaluation gets to it.
        var rule = Rule(
            new ContextAttributeIsOneOfCondition("plan", ["pro"]),
            new PercentageByContextCondition(percentage: null));

        rule.Matches(Contexts.ForRules(Contexts.TargetingKey, ("plan", "free"))).Should().BeFalse();
    }

    // The server only defers a named rule carrying at least one condition it wants the client to check,
    // so anything else is a malformed response. The rule is named in the problem so whoever reads the
    // error knows which one to look at.
    //
    // Deserialised rather than constructed: Name and Conditions are declared non-nullable, so a missing
    // name, and a null or absent conditions array — or a null element within it — can only arrive off the
    // wire.
    [Theory]
    [InlineData("""{ "conditions": [ { "type": "percentage-by-context", "percentage": 50 } ] }""",
        "A rule has no name.")]
    [InlineData("""{ "name": "R", "conditions": [] }""", "Rule 'R' has no conditions.")]
    [InlineData("""{ "name": "R" }""", "Rule 'R' has no conditions.")]
    [InlineData("""{ "name": "R", "conditions": null }""", "Rule 'R' has no conditions.")]
    [InlineData("""{ "name": "R", "conditions": [ null ] }""", "Rule 'R' has a missing condition.")]
    [InlineData("""{ "name": "R", "conditions": [ { "type": "percentage-by-context" } ] }""",
        "A condition has no percentage.")]
    public void AMalformedRule_ThrowsAParseErrorDescribingTheProblem(string ruleJson, string expectedProblem)
    {
        var rule = JsonSerializer.Deserialize<ClientSideRule>(ruleJson, JsonSerializerOptions.Web)!;

        var matches = () => rule.Matches(Contexts.ForRules(Contexts.TargetingKey));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be(expectedProblem);
    }

    [Fact]
    public void ANamedRuleWithConditions_Evaluates()
    {
        using var scope = new AssertionScope();
        Rule(new ContextAttributeIsOneOfCondition("plan", ["pro"]))
            .Matches(Contexts.ForRules(attributes: ("plan", "pro"))).Should().BeTrue();
        Rule(new UnknownCondition("some-future-condition"))
            .Matches(Contexts.ForRules(Contexts.TargetingKey))
            .Should().BeFalse("a condition from a newer server is well-formed, it just never matches");
    }
}
