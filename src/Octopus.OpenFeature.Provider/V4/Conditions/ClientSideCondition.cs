using System.Text.Json.Serialization;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Base type for a client-side rule condition, selected from the camelCase <c>type</c> discriminator
/// when deserialising a v4 evaluation response. Each condition knows how to match itself against a
/// <see cref="ClientSideEvaluationContext"/>.
///
/// A discriminator this version of the provider does not recognise deserialises to
/// <see cref="UnknownCondition"/> rather than failing, so a condition type introduced by a newer
/// server degrades safely on an older client.
/// </summary>
[JsonConverter(typeof(ClientSideConditionJsonConverter))]
internal abstract class ClientSideCondition
{
    /// <summary>
    /// Whether this condition is met for the given context.
    ///
    /// The condition is assumed to have arrived in a shape its type can be evaluated in. One that did
    /// not — a rollout with no percentage, an attribute condition with no values — throws the parse
    /// error <see cref="ClientSideEvaluationContext.ParseError"/> builds rather than reading a value it
    /// was not sent.
    /// </summary>
    public abstract bool Matches(ClientSideEvaluationContext context);
}
