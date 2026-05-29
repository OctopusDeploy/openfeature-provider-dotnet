using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Establishes and maintains a cache of evaluated feature toggles to be used by the feature provider.
/// </summary>
class OctopusFeatureContextProvider(
    OctopusFeatureConfiguration configuration,
    IOctopusFeatureClient client,
    ILogger logger)
{
    readonly CancellationTokenSource cancellationTokenSource = new();

    OctopusFeatureContext currentContext = OctopusFeatureContext.Empty(configuration.LoggerFactory);
    Task? evaluationContextRefreshTask;
    bool initialized;

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
    /// This method will retry forever on failures, until a shutdown event triggers the cancellation token.
    /// We never want to cease trying to refresh the evaluation context while the provider is still alive,
    /// otherwise the state will be left stale whilst the consumer continues to make use it.
    /// </summary>
    async Task RefreshEvaluationContext(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(configuration.CacheDuration, cancellationToken);

                if (await client.HaveFeaturesChanged(currentContext.ContentHash, cancellationToken))
                {
                    var toggles = await client.GetFeatureToggleEvaluationManifest(cancellationToken);
                    if (toggles is not null)
                    {
                        currentContext = new OctopusFeatureContext(toggles, configuration.LoggerFactory);
                    }
                    else
                    {
                        logger.LogError("Failed to retrieve updated feature manifest. Retaining existing context which may be stale.");
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // OperationCanceledException during delay is ordinary cancellation behaviour. Ignore it and let the loop exit if IsCancellationRequested
            }
            catch (Exception e)
            {
                logger.LogError(e, "Failed to retrieve updated feature manifest. Retaining existing context which may be stale.");
            }
        }
    }

    public async ValueTask Shutdown()
    {
        cancellationTokenSource.Cancel();

        if (evaluationContextRefreshTask is not null)
        {
            await evaluationContextRefreshTask;
        }

        cancellationTokenSource.Dispose();
    }
}
