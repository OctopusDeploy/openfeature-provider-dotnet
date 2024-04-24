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

        // TODO: Validate clientIdentifier in constructor

        /// <summary>
        /// Client identifier is a string in the format {installationId}:{projectId}:{environmentId}:{tenantId?}
        /// </summary>
        public string ClientIdentifier { get; set; } 
        
        public TimeSpan CacheDuration { get; set; } = TimeSpan.FromMinutes(1);
        
        public CancellationToken CancellationToken { get; set; } = CancellationToken.None;

        // TODO: Consumption security
        public string? ApiKey { get; set; }
    }
}
