using System.Diagnostics.CodeAnalysis;
using OpenFeature;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider
{
    [SuppressMessage("Octopus", "OCT1003:Methods returning Task must be async.", Justification = "These are not the droids you are looking for")]
    public class OctopusFeatureProvider(OctopusFeatureConfiguration configuration) : FeatureProvider
    {
        readonly OctopusFeatureClient client = new(configuration);

        public override Metadata GetMetadata()
        {
            return new Metadata("octopus-feature");
        }

        public override async Task<ResolutionDetails<bool>> ResolveBooleanValue(string flagKey, bool defaultValue, EvaluationContext? context = null)
        {
            // TODO: multiple segments
            string? segment = null;
            if (context!.ContainsKey("segment")) segment = context.GetValue("segment").AsString;

            var evaluator = await client.GetEvaluationContext(configuration.CancellationToken);
            var isFeatureEnabled = evaluator != null && evaluator.Evaluate(flagKey, segment);
            return new(flagKey, isFeatureEnabled);
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
