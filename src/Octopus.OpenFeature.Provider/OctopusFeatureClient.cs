using System.Net;
using System.Net.Http.Json;

namespace Octopus.OpenFeature.Provider
{
    public class OctopusFeatureClient
    {
        DateTimeOffset? lastRefreshed;
        OctopusFeatureContext? currentContext;

        readonly SemaphoreSlim cacheSemaphore = new(1, 1);

        readonly OctopusFeatureConfiguration configuration;
        readonly string installationId;
        readonly string projectId;

        public OctopusFeatureClient(OctopusFeatureConfiguration configuration)
        {
            this.configuration = configuration;
            var components = configuration.ClientIdentifier.Split(":");
            installationId = components[0];
            projectId = components[1];
        }

        public async Task<OctopusFeatureContext?> GetEvaluationContext(CancellationToken cancellationToken)
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
                                ? new OctopusFeatureContext(toggles, configuration)
                                : new OctopusFeatureContext(OctopusFeatureManifest.Empty(projectId), configuration);
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

            var hash = await ExecuteWithRetry(async ct => await client.GetFromJsonAsync<FeatureCheck>($"api/installation/{installationId}/project/{projectId}/check", ct), cancellationToken);
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
        async Task<OctopusFeatureManifest?> GetFeatureManifest(CancellationToken cancellationToken)
        {
            var client = new HttpClient
            {
                BaseAddress = configuration.ServerUri
            };

            var response = await ExecuteWithRetry(async ct => await client.GetAsync($"api/installation/{installationId}/project/{projectId}", ct), cancellationToken);

            if (response is null or { StatusCode: HttpStatusCode.NotFound })
            {
                // TODO: Logging
                return null;
            }
            
            var rawContentHash = response.Headers.GetValues("ContentHash").FirstOrDefault();
            if (rawContentHash is null)
            {
                // TODO: Logging
                return null;
            }

            var toggles = await response.Content.ReadFromJsonAsync<OctopusFeatureManifest>(cancellationToken);
            if (toggles is null)
            {
                // TODO: Logging
                return null;
            }

            toggles.ContentHash = Convert.FromBase64String(rawContentHash);
#pragma warning disable OCT1015
            lastRefreshed = DateTimeOffset.Now;
#pragma warning restore OCT1015

            return toggles;
        }

        static async Task<T?> ExecuteWithRetry<T>(Func<CancellationToken, Task<T>> callback, CancellationToken cancellationToken)
        {
            var attempts = 0;
            while (attempts < 3)
            {
                try
                {
                    return await callback(cancellationToken);
                }
                catch (Exception)
                {
                    attempts++;
                    await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempts)), cancellationToken);
                    // TODO: Logging
                }
            }

            return default;
        }
    }
}
