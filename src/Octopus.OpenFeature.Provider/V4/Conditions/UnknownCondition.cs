using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// A condition whose <c>type</c> this version of the provider does not recognise. It never matches, so
/// a newer server's capability is treated as "not met" by an older client rather than failing the flag.
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
        // No server version emits a condition without a type, so unlike an unrecognised type this is a
        // response that could not have been sent.
        if (Type is null)
        {
            throw new ParseErrorException("A condition has no type.");
        }

        return false;
    }
}
