using System.Text.RegularExpressions;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider;

public partial class OctopusFeatureContext(FeatureToggles toggles)
{
    public byte[] ContentHash => toggles.ContentHash;
    private readonly Regex expression = MyRegex();
    
    public ResolutionDetails<bool> Evaluate(string slug, bool defaultValue, EvaluationContext? context)
    {
        if (expression.IsMatch(slug) == false)
        {
            return new ResolutionDetails<bool>(slug, defaultValue, ErrorType.FlagNotFound,
                "Flag key provided was not a slug. Please ensure to provide the slug associated with your Octopus Feature Toggle.");
        }
        
        var feature =
            toggles.Evaluations.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
        
        if (feature == null)
        {
            return new ResolutionDetails<bool>(slug, defaultValue, ErrorType.FlagNotFound,
                "The slug provided did not match any of your Octopus Feature Toggles. Please double check your slug and try again.");
        }
            

        return new ResolutionDetails<bool>(slug, Evaluate(feature, context));
    }

    bool MatchesSegment(EvaluationContext? context, IEnumerable<KeyValuePair<string, string>> segments)
    {
        if (context == null) return false;

        var contextValues = context.AsDictionary();

        return segments.Any(segment =>
            contextValues.Any(x =>
                x.Key.Equals(segment.Key, StringComparison.OrdinalIgnoreCase)
                && x.Value.AsString.Equals(segment.Value, StringComparison.OrdinalIgnoreCase)));
    }

    bool Evaluate(FeatureToggleEvaluation evaluation, EvaluationContext? context = null)
    {
        return evaluation.IsEnabled && (evaluation.Segments.Length == 0 || MatchesSegment(context, evaluation.Segments));
    }

    [GeneratedRegex("^([a-z0-9]+(-[a-z0-9]+)*)$", RegexOptions.Compiled)]
    private static partial Regex MyRegex();
}