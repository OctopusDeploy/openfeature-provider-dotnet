using OpenFeature;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider
{
    public class OctopusFeatureProvider(OctopusFeatureConfiguration configuration) : FeatureProvider
    {
        readonly OctopusFeatureClient client = new(configuration);

        public override Metadata GetMetadata()
        {
            return new Metadata("octopus-feature");
        }

        public override async Task<ResolutionDetails<bool>> ResolveBooleanValueAsync(string flagKey, bool defaultValue, EvaluationContext? context = null,
            CancellationToken cancellationToken = default)
        {
            var evaluator = await client.GetEvaluationContext(configuration.CancellationToken);
            
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
