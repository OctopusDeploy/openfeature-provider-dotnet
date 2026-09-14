using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider;

public class OctopusFeatureProvider : FeatureProvider
{
    readonly FeatureFlagEvaluatorCache evaluatorCache;

    public OctopusFeatureProvider(OctopusFeatureConfiguration configuration)
        : this(configuration, new FeatureFlagApiClient(configuration, configuration.LoggerFactory.CreateLogger<OctopusFeatureProvider>()))
    {
    }

    // Allows us to pass in a fake IFeatureFlagApiClient for testing purposes.
    internal OctopusFeatureProvider(OctopusFeatureConfiguration configuration, IFeatureFlagApiClient client)
    {
        var logger = configuration.LoggerFactory.CreateLogger<OctopusFeatureProvider>();
        evaluatorCache = new FeatureFlagEvaluatorCache(configuration, client, logger);

        evaluatorCache.RefreshFailed += () => Emit(
            ProviderEventTypes.ProviderStale,
            "Failed to refresh the feature manifest. Evaluations are served from the last manifest retrieved, which may be stale.");
        evaluatorCache.RefreshRecovered += () => Emit(
            ProviderEventTypes.ProviderReady,
            "The feature manifest refresh has recovered.");
    }

    void Emit(ProviderEventTypes type, string message)
    {
        // The channel is bounded and only drained once the provider is registered with the SDK.
        // Drop the event rather than block the refresh loop when nothing is listening.
        EventChannel.Writer.TryWrite(new ProviderEventPayload
        {
            Type = type,
            ProviderName = GetMetadata().Name,
            Message = message
        });
    }

    public override Metadata GetMetadata()
    {
        return new Metadata("octopus-dotnet-provider");
    }

    public override async Task InitializeAsync(EvaluationContext context, CancellationToken cancellationToken = new())
    {
        await base.InitializeAsync(context, cancellationToken);
        await evaluatorCache.Initialize();
    }

    public override async Task ShutdownAsync(CancellationToken cancellationToken = new())
    {
        await base.ShutdownAsync(cancellationToken);
        await evaluatorCache.Shutdown();
    }

    public override async Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var evaluator = evaluatorCache.GetEvaluator();

        var isFeatureEnabled = evaluator.Evaluate(flagKey, context);

        return isFeatureEnabled;
    }

    public override Task<ResolutionDetails<string>> ResolveStringValueAsync(string flagKey, string defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        throw RejectNonBooleanEvaluation(flagKey);
    }

    public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        throw RejectNonBooleanEvaluation(flagKey);
    }

    public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        throw RejectNonBooleanEvaluation(flagKey);
    }

    public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        throw RejectNonBooleanEvaluation(flagKey);
    }

    Exception RejectNonBooleanEvaluation(string flagKey)
    {
        var evaluator = evaluatorCache.GetEvaluator();
        var evaluation = evaluator.FindEvaluationBySlug(flagKey);
        if (evaluation == null)
        {
            return new FlagNotFoundException("The slug provided did not match any of your Octopus Feature Flags. Please double check your slug and try again.");
        }
        return new TypeMismatchException("Octopus only supports boolean flags.");
    }
}
