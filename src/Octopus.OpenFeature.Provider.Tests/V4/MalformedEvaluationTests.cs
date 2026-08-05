using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// A response the server could not legitimately have sent resolves to the caller's default value with
/// a parse error, rather than being evaluated as far as it can be. The shapes below mirror
/// <c>malformed-evaluations.json</c> in the shared provider specification, so the two stay in step.
///
/// The deliberate exception — a condition naming a type this client does not recognise — is covered by
/// <see cref="UnrecognisedConditionTests"/>.
///
/// Every case is deserialised rather than constructed: the declared types are non-nullable, so these
/// shapes are only reachable off the wire.
/// </summary>
public class MalformedEvaluationTests
{
    const string ExpectedReason = "Feature toggle my-feature is missing necessary information for client-side evaluation.";

    /// <summary>
    /// A context that satisfies every rule in the cases below, so a flag that failed to error would
    /// visibly turn on rather than quietly resolving to the same value by another route.
    /// </summary>
    static EvaluationContext MatchingContext()
        => Contexts.OpenFeature(Contexts.TargetingKey, ("license", "trial"), ("ring", "beta"));

    static EvaluationResource Flag(string json)
        => JsonSerializer.Deserialize<EvaluationResource>(json, JsonSerializerOptions.Web)!;

