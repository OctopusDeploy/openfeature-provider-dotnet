using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.V4;

/// <summary>
/// The parse error raised when a v4 evaluation response turns out to be in a shape the server could
/// not legitimately have sent.
///
/// Nothing checks the response up front. It is assumed to be well-formed and evaluated as such, and
/// this is thrown at the point evaluation runs into something it cannot make sense of. The OpenFeature
/// SDK catches it and hands the caller the default value they passed, with
/// <see cref="OpenFeature.Constant.ErrorType.ParseError"/> and this message — the same way v3's
/// evaluation path reports a flag it cannot evaluate.
///
/// Because it is raised during evaluation rather than ahead of it, only the part of the response
/// evaluation actually reads can fail it: a malformed rule the flag never needed to look at costs
/// nothing.
/// </summary>
internal static class MalformedEvaluation
{
    /// <summary>
    /// <paramref name="problem"/> is a noun phrase describing what could not be made sense of, and
    /// completes the sentence naming the flag.
    /// </summary>
    public static ParseErrorException ParseError(string slug, string problem)
        => new($"Feature toggle {slug} could not be evaluated because {problem}.");
}
