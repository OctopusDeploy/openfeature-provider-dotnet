namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// A client-side condition whose <c>type</c> discriminator this version of the provider does not
/// recognise (or which carried no discriminator). Rather than failing the whole evaluation response,
/// an unrecognised condition is preserved as this type. It never matches, so a rule containing one can
/// never match — a newer server capability is safely treated as "not met" by an older client.
///
/// The two cases part company at validation. A condition naming a type this client has never heard of
/// is a well-formed condition from a newer server, so it quietly fails its own rule. A condition with
/// no type at all is a payload no server version could have produced, so it fails the whole flag.
/// </summary>
internal sealed class UnknownConditionResource : ClientSideConditionResource
{
    public UnknownConditionResource(string? type)
    {
        Type = type;
    }

    /// <summary>The unrecognised discriminator value, or <c>null</c> if none was present.</summary>
    public string? Type { get; }

    public override bool Matches(ClientSideEvaluationContext context) => false;

    public override string? Validate() => Type is null ? "a condition with no type" : null;
}
