using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider;

public class OctopusFeatureContext(FeatureToggles toggles)
{
    public byte[] ContentHash => toggles.ContentHash;

    public bool Evaluate(string slug, EvaluationContext? context)
    {
        var feature =
            toggles.Evaluations.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

        if (feature == null) return false;

        return Evaluate(feature, context);
    }

    bool MatchesSegment(EvaluationContext? context, Dictionary<string, string> segments)
    {
        if (context == null) return false;

        var contextValues = context.AsDictionary();

        return segments.All(segment =>
            contextValues.Any(x =>
                x.Key.Equals(segment.Key, StringComparison.OrdinalIgnoreCase)
                && x.Value.AsString.Equals(segment.Value, StringComparison.OrdinalIgnoreCase)));
    }

    bool Evaluate(FeatureToggleEvaluation evaluation, EvaluationContext? context = null)
    {
        return evaluation.IsEnabled && (evaluation.Segments.Count == 0 || MatchesSegment(context, evaluation.Segments));
    }
}