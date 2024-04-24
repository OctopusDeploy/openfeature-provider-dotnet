namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureManifestBuilder
{
    private string? environmentId = null;
    private string[]? tenantIds = null;
    public int? percentage = null;
    public string[]? segments = null;
    private string? name = null;
        
    public OctopusFeatureManifestBuilder ForEnvironment(string environmentId)
    {
        this.environmentId = environmentId;
        return this;
    }

    public OctopusFeatureManifestBuilder ForTenants(string[] tenantIds)
    {
        this.tenantIds = tenantIds;
        return this;
    }

    public OctopusFeatureManifestBuilder ForPercentageOfTenants(int percentage)
    {
        this.percentage = percentage;
        return this;
    }

    public OctopusFeatureManifestBuilder ForSegments(string[] segments)
    {
        this.segments = segments;
        return this;
    }

    public OctopusFeatureManifestBuilder WithFeature(string name)
    {
        this.name = name;
        return this;
    }
        
    public OctopusFeatureManifest Build()
    {
        var toggleDefinition = new OctopusToggleDefinition(
            "feature-id",
            environmentId ?? "environment-1",
            true,
            "EntireEnvironment",
            "All");
            
        if (tenantIds is not null)
        {
            toggleDefinition = new OctopusToggleDefinition(
                "feature-id",
                environmentId ?? "environment-1",
                true,
                "Targeted",
                "SpecificTenants")
            {
                Tenants = tenantIds
            };

            if (segments is not null)
            {
                toggleDefinition.Segments = segments;
            }
        }

        if (percentage is not null)
        {
            toggleDefinition = new OctopusToggleDefinition(
                "feature-id",
                environmentId ?? "environment-1",
                true,
                "Targeted",
                "PercentageOfTenants")
            {
                RolloutPercentage = percentage
            };
                
            if (segments is not null)
            {
                toggleDefinition.Segments = segments;
            }
        }

        if (tenantIds is null && percentage is null && segments is not null)
        {
            toggleDefinition = new OctopusToggleDefinition(
                "feature-id",
                environmentId ?? "environment-1",
                true,
                "Targeted",
                "All")
            {
                Segments = segments
            };
        }
            
        var feature = new OctopusFeatureManifest("test-project-id",
        [
            new OctopusFeature(
                "feature-id", 
                "space-id", 
                "test-project-id", 
                name ?? "testfeature", 
                name ?? "testfeature",
                [toggleDefinition]
            )
        ]);

        return feature;
    }
}