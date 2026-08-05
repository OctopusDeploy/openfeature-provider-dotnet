namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// A client-side condition whose <c>type</c> discriminator this version of the provider does not
/// recognise (or which carried no discriminator). Rather than failing the whole evaluation response,
/// an unrecognised condition is preserved as this type. It never matches, so a rule containing one can
/// never match — a newer server capability is safely treated as "not met" by an older client.
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
}
