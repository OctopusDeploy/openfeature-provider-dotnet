namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Discriminator values for the polymorphic v4 rule conditions. These mirror the values OctoToggle
/// writes into the evaluation response, so they must not drift from the server.
///
/// Only the client-side conditions are ever returned to a provider library; the server-side
/// conditions are resolved by OctoToggle before the response is produced and are listed here purely
/// to document the full vocabulary.
/// </summary>
internal static class ConditionTypeNames
{
    // Client-side conditions: returned to provider libraries for client-side evaluation.
    public const string ContextAttributeIsNotOneOf = "context-attribute-is-not-one-of";
    public const string ContextAttributeIsOneOf = "context-attribute-is-one-of";
    public const string PercentageByContext = "percentage-by-context";

    // Server-side conditions: resolved by OctoToggle and never sent to a provider library.
    public const string PercentageByTenant = "percentage-by-tenant";
    public const string TenantDoesNotHaveTag = "tenant-does-not-have-tag";
    public const string TenantHasTag = "tenant-has-tag";
    public const string TenantIsNotOneOf = "tenant-is-not-one-of";
    public const string TenantIsOneOf = "tenant-is-one-of";
    public const string VersionIsAtLeast = "version-is-at-least";
}
