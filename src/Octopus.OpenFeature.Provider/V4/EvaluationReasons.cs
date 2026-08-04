namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Human-readable reasons returned alongside a client-side v4 flag evaluation. The wording matches
/// the strings OctoToggle produces server-side, so a flag reads consistently whether it was resolved
/// by the server or by the client.
/// </summary>
internal static class EvaluationReasons
{
    public static string MatchedRule(string ruleName) => $"Matched rule '{ruleName}'.";

    public static string DidNotMatchAnyRules() => "Did not match any rules.";
}
