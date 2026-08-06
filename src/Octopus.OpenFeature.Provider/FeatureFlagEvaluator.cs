using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Murmur;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Constant;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Holds one evaluation response and resolves a flag from it, applying any client-side rules the server deferred.
/// </summary>
internal class FeatureFlagEvaluator(EvaluationResponse evaluationResponse, ILoggerFactory loggerFactory)
{
    public byte[] ContentHash => evaluationResponse.ContentHash;
    readonly ILogger logger = loggerFactory.CreateLogger<FeatureFlagEvaluator>();
    readonly ConcurrentDictionary<string, byte> warnedSlugs = new(StringComparer.OrdinalIgnoreCase);

    public static FeatureFlagEvaluator Empty(ILoggerFactory loggerFactory)
    {
        return new FeatureFlagEvaluator(new EvaluationResponse([], []), loggerFactory);
    }

    public ServerSideEvaluation? FindEvaluationBySlug(string slug)
    {
        return evaluationResponse.Evaluations.FirstOrDefault(x => x.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase));
    }

    public ResolutionDetails<bool> Evaluate(string slug, EvaluationContext? context)
    {
        var serverSideEvaluation = FindEvaluationBySlug(slug);

        if (serverSideEvaluation == null)
        {
            if (warnedSlugs.TryAdd(slug, 0))
            {
                logger.LogWarning(
                    "The slug {Slug} did not match any of your Octopus Feature Toggles. Please double check your slug and try again.",
                    slug);
            }

            throw new FlagNotFoundException("The slug provided did not match any of your Octopus Feature Flags. Please double check your slug and try again.");
        }

        return serverSideEvaluation.Evaluate(context);
    }

    bool MatchesSegment(EvaluationContext? context, IEnumerable<KeyValuePair<string, string>> segments)
    {
        if (context == null)
        {
            return false;
        }

        var contextEntries = context.AsDictionary();

        return segments.GroupBy(x => x.Key).All(group =>
            group.Any(segment =>
                contextEntries.Any(contextEntry =>
                    contextEntry.Key.Equals(segment.Key, StringComparison.OrdinalIgnoreCase)
                    && contextEntry.Value.AsString is { } value &&
                    value.Equals(segment.Value, StringComparison.OrdinalIgnoreCase))));
    }

    bool Evaluate(FeatureToggleEvaluation evaluation, EvaluationContext? context = null)
    {
        // Remove in BMBB-702

        if (!evaluation.IsEnabled)
        {
            return false;
        }
        if (evaluation.EvaluationKey == null)
        {
            throw new InvalidOperationException($"Enabled feature toggles require an evaluation key.");
        }

        var targetingKey = context?.TargetingKey;
        if (targetingKey == null || targetingKey == "")
        {
            if (evaluation.ClientRolloutPercentage < 100)
            {
                return false;
            }
        }
        else
        {
            if (GetNormalizedNumber(evaluation.EvaluationKey, targetingKey) > evaluation.ClientRolloutPercentage)
            {
                return false; // return false if hash number is larger than rollout percentage
            }
        }

        return evaluation.Segments == null || evaluation.Segments.Length == 0 || MatchesSegment(context, evaluation.Segments);
    }

    /// <summary>
    /// Computes a deterministic integer bucket in the inclusive range 1–100 for the given evaluation and targeting keys.
    /// </summary>
    internal static int GetNormalizedNumber(string evaluationKey, string targetingKey)
    {
        // Move to own class in BMBB-702. Perhaps copy+paste of PercentageRollout.cs in OctoToggle?

        var bytes = Encoding.UTF8.GetBytes(string.Concat(evaluationKey, ":", targetingKey));

        using var algorithm = MurmurHash.Create32();
        var hash = algorithm.ComputeHash(bytes);
        // Explicitly little-endian to ensure consistent int values across all client libraries.
        var value = BinaryPrimitives.ReadUInt32LittleEndian(hash);
        return (int)(value % 100 + 1);
    }


    private static bool MissingRequiredPropertiesForClientSideEvaluation(FeatureToggleEvaluation evaluation)
    {
        if (!evaluation.IsEnabled)
        {
            return false;
        }

        return evaluation.ClientRolloutPercentage is null || evaluation.EvaluationKey is null || evaluation.Segments is null;
    }
}
