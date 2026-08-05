using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider.Tests.V4;

public class ClientSideRuleResourceTests
{
    static ClientSideRuleResource Rule(params ClientSideConditionResource[] conditions)
        => new("Rule 1", conditions);

    [Fact]
    public void ASingleMatchingCondition_Matches()
    {
        Rule(new ContextAttributeIsOneOfConditionResource("plan", ["pro"]))
            .Matches(Contexts.ForRules(attributes: ("plan", "pro"))).Should().BeTrue();
    }

    [Fact]
    public void ConditionsAreCombinedWithAnd()
    {
        var rule = Rule(
            new PercentageByContextConditionResource(100),
            new ContextAttributeIsOneOfConditionResource("plan", ["pro"]));

        using var scope = new AssertionScope();
        rule.Matches(Contexts.ForRules(Contexts.TargetingKey, ("plan", "pro"))).Should().BeTrue("both conditions match");
        rule.Matches(Contexts.ForRules(Contexts.TargetingKey, ("plan", "free"))).Should().BeFalse("one condition fails");
    }

    [Fact]
    public void ARuleContainingAnUnknownCondition_CanNeverMatch()
    {
        var rule = Rule(
            new ContextAttributeIsOneOfConditionResource("plan", ["pro"]),
            new UnknownConditionResource("some-future-condition"));

        rule.Matches(Contexts.ForRules(Contexts.TargetingKey, ("plan", "pro"))).Should().BeFalse();
    }

    [Fact]
    public void ARuleWithNoConditions_DoesNotMatch()
    {
        // The server only defers a rule that has at least one client-side condition, so a rule with
        // none is a malformed response rather than a "matches everyone" rule. A rule the client cannot
        // make sense of must not turn a flag on.
        Rule().Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    // Deserialised rather than constructed: Conditions is declared non-nullable, so a null or absent
    // array — and a null element within it — can only arrive off the wire.
    [Theory]
    [InlineData("""{ "name": "R" }""", "an absent conditions array")]
    [InlineData("""{ "name": "R", "conditions": null }""", "a null conditions array")]
    [InlineData("""{ "name": "R", "conditions": [null] }""", "a null condition")]
    public void AMalformedRule_DoesNotMatchAndDoesNotThrow(string ruleJson, string because)
    {
        // The flag carrying such a rule is rejected before evaluation, but matching still has to be total.
        var rule = JsonSerializer.Deserialize<ClientSideRuleResource>(ruleJson, JsonSerializerOptions.Web)!;

        var matches = () => rule.Matches(Contexts.ForRules(Contexts.TargetingKey));

        using var scope = new AssertionScope();
        matches.Should().NotThrow(because);
        matches().Should().BeFalse(because);
    }

    [Fact]
    public void ANamedRuleWithConditions_IsWellFormed()
    {
        using var scope = new AssertionScope();
        Rule(new ContextAttributeIsOneOfConditionResource("plan", ["pro"])).Validate().Should().BeNull();
        Rule(new UnknownConditionResource("some-future-condition")).Validate()
            .Should().BeNull("a condition from a newer server is well-formed, it just never matches");
    }

    // The server only defers a named rule carrying at least one condition it wants the client to check,
    // so anything else is a malformed response. The rule is named in the problem so whoever reads the
    // error knows which one to look at.
    [Theory]
    [InlineData("""{ "conditions": [ { "type": "percentage-by-context", "percentage": 50 } ] }""",
        "a rule has no name")]
    [InlineData("""{ "name": "R", "conditions": [] }""", "rule 'R' has no conditions")]
    [InlineData("""{ "name": "R" }""", "rule 'R' has no conditions")]
    [InlineData("""{ "name": "R", "conditions": null }""", "rule 'R' has no conditions")]
    [InlineData("""{ "name": "R", "conditions": [ null ] }""", "rule 'R' has a missing condition")]
    [InlineData("""{ "name": "R", "conditions": [ { "type": "percentage-by-context" } ] }""",
        "rule 'R' has a percentage-by-context condition with no percentage")]
    public void AMalformedRule_DescribesTheProblem(string ruleJson, string expectedProblem)
    {
        var rule = JsonSerializer.Deserialize<ClientSideRuleResource>(ruleJson, JsonSerializerOptions.Web)!;

        rule.Validate().Should().Be(expectedProblem);
    }
}