    [Theory]
    // Neither shape, or both at once.
    [InlineData("""{ "slug": "my-feature" }""",
        "the flag has neither a value nor rules")]
    [InlineData("""{ "slug": "my-feature", "value": true, "reason": "Enabled.", "evaluationKey": "evaluation-key", "rules": [ { "name": "Beta ring", "conditions": [ { "type": "context-attribute-is-one-of", "key": "ring", "values": [ "beta" ] } ] } ] }""",
        "the flag carries both a server-resolved value and client-side rules")]
    [InlineData("""{ "slug": "my-feature", "value": true }""",
        "the server resolved the flag but sent no reason")]
    // Deferred, but not evaluable.
    [InlineData("""{ "slug": "my-feature", "rules": [ { "name": "Beta ring", "conditions": [ { "type": "context-attribute-is-one-of", "key": "ring", "values": [ "beta" ] } ] } ] }""",
        "the flag defers to the client but has no evaluation key")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [] }""",
        "the flag defers to the client with no rules")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ null ] }""",
        "the flag has a missing rule")]
    // Rules.
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "conditions": [ { "type": "context-attribute-is-one-of", "key": "ring", "values": [ "beta" ] } ] } ] }""",
        "a rule has no name")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Beta ring", "conditions": [] } ] }""",
        "rule 'Beta ring' has no conditions")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Beta ring" } ] }""",
        "rule 'Beta ring' has no conditions")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Beta ring", "conditions": null } ] }""",
        "rule 'Beta ring' has no conditions")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Beta ring", "conditions": [ null ] } ] }""",
        "rule 'Beta ring' has a missing condition")]
    // Conditions with no usable type. Unlike an unrecognised type, no server version emits these.
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "key": "license", "values": [ "trial" ] } ] } ] }""",
        "rule 'Trial licences' has a condition with no type")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": 123, "key": "license", "values": [ "trial" ] } ] } ] }""",
        "rule 'Trial licences' has a condition with no type")]
    // percentage-by-context.
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Partial rollout", "conditions": [ { "type": "percentage-by-context" } ] } ] }""",
        "rule 'Partial rollout' has a percentage-by-context condition with no percentage")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Partial rollout", "conditions": [ { "type": "percentage-by-context", "percentage": 101 } ] } ] }""",
        "rule 'Partial rollout' has a percentage-by-context condition with a percentage of 101")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Partial rollout", "conditions": [ { "type": "percentage-by-context", "percentage": -1 } ] } ] }""",
        "rule 'Partial rollout' has a percentage-by-context condition with a percentage of -1")]
    // Attribute conditions.
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-one-of", "key": "license" } ] } ] }""",
        "rule 'Trial licences' has a context-attribute condition on 'license' with no values")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-one-of", "key": "license", "values": [] } ] } ] }""",
        "rule 'Trial licences' has a context-attribute condition on 'license' with no values")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-one-of", "key": "license", "values": [ null ] } ] } ] }""",
        "rule 'Trial licences' has a context-attribute condition on 'license' with a missing value")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-not-one-of", "values": [ "trial" ] } ] } ] }""",
        "rule 'Trial licences' has a context-attribute condition with no key")]
    public void AMalformedFlag_ResolvesToTheDefaultValueWithAParseError(string flagJson, string expectedProblem)
    {
        using var scope = new AssertionScope(expectedProblem);

        // Both defaults, so "returns the default" is not satisfied by coincidence.
        foreach (var defaultValue in new[] { true, false })
        {
            var result = ClientSideEvaluator.Evaluate(Flag(flagJson), defaultValue, MatchingContext());

            result.Value.Should().Be(defaultValue);
            result.ErrorType.Should().Be(ErrorType.ParseError);
            result.Reason.Should().Be(ExpectedReason);
            result.ErrorMessage.Should().Be($"Feature toggle my-feature could not be evaluated because {expectedProblem}.");
        }
    }

    [Fact]
    public void AMalformedConditionFailsTheWholeFlag_EvenWhenAnotherRuleMatches()
    {
        // The second rule matches this context. A rule the client cannot make sense of is not simply
        // skipped: the response is untrustworthy, so the flag defaults rather than resolving off the
        // rules that happened to parse.
        const string json = """
            {
                "slug": "my-feature",
                "evaluationKey": "evaluation-key",
                "rules": [
                    { "name": "Trial licences", "conditions": [ { "key": "license", "values": [ "trial" ] } ] },
                    { "name": "Beta ring", "conditions": [ { "type": "context-attribute-is-one-of", "key": "ring", "values": [ "beta" ] } ] }
                ]
            }
            """;

        var result = ClientSideEvaluator.Evaluate(Flag(json), defaultValue: false, MatchingContext());

        using var scope = new AssertionScope();
        result.Value.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.ParseError);
        result.ErrorMessage.Should().Be(
            "Feature toggle my-feature could not be evaluated because rule 'Trial licences' has a condition with no type.");
    }

    [Fact]
    public void AMalformedFlag_DoesNotAffectTheRestOfTheResponse()
    {
        const string json = """
            [
                { "slug": "malformed-feature" },
                { "slug": "well-formed-feature", "value": true, "reason": "The flag is enabled for this environment." }
            ]
            """;

        var flags = JsonSerializer.Deserialize<EvaluationResource[]>(json, JsonSerializerOptions.Web)!;

        using var scope = new AssertionScope();

        var malformed = ClientSideEvaluator.Evaluate(flags[0], defaultValue: false, MatchingContext());
        malformed.ErrorType.Should().Be(ErrorType.ParseError);

        var wellFormed = ClientSideEvaluator.Evaluate(flags[1], defaultValue: false, MatchingContext());
        wellFormed.Value.Should().BeTrue();
        wellFormed.ErrorType.Should().Be(ErrorType.None);
        wellFormed.Reason.Should().Be("The flag is enabled for this environment.");
    }

    [Fact]
    public void EvaluatingAMalformedFlag_DoesNotThrow()
    {
        // v3's evaluation path never throws and OctopusFeatureProvider does not wrap the call, so the
        // v4 path must not be the first one that can.
        const string json = """
            {
                "slug": "my-feature",
                "evaluationKey": "evaluation-key",
                "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-one-of", "key": "license" } ] } ]
            }
            """;

        var evaluate = () => ClientSideEvaluator.Evaluate(Flag(json), defaultValue: false, MatchingContext());

        evaluate.Should().NotThrow();
    }
}
