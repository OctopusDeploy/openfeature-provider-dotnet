using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Establishes and maintains the <see cref="FeatureFlagEvaluator"/> the feature provider evaluates against,
/// refreshing it whenever the Feature Flags service reports the flags have changed.
/// </summary>
internal class FeatureFlagEvaluatorCache(
    OctopusFeatureConfiguration configuration,
    IFeatureFlagApiClient client,
    ILogger logger)
{
    readonly CancellationTokenSource cancellationTokenSource = new();

    FeatureFlagEvaluator currentEvaluator = FeatureFlagEvaluator.Empty(configuration.LoggerFactory);
    Task? refreshTask;
    bool initialized;

    public FeatureFlagEvaluator GetEvaluator()
    {
        return currentEvaluator;
    }

    public async Task Initialize()
    {
        if (initialized)
        {
            return;
        }

        try
        {
            var evaluationResponse = await client.GetServerSideEvaluations(cancellationTokenSource.Token);
            currentEvaluator =
                evaluationResponse is not null
                    ? new FeatureFlagEvaluator(evaluationResponse, configuration.LoggerFactory)
                    : FeatureFlagEvaluator.Empty(configuration.LoggerFactory);
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to retrieve feature manifest during initialization. Falling back to no evaluations, defaults will be used during evaluation.");
            currentEvaluator = FeatureFlagEvaluator.Empty(configuration.LoggerFactory);
        }

        refreshTask = RefreshEvaluator(cancellationTokenSource.Token);
        initialized = true;
    }

    /// <summary>
    /// This method will retry forever on failures, until a shutdown event triggers the cancellation token.
    /// We never want to cease trying to refresh the evaluator while the provider is still alive,
    /// otherwise the state will be left stale whilst the consumer continues to make use it.
    /// </summary>
    async Task RefreshEvaluator(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(configuration.CacheDuration, cancellationToken);

                if (await client.HaveFeaturesChanged(currentEvaluator.ContentHash, cancellationToken))
                {
                    var evaluationResponse = await client.GetServerSideEvaluations(cancellationToken);
                    if (evaluationResponse is not null)
                    {
                        currentEvaluator = new FeatureFlagEvaluator(evaluationResponse, configuration.LoggerFactory);
                    }
                    else
                    {
                        logger.LogError("Failed to retrieve updated feature manifest. Retaining the existing evaluations, which may be stale.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // OperationCanceledException during delay is ordinary cancellation behaviour. Ignore it and let the loop exit if IsCancellationRequested
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to retrieve updated feature manifest. Retaining the existing evaluations, which may be stale.");
            }
        }
    }

    public async ValueTask Shutdown()
    {
        cancellationTokenSource.Cancel();

        if (refreshTask is not null)
        {
            await refreshTask;
        }

        cancellationTokenSource.Dispose();
    }
}
