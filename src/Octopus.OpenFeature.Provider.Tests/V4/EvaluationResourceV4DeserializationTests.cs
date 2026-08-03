using System.Text.Json;
using FluentAssertions;
using Octopus.OpenFeature.Provider.V4;

namespace Octopus.OpenFeature.Provider.Tests.V4;

/// <summary>
/// Exercises polymorphic JSON deserialisation of the v4 evaluation response. The response is
/// deserialised with <see cref="JsonSerializerOptions.Web"/> — the same options the provider client
/// uses in production — so the discriminator matching, camelCase property binding and null-omission
/// behaviour are all covered end to end.
/// </summary>
public class EvaluationResourceV4DeserializationTests
{
    static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    [Fact]
    public void PercentageByContextCondition_DeserialisesToConcreteType()
    {
        const string json = """{ "type": "percentage-by-context", "percentage": 50 }""";

        var condition = JsonSerializer.Deserialize<ClientSideConditionResource>(json, Options);

        var percentage = condition.Should().BeOfType<PercentageByContextConditionResource>().Subject;
        percentage.Percentage.Should().Be(50);
    }

    [Fact]
    public void ContextAttributeIsOneOfCondition_DeserialisesToConcreteType()
    {
        const string json = """{ "type": "context-attribute-is-one-of", "key": "user-id", "values": ["1234", "5678"] }""";

        var condition = JsonSerializer.Deserialize<ClientSideConditionResource>(json, Options);

        var isOneOf = condition.Should().BeOfType<ContextAttributeIsOneOfConditionResource>().Subject;
        isOneOf.Key.Should().Be("user-id");
        isOneOf.Values.Should().Equal("1234", "5678");
    }

    [Fact]
    public void ContextAttributeIsNotOneOfCondition_DeserialisesToConcreteType()
    {
        const string json = """{ "type": "context-attribute-is-not-one-of", "key": "region", "values": ["us", "eu"] }""";

        var condition = JsonSerializer.Deserialize<ClientSideConditionResource>(json, Options);

        var isNotOneOf = condition.Should().BeOfType<ContextAttributeIsNotOneOfConditionResource>().Subject;
        isNotOneOf.Key.Should().Be("region");
        isNotOneOf.Values.Should().Equal("us", "eu");
    }

    [Fact]
    public void MixedConditionArray_DeserialisesEachToItsConcreteType()
    {
        const string json = """
            [
                { "type": "percentage-by-context", "percentage": 25 },
                { "type": "context-attribute-is-one-of", "key": "user-id", "values": ["1234"] },
                { "type": "context-attribute-is-not-one-of", "key": "region", "values": ["au"] }
            ]
            """;

        var conditions = JsonSerializer.Deserialize<ClientSideConditionResource[]>(json, Options);

        conditions.Should().HaveCount(3);
        conditions![0].Should().BeOfType<PercentageByContextConditionResource>();
        conditions[1].Should().BeOfType<ContextAttributeIsOneOfConditionResource>();
        conditions[2].Should().BeOfType<ContextAttributeIsNotOneOfConditionResource>();
    }

    [Fact]
    public void UnknownConditionType_ThrowsJsonException()
    {
        const string json = """{ "type": "not-a-real-condition", "percentage": 50 }""";

        var deserialise = () => JsonSerializer.Deserialize<ClientSideConditionResource>(json, Options);

        deserialise.Should().Throw<JsonException>();
    }

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

        var flag = JsonSerializer.Deserialize<EvaluationResourceV4>(json, Options);

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

        var flag = JsonSerializer.Deserialize<EvaluationResourceV4>(json, Options);

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

        var flags = JsonSerializer.Deserialize<EvaluationResourceV4[]>(json, Options);

        flags.Should().HaveCount(2);

        flags![0].Slug.Should().Be("resolved-feature");
        flags[0].Value.Should().BeFalse();
        flags[0].Rules.Should().BeNull();

        flags[1].Slug.Should().Be("deferred-feature");
        flags[1].Value.Should().BeNull();
        flags[1].Rules.Should().ContainSingle();
        flags[1].Rules![0].Conditions[0].Should().BeOfType<PercentageByContextConditionResource>();
    }

    [Fact]
    public void DeferredFlag_RoundTripsThroughSerialisation()
    {
        var original = new EvaluationResourceV4(
            slug: "my-feature",
            value: null,
            reason: null,
            evaluationKey: "0f8fad5b-d9cb-469f-a165-70867728950e",
            rules:
            [
                new ClientSideRuleResource("Rule 1",
                [
                    new PercentageByContextConditionResource(50),
                    new ContextAttributeIsOneOfConditionResource("user-id", ["1234", "5678"])
                ])
            ]);

        var json = JsonSerializer.Serialize(original, Options);
        var roundTripped = JsonSerializer.Deserialize<EvaluationResourceV4>(json, Options);

        // The server-resolved properties are omitted from the wire form when null. (Guard against the
        // "values" array key, which also contains the substring "value".)
        json.Should().NotContain("\"value\":").And.NotContain("\"reason\":");
        // The polymorphic discriminator is written with the camelCase property name and mirrors the
        // vocabulary the server uses.
        json.Should().Contain("\"type\":\"percentage-by-context\"");

        roundTripped.Should().NotBeNull();
        roundTripped!.EvaluationKey.Should().Be("0f8fad5b-d9cb-469f-a165-70867728950e");
        roundTripped.Rules!.Should().ContainSingle();
        roundTripped.Rules![0].Conditions[0].Should().BeOfType<PercentageByContextConditionResource>()
            .Which.Percentage.Should().Be(50);
    }
}
