using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Constant;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// A response the server could not legitimately have sent throws a <see cref="ParseErrorException"/>,
/// rather than being evaluated as far as it can be. The OpenFeature SDK catches that and hands the
/// caller the default value they passed, with <see cref="ErrorType.ParseError"/> and the exception's
/// message — so these tests assert on what is thrown, and the specification fixture tests assert on the
/// details the SDK returns. The shapes below mirror <c>malformed-evaluations.json</c> in the shared
/// provider specification, so the two stay in step.
///
/// The deliberate exception — a condition naming a type this client does not recognise — is covered by
/// <see cref="UnrecognisedConditionTests"/>.
///
/// Every case is deserialised rather than constructed: the declared types are non-nullable, so these
/// shapes are only reachable off the wire.
/// </summary>
public class MalformedEvaluationTests
{
    /// <summary>
    /// A context that satisfies every rule in the cases below, so a flag that failed to throw would
    /// visibly turn on rather than quietly resolving to the same value by another route.
    /// </summary>
    static EvaluationContext MatchingContext()
        => Contexts.OpenFeature(Contexts.TargetingKey, ("license", "trial"), ("ring", "beta"));

    static ServerSideEvaluation Flag(string json)
        => JsonSerializer.Deserialize<ServerSideEvaluation>(json, JsonSerializerOptions.Web)!;

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
        "a condition with no type")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": 123, "key": "license", "values": [ "trial" ] } ] } ] }""",
        "a condition with no type")]
    // percentage-by-context.
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Partial rollout", "conditions": [ { "type": "percentage-by-context" } ] } ] }""",
        "a percentage-by-context condition with no percentage")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Partial rollout", "conditions": [ { "type": "percentage-by-context", "percentage": 101 } ] } ] }""",
        "a percentage-by-context condition with a percentage of 101")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Partial rollout", "conditions": [ { "type": "percentage-by-context", "percentage": -1 } ] } ] }""",
        "a percentage-by-context condition with a percentage of -1")]
    // Attribute conditions.
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-one-of", "key": "license" } ] } ] }""",
        "a context-attribute condition on 'license' with no values")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-one-of", "key": "license", "values": [] } ] } ] }""",
        "a context-attribute condition on 'license' with no values")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-one-of", "key": "license", "values": [ null ] } ] } ] }""",
        "a context-attribute condition on 'license' with a missing value")]
    [InlineData("""{ "slug": "my-feature", "evaluationKey": "evaluation-key", "rules": [ { "name": "Trial licences", "conditions": [ { "type": "context-attribute-is-not-one-of", "values": [ "trial" ] } ] } ] }""",
        "a context-attribute condition with no key")]
    public void AMalformedFlag_ThrowsAParseError(string flagJson, string expectedProblem)
    {
        var evaluate = () => Flag(flagJson).Evaluate(MatchingContext());

        using var scope = new AssertionScope(expectedProblem);
        var exception = evaluate.Should().Throw<ParseErrorException>().Which;
        exception.ErrorType.Should().Be(ErrorType.ParseError);
        exception.Message.Should().Be(Contexts.MalformedMessage(expectedProblem));
    }

    [Fact]
    public void AMalformedRule_FailsTheFlagEvenWhenALaterRuleMatches()
    {
        // The second rule matches this context. A rule the client cannot make sense of is not simply
        // skipped: evaluation reaches it and bails, rather than answering off the rules that happened to
        // parse.
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

        var evaluate = () => Flag(json).Evaluate(MatchingContext());

        evaluate.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be(Contexts.MalformedMessage("a condition with no type"));
    }

    [Fact]
    public void AMalformedRule_BehindAMatchingRule_IsNeverRead()
    {
        // Nothing checks the response up front, so a rule only fails the flag if evaluation gets as far
        // as reading it. An earlier rule matching means the flag has its answer without the bad one.
        const string json = """
            {
                "slug": "my-feature",
                "evaluationKey": "evaluation-key",
                "rules": [
                    { "name": "Beta ring", "conditions": [ { "type": "context-attribute-is-one-of", "key": "ring", "values": [ "beta" ] } ] },
                    { "name": "Trial licences", "conditions": [ { "key": "license", "values": [ "trial" ] } ] }
                ]
            }
            """;

        var result = Flag(json).Evaluate(MatchingContext());

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("Matched rule 'Beta ring'.");
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

        var flags = JsonSerializer.Deserialize<ServerSideEvaluation[]>(json, JsonSerializerOptions.Web)!;

        using var scope = new AssertionScope();

        var malformed = () => flags[0].Evaluate(MatchingContext());
        malformed.Should().Throw<ParseErrorException>();

        var wellFormed = flags[1].Evaluate(MatchingContext());
        wellFormed.Value.Should().BeTrue();
        wellFormed.ErrorType.Should().Be(ErrorType.None);
        wellFormed.Reason.Should().Be("The flag is enabled for this environment.");
    }
}
