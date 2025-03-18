using OpenFeature;
using OpenFeature.Model;
using Microsoft.Extensions.Logging;

namespace Octopus.OpenFeature.Provider
{
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
            return new Metadata("octopus-feature");
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
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValueAsync(string flagKey, int defaultValue, EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValueAsync(string flagKey, double defaultValue, EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValueAsync(string flagKey, Value defaultValue, EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }
    }
}
