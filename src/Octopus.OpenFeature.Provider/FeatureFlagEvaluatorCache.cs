using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Establishes and maintains the <see cref="FeatureFlagEvaluator"/> the feature provider evaluates against,
/// refreshing it whenever the Feature Flags service reports the flags have changed.
/// </summary>
internal class FeatureFlagEvaluatorCache(
    OctopusFeatureConfiguration configuration,
    IFeatureFlagApiClient client,
    ILogger logger,
    Func<DateTimeOffset>? utcNow = null)
{
    readonly CancellationTokenSource cancellationTokenSource = new();
    readonly Func<DateTimeOffset> utcNow = utcNow ?? (() => DateTimeOffset.UtcNow);

    FeatureFlagEvaluator currentEvaluator = FeatureFlagEvaluator.Empty(configuration.LoggerFactory);
    Task? refreshTask;
    bool initialized;
    DateTimeOffset startedAt;

    /// <summary>
    /// When a manifest was last retrieved, or confirmed unchanged. Null until the first manifest arrives.
    /// </summary>
    DateTimeOffset? lastSuccessfulRefresh;
    bool refreshFailing;

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

        startedAt = utcNow();

        try
        {
            var evaluationResponse = await client.GetServerSideEvaluations(cancellationTokenSource.Token);
            if (evaluationResponse is not null)
            {
                currentEvaluator = new FeatureFlagEvaluator(evaluationResponse, configuration.LoggerFactory);
                lastSuccessfulRefresh = utcNow();
            }
            else
            {
                currentEvaluator = FeatureFlagEvaluator.Empty(configuration.LoggerFactory);
                refreshFailing = true;
            }
        }
        catch (Exception e)
        {
            logger.LogError(e, "Failed to retrieve feature manifest during initialization. Falling back to no evaluations, defaults will be used during evaluation.");
            currentEvaluator = FeatureFlagEvaluator.Empty(configuration.LoggerFactory);
            refreshFailing = true;
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
                        RecordSuccessfulRefresh();
                    }
                    else
                    {
                        ReportRefreshFailure(exception: null);
                    }
                }
                else
                {
                    RecordSuccessfulRefresh();
                }
            }
            catch (OperationCanceledException)
            {
                // OperationCanceledException during delay is ordinary cancellation behaviour. Ignore it and let the loop exit if IsCancellationRequested
            }
            catch (Exception e)
            {
                ReportRefreshFailure(e);
            }
        }
    }

    void RecordSuccessfulRefresh()
    {
        var now = utcNow();

        if (refreshFailing)
        {
            if (lastSuccessfulRefresh is { } previous)
            {
                logger.LogInformation(
                    "Feature manifest refresh recovered. Evaluations may have been stale for {TimeSinceLastRefresh}, since the last successful refresh at {LastSuccessfulRefresh}.",
                    now - previous,
                    previous);
            }
            else
            {
                logger.LogInformation(
                    "Feature manifest refresh recovered. No refresh had succeeded since the provider started {TimeSinceStart} ago, at {StartedAt}.",
                    now - startedAt,
                    startedAt);
            }

            refreshFailing = false;
        }

        lastSuccessfulRefresh = now;
    }

    void ReportRefreshFailure(Exception? exception)
    {
        var now = utcNow();

        if (lastSuccessfulRefresh is { } previous)
        {
            logger.Log(
                LogLevel.Error,
                exception,
                "Failed to retrieve updated feature manifest. Retaining the existing evaluations, which may be stale. The last successful refresh was {TimeSinceLastRefresh} ago, at {LastSuccessfulRefresh}.",
                now - previous,
                previous);
        }
        else
        {
            logger.Log(
                LogLevel.Error,
                exception,
                "Failed to retrieve updated feature manifest. No refresh has succeeded since the provider started {TimeSinceStart} ago, at {StartedAt}. Defaults are being used during evaluation.",
                now - startedAt,
                startedAt);
        }

        refreshFailing = true;
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
