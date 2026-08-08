using Octopus.OpenFeature.Provider.V4;

namespace Octopus.OpenFeature.Provider;

internal class EvaluationResponse(ServerSideEvaluation[] evaluations, byte[] contentHash)
{
    public ServerSideEvaluation[] Evaluations { get; } = evaluations;

    public byte[] ContentHash { get; } = contentHash;
}
