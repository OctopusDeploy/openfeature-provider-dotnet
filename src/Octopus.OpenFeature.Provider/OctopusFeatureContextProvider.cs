using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider
{
    /// <summary>
    /// Establishes and maintains a cache of evaluated feature toggles to be used by the feature provider.
    /// </summary>
    internal class OctopusFeatureContextProvider(
        OctopusFeatureConfiguration configuration,
        IOctopusFeatureClient client,
        ILogger logger)
    {
        readonly CancellationTokenSource cancellationTokenSource = new();
            
        OctopusFeatureContext currentContext = OctopusFeatureContext.Empty(configuration.LoggerFactory);
        Task? evaluationContextRefreshTask;
        bool initialized;
        int retryAttempt;
        readonly TimeSpan retryDelay = TimeSpan.FromSeconds(5);

        public OctopusFeatureContext GetEvaluationContext()
        {
            return currentContext;
        }
            
        public async Task Initialize()
        {
            if (initialized)
            {
                return;
            }

            try
            {
                var toggles = await client.GetFeatureToggleEvaluationManifest(cancellationTokenSource.Token);
                currentContext =
                    toggles is not null
                        ? new OctopusFeatureContext(toggles, configuration.LoggerFactory)
                        : OctopusFeatureContext.Empty(configuration.LoggerFactory);
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to retrieve feature manifest during initialization. Falling back to empty context, defaults will be used during evaluation.");
                currentContext = OctopusFeatureContext.Empty(configuration.LoggerFactory);
            }
                
            evaluationContextRefreshTask = RefreshEvaluationContext(cancellationTokenSource.Token);
            initialized = true;
        }

        /// <summary>
        /// Unlike the check and get feature manifest methods, this method will retry forever on failures,
        /// until a shutdown event triggers the cancellation token.
        /// We never want to cease trying to refresh the evaluation context if we think we have to, as if we do,
        /// the state will be left stale whilst the consumer continues to make use it.
        /// </summary>
        async Task RefreshEvaluationContext(CancellationToken cancellationToken)
        {
            var delay = configuration.CacheDuration;
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(delay, cancellationToken);

                    if (await client.HaveFeaturesChanged(currentContext.ContentHash, cancellationToken))
                    {
                        var toggles = await client.GetFeatureToggleEvaluationManifest(cancellationToken);
                        currentContext =
                            toggles is not null
                                ? new OctopusFeatureContext(toggles, configuration.LoggerFactory)
                                : OctopusFeatureContext.Empty(configuration.LoggerFactory);
                    }

                    delay = configuration.CacheDuration;
                    retryAttempt = 0;
                }
                catch (OperationCanceledException)
                {
                    // OperationCanceledException during delay is ordinary cancellation behaviour. Ignore it and let the loop exit if IsCancellationRequested
                }
                catch (Exception e)
                {
                    logger.LogError(e, "{FailedMessage}, attempt {RetryAttempt}. Trying again after {Delay}...", "Failed to retrieve feature manifest", retryAttempt, delay);
                    delay = retryDelay;
                    retryAttempt++;
                }
            }
        }
            
        public async ValueTask Shutdown()
        {
            await cancellationTokenSource.CancelAsync();

            if (evaluationContextRefreshTask is not null)
            {
                await evaluationContextRefreshTask;
            }

            cancellationTokenSource.Dispose();
        }
    }
}
