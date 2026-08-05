using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// A named rule the provider library still has to evaluate on the client side. The rule matches
/// when every one of its <see cref="Conditions"/> matches.
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

    /// <summary>
    /// Whether every condition matches.
    ///
    /// The server only defers a named rule carrying at least one condition, so a rule without either is
    /// a malformed response and fails the evaluation rather than being read as a rule that matches
    /// everyone. Individual conditions are null-checked for the same reason: the declared types are
    /// non-nullable, but nothing enforces that on a deserialised payload.
    ///
    /// Conditions are combined with AND and stop at the first one that does not match, so a malformed
    /// condition behind a condition that already failed is never read — the rule has its answer without
    /// it.
    /// </summary>
    public bool Matches(ClientSideEvaluationContext context)
    {
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
