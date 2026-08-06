using System.Buffers.Binary;
using System.Text;
using Murmur;

namespace Octopus.OpenFeature.Provider.V4.Conditions;

/// <summary>
/// Buckets a targeting key into a percentage rollout. Hashing the flag's evaluation key together with the
/// targeting key keeps a bucket stable across evaluations, while giving each flag an independent spread of
/// targeting keys.
/// </summary>
internal static class PercentageRollout
{
    /// <summary>
    /// Whether <paramref name="targetingKey"/> falls within the first <paramref name="percentage"/> percent
    /// of targeting keys for the flag identified by <paramref name="evaluationKey"/>.
    /// </summary>
    public static bool Includes(string evaluationKey, string targetingKey, int percentage)
        => GetNormalizedNumber(evaluationKey, targetingKey) <= percentage;

    /// <summary>
    /// Computes a deterministic integer bucket in the inclusive range 1–100 for the given evaluation and targeting keys.
    /// </summary>
    /// <remarks>
    /// Internal rather than private so the shared cross-library test vectors can assert on the bucket itself.
    /// </remarks>
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
