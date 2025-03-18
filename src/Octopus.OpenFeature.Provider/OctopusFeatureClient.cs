using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider
{
    public record FeatureToggles(FeatureToggleEvaluation[] Evaluations, byte[] ContentHash);
    public record FeatureToggleEvaluation(string Name, string Slug, bool IsEnabled, KeyValuePair<string, string>[] Segments);

    interface IOctopusFeatureClient
    {
        Task<bool> HaveFeaturesChanged(OctopusFeatureContext? currentContext, CancellationToken cancellationToken);

        /// <summary>
        /// Retrieves the evaluated feature set from OctoToggle for a given installation and project.
        /// This method will return null if:
        /// - Toggles are not found for the installation and id
        /// - We don't receive a ContentHash header
        /// - We cannot deserialize the content response into a OctoToggleFeatureManifest
        /// </summary>
        Task<FeatureToggles?> GetFeatureToggleEvaluationManifest(CancellationToken cancellationToken);
    }

    class OctopusFeatureClient(OctopusFeatureConfiguration configuration, ILogger logger) : IOctopusFeatureClient
    {
        public async Task<bool> HaveFeaturesChanged(OctopusFeatureContext? currentContext, CancellationToken cancellationToken)
        {
            if (currentContext is null || currentContext.ContentHash.Length == 0)
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
            FeatureCheck? hash;
            if (configuration.IsV3ClientIdentifierSupplied())
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration.ClientIdentifier}");
            
                hash = await ExecuteWithRetry(async ct => await client.GetFromJsonAsync<FeatureCheck>("api/featuretoggles/check/v3/", ct), cancellationToken);
            }
            else
            {
                hash = await ExecuteWithRetry(async ct => await client.GetFromJsonAsync<FeatureCheck>($"api/featuretoggles/{configuration.ClientIdentifier}/check", ct), cancellationToken);
            }
            
            if (hash is null)
            {
                logger.LogWarning("Failed to retrieve feature toggles after 3 retries. Previously retrieved feature toggle values will continue to be used.");
                return false;
            }

            var haveFeaturesChanged = !hash.ContentHash.SequenceEqual(currentContext.ContentHash);

            return haveFeaturesChanged;
        }

        record FeatureCheck(byte[] ContentHash);

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
            var evaluations = await response.Content.ReadFromJsonAsync<FeatureToggleEvaluation[]>(cancellationToken);
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
}
