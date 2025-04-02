using FluentAssertions;

namespace Octopus.OpenFeature.Provider.Tests;

public class SegmentEvaluatorTests
{
    [Theory]
    [InlineData("1.0.0", "1.0.1", true, "true when context version is greater than segment version")]
    [InlineData("1.0.0-mybranch", "1.0.1-mybranch", true, "tolerates pre-release versions")]
    [InlineData("1.0.1", "1.0.0", false, "false when context version is less than segment version")]
    [InlineData("1.0.1", "1.0.1", true, "true when context version is equal to segment version")]
    public void GivenASemanticVersionSegmentEvaluator_EvaluatesAsExpected(string segmentVersion, string contextVersion, bool expectedValue, string reason)
    {
        var evaluator = new SemanticVersionSegmentEvaluator();

        var result = evaluator.Evaluate(segmentVersion, contextVersion);

        result.Should().Be(expectedValue);
    }
}