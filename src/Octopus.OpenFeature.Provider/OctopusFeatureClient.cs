using System.Net;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Octopus.OpenFeature.Provider;

public class FeatureToggles(FeatureToggleEvaluation[] evaluations, byte[] contentHash)
{
    public FeatureToggleEvaluation[] Evaluations { get; } = evaluations;
    
    public byte[] ContentHash { get; } = contentHash;
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
    Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken);
    Task<FeatureToggles?> GetFeatureToggleEvaluationManifest(CancellationToken cancellationToken);
}

/// <summary>
/// Responsible for retrieving feature toggles from OctoToggle and determining if they have changed.
/// </summary>
class OctopusFeatureClient(OctopusFeatureConfiguration configuration, ILogger logger) : IOctopusFeatureClient
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

        // WARNING: v2 and v3 check endpoints have identical response contracts.
        // If for any reason the v3 endpoint response contract starts to diverge from the v2 contract,
        // This code will need to update accordingly
        FeatureCheck? hash = null;
        if (configuration.IsV3ClientIdentifierSupplied())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ClientIdentifier}");

            var result = await ExecuteWithRetry(async ct => await client.GetAsync("api/featuretoggles/check/v3/", ct), cancellationToken);

            if (result is not null && result.IsSuccessStatusCode)
            {
                var rawResult = await result.Content.ReadAsStringAsync();
                
                hash = JsonSerializer.Deserialize<FeatureCheck>(rawResult, JsonSerializerOptions.Web);
            }
        }
        else
        {
            var result = await ExecuteWithRetry(async ct => await client.GetAsync($"api/featuretoggles/{configuration.ClientIdentifier}/check", ct), cancellationToken);
            
            if (result is not null && result.IsSuccessStatusCode)
            {
                var rawResult = await result.Content.ReadAsStringAsync();
            
                hash = JsonSerializer.Deserialize<FeatureCheck>(rawResult, JsonSerializerOptions.Web);
            }
        }
            
        if (hash is null)
        {
            logger.LogWarning("Failed to retrieve feature toggles after 3 retries. Previously retrieved feature toggle values will continue to be used.");
            return false;
        }

        var haveFeaturesChanged = !hash.ContentHash.SequenceEqual(contentHash);

        return haveFeaturesChanged;
    }

    class FeatureCheck(byte[] contentHash)
    {
        public byte[] ContentHash { get; } = contentHash;
    }

    /// <summary>
    /// Retrieves the evaluated feature set from OctoToggle for a given installation and project.
    /// This method will return null if:
    /// - Toggles are not found for the installation and id
    /// - We don't receive a ContentHash header
    /// - We cannot deserialize the content response into a OctoToggleFeatureManifest
    /// </summary>
    public async Task<FeatureToggles?> GetFeatureToggleEvaluationManifest(CancellationToken cancellationToken)
    {
        var client = new HttpClient
        {
            BaseAddress = configuration.ServerUri
        };

        HttpResponseMessage? response;
        if (configuration.IsV3ClientIdentifierSupplied())
        {
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ClientIdentifier}");
            
            response = await ExecuteWithRetry(async ct => await client.GetAsync("api/featuretoggles/v3/", ct), cancellationToken);
        }
        else
        {
            response = await ExecuteWithRetry(async ct => await client.GetAsync($"api/featuretoggles/v2/{configuration.ClientIdentifier}", ct), cancellationToken);
        }
           
        if (response is null or { StatusCode: HttpStatusCode.NotFound })
        {
            logger.LogWarning("Failed to retrieve feature toggles for client identifier {ClientIdentifier} from {OctoToggleUrl}", configuration.ClientIdentifier, configuration.ServerUri);
            return null;
        }
            
        var rawContentHash = response.Headers.GetValues("ContentHash").FirstOrDefault();
        if (rawContentHash is null)
        {
            logger.LogWarning("Feature toggle response from {OctoToggleUrl} did not contain expected ContentHash header.", configuration.ServerUri);
            return null;
        }

        // WARNING: v2 and v3 endpoints have identical response contracts.
        // If for any reason the v3 endpoint response contract starts to diverge from the v2 contract,
        // This code will need to update accordingly
        var result = await response.Content.ReadAsStringAsync();
        
        var evaluations = JsonSerializer.Deserialize<FeatureToggleEvaluation[]>(result, JsonSerializerOptions.Web);
        
        if (evaluations is null)
        {
            logger.LogWarning("Feature toggle response content from {OctoToggleUrl} was empty.", configuration.ServerUri);
            return null;
        }

        var toggles = new FeatureToggles(evaluations, Convert.FromBase64String(rawContentHash));

        return toggles;
    }
        
    async Task<T?> ExecuteWithRetry<T>(Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken)
    {
        var attempts = 0;
        while (attempts < 3)
        {
            try
            {
                return await callback(cancellationToken);
            }
            catch (Exception e)
            {
                attempts++;
                await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts)), cancellationToken);
                logger.LogTrace(e, "Error occurred retrieving feature toggles from {OctoToggleUrl}. Retrying (attempt {attempt} out of 3).", configuration.ServerUri, attempts);
            }
        }

        return default;
    }
}