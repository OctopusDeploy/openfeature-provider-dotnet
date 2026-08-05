using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// What a flag's rules and conditions are evaluated against.
/// </summary>
internal sealed class ClientSideEvaluationContext
{
    public ClientSideEvaluationContext(string evaluationKey, EvaluationContext? openFeatureContext)
    {
        EvaluationKey = evaluationKey;
        OpenFeatureContext = openFeatureContext;
    }

    /// <summary>The key <c>percentage-by-context</c> buckets against.</summary>
    public string EvaluationKey { get; }

    /// <summary>The caller's context, or <c>null</c> if they supplied none.</summary>
    public EvaluationContext? OpenFeatureContext { get; }
}
