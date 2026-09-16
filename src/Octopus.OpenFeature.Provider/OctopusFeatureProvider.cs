using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider;

public class OctopusFeatureProvider : FeatureProvider
{
    readonly FeatureFlagEvaluatorCache evaluatorCache;

    // Null when the caller supplied the client and therefore keeps ownership of it.
    readonly FeatureFlagApiClient? ownedClient;

    public OctopusFeatureProvider(OctopusFeatureConfiguration configuration)
    {
        var logger = configuration.LoggerFactory.CreateLogger<OctopusFeatureProvider>();
        ownedClient = new FeatureFlagApiClient(configuration, logger);
        evaluatorCache = new FeatureFlagEvaluatorCache(configuration, ownedClient, logger);
    }

    // Allows us to pass in a fake IFeatureFlagApiClient for testing purposes.
    internal OctopusFeatureProvider(OctopusFeatureConfiguration configuration, IFeatureFlagApiClient client)
    {
        var logger = configuration.LoggerFactory.CreateLogger<OctopusFeatureProvider>();
        evaluatorCache = new FeatureFlagEvaluatorCache(configuration, client, logger);
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

        // Shut the cache down first. Disposing the client while a refresh is still in flight
        // would fail that request on the way out.
        await evaluatorCache.Shutdown();

        ownedClient?.Dispose();
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
