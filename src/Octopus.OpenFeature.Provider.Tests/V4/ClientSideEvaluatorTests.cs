using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4;

public class ClientSideEvaluatorTests
{
    // OctopusFeatureContext.GetNormalizedNumber("evaluation-key", "targeting-key") == 13, so a
    // targeting key of "targeting-key" is inside a >=13% rollout and outside a <13% one.
    const string EvaluationKey = "evaluation-key";
    const string TargetingKey = "targeting-key";
    const int TargetingKeyBucket = 13;

    static EvaluationContext Context(string? targetingKey = null, params (string key, string value)[] attributes)
    {
        var builder = EvaluationContext.Builder();
        foreach (var (key, value) in attributes)
        {
            builder.Set(key, value);
        }

        if (targetingKey is not null)
        {
            builder.SetTargetingKey(targetingKey);
        }

        return builder.Build();
    }

    static EvaluationResource ServerResolved(bool value, string reason)
        => new("my-feature", value, reason, evaluationKey: null, rules: null);

    static EvaluationResource Deferred(params ClientSideConditionResource[] conditions)
        => new("my-feature", value: null, reason: null, evaluationKey: EvaluationKey,
            rules: [new ClientSideRuleResource("Rule 1", conditions)]);

    static EvaluationResource DeferredWithRules(params ClientSideRuleResource[] rules)
        => new("my-feature", value: null, reason: null, evaluationKey: EvaluationKey, rules: rules);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ServerResolvedFlag_ReturnsTheServerValueAndReason(bool value)
    {
        var result = ClientSideEvaluator.Evaluate(ServerResolved(value, "the server said so"), Context());

        using var scope = new AssertionScope();
        result.FlagKey.Should().Be("my-feature");
        result.Value.Should().Be(value);
        result.Reason.Should().Be("the server said so");
    }

    [Fact]
    public void MatchingRule_ResolvesToTrueWithTheMatchedRuleReason()
    {
        var flag = DeferredWithRules(new ClientSideRuleResource("beta-testers",
            [new ContextAttributeIsOneOfConditionResource("plan", ["beta"])]));

        var result = ClientSideEvaluator.Evaluate(flag, Context(attributes: ("plan", "beta")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'beta-testers'.");
    }

    [Fact]
    public void NoMatchingRule_ResolvesToFalseWithTheDidNotMatchReason()
    {
        var flag = DeferredWithRules(new ClientSideRuleResource("beta-testers",
            [new ContextAttributeIsOneOfConditionResource("plan", ["beta"])]));

        var result = ClientSideEvaluator.Evaluate(flag, Context(attributes: ("plan", "free")));

        using var scope = new AssertionScope();
        result.Value.Should().BeFalse();
        result.Reason.Should().Be("Did not match any rules.");
    }

    [Fact]
    public void DeferredFlagWithNoRules_EvaluatesToFalse()
    {
        var flag = DeferredWithRules();

        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey)).Value.Should().BeFalse();
    }

    [Fact]
    public void RuleWithNoConditions_MatchesEverything()
    {
        var flag = DeferredWithRules(new ClientSideRuleResource("everyone", []));

        var result = ClientSideEvaluator.Evaluate(flag, Context());

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'everyone'.");
    }

