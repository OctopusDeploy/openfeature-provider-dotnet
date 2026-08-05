using System.Text.Json;
using FluentAssertions;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// Exercises JSON deserialisation of a v4 evaluation response: both flag shapes, the array the
/// endpoint returns, and the rules and conditions hanging off a deferred flag. Deserialisation of an
/// individual condition is covered by
/// <see cref="Conditions.ClientSideConditionResourceDeserializationTests"/>.
///
/// Uses <see cref="JsonSerializerOptions.Web"/> — the same options the provider client uses in
/// production — so camelCase property binding and null-omission behaviour are covered end to end.
/// </summary>
public class EvaluationResourceDeserializationTests
{
    static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    [Fact]
    public void ServerResolvedFlag_DeserialisesSlugValueAndReason()
    {
        const string json = """
            {
                "slug": "my-feature",
                "value": true,
                "reason": "The flag is enabled for this environment."
            }
            """;

        var flag = JsonSerializer.Deserialize<EvaluationResource>(json, Options);

        flag.Should().NotBeNull();
        flag!.Slug.Should().Be("my-feature");
        flag.Value.Should().BeTrue();
        flag.Reason.Should().Be("The flag is enabled for this environment.");
        flag.EvaluationKey.Should().BeNull();
        flag.Rules.Should().BeNull();
    }

    [Fact]
    public void DeferredFlag_DeserialisesRulesWithPolymorphicConditions()
    {
        const string json = """
            {
                "slug": "my-feature",
                "evaluationKey": "0f8fad5b-d9cb-469f-a165-70867728950e",
                "rules": [
                    {
                        "name": "Rule 1",
                        "conditions": [
                            { "type": "percentage-by-context", "percentage": 50 },
                            { "type": "context-attribute-is-one-of", "key": "user-id", "values": ["1234", "5678"] }
                        ]
                    }
                ]
            }
            """;

        var flag = JsonSerializer.Deserialize<EvaluationResource>(json, Options);

        flag.Should().NotBeNull();
        flag!.Slug.Should().Be("my-feature");
        flag.EvaluationKey.Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
        flag.Value.Should().BeNull();
        flag.Reason.Should().BeNull();

        flag.Rules.Should().ContainSingle();
        var rule = flag.Rules![0];
        rule.Name.Should().Be("Rule 1");
        rule.Conditions.Should().HaveCount(2);

        var percentage = rule.Conditions[0].Should().BeOfType<PercentageByContextConditionResource>().Subject;
        percentage.Percentage.Should().Be(50);

        var isOneOf = rule.Conditions[1].Should().BeOfType<ContextAttributeIsOneOfConditionResource>().Subject;
        isOneOf.Key.Should().Be("user-id");
        isOneOf.Values.Should().Equal("1234", "5678");
    }

    [Fact]
    public void UnknownConditionAlongsideKnownConditions_IsPreservedWithoutFailingTheResponse()
    {
        const string json = """
            {
                "slug": "my-feature",
                "evaluationKey": "0f8fad5b-d9cb-469f-a165-70867728950e",
                "rules": [
                    {
                        "name": "Rule 1",
                        "conditions": [
                            { "type": "percentage-by-context", "percentage": 50 },
                            { "type": "some-future-condition", "someField": "someValue" }
                        ]
                    }
                ]
            }
            """;

        var flag = JsonSerializer.Deserialize<EvaluationResource>(json, Options);

        var conditions = flag!.Rules![0].Conditions;
        conditions[0].Should().BeOfType<PercentageByContextConditionResource>();
        conditions[1].Should().BeOfType<UnknownConditionResource>()
            .Which.Type.Should().Be("some-future-condition");
    }

    [Fact]
    public void EvaluationsResponse_DeserialisesAsArrayOfFlags()
    {
        const string json = """
            [
                { "slug": "resolved-feature", "value": false, "reason": "The flag is disabled for this environment." },
                {
                    "slug": "deferred-feature",
                    "evaluationKey": "0f8fad5b-d9cb-469f-a165-70867728950e",
                    "rules": [
                        { "name": "Rule 1", "conditions": [ { "type": "percentage-by-context", "percentage": 10 } ] }
                    ]
                }
            ]
            """;

        var flags = JsonSerializer.Deserialize<EvaluationResource[]>(json, Options);

        flags.Should().HaveCount(2);

        flags![0].Slug.Should().Be("resolved-feature");
        flags[0].Value.Should().BeFalse();
        flags[0].Rules.Should().BeNull();

        flags[1].Slug.Should().Be("deferred-feature");
        flags[1].Value.Should().BeNull();
        flags[1].Rules.Should().ContainSingle();
        flags[1].Rules![0].Conditions[0].Should().BeOfType<PercentageByContextConditionResource>();
    }
}
