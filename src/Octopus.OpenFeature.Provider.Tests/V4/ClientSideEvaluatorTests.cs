using System.Text.Json;
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

    // A malformed response: the server always sends an evaluation key alongside deferred rules.
    static EvaluationResource DeferredWithoutEvaluationKey(params ClientSideConditionResource[] conditions)
        => new("my-feature", value: null, reason: null, evaluationKey: null,
            rules: [new ClientSideRuleResource("Rule 1", conditions)]);

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
    public void RuleWithNoConditions_DoesNotMatch()
    {
        // The server only defers a rule that has at least one client-side condition, so a rule with
        // none is a malformed response rather than a "matches everyone" rule. A rule the client cannot
        // make sense of must not turn a flag on.
        var flag = DeferredWithRules(new ClientSideRuleResource("everyone", []));

        var result = ClientSideEvaluator.Evaluate(flag, Context());

        using var scope = new AssertionScope();
        result.Value.Should().BeFalse();
        result.Reason.Should().Be("Did not match any rules.");
    }

    // Deserialised rather than constructed: the declared types are non-nullable, so a null rule or a
    // null/absent conditions array can only arrive off the wire. These shapes cannot come from
    // OctoToggle today, but none of them may throw out of the evaluator — v3's evaluation path never
    // throws, and the provider does not wrap this call.
    [Theory]
    [InlineData("""{ "name": "R", "conditions": [] }""", "an empty conditions array")]
    [InlineData("""{ "name": "R" }""", "an absent conditions array")]
    [InlineData("""{ "name": "R", "conditions": null }""", "a null conditions array")]
    [InlineData("""{ "name": "R", "conditions": [null] }""", "a null condition")]
    [InlineData("null", "a null rule")]
    public void AMalformedRule_DoesNotMatchAndDoesNotThrow(string ruleJson, string because)
    {
        var json = $$"""{ "slug": "my-feature", "evaluationKey": "{{EvaluationKey}}", "rules": [ {{ruleJson}} ] }""";
        var flag = JsonSerializer.Deserialize<EvaluationResource>(json, JsonSerializerOptions.Web)!;

        var evaluate = () => ClientSideEvaluator.Evaluate(flag, Context(TargetingKey));

        using var scope = new AssertionScope();
        evaluate.Should().NotThrow(because);
        evaluate().Value.Should().BeFalse(because);
        evaluate().Reason.Should().Be("Did not match any rules.");
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

    [Theory]
    [InlineData("Plan", "free", "plan", "pro")]
    [InlineData("plan", "pro", "Plan", "free")]
    public void ContextAttributeIsOneOf_ChecksEveryEntryWhoseKeyMatches(string firstKey, string firstValue, string secondKey, string secondValue)
    {
        // A context can carry several case variants of the same key. Every one of them has to be
        // considered: AsDictionary returns an immutable dictionary ordered by key hash, and .NET
        // randomises string hashing per process, so checking only the first matching entry made the
        // same flag, context and rule evaluate differently from one process to the next.
        var flag = Deferred(new ContextAttributeIsOneOfConditionResource("plan", ["pro"]));

        var result = ClientSideEvaluator.Evaluate(flag, Context(attributes: [(firstKey, firstValue), (secondKey, secondValue)]));

        result.Value.Should().BeTrue("one of the 'plan' entries is 'pro', whichever order they are iterated in");
    }

    [Fact]
    public void ContextAttributeIsOneOf_TreatsANonStringValueAsAbsent()
    {
        // OpenFeature's Value.AsString is null for a non-string, and v3 segment matching skips those
        // entries too, so a numeric attribute never matches a string value.
        var context = EvaluationContext.Builder().Set("user-id", 1234).Build();

        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(Deferred(new ContextAttributeIsOneOfConditionResource("user-id", ["1234"])), context)
            .Value.Should().BeFalse();
        ClientSideEvaluator.Evaluate(Deferred(new ContextAttributeIsNotOneOfConditionResource("user-id", ["1234"])), context)
            .Value.Should().BeTrue();
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
    public void WithoutAnEvaluationKey_AnAttributeOnlyRuleIsStillEvaluated()
    {
        // Only percentage-by-context needs the evaluation key, so a response missing one must not stop
        // an attribute-only rule from matching.
        var flag = DeferredWithoutEvaluationKey(new ContextAttributeIsOneOfConditionResource("plan", ["pro"]));

        var result = ClientSideEvaluator.Evaluate(flag, Context(attributes: ("plan", "pro")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'Rule 1'.");
    }

    [Fact]
    public void WithoutAnEvaluationKey_APercentageRolloutCannotMatch()
    {
        // There is no key to bucket against, so the condition is unmet rather than assumed.
        var flag = DeferredWithoutEvaluationKey(new PercentageByContextConditionResource(100));

        ClientSideEvaluator.Evaluate(flag, Context(TargetingKey)).Value.Should().BeFalse();
    }

    [Fact]
    public void ANullContext_IsTreatedAsAnEmptyContext()
    {
        using var scope = new AssertionScope();
        ClientSideEvaluator.Evaluate(Deferred(new ContextAttributeIsOneOfConditionResource("plan", ["pro"])), null)
            .Value.Should().BeFalse("there is no attribute to match");
        ClientSideEvaluator.Evaluate(Deferred(new PercentageByContextConditionResource(100)), null)
            .Value.Should().BeTrue("a 100% rollout matches without a targeting key");
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
