using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Selects the concrete <see cref="ClientSideCondition"/> from the camelCase <c>type</c>
/// discriminator. An unrecognised (or absent) discriminator deserialises to
/// <see cref="UnknownCondition"/> rather than throwing, so a condition type introduced by a newer
/// server degrades safely on an older client.
///
/// The provider only ever reads these conditions, so serialisation is not implemented.
/// </summary>
internal sealed class ClientSideConditionJsonConverter : JsonConverter<ClientSideCondition>
{
    const string Discriminator = "type";

    public override ClientSideCondition Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        using var document = JsonDocument.ParseValue(ref reader);
        var element = document.RootElement;

        var type = element.TryGetProperty(Discriminator, out var typeProperty) && typeProperty.ValueKind == JsonValueKind.String
            ? typeProperty.GetString()
            : null;

        // Deserialising the concrete type uses that type directly, so this converter (registered for
        // the base type only) is not re-entered.
        return type switch
        {
            ConditionTypeNames.PercentageByContext => element.Deserialize<PercentageByContextCondition>(options)!,
            ConditionTypeNames.ContextAttributeIsOneOf => element.Deserialize<ContextAttributeIsOneOfCondition>(options)!,
            ConditionTypeNames.ContextAttributeIsNotOneOf => element.Deserialize<ContextAttributeIsNotOneOfCondition>(options)!,
            _ => new UnknownCondition(type)
        };
    }

    public override void Write(Utf8JsonWriter writer, ClientSideCondition value, JsonSerializerOptions options)
        => throw new NotImplementedException("The provider does not serialise client-side conditions.");
}
