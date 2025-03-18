using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider
{
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
        readonly TimeSpan delay = TimeSpan.FromSeconds(5);

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
                
            await BuildAndCacheEvaluationContext(cancellationTokenSource.Token);
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
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (await client.HaveFeaturesChanged(currentContext, cancellationToken))
                    {
                        await BuildAndCacheEvaluationContext(cancellationToken);
                    }

                    retryAttempt = 0;
                    await Task.Delay(configuration.CacheDuration, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // OperationCanceledException during delay is ordinary cancellation behaviour. Ignore it and let the loop exit if IsCancellationRequested
                }
                catch (Exception e)
                {
                    logger.LogError(e, "{FailedMessage}, attempt {RetryAttempt}. Trying again after {Delay}...", "Failed to retrieve feature manifest", retryAttempt, delay);
                    await Task.Delay(delay, cancellationToken);
                    retryAttempt++;
                }
            }
        }
            
        async Task BuildAndCacheEvaluationContext(CancellationToken cancellationToken)
        {
            var toggles = await client.GetFeatureToggleEvaluationManifest(cancellationToken);
            currentContext =
                toggles is not null
                    ? new OctopusFeatureContext(toggles, configuration.LoggerFactory)
                    : OctopusFeatureContext.Empty(configuration.LoggerFactory);
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
