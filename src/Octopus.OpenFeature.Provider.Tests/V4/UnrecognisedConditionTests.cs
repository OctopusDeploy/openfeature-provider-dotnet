using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Constant;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// A condition naming a type this version of the provider does not recognise is a capability from a
/// newer server, not a bad payload. It never matches, so it fails its own rule and nothing else — no
/// parse error, and other rules still decide the flag. Mirrors <c>unrecognised-conditions.json</c> in
/// the shared provider specification.
///
/// This is the one deliberate departure from <see cref="MalformedEvaluationTests"/>, which is what
/// happens to every other shape the server could not have sent — including a condition with no type at
/// all.
/// </summary>
public class UnrecognisedConditionTests
{
    static EvaluationResource Flag(string json)
        => JsonSerializer.Deserialize<EvaluationResource>(json, JsonSerializerOptions.Web)!;

    [Fact]
    public void AnUnrecognisedConditionType_FailsItsRuleWithoutAnError()
    {
        const string json = """
            {
                "slug": "my-feature",
                "evaluationKey": "evaluation-key",
                "rules": [
                    {
                        "name": "Something newer than this library",
                        "conditions": [ { "type": "not-a-real-condition", "key": "license", "values": [ "trial" ] } ]
                    }
                ]
            }
            """;

        // Defaulting to true: an unevaluable rule leaves the flag off, it does not fall back.
        var result = ClientSideEvaluator.Evaluate(
            Flag(json), defaultValue: true, Contexts.OpenFeature(Contexts.TargetingKey, ("license", "trial")));

        using var scope = new AssertionScope();
        result.Value.Should().BeFalse();
        result.ErrorType.Should().Be(ErrorType.None);
        result.Reason.Should().Be("Did not match any rules.");
    }

    [Fact]
    public void AnUnrecognisedConditionInOneRule_LeavesTheOtherRulesToDecide()
    {
        const string json = """
            {
                "slug": "my-feature",
                "evaluationKey": "evaluation-key",
                "rules": [
                    {
                        "name": "Something newer than this library",
                        "conditions": [ { "type": "not-a-real-condition", "key": "license", "values": [ "trial" ] } ]
                    },
                    {
                        "name": "Beta ring",
                        "conditions": [ { "type": "context-attribute-is-one-of", "key": "ring", "values": [ "beta" ] } ]
                    }
                ]
            }
            """;

        var result = ClientSideEvaluator.Evaluate(
            Flag(json), defaultValue: false, Contexts.OpenFeature(Contexts.TargetingKey, ("license", "trial"), ("ring", "beta")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.ErrorType.Should().Be(ErrorType.None);
        result.Reason.Should().Be("Matched rule 'Beta ring'.");
    }
}
