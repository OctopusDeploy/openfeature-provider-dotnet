using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Octopus.OpenFeature.Provider;

public class OctopusFeatureConfiguration
{
    const string DefaultServerUri = "https://features.octopus.com";

    public OctopusFeatureConfiguration(string clientIdentifier, ProductMetadata productMetadata)
    {
        ClientIdentifier = clientIdentifier;
        ProductMetadata = productMetadata;
        var serverUri = Environment.GetEnvironmentVariable("OctoToggle__Url");
        ServerUri = serverUri is not null ? new Uri(serverUri) : new Uri(DefaultServerUri);
    }

    public Uri ServerUri { get; private set; }

    /// <summary>
    /// Overrides the application release version embedded in the ClientIdentifier
    /// </summary>
    public string? ReleaseVersionOverride { get; set; }

    /// <summary>
    /// The ClientIdentifier provided by the Octopus variable Octopus.FeatureToggles.ClientIdentifier
    /// </summary>
    public string ClientIdentifier { get; set; }

    /// <summary>
    /// Metadata about the product using the OpenFeature provider
    /// </summary>
    public ProductMetadata ProductMetadata { get; set; }

    /// <summary>
    /// The amount of time between checks to see if new feature toggles are available
    /// The cache will be refreshed if new feature toggles are available
    /// </summary>
    public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);

    public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

    public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
}
