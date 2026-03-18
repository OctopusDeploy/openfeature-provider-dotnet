using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider;

class FeatureToggles(FeatureToggleEvaluation[] evaluations, EntityTagHeaderValue? eTag)
{
    internal FeatureToggleEvaluation[] Evaluations { get; } = evaluations;

    internal EntityTagHeaderValue? ETag { get; } = eTag;
}

public class FeatureToggleEvaluation(string name, string slug, bool isEnabled, KeyValuePair<string, string>[] segments)
{
    public string Name { get; } = name;

    public string Slug { get; } = slug;

    public bool IsEnabled { get; } = isEnabled;

    public KeyValuePair<string, string>[] Segments { get; } = segments;
}

interface IOctopusFeatureClient
{
    Task<FeatureToggles?> GetLatestManifest(EntityTagHeaderValue? eTag, CancellationToken cancellationToken);
}

/// <summary>
/// Responsible for retrieving feature toggles from OctoToggle and determining if they have changed.
/// </summary>
class OctopusFeatureClient(OctopusFeatureConfiguration configuration, ILogger logger) : IOctopusFeatureClient
{
    /// <summary>
    /// Retrieves the evaluated feature set from OctoToggle for a given installation and project.
    /// This method will return null if:
    /// - The toggles have not changed since the last request.
    /// - Toggles are not found for the installation and id.
    /// - We cannot deserialize the content response into a OctoToggleFeatureManifest.
    /// </summary>
    public async Task<FeatureToggles?> GetLatestManifest(EntityTagHeaderValue? eTag, CancellationToken cancellationToken)
    {
        var client = new HttpClient
        {
            BaseAddress = configuration.ServerUri
        };
        client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue(configuration.ProductMetadata.ProductHeaderValue));

        if (configuration.ReleaseVersionOverride is not null)
        {
            client.DefaultRequestHeaders.Add(OctopusHttpHeaderNames.ReleaseVersion, configuration.ReleaseVersionOverride);
        }

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ClientIdentifier}");

        if (eTag is not null)
        {
            client.DefaultRequestHeaders.IfNoneMatch.Add(eTag);
        }

        var response = await client.GetAsync("api/featuretoggles/v3/", cancellationToken);

        if (response is null or { StatusCode: HttpStatusCode.NotFound })
        {
            logger.LogWarning("Failed to retrieve feature toggles for client identifier {ClientIdentifier} from {OctoToggleUrl}", configuration.ClientIdentifier, configuration.ServerUri);
            return null;
        }

        if (response.StatusCode == HttpStatusCode.NotModified)
        {
            return null;
        }

        var result = await response.Content.ReadAsStreamAsync();

        var evaluations = JsonSerializer.Deserialize<FeatureToggleEvaluation[]>(result, JsonSerializerOptions.Web);

        if (evaluations is null)
        {
            logger.LogWarning("Feature toggle response content from {OctoToggleUrl} was empty", configuration.ServerUri);
            return null;
        }

        return new FeatureToggles(evaluations, response.Headers.ETag);
    }
}
