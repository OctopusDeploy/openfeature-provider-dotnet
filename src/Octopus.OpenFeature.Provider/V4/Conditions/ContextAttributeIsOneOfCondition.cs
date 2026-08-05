namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Matches when the context attribute <see cref="Key"/> is one of <see cref="Values"/>. A missing
/// attribute is not one of them, so it does not match.
/// </summary>
internal sealed class ContextAttributeIsOneOfCondition : ClientSideCondition
{
    public ContextAttributeIsOneOfCondition(string key, string[] values)
    {
        Key = key;
        Values = values;
    }

    public string Key { get; }
    public string[] Values { get; }

    public override bool Matches(ClientSideEvaluationContext context)
        => ContextAttributes.IsOneOf(context, Key, Values);
}
