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
    int retryAttempt;

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
            await FetchToggles(cancellationTokenSource.Token);
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
        var delay = configuration.CacheDuration;
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(configuration.CacheDuration, cancellationToken);
                await FetchToggles(cancellationToken);

                retryAttempt = 0;
            }
            catch (OperationCanceledException)
            {
                // OperationCanceledException during delay is ordinary cancellation behaviour. Ignore it and let the loop exit if IsCancellationRequested
            }
            catch (Exception e)
            {
                logger.LogError(e, "{FailedMessage}, attempt {RetryAttempt}. Trying again after {Delay}...", "Failed to retrieve feature manifest", retryAttempt, delay);
                retryAttempt++;
            }
        }
    }

    async Task FetchToggles(CancellationToken cancellationToken)
    {
        var toggles = await client.GetLatestManifest(currentContext.ETag, cancellationToken);

        // If the response is null, it means the toggles have not changed since the last request
        // of there was an error getting the latest toggles. Either way we want to keep using
        // what we already have.
        if (toggles is not null)
        {
            currentContext = new OctopusFeatureContext(toggles, configuration.LoggerFactory);
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
