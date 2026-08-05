using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Everything a flag's rules and conditions are evaluated against: the OpenFeature context supplied
/// by the caller, and the evaluation key of the flag being evaluated.
///
/// A condition needing some other input widens this context rather than changing every signature.
/// </summary>
internal sealed class ClientSideEvaluationContext
{
    public ClientSideEvaluationContext(string evaluationKey, EvaluationContext? openFeatureContext)
    {
        EvaluationKey = evaluationKey;
        OpenFeatureContext = openFeatureContext;
    }

    /// <summary>
    /// The key <c>percentage-by-context</c> buckets against. Non-nullable: the server always sends one
    /// alongside deferred rules, and a response without one never gets as far as building this context.
    /// </summary>
    public string EvaluationKey { get; }

    /// <summary>The caller's context, or <c>null</c> if they supplied none.</summary>
    public EvaluationContext? OpenFeatureContext { get; }
}
