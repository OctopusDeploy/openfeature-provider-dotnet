using System.Linq;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// A named rule the provider library still has to evaluate on the client side. The rule matches
/// when every one of its <see cref="Conditions"/> matches.
/// </summary>
internal sealed class ClientSideRuleResource
{
    public ClientSideRuleResource(string name, ClientSideConditionResource[] conditions)
    {
        Name = name;
        Conditions = conditions;
    }

    public string Name { get; }
    public ClientSideConditionResource[] Conditions { get; }

    /// <summary>
    /// Whether every condition matches. A rule with no conditions does not match: the server only
    /// defers rules carrying at least one, so an empty or missing set is a malformed response and must
    /// not turn a flag on. Individual conditions are null-checked for the same reason — the declared
    /// types are non-nullable, but nothing enforces that on a deserialised payload. Such a rule is
    /// rejected by <see cref="Validate"/> before evaluation, so this only has to stay total.
    /// </summary>
    public bool Matches(ClientSideEvaluationContext context)
        => Conditions is { Length: > 0 } conditions
           && conditions.All(condition => condition?.Matches(context) is true);

    /// <summary>
    /// Describes why this rule is not well-formed, or returns <c>null</c> when it is. The server only
    /// defers a named rule carrying at least one condition, so anything else is a malformed response.
    /// </summary>
    public string? Validate()
    {
        if (Name is null)
        {
            return "a rule has no name";
        }

        if (Conditions is not { Length: > 0 })
        {
            return $"rule '{Name}' has no conditions";
        }

        foreach (var condition in Conditions)
        {
            if (condition is null)
            {
                return $"rule '{Name}' has a missing condition";
            }

            if (condition.Validate() is { } problem)
            {
                return $"rule '{Name}' has {problem}";
            }
        }

        return null;
    }
}
