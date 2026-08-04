namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Discriminator values for the polymorphic v4 client-side conditions. These mirror the values in the
/// evaluation response, so they must not drift from the server.
/// </summary>
internal static class ConditionTypeNames
{
    public const string ContextAttributeIsNotOneOf = "context-attribute-is-not-one-of";
    public const string ContextAttributeIsOneOf = "context-attribute-is-one-of";
    public const string PercentageByContext = "percentage-by-context";
}
