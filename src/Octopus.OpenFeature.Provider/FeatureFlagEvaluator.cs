using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using Murmur;
using Octopus.OpenFeature.Provider.V4;
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
                    "The slug {Slug} did not match any of your Octopus Feature Flags. Please double check your slug and try again.",
                    slug);
            }

            throw new FlagNotFoundException("The slug provided did not match any of your Octopus Feature Flags. Please double check your slug and try again.");
        }

        return serverSideEvaluation.Evaluate(context);
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
}
