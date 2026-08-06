namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Reasons returned alongside a client-side evaluation. Both match the strings the Feature Flags service produces
/// server-side, so a flag reads the same whichever side resolved it.
/// </summary>
internal static class EvaluationReasons
{
    public static string MatchedRule(string ruleName) => $"Matched rule '{ruleName}'.";

    public static string DidNotMatchAnyRules() => "Did not match any rules.";
}
