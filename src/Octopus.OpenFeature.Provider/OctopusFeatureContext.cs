using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider;

partial class OctopusFeatureContext(FeatureToggles toggles, ILoggerFactory loggerFactory)
{
    public byte[] ContentHash => toggles.ContentHash;
    readonly Regex expression = SlugExpression();
    readonly ILogger logger = loggerFactory.CreateLogger<OctopusFeatureContext>();

    public static OctopusFeatureContext Empty(ILoggerFactory loggerFactory)
    {
        return new OctopusFeatureContext(new FeatureToggles([], []), loggerFactory);
    }

    public ResolutionDetails<bool> Evaluate(string slug, bool defaultValue, EvaluationContext? context)
    {
        if (expression.IsMatch(slug) == false)
        {
            logger.LogWarning(
                "Flag key {FlagKey} does not appear to be a slug. Please ensure to provide the slug associated with your Octopus Feature Toggle.",
                slug);

            return new ResolutionDetails<bool>(slug, defaultValue, ErrorType.FlagNotFound,
                "Flag key provided was not a slug. Please ensure to provide the slug associated with your Octopus Feature Toggle.");
        }

        var feature =
            toggles.Evaluations.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));

        if (feature == null)
        {
            logger.LogWarning(
                "The slug {Slug} did not match any of your Octopus Feature Toggles. Please double check your slug and try again.",
                slug);

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
                && x.Value.AsString is { } value &&
                value.Equals(segment.Value, StringComparison.OrdinalIgnoreCase)));
    }

    bool Evaluate(FeatureToggleEvaluation evaluation, EvaluationContext? context = null)
    {
        return evaluation.IsEnabled &&
               (evaluation.Segments.Length == 0 || MatchesSegment(context, evaluation.Segments));
    }

    [GeneratedRegex("^([a-z0-9]+(-[a-z0-9]+)*)$", RegexOptions.Compiled | RegexOptions.IgnoreCase)]
    private static partial Regex SlugExpression();
}