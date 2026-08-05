using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Base type for a client-side rule condition, selected from the camelCase <c>type</c> discriminator
/// when deserialising a v4 evaluation response.
/// </summary>
[JsonConverter(typeof(ClientSideConditionJsonConverter))]
internal abstract class ClientSideCondition
{
    /// <summary>
    /// Whether this condition is met. A condition that did not arrive in a shape its type can evaluate
    /// throws <see cref="OpenFeature.Error.ParseErrorException"/> rather than reading a value it was not
    /// sent.
    /// </summary>
    public abstract bool Matches(ClientSideEvaluationContext context);
}
