using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider
{
    public record FeatureToggles(FeatureToggleEvaluation[] Evaluations, byte[] ContentHash);
    public record FeatureToggleEvaluation(string Name, string Slug, bool IsEnabled, KeyValuePair<string, string>[] Segments);
    
    public class OctopusFeatureClient(OctopusFeatureConfiguration configuration)
    {
        DateTimeOffset? lastRefreshed;
        OctopusFeatureContext currentContext = OctopusFeatureContext.Empty(configuration.LoggerFactory);
        readonly SemaphoreSlim cacheSemaphore = new(1, 1); 
        ILogger logger = configuration.LoggerFactory.CreateLogger<OctopusFeatureClient>();

        public async Task<OctopusFeatureContext> GetEvaluationContext(CancellationToken cancellationToken)
        {
            if (await HaveFeaturesChanged(cancellationToken))
            {
                await cacheSemaphore.WaitAsync(cancellationToken);
                try
                {
                    if (await HaveFeaturesChanged(cancellationToken))
                    {
                        var toggles = await GetFeatureManifest(cancellationToken);
                        currentContext =
                            toggles is not null
                                ? new OctopusFeatureContext(toggles, configuration.LoggerFactory)
                                : OctopusFeatureContext.Empty(configuration.LoggerFactory);
                    }
                }
                finally
                {
                    cacheSemaphore.Release();
                }
            }

            return currentContext;
        }

        async Task<bool> HaveFeaturesChanged(CancellationToken cancellationToken)
        {
            if (currentContext is null || currentContext.ContentHash.Length == 0)
            {
                return true;
            }
            
#pragma warning disable OCT1015
            if (DateTime.Now < lastRefreshed + configuration.CacheDuration)
#pragma warning restore OCT1015
            {
                return false;
            }

            var client = new HttpClient
            {
                BaseAddress = configuration.ServerUri
            };

            var hash = await ExecuteWithRetry(async ct => await client.GetFromJsonAsync<FeatureCheck>($"api/featuretoggles/{configuration.ClientIdentifier}/check", ct), cancellationToken);
            if (hash is null)
            {
                return true;
            }

            var haveFeaturesChanged = !hash.ContentHash.SequenceEqual(currentContext.ContentHash);

            // Extend the cache duration if the features have not changed
            if (!haveFeaturesChanged)
            {
#pragma warning disable OCT1015
                lastRefreshed = DateTimeOffset.Now;
#pragma warning restore OCT1015
            }

            return haveFeaturesChanged;
        }

        record FeatureCheck(byte[] ContentHash);

        /// <summary>
        /// Retrieves the feature manifest from OctoToggle for a given installation and project.
        /// This method will return null if:
        /// - Toggles are not found for the installation and id
        /// - We don't receive a ContentHash header
        /// - We cannot deserialize the content response into a OctoToggleFeatureManifest
        /// </summary>
        async Task<FeatureToggles?> GetFeatureManifest(CancellationToken cancellationToken)
        {
            var client = new HttpClient
            {
                BaseAddress = configuration.ServerUri
            };

            var response = await ExecuteWithRetry(async ct => await client.GetAsync($"api/featuretoggles/v2/{configuration.ClientIdentifier}", ct), cancellationToken);

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

            var evaluations = await response.Content.ReadFromJsonAsync<FeatureToggleEvaluation[]>(cancellationToken);
            if (evaluations is null)
            {
                logger.LogWarning("Feature toggle response content from {OctoToggleUrl} was empty.", configuration.ServerUri);
                return null;
            }

            var toggles = new FeatureToggles(evaluations, Convert.FromBase64String(rawContentHash));

#pragma warning disable OCT1015
            lastRefreshed = DateTimeOffset.Now;
#pragma warning restore OCT1015

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
                    logger.LogError(e, "Error occurred retrieving feature toggles from {OctoToggleUrl}. Retrying (attempt {attempt} out of 3).", configuration.ServerUri, attempts);
                }
            }

            return default;
        }
    }
}
