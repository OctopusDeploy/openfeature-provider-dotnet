namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// A client-side condition whose <c>type</c> discriminator this version of the provider does not
/// recognise (or which carried no discriminator). Rather than failing deserialisation of the whole
/// evaluation response, an unrecognised condition is preserved as this type.
///
/// The two cases part company when the condition is evaluated. A condition naming a type this client
/// has never heard of is a well-formed condition from a newer server: it never matches, so a rule
/// containing one can never match, and a newer server capability is safely treated as "not met" by an
/// older client. A condition with no type at all is a payload no server version could have produced, so
/// it fails the evaluation.
/// </summary>
internal sealed class UnknownCondition : ClientSideCondition
{
    public UnknownCondition(string? type)
    {
        Type = type;
    }

    /// <summary>The unrecognised discriminator value, or <c>null</c> if none was present.</summary>
    public string? Type { get; }

    public override bool Matches(ClientSideEvaluationContext context)
    {
        if (Type is null)
        {
            throw context.ParseError("a condition with no type");
        }

        return false;
    }
}
