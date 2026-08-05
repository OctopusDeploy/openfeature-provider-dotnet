namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// Human-readable reasons returned alongside a client-side v4 flag evaluation. Both match the strings
/// OctoToggle produces server-side, so a flag reads consistently whether it was resolved by the server
/// or by the client.
///
/// A flag that could not be evaluated at all has no reason of ours: the OpenFeature SDK reports those
/// as <c>ERROR</c>, carrying the message from <see cref="MalformedEvaluation"/>.
/// </summary>
internal static class EvaluationReasons
{
    public static string MatchedRule(string ruleName) => $"Matched rule '{ruleName}'.";

    public static string DidNotMatchAnyRules() => "Did not match any rules.";
}
