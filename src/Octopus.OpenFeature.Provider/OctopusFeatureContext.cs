namespace Octopus.OpenFeature.Provider;

public class OctopusFeatureContext(FeatureToggles toggles)
{
    public byte[] ContentHash => toggles.ContentHash;
    
    public bool Evaluate(string slug, string? segment)
    {
        var feature = toggles.Evaluations.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.InvariantCultureIgnoreCase));

        if (feature == null) return false;

        return Evaluate(feature, segment);
    }

    bool Evaluate(FeatureToggleEvaluation evaluation, string? segment = null)
    {
        return evaluation.IsEnabled && (segment == null || evaluation.Segments.Any(s => s.Equals(segment, StringComparison.OrdinalIgnoreCase)));
    }
}
