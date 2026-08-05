using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Base type for a client-side rule condition, selected from the camelCase <c>type</c> discriminator
/// when deserialising a v4 evaluation response. Each condition knows how to match itself against a
/// <see cref="ClientSideEvaluationContext"/>.
///
/// A discriminator this version of the provider does not recognise deserialises to
/// <see cref="UnknownConditionResource"/> rather than failing, so a condition type introduced by a
/// newer server degrades safely on an older client.
/// </summary>
[JsonConverter(typeof(ClientSideConditionResourceJsonConverter))]
internal abstract class ClientSideConditionResource
{
    /// <summary>Whether this condition is met for the given context.</summary>
    public abstract bool Matches(ClientSideEvaluationContext context);
}
