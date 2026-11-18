using System.Text.Json;
using FluentAssertions;
using Octopus.OpenFeature.Provider.V4;

namespace Octopus.OpenFeature.Provider.Tests;

/// <summary>
/// The provider reads the Octopus API through the source-generated <see cref="ProviderJsonContext"/>, which keeps it
/// working when reflection-based serialization is disabled (Native AOT and trimmed apps). These tests pin that the
/// context reads every response shape exactly as the reflection-based <see cref="JsonSerializerOptions.Web"/> path did,
/// so JIT consumers see no change. The Native AOT behaviour itself is checked by the AotSmokeTest project in CI.
/// </summary>
public class ProviderJsonContextTests
{
    public static TheoryData<string> EvaluationResponses => new()
    {
        """
        [
            { "slug": "enabled", "value": true, "reason": "The flag is enabled for this environment." },
            { "slug": "disabled", "value": false, "reason": "The flag is disabled for this environment." }
        ]
        """,
        """
        [
            {
                "slug": "deferred",
                "evaluationKey": "0f8fad5b-d9cb-469f-a165-70867728950e",
                "rules": [
                    {
                        "name": "Rule 1",
                        "conditions": [
                            { "type": "percentage-by-context", "percentage": 50 },
                            { "type": "context-attribute-is-one-of", "key": "region", "values": ["eu", "uk"] },
                            { "type": "context-attribute-is-not-one-of", "key": "plan", "values": ["trial"] }
                        ]
                    }
                ]
            }
        ]
        """,
        """
        [
            {
                "slug": "newer-server",
                "evaluationKey": "evaluation-key",
                "futureField": { "nested": [1, 2, 3] },
                "rules": [
                    { "name": "Rule 1", "conditions": [ { "type": "condition-from-the-future", "x": 1 }, { "percentage": 10 } ] }
                ]
            }
        ]
        """
    };

    [Theory]
    [MemberData(nameof(EvaluationResponses))]
    public void EvaluationResponse_ReadsTheSameAsTheReflectionPath(string json)
    {
        var fromContext = JsonSerializer.Deserialize(json, ProviderJsonContext.Default.ServerSideEvaluationArray);
        var fromReflection = JsonSerializer.Deserialize<ServerSideEvaluation[]>(json, JsonSerializerOptions.Web);

        fromContext.Should().BeEquivalentTo(fromReflection, options => options.RespectingRuntimeTypes().WithStrictOrdering());
    }

    [Fact]
    public void CheckResponse_ReadsTheContentHash()
    {
        var check = JsonSerializer.Deserialize("""{ "contentHash": "AQID" }""", ProviderJsonContext.Default.FeatureCheck);

        check!.ContentHash.Should().Equal(1, 2, 3);
    }
}