    [Fact]
    public void PercentageByContext_TargetingKeyInsideRollout_Matches()
    {
        var flag = Deferred(new PercentageByContextConditionResource(TargetingKeyBucket));

        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey)).Value.Should().BeTrue();
    }

    [Fact]
    public void PercentageByContext_TargetingKeyOutsideRollout_DoesNotMatch()
    {
        var flag = Deferred(new PercentageByContextConditionResource(TargetingKeyBucket - 1));

        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey)).Value.Should().BeFalse();
    }

    [Fact]
    public void PercentageByContext_NoTargetingKey_OnlyFullRolloutMatches()
    {
        using var scope = new AssertionScope();

        ClientSideEvaluator.Evaluate(Deferred(new PercentageByContextConditionResource(100)), Context())
            .Value.Should().BeTrue("a 100% rollout matches even without a targeting key");
        ClientSideEvaluator.Evaluate(Deferred(new PercentageByContextConditionResource(99)), Context())
            .Value.Should().BeFalse("a partial rollout cannot bucket without a targeting key");
        ClientSideEvaluator.Evaluate(Deferred(new PercentageByContextConditionResource(50)), Context(targetingKey: ""))
            .Value.Should().BeFalse("an empty targeting key is treated the same as none");
    }

    [Fact]
    public void ContextAttributeIsOneOf_MatchesWhenAttributeValueIsListed()
    {
        var flag = Deferred(new ContextAttributeIsOneOfConditionResource("user-id", ["1234", "5678"]));

        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("user-id", "5678"))).Value.Should().BeTrue();
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("user-id", "9999"))).Value.Should().BeFalse();
        ClientSideEvaluator.Evaluate(flag, Context()).Value.Should().BeFalse("a missing attribute is not one of the values");
    }

    [Fact]
    public void ContextAttributeIsOneOf_IsCaseInsensitiveForKeyAndValue()
    {
        var flag = Deferred(new ContextAttributeIsOneOfConditionResource("Region", ["EU", "US"]));

        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("region", "eu"))).Value.Should().BeTrue();
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("REGION", "Us"))).Value.Should().BeTrue();
    }

    [Fact]
    public void ContextAttributeIsNotOneOf_MatchesUnlessAttributeValueIsListed()
    {
        var flag = Deferred(new ContextAttributeIsNotOneOfConditionResource("region", ["eu"]));

        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("region", "us"))).Value.Should().BeTrue();
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("region", "eu"))).Value.Should().BeFalse();
        ClientSideEvaluator.Evaluate(flag, Context()).Value.Should().BeTrue("a missing attribute is not one of the values");
    }

    [Fact]
    public void UnknownCondition_NeverMatches()
    {
        var flag = Deferred(new UnknownConditionResource("some-future-condition"));

        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey)).Value.Should().BeFalse();
    }

    [Fact]
    public void ConditionsWithinARule_AreCombinedWithAnd()
    {
        var flag = Deferred(
            new PercentageByContextConditionResource(100),
            new ContextAttributeIsOneOfConditionResource("plan", ["pro"]));

        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey, ("plan", "pro"))).Value.Should().BeTrue("both conditions match");
        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey, ("plan", "free"))).Value.Should().BeFalse("one condition fails");
    }

    [Fact]
    public void ARuleContainingAnUnknownCondition_CanNeverMatch()
    {
        var flag = Deferred(
            new ContextAttributeIsOneOfConditionResource("plan", ["pro"]),
            new UnknownConditionResource("some-future-condition"));

        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey, ("plan", "pro"))).Value.Should().BeFalse();
    }

    [Fact]
    public void RulesAcrossAFlag_AreCombinedWithOr()
    {
        var flag = DeferredWithRules(
            new ClientSideRuleResource("beta-testers", [new ContextAttributeIsOneOfConditionResource("plan", ["beta"])]),
            new ClientSideRuleResource("internal", [new ContextAttributeIsOneOfConditionResource("email", ["staff@octopus.com"])]));

        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("plan", "beta"))).Value.Should().BeTrue("first rule matches");
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("email", "staff@octopus.com"))).Value.Should().BeTrue("second rule matches");
        ClientSideEvaluator.Evaluate(flag, Context(attributes: ("plan", "free"))).Value.Should().BeFalse("no rule matches");
    }

    [Fact]
    public void FirstMatchingRule_ProvidesTheReason()
    {
        // Both rules match; the reason should name the first one that did.
        var flag = DeferredWithRules(
            new ClientSideRuleResource("first", [new ContextAttributeIsOneOfConditionResource("plan", ["pro"])]),
            new ClientSideRuleResource("second", [new ContextAttributeIsOneOfConditionResource("plan", ["pro"])]));

        var result = ClientSideEvaluator.Evaluate(flag, Context(attributes: ("plan", "pro")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'first'.");
    }
}
