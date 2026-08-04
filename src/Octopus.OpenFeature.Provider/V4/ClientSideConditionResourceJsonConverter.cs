using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Polymorphic (de)serialisation for <see cref="ClientSideConditionResource"/> using the camelCase
/// <c>type</c> discriminator OctoToggle writes.
///
/// Unlike the built-in <c>[JsonPolymorphic]</c> handling, an unrecognised (or absent) discriminator
/// does not throw: it deserialises to <see cref="UnknownConditionResource"/>, so a condition type a
/// newer server introduces degrades safely on an older client instead of failing the whole response.
/// </summary>
internal sealed class ClientSideConditionResourceJsonConverter : JsonConverter<ClientSideConditionResource>
{
    const string Discriminator = "type";

    public override ClientSideConditionResource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
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
            ConditionTypeNames.PercentageByContext => element.Deserialize<PercentageByContextConditionResource>(options)!,
            ConditionTypeNames.ContextAttributeIsOneOf => element.Deserialize<ContextAttributeIsOneOfConditionResource>(options)!,
            ConditionTypeNames.ContextAttributeIsNotOneOf => element.Deserialize<ContextAttributeIsNotOneOfConditionResource>(options)!,
            _ => new UnknownConditionResource(type)
        };
    }

    public override void Write(Utf8JsonWriter writer, ClientSideConditionResource value, JsonSerializerOptions options)
    {
        if (value is UnknownConditionResource unknown)
        {
            // The original payload is not retained, so an unknown condition round-trips as just its
            // discriminator (if it had one).
            writer.WriteStartObject();
            if (unknown.Type is not null)
            {
                writer.WriteString(Discriminator, unknown.Type);
            }

            writer.WriteEndObject();
            return;
        }

        var type = value switch
        {
            PercentageByContextConditionResource => ConditionTypeNames.PercentageByContext,
            ContextAttributeIsOneOfConditionResource => ConditionTypeNames.ContextAttributeIsOneOf,
            ContextAttributeIsNotOneOfConditionResource => ConditionTypeNames.ContextAttributeIsNotOneOf,
            _ => throw new JsonException($"Unexpected client-side condition type '{value.GetType().Name}'.")
        };

        writer.WriteStartObject();
        writer.WriteString(Discriminator, type);

        // Serialise the concrete instance's own properties into the same object. Serialising by the
        // runtime type bypasses this converter, so there's no recursion and no duplicate discriminator.
        using var document = JsonSerializer.SerializeToDocument(value, value.GetType(), options);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            property.WriteTo(writer);
        }

        writer.WriteEndObject();
    }
}
