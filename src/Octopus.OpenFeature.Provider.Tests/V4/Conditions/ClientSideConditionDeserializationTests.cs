using System.Text.Json;
using FluentAssertions;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

/// <summary>
/// Exercises polymorphic JSON deserialisation of a single client-side condition: selecting the
/// concrete type from the camelCase <c>type</c> discriminator, and degrading to
/// <see cref="UnknownCondition"/> when it is unrecognised or absent. Deserialisation of a
/// whole evaluation response is covered by <see cref="V4.ServerSideEvaluationDeserializationTests"/>.
///
/// Uses <see cref="JsonSerializerOptions.Web"/> — the same options the provider client uses in
/// production — so discriminator matching and camelCase property binding are covered end to end.
/// </summary>
public class ClientSideConditionDeserializationTests
{
    static readonly JsonSerializerOptions Options = JsonSerializerOptions.Web;

    [Fact]
    public void PercentageByContextCondition_DeserialisesToConcreteType()
    {
        const string json = """{ "type": "percentage-by-context", "percentage": 50 }""";

        var condition = JsonSerializer.Deserialize<ClientSideCondition>(json, Options);

        var percentage = condition.Should().BeOfType<PercentageByContextCondition>().Subject;
        percentage.Percentage.Should().Be(50);
    }

    [Fact]
    public void ContextAttributeIsOneOfCondition_DeserialisesToConcreteType()
    {
        const string json = """{ "type": "context-attribute-is-one-of", "key": "user-id", "values": ["1234", "5678"] }""";

        var condition = JsonSerializer.Deserialize<ClientSideCondition>(json, Options);

        var isOneOf = condition.Should().BeOfType<ContextAttributeIsOneOfCondition>().Subject;
        isOneOf.Key.Should().Be("user-id");
        isOneOf.Values.Should().Equal("1234", "5678");
    }

    [Fact]
    public void ContextAttributeIsNotOneOfCondition_DeserialisesToConcreteType()
    {
        const string json = """{ "type": "context-attribute-is-not-one-of", "key": "region", "values": ["us", "eu"] }""";

        var condition = JsonSerializer.Deserialize<ClientSideCondition>(json, Options);

        var isNotOneOf = condition.Should().BeOfType<ContextAttributeIsNotOneOfCondition>().Subject;
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

        var conditions = JsonSerializer.Deserialize<ClientSideCondition[]>(json, Options);

        conditions.Should().HaveCount(3);
        conditions![0].Should().BeOfType<PercentageByContextCondition>();
        conditions[1].Should().BeOfType<ContextAttributeIsOneOfCondition>();
        conditions[2].Should().BeOfType<ContextAttributeIsNotOneOfCondition>();
    }

    [Fact]
    public void UnknownConditionType_DeserialisesToUnknownConditionInsteadOfThrowing()
    {
        const string json = """{ "type": "not-a-real-condition", "percentage": 50 }""";

        var condition = JsonSerializer.Deserialize<ClientSideCondition>(json, Options);

        var unknown = condition.Should().BeOfType<UnknownCondition>().Subject;
        unknown.Type.Should().Be("not-a-real-condition");
    }

    [Fact]
    public void ConditionWithoutTypeDiscriminator_DeserialisesToUnknownCondition()
    {
        const string json = """{ "percentage": 50 }""";

        var condition = JsonSerializer.Deserialize<ClientSideCondition>(json, Options);

        var unknown = condition.Should().BeOfType<UnknownCondition>().Subject;
        unknown.Type.Should().BeNull();
    }
}
