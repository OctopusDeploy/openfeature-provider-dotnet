namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Matches when the context attribute <see cref="Key"/> is not one of <see cref="Values"/>. A missing attribute
/// matches.
/// </summary>
internal sealed class ContextAttributeIsNotOneOfCondition : ClientSideCondition
{
    public ContextAttributeIsNotOneOfCondition(string key, string[] values)
    {
        Key = key;
        Values = values;
    }

    public string Key { get; }
    public string[] Values { get; }

    public override bool Matches(ClientSideEvaluationContext context)
        => !ContextAttributes.IsOneOf(context, Key, Values);
}
