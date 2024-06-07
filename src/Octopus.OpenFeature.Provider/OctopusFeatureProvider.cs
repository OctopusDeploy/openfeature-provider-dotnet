using OpenFeature;
using OpenFeature.Constant;
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

        public override async Task<ResolutionDetails<bool>> ResolveBooleanValue(string flagKey, bool defaultValue, EvaluationContext? context = null)
        {
            var evaluator = await client.GetEvaluationContext(configuration.CancellationToken);
            if (evaluator == null)
            {
                return new ResolutionDetails<bool>(flagKey, defaultValue, ErrorType.ProviderNotReady,
                    "Failed to retrieve feature toggles from Octopus Features.");
            }
            
            var isFeatureEnabled = evaluator.Evaluate(flagKey, defaultValue, context);

            return isFeatureEnabled;
        }

        public override Task<ResolutionDetails<string>> ResolveStringValue(string flagKey, string defaultValue, EvaluationContext? context = null)
        {
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }

        public override Task<ResolutionDetails<int>> ResolveIntegerValue(string flagKey, int defaultValue, EvaluationContext? context = null)
        {
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }

        public override Task<ResolutionDetails<double>> ResolveDoubleValue(string flagKey, double defaultValue, EvaluationContext? context = null)
        {
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }

        public override Task<ResolutionDetails<Value>> ResolveStructureValue(string flagKey, Value defaultValue, EvaluationContext? context = null)
        {
            throw new NotImplementedException("Octopus Features only support boolean toggles.");
        }
    }
}
