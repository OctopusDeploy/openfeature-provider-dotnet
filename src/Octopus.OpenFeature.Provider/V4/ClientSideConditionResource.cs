using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Base type for a rule condition that a provider library is expected to evaluate on the client
/// side. OctoToggle serialises each condition with a camelCase <c>type</c> discriminator, which
/// System.Text.Json uses to select the concrete type when deserialising the v4 evaluation response.
///
/// The response only ever carries the client-side conditions; OctoToggle resolves the server-side
/// conditions itself. These types model the wire shape so the response can be deserialised — the
/// conditions are intentionally not evaluated yet. Client-side evaluation will be implemented
/// separately.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(ContextAttributeIsNotOneOfConditionResource), ConditionTypeNames.ContextAttributeIsNotOneOf)]
[JsonDerivedType(typeof(ContextAttributeIsOneOfConditionResource), ConditionTypeNames.ContextAttributeIsOneOf)]
[JsonDerivedType(typeof(PercentageByContextConditionResource), ConditionTypeNames.PercentageByContext)]
internal abstract class ClientSideConditionResource
{
}

/// <summary>
/// Matches when the context attribute <see cref="Key"/> is one of <see cref="Values"/>.
/// </summary>
internal sealed class ContextAttributeIsOneOfConditionResource : ClientSideConditionResource
{
    public ContextAttributeIsOneOfConditionResource(string key, string[] values)
    {
        Key = key;
        Values = values;
    }

    public string Key { get; }
    public string[] Values { get; }
}

/// <summary>
/// Matches when the context attribute <see cref="Key"/> is not one of <see cref="Values"/>.
/// </summary>
internal sealed class ContextAttributeIsNotOneOfConditionResource : ClientSideConditionResource
{
    public ContextAttributeIsNotOneOfConditionResource(string key, string[] values)
    {
        Key = key;
        Values = values;
    }

    public string Key { get; }
    public string[] Values { get; }
}

/// <summary>
/// Matches for <see cref="Percentage"/>% of the population, bucketed by the OpenFeature targeting key.
/// </summary>
internal sealed class PercentageByContextConditionResource : ClientSideConditionResource
{
    public PercentageByContextConditionResource(int percentage)
    {
        Percentage = percentage;
    }

    public int Percentage { get; }
}
