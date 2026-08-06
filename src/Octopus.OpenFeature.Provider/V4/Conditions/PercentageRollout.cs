using System.Buffers.Binary;
using System.Text;
using Murmur;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Buckets a targeting key for a percentage rollout.
/// </summary>
internal static class PercentageRollout
{
    /// <summary>
    /// Computes a deterministic integer bucket in the inclusive range 1–100 for the given evaluation and targeting keys.
    /// </summary>
    internal static int GetNormalizedNumber(string evaluationKey, string targetingKey)
    {
        var bytes = Encoding.UTF8.GetBytes(string.Concat(evaluationKey, ":", targetingKey));

        using var algorithm = MurmurHash.Create32();
        var hash = algorithm.ComputeHash(bytes);
        // Explicitly little-endian to ensure consistent int values across all client libraries.
        var value = BinaryPrimitives.ReadUInt32LittleEndian(hash);
        return (int)(value % 100 + 1);
    }
}
