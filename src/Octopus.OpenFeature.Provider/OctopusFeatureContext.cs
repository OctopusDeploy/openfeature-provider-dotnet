using System.Text;
using Murmur;

namespace Octopus.OpenFeature.Provider;

public class OctopusFeatureContext
{
    readonly OctopusFeatureManifest toggles;
    readonly string environmentId;
    readonly string? tenantId;

    public OctopusFeatureContext(OctopusFeatureManifest toggles, OctopusFeatureConfiguration configuration)
    {
        this.toggles = toggles;
        
        var components = configuration.ClientIdentifier.Split(":");
        environmentId = components[2];

        if (components.Length == 4)
        {
            tenantId = components[3];
        }
    }

    public byte[] ContentHash => toggles.ContentHash;
    
    public bool Evaluate(string slug, string? segment)
    {
        var feature = toggles.Features.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.InvariantCultureIgnoreCase));

        if (feature == null) return false;

        return Evaluate(feature, segment);
    }

    bool Evaluate(OctopusFeature feature, string? segment = null)
    {
        var toggle = feature.Toggles.FirstOrDefault(x => x.DeploymentEnvironmentId == environmentId);

        if (toggle is null)
        {
            return false;
        }

        if (toggle.ToggleStrategy == "EntireEnvironment")
        {
            return toggle.IsEnabled;
        }

        if (toggle.ToggleStrategy == "Targeted")
        {
            var matchesTenant =
                toggle.TenantTargetingStrategy switch
                {
                    "All" => true,
                    "SpecificTenants" => toggle.Tenants.Any(x => x == tenantId),
                    "PercentageOfTenants" => tenantId is not null && GetNormalizedNumber(tenantId) < toggle.RolloutPercentage,
                    _ => throw new ArgumentOutOfRangeException()
                };
                
            var matchesSegment = toggle.Segments.Any(x => x == segment);

            var result = toggle.IsEnabled && matchesTenant;

            if (toggle.Segments.Any())
            {
                result &= matchesSegment;
            }

            return result;
        }

        return false;
    }

    /// <summary>
    /// Produces a normalized number between 0 and 100 for a given TenantId, with less than 1% variance (TODO: Tests)
    /// </summary>
    int GetNormalizedNumber(string featureName)
    {
        const int one = 1;
        const string separator = ":";

        var bytes = Encoding.UTF8.GetBytes(string.Concat(featureName, separator, tenantId));

        using var algorithm = MurmurHash.Create32();
        var hash = algorithm.ComputeHash(bytes);
        var value = BitConverter.ToUInt32(hash, 0);
        return (int)(value % 100 + one);
    }
}
