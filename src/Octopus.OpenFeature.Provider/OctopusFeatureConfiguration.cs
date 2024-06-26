using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Octopus.OpenFeature.Provider
{
    public class OctopusFeatureConfiguration
    {
        const string DefaultServerUri = "https://features.octopus.com";
        
        public OctopusFeatureConfiguration(string clientIdentifier)
        {
            ClientIdentifier = clientIdentifier;
            var serverUri = Environment.GetEnvironmentVariable("OctoToggle__Url");
            ServerUri = serverUri is not null ? new Uri(serverUri) : new Uri(DefaultServerUri);
        }
        
        public Uri ServerUri { get; private set; } 

        /// <summary>
        /// The ClientIdentifier provided by the Octopus variable Octopus.FeatureToggles.ClientIdentifier
        /// </summary>
        public string ClientIdentifier { get; set; } 
        
        /// <summary>
        /// The amount of time between checks to see if new feature toggles are available
        /// The cache will be refreshed if new feature toggles are available
        /// </summary>
        public TimeSpan CacheRefreshInterval { get; set; } = TimeSpan.FromMinutes(1);
        
        /// <summary>
        /// The amount of time feature toggle cache will be considered valid
        /// If feature toggles cannot be retrieved for any reason, the cached set will continue to be used up to this
        /// amount of time.
        /// </summary>
        public TimeSpan CacheExpiry { get; set; } = TimeSpan.FromHours(24);
        
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        // TODO: Consumption security
        public string? ApiKey { get; set; }
        
        public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;
    }
}
