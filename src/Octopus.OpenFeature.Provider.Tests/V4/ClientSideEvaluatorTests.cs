using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// Flag-level evaluation: choosing between the server's answer and the client's rules, combining
/// rules, and the reason reported. Rule and condition matching are covered by
/// <see cref="ClientSideRuleResourceTests"/> and the tests in <c>V4/Conditions</c>.
/// </summary>
public class ClientSideEvaluatorTests
{
    static EvaluationResource ServerResolved(bool value, string reason)
        => new("my-feature", value, reason, evaluationKey: null, rules: null);

    static EvaluationResource Deferred(params ClientSideRuleResource[] rules)
        => new("my-feature", value: null, reason: null, evaluationKey: Contexts.EvaluationKey, rules: rules);

    static ClientSideRuleResource RuleMatching(string name, string plan)
        => new(name, [new ContextAttributeIsOneOfConditionResource("plan", [plan])]);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerResolvedFlag_ReturnsTheServerValueAndReason(bool value)
    {
        var result = ClientSideEvaluator.Evaluate(ServerResolved(value, "the server said so"), Contexts.OpenFeature());

        using var scope = new AssertionScope();
        result.FlagKey.Should().Be("my-feature");
        result.Value.Should().Be(value);
        result.Reason.Should().Be("the server said so");
    }

    [Fact]
    public void MatchingRule_ResolvesToTrueWithTheMatchedRuleReason()
    {
        var flag = Deferred(RuleMatching("beta-testers", "beta"));

        var result = ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(attributes: ("plan", "beta")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'beta-testers'.");
    }

    [Fact]
    public void NoMatchingRule_ResolvesToFalseWithTheDidNotMatchReason()
    {
        var flag = Deferred(RuleMatching("beta-testers", "beta"));

        var result = ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(attributes: ("plan", "free")));

        using var scope = new AssertionScope();
        result.Value.Should().BeFalse();
        result.Reason.Should().Be("Did not match any rules.");
    }

    [Fact]
    public void RulesAcrossAFlag_AreCombinedWithOr()
    {
        var flag = Deferred(
            RuleMatching("beta-testers", "beta"),
            new ClientSideRuleResource("internal",
                [new ContextAttributeIsOneOfConditionResource("email", ["staff@octopus.com"])]));

        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(attributes: ("plan", "beta")))
            .Value.Should().BeTrue("first rule matches");
        ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(attributes: ("email", "staff@octopus.com")))
            .Value.Should().BeTrue("second rule matches");
        ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(attributes: ("plan", "free")))
            .Value.Should().BeFalse("no rule matches");
    }

    [Fact]
    public void FirstMatchingRule_ProvidesTheReason()
    {
        // Both rules match; the reason should name the first one that did.
        var flag = Deferred(RuleMatching("first", "pro"), RuleMatching("second", "pro"));

        var result = ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(attributes: ("plan", "pro")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'first'.");
    }

    [Fact]
    public void DeferredFlagWithNoRules_EvaluatesToFalse()
    {
        ClientSideEvaluator.Evaluate(Deferred(), Contexts.OpenFeature(Contexts.TargetingKey))
            .Value.Should().BeFalse();
    }

    [Fact]
    public void WithoutAnEvaluationKey_AnAttributeOnlyRuleIsStillEvaluated()
    {
        // Only percentage-by-context needs the evaluation key, so a response missing one must not stop
        // an attribute-only rule from matching.
        var flag = new EvaluationResource("my-feature", value: null, reason: null, evaluationKey: null,
            rules: [RuleMatching("beta-testers", "pro")]);

        var result = ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(attributes: ("plan", "pro")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'beta-testers'.");
    }

    [Fact]
    public void ANullContext_IsTreatedAsAnEmptyContext()
    {
        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(Deferred(RuleMatching("pro-users", "pro")), null)
            .Value.Should().BeFalse("there is no attribute to match");
        ClientSideEvaluator.Evaluate(
                Deferred(new ClientSideRuleResource("everyone", [new PercentageByContextConditionResource(100)])), null)
            .Value.Should().BeTrue("a 100% rollout matches without a targeting key");
    }

    // Deserialised rather than constructed: the declared types are non-nullable, so a null rule can
    // only arrive off the wire. This cannot come from OctoToggle today, but it may not throw out of the
    // evaluator — v3's evaluation path never throws, and the provider does not wrap this call.
    [Fact]
    public void ANullRule_DoesNotMatchAndDoesNotThrow()
    {
        const string json = """{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ null ] }""";
        var flag = JsonSerializer.Deserialize<EvaluationResource>(json, JsonSerializerOptions.Web)!;

        var evaluate = () => ClientSideEvaluator.Evaluate(flag, Contexts.OpenFeature(Contexts.TargetingKey));

        using var scope = new AssertionScope();
        evaluate.Should().NotThrow();
        evaluate().Value.Should().BeFalse();
        evaluate().Reason.Should().Be("Did not match any rules.");
    }
}
