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
}
