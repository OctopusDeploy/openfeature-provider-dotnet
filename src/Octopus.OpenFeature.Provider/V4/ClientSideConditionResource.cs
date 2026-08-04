using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Base type for a client-side rule condition, selected from the camelCase <c>type</c> discriminator
/// when deserialising a v4 evaluation response. These types model the wire shape only; client-side
/// evaluation is not implemented yet.
///
/// A discriminator this version of the provider does not recognise deserialises to
/// <see cref="UnknownConditionResource"/> rather than failing, so a condition type introduced by a
/// newer server degrades safely on an older client.
/// </summary>
[JsonConverter(typeof(ClientSideConditionResourceJsonConverter))]
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
/// Matches when the OpenFeature targeting key falls within the <see cref="Percentage"/>% rollout.
/// </summary>
internal sealed class PercentageByContextConditionResource : ClientSideConditionResource
{
    public PercentageByContextConditionResource(int percentage)
    {
        Percentage = percentage;
    }

    public int Percentage { get; }
}

/// <summary>
/// A client-side condition whose <c>type</c> discriminator this version of the provider does not
/// recognise (or which carried no discriminator). Rather than failing the whole evaluation response,
/// an unrecognised condition is preserved as this type. It always evaluates to <c>false</c>, so a
/// rule containing an unknown condition can never match — a newer server capability is safely treated
/// as "not met" by an older client. Client-side evaluation is not implemented yet.
/// </summary>
internal sealed class UnknownConditionResource : ClientSideConditionResource
{
    public UnknownConditionResource(string? type)
    {
        Type = type;
    }

    /// <summary>The unrecognised discriminator value, or <c>null</c> if none was present.</summary>
    public string? Type { get; }
}
