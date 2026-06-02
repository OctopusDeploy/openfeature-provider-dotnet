using Microsoft.Extensions.Logging;
using OpenFeature;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider;

public class OctopusFeatureProvider : FeatureProvider
{
    readonly OctopusFeatureContextProvider contextProvider;

    public OctopusFeatureProvider(OctopusFeatureConfiguration configuration)
    {
        var logger = configuration.LoggerFactory.CreateLogger<OctopusFeatureProvider>();
        var client = new OctopusFeatureClient(configuration, logger);
        contextProvider = new OctopusFeatureContextProvider(configuration, client, logger);
    }

    public override Metadata GetMetadata()
    {
        return new Metadata("octopus-dotnet-provider");
    }

    public override async Task InitializeAsync(EvaluationContext context, CancellationToken cancellationToken = new())
    {
        await base.InitializeAsync(context, cancellationToken);
        await contextProvider.Initialize();
    }

    public override async Task ShutdownAsync(CancellationToken cancellationToken = new())
    {
        await base.ShutdownAsync(cancellationToken);
        await contextProvider.Shutdown();
    }

    public override async Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null,
        CancellationToken cancellationToken = default)
    {
        await Task.CompletedTask;

        var evaluator = contextProvider.GetEvaluationContext();

        var isFeatureEnabled = evaluator.Evaluate(flagKey, defaultValue, context);

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
        var evaluator = contextProvider.GetEvaluationContext();
        var toggle = evaluator.FindFeatureToggleBySlug(flagKey);
        if (toggle == null)
        {
            return new FlagNotFoundException(flagKey);
        }
        return new TypeMismatchException("Octopus Feature Toggles only support boolean toggles.");
    }
}
