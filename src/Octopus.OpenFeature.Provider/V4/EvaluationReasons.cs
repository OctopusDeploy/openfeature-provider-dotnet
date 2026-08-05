namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Human-readable reasons returned alongside a client-side v4 flag evaluation.
///
/// <see cref="MatchedRule"/> and <see cref="DidNotMatchAnyRules"/> match the strings OctoToggle
/// produces server-side, so a flag reads consistently whether it was resolved by the server or by the
/// client. <see cref="MalformedEvaluation"/> is instead the wording the v3 evaluation path already
/// returns with a parse error, kept verbatim so that failure reads the same across both versions.
/// </summary>
internal static class EvaluationReasons
{
    public static string MatchedRule(string ruleName) => $"Matched rule '{ruleName}'.";

    public static string DidNotMatchAnyRules() => "Did not match any rules.";

    public static string MalformedEvaluation(string slug)
        => $"Feature toggle {slug} is missing necessary information for client-side evaluation.";
}
