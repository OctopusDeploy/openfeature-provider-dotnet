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
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);
        
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;
        
        public ILoggerFactory LoggerFactory { get; set; } = NullLoggerFactory.Instance;

        public bool IsV3ClientIdentifierSupplied()
        {
            // A very basic test to see if we have a JWT-formatted client identifier
            var tokenSegments = ClientIdentifier.Split(".");
            return tokenSegments.Length == 3;
        }
    }
}
