using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Everything a flag's rules and conditions are evaluated against: the OpenFeature context supplied
/// by the caller, and the evaluation key of the flag being evaluated.
///
/// The evaluation key is nullable because only <c>percentage-by-context</c> needs it. The server
/// always sends one alongside deferred rules, so a missing key means a malformed response, and the
/// conditions that need it treat it as unmet rather than failing the whole flag.
/// </summary>
internal sealed class ClientSideEvaluationContext
{
    public ClientSideEvaluationContext(string? evaluationKey, EvaluationContext? openFeatureContext)
    {
        EvaluationKey = evaluationKey;
        OpenFeatureContext = openFeatureContext;
    }

    public string? EvaluationKey { get; }

    /// <summary>The caller's context, or <c>null</c> if they supplied none.</summary>
    public EvaluationContext? OpenFeatureContext { get; }
}
