using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Constant;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// A condition naming a type this version does not recognise is a capability from a newer server, not a
/// bad payload: it fails its own rule and nothing else. The deliberate departure from
/// <see cref="MalformedEvaluationTests"/>, which covers every other shape — including a condition with
/// no type at all.
/// </summary>
public class UnrecognisedConditionTests
{
    static ServerSideEvaluation Flag(string json)
        => JsonSerializer.Deserialize<ServerSideEvaluation>(json, JsonSerializerOptions.Web)!;

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

        var result = Flag(json).Evaluate(Contexts.OpenFeature(Contexts.TargetingKey, ("license", "trial")));

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

        var result = Flag(json).Evaluate(Contexts.OpenFeature(Contexts.TargetingKey, ("license", "trial"), ("ring", "beta")));

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.ErrorType.Should().Be(ErrorType.None);
        result.Reason.Should().Be("Matched rule 'Beta ring'.");
    }
}
