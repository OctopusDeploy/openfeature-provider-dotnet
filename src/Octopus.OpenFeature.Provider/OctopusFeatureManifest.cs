namespace Octopus.OpenFeature.Provider
{
    public class OctopusFeatureManifest(string projectId, OctopusFeature[] features)
    {
        public static OctopusFeatureManifest Empty(string projectId) => new(projectId, Array.Empty<OctopusFeature>());
        
        public string ProjectId { get; set; } = projectId;

        public OctopusFeature[] Features { get; set; } = features;

        public byte[] ContentHash { get; set; } = [];
    }
    
    public class OctopusFeature(string id, string spaceId, string projectId, string name, string slug, OctopusToggleDefinition[] toggles)
    {
        public string Id { get; set; } = id;

        public string SpaceId { get; set; } = spaceId;

        public string ProjectId { get; set; } = projectId;

        public string Name { get; set; } = name;
        
        public string Slug { get; set; } = slug;

        public OctopusToggleDefinition[] Toggles { get; set; } = toggles;
    }
    
    public class OctopusToggleDefinition(string featureId, string deploymentEnvironmentId, bool isEnabled, string toggleStrategy, string tenantTargetingStrategy)
    {
        public string FeatureId { get; set; } = featureId;

        public string DeploymentEnvironmentId { get; set; } = deploymentEnvironmentId;

        public bool IsEnabled { get; set; } = isEnabled;

        public string ToggleStrategy { get; set; } = toggleStrategy;
        
        public string TenantTargetingStrategy { get; set; } = tenantTargetingStrategy;

        public int? RolloutPercentage { get; set; }

        public string[] Tenants { get; set; } = [];

        public string[] Segments { get; set; } = [];
    }
}
