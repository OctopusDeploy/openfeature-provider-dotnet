using System.Text;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using Murmur;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextTests
{
    [Fact]
    public void EvaluatesToTrue_IfFeatureIsContainedWithinTheSet_AndFeatureIsEnabled()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("test-feature", false, context: null);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public void WhenEvaluatedWithCasingDifferences_EvaluationIsInsensitiveToCase()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("Test-Feature", false, context: null);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public void EvaluatesToFalse_IfFeatureIsContainedWithinTheSet_AndFeatureIsNotEnabled()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", false, "evaluation-key", [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("test-feature", false, context: null);

        result.Value.Should().BeFalse();
    }

    [Fact]
    public void GivenAFlagKeyThatIsNotASlug_ReturnsFlagNotFound_AndEvaluatesToDefaultValue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("this-is-clearly-not-a-slug", true, "evaluation-key", [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("This is clearly not a slug!", defaultValue: true, context: null);

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void EvaluatesToDefaultValue_IfFeatureIsNotContainedWithinSet()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("anotherfeature", defaultValue: true, context: null);

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
        result.Value.Should().BeTrue();
    }

    EvaluationContext BuildContext(IEnumerable<(string key, string value)> values, string? targetingKey = null)
    {
        var builder = EvaluationContext.Builder();
        foreach (var (key, value) in values)
        {
            builder.Set(key, value);
        }
        if (targetingKey != null)
        {
            builder.SetTargetingKey(targetingKey);
        }
        return builder.Build();
    }

    [Fact]
    public void
        WhenAFeatureIsToggledOnForASpecificSegment_EvaluatesToTrueWhenSegmentIsSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [new("license", "trial")])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial")])).Value.Should().BeTrue();
        context.Evaluate("testfeature", false, context: BuildContext([("other", "segment")])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: null).Value.Should().BeFalse();
    }

    [Fact]
    public void
        WhenFeatureIsNotToggledOnForSpecificSegments_EvaluatesToTrueRegardlessOfSegmentSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial")])).Value.Should().BeTrue();
        context.Evaluate("testfeature", false, context: null).Value.Should().BeTrue();
    }

    [Fact]
    public void WhenAFeatureIsToggledOnForMultipleSegments_EvaluatesCorrectly()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation(
                "testfeature", true, "evaluation-key", [
                    new("license", "trial"),
                    new("region", "au"),
                    new("region", "us"),
                ],
                100
            )
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();

        // A matching context value is present for each toggled segment
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial"), ("region", "us")])).Value
            .Should().BeTrue("when there is a matching context value for each toggled segment, the toggle should be enabled");

        // A context value is present for each toggled segment, but it is not toggled on for one of the supplied values
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial"), ("region", "eu")])).Value
            .Should().BeFalse("when there is a matching context value for each toggled segment, but the context value does not match the toggled segment, the toggle should be disabled");

        // A matching context value is present for each toggled segment, and an additional segment is present in the provided context values
        context.Evaluate("testfeature", false,
                context: BuildContext([("license", "trial"), ("region", "us"), ("language", "english")])).Value.Should()
            .BeTrue("when extra context values are present, the toggle should still be enabled");

        // A context value is present for only one of the two toggled segments
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial")])).Value.Should()
            .BeFalse("when the context does not contain a value for all toggled segments, the toggle should be disabled");

        // No context values are present for the two toggled segment
        // Note that the default value is only returned if evaluation fails for an unexpected reason.
        // In this case, the default value is not returned, as we have a successful, but false, flag evaluation.
        context.Evaluate("testfeature", true, context: BuildContext([("other", "segment")])).Value.Should()
            .BeFalse("when the context does not contain a value for all toggled segments, the toggle should be disabled");

        // None specified
        context.Evaluate("testfeature", true, context: null).Value.Should().BeFalse("when no context values are present, and the feature is toggled on for a segment, the toggle should be disabled");
    }

    [Fact]
    public void
        WhenAFeatureIsToggledOnForASpecificSegment_ToleratesNullValuesInContext()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [new("license", "trial")])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", false, context: BuildContext([("license", null)!])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: BuildContext([("other", "segment")])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: null).Value.Should().BeFalse();
    }

    [Fact]
    public void WhenSegmentFallsWithinRolloutPercentage_AndFeatureIsNotToggledForSegments_ResolvesToTrue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], rolloutPercentage: 20)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // 13 < 20 => segment is within rollout percentage
        var evaluationContext = BuildContext([], targetingKey: "targeting-key");
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeTrue("segment is within rollout percentage");
    }

    [Fact]
    public void WhenSegmentFallsOutsideRolloutPercentage_AndFeatureIsNotToggledForSegments_ResolvesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], rolloutPercentage: 10)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // 13 > 10 => segment is outside of rollout percentage
        var evaluationContext = BuildContext([], targetingKey: "targeting-key");
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeFalse("segment is outside of rollout percentage");
    }

    [Fact]
    public void WhenSegmentIsWithinRolloutPercentage_AndSegmentMatchesRequiredSegments_EvaluatesToTrue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [new("license", "trial")], rolloutPercentage: 20)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // 13 < 20 => segment is within rollout percentage
        var evaluationContext = BuildContext([("license", "trial")], targetingKey: "targeting-key");

        using var scope = new AssertionScope();
        context.Evaluate("test-feature", false, evaluationContext).Value.Should()
            .BeTrue("segment matches required segment and falls within rollout percentage");
    }

    [Fact]
    public void WhenSegmentIsWithinRolloutPercentage_AndSegmentDoesNotMatchRequiredSegments_EvaluatesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [new("license", "enterprise")], rolloutPercentage: 20)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // 13 < 20 => segment is within rollout percentage
        var evaluationContext = BuildContext([("license", "trial")], targetingKey: "targeting-key");

        using var scope = new AssertionScope();
        context.Evaluate("test-feature", false, evaluationContext).Value.Should()
            .BeFalse("segment does not match required segment");
    }

    [Fact]
    public void WhenSegmentIsOutsideRolloutPercentage_AndSegmentDoesNotMatchRequiredSegments_EvaluatesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [new("license", "enterprise")], rolloutPercentage: 10)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // 13 > 10 => segment is outside of rollout percentage
        var evaluationContext = BuildContext([("license", "trial")], targetingKey: "targeting-key");

        using var scope = new AssertionScope();
        context.Evaluate("test-feature", false, evaluationContext).Value.Should()
            .BeFalse("segment is outside of rollout percentage");
    }

    [Fact]
    public void WhenNoTargetingKey_RolloutIsLessThanOneHundredPercent_ResolvesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], rolloutPercentage: 95)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var evaluationContext = BuildContext([], targetingKey: null);
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeFalse("no targeting key and rollout is less than 100%");
    }

    [Fact]
    public void WhenNoTargetingKey_RolloutIsEqualToOneHundredPercent_ResolvesToTrue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], rolloutPercentage: 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var evaluationContext = BuildContext([], targetingKey: null);
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeTrue("no targeting key and rollout is 100%");
    }
}
