using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// A named rule the provider library evaluates on the client side. The rule matches when every one of
/// its <see cref="Conditions"/> matches.
/// </summary>
internal sealed class ClientSideRule
{
    public ClientSideRule(string name, ClientSideCondition[] conditions)
    {
        Name = name;
        Conditions = conditions;
    }

    public string Name { get; }
    public ClientSideCondition[] Conditions { get; }

    public bool Matches(ClientSideEvaluationContext context)
    {
        // Name and Conditions are declared non-nullable, but nothing enforces that on a deserialised
        // payload. The server only defers a named rule carrying at least one condition, so anything else
        // is a response it could not have sent.
        if (Name is null)
        {
            throw new ParseErrorException("A rule has no name.");
        }

        if (Conditions is not { Length: > 0 })
        {
            throw new ParseErrorException($"Rule '{Name}' has no conditions.");
        }

        foreach (var condition in Conditions)
        {
            if (condition is null)
            {
                throw new ParseErrorException($"Rule '{Name}' has a missing condition.");
            }

            if (!condition.Matches(context))
            {
                return false;
            }
        }

        return true;
    }
}
