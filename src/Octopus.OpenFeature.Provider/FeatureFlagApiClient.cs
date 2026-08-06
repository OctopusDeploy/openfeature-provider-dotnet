using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Octopus.OpenFeature.Provider.V4;

namespace Octopus.OpenFeature.Provider;

internal class EvaluationResponse(ServerSideEvaluation[] evaluations, byte[] contentHash)
{
    public ServerSideEvaluation[] Evaluations { get; } = evaluations;

    public byte[] ContentHash { get; } = contentHash;
}

internal interface IFeatureFlagApiClient
{
    Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken);
    Task<EvaluationResponse?> GetServerSideEvaluations(CancellationToken cancellationToken);
}

/// <summary>
/// Responsible for determining if feature flags have been modified and for retrieving their server-side evaluations.
/// </summary>
internal class FeatureFlagApiClient(OctopusFeatureConfiguration configuration, ILogger logger) : IFeatureFlagApiClient
{
    public async Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken)
    {
        if (contentHash.Length == 0)
        {
            return true;
        }

        var client = new HttpClient
        {
            BaseAddress = configuration.ServerUri
        };
        AddOctopusClientHeader(client);

        FeatureCheck? hash = null;
        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ClientIdentifier}");

        var result = await client.GetAsync("api/feature-flags/check/v4/", cancellationToken);

        if (result.IsSuccessStatusCode)
        {
            var rawResult = await result.Content.ReadAsStringAsync();

            hash = JsonSerializer.Deserialize<FeatureCheck>(rawResult, JsonSerializerOptions.Web);
        }

        if (hash is null)
        {
            throw new InvalidOperationException("Failed to retrieve feature flags for client identifier. Check did not return a valid content hash.");
        }

        var haveFeaturesChanged = !hash.ContentHash.SequenceEqual(contentHash);

        return haveFeaturesChanged;
    }

    public void AddOctopusClientHeader(HttpClient client)
    {
        var clientHeaderValueBuilder = new StringBuilder(configuration.ProductMetadata.Name);

        if (configuration.ProductMetadata.Version is not null)
        {
            clientHeaderValueBuilder.Append($"/{configuration.ProductMetadata.Version}");
        }

        clientHeaderValueBuilder.Append(
            $" openfeature-provider-dotnet/{typeof(FeatureFlagApiClient).Assembly.GetName().Version?.ToString(3)}"
        );

        client.DefaultRequestHeaders.Add("X-Octopus-Client", clientHeaderValueBuilder.ToString());
    }

    class FeatureCheck(byte[] contentHash)
    {
        public byte[] ContentHash { get; } = contentHash;
    }

    /// <summary>
    /// Retrieves the server-side evaluated feature flags for a given installation and project.
    /// This method will return null if:
    /// - Flags are not found for the installation and id
    /// - We don't receive a ContentHash header
    /// - We cannot deserialize the content response
    /// </summary>
    public async Task<EvaluationResponse?> GetServerSideEvaluations(CancellationToken cancellationToken)
    {
        var client = new HttpClient
        {
            BaseAddress = configuration.ServerUri
        };
        AddOctopusClientHeader(client);

        if (configuration.ReleaseVersionOverride is not null)
        {
            client.DefaultRequestHeaders.Add(OctopusHttpHeaderNames.ReleaseVersion, configuration.ReleaseVersionOverride);
        }

        client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ClientIdentifier}");

        var response = await client.GetAsync("api/feature-flags/evaluations/v4/", cancellationToken);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            logger.LogWarning("Failed to retrieve feature flags for client identifier {ClientIdentifier} from {OctoToggleUrl}", configuration.ClientIdentifier, configuration.ServerUri);
            return null;
        }

        if (!response.Headers.TryGetValues("ContentHash", out IEnumerable<string> values))
        {
            logger.LogWarning("Feature flag response from {OctoToggleUrl} did not contain expected ContentHash header", configuration.ServerUri);
            return null;
        }

        var headerValues = values.ToArray();
        if (!headerValues.Any())
        {
            logger.LogWarning("Feature flag response from {OctoToggleUrl} returned an empty ContentHash header", configuration.ServerUri);
            return null;
        }

        var rawContentHash = headerValues.First();

        var result = await response.Content.ReadAsStringAsync();

        var evaluations = JsonSerializer.Deserialize<ServerSideEvaluation[]>(result, JsonSerializerOptions.Web);

        if (evaluations is null)
        {
            logger.LogWarning("Feature flag response content from {OctoToggleUrl} was empty", configuration.ServerUri);
            return null;
        }

        var evaluationResponse = new EvaluationResponse(evaluations, Convert.FromBase64String(rawContentHash));

        return evaluationResponse;
    }
}
