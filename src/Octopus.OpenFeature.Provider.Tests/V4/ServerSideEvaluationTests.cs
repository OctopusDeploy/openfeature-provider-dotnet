using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Constant;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// <see cref="ServerSideEvaluation.Evaluate"/> against a well-formed response: choosing between the
/// server's answer and the client's rules, combining rules, and the reason reported. Rule and condition
/// matching are covered by <see cref="ClientSideRuleTests"/> and the tests in <c>V4/Conditions</c>; a
/// response the client refuses to evaluate is covered by <see cref="MalformedEvaluationTests"/>.
/// </summary>
public class ServerSideEvaluationTests
{
    static ServerSideEvaluation ServerResolved(bool value, string reason)
        => new(Contexts.Slug, value, reason, evaluationKey: null, rules: null);

    static ServerSideEvaluation Deferred(params ClientSideRule[] rules)
        => new(Contexts.Slug, value: null, reason: null, Contexts.EvaluationKey, rules);

    static ClientSideRule RuleMatching(string name, string plan)
        => new(name, [new ContextAttributeIsOneOfCondition("plan", [plan])]);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerResolvedFlag_ReturnsTheServerValueAndReason(bool value)
    {
        var result = ServerResolved(value, "the server said so").Evaluate(Contexts.OpenFeature());

        using var scope = new AssertionScope();
        result.FlagKey.Should().Be(Contexts.Slug);
        result.Value.Should().Be(value);
        result.Reason.Should().Be("the server said so");
        result.ErrorType.Should().Be(ErrorType.None);
    }

    [Fact]
    public void MatchingRule_ResolvesToTrueWithTheMatchedRuleReason()
    {
        var flag = Deferred(RuleMatching("beta-testers", "beta"));

        var result = flag.Evaluate(Contexts.OpenFeature(attributes: ("plan", "beta")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'beta-testers'.");
        result.ErrorType.Should().Be(ErrorType.None);
    }

    [Fact]
    public void NoMatchingRule_ResolvesToFalseWithTheDidNotMatchReason()
    {
        // A flag whose rules simply did not match is off, not defaulted: it resolves rather than erroring,
        // so the caller's default value never comes into it.
        var flag = Deferred(RuleMatching("beta-testers", "beta"));

        var result = flag.Evaluate(Contexts.OpenFeature(attributes: ("plan", "free")));

        using var scope = new AssertionScope();
        result.Value.Should().BeFalse();
        result.Reason.Should().Be("Did not match any rules.");
        result.ErrorType.Should().Be(ErrorType.None);
    }

    [Fact]
    public void RulesAcrossAFlag_AreCombinedWithOr()
    {
        var flag = Deferred(
            RuleMatching("beta-testers", "beta"),
            new ClientSideRule("internal",
                [new ContextAttributeIsOneOfCondition("email", ["staff@octopus.com"])]));

        using var scope = new AssertionScope();
        flag.Evaluate(Contexts.OpenFeature(attributes: ("plan", "beta")))
            .Value.Should().BeTrue("first rule matches");
        flag.Evaluate(Contexts.OpenFeature(attributes: ("email", "staff@octopus.com")))
            .Value.Should().BeTrue("second rule matches");
        flag.Evaluate(Contexts.OpenFeature(attributes: ("plan", "free")))
            .Value.Should().BeFalse("no rule matches");
    }

    [Fact]
    public void FirstMatchingRule_ProvidesTheReason()
    {
        // Both rules match; the reason should name the first one that did.
        var flag = Deferred(RuleMatching("first", "pro"), RuleMatching("second", "pro"));

        var result = flag.Evaluate(Contexts.OpenFeature(attributes: ("plan", "pro")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'first'.");
    }

    [Fact]
    public void ANullContext_IsTreatedAsAnEmptyContext()
    {
        using var scope = new AssertionScope();
        Deferred(RuleMatching("pro-users", "pro")).Evaluate(context: null)
            .Value.Should().BeFalse("there is no attribute to match");
        Deferred(new ClientSideRule("everyone", [new PercentageByContextCondition(100)]))
            .Evaluate(context: null)
            .Value.Should().BeTrue("a 100% rollout matches without a targeting key");
    }
}
