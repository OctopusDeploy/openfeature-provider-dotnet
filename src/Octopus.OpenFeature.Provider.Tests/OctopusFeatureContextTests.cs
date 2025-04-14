using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextTests
{
    [Fact]
    public void EvaluatesToTrue_IfFeatureIsContainedWithinTheSet_AndFeatureIsEnabled()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "test-feature", true, [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("test-feature", false, context: null);

        result.Value.Should().BeTrue();
    }
    
    [Fact]
    public void WhenEvaluatedWithCasingDifferences_EvaluationIsInsensitiveToCase()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "test-feature", true, [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("Test-Feature", false, context: null);

        result.Value.Should().BeTrue();
    }
    
    [Fact]
    public void EvaluatesToFalse_IfFeatureIsContainedWithinTheSet_AndFeatureIsNotEnabled()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "test-feature", false, [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("test-feature", false, context: null);

        result.Value.Should().BeFalse();
    }
    
    [Fact]
    public void GivenAFlagKeyThatIsNotASlug_ReturnsFlagNotFound_AndEvaluatesToDefaultValue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("This is clearly not a slug!", "this-is-clearly-not-a-slug", true, [])
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
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("anotherfeature", defaultValue: true, context: null);

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
        result.Value.Should().BeTrue();
    }

    EvaluationContext BuildContext(IEnumerable<(string key, string value)> values)
    {
        var builder = EvaluationContext.Builder();
        foreach (var (key, value) in values)
        {
            builder.Set(key, value);
        }

        return builder.Build();
    }

    [Fact]
    public void
        WhenAFeatureIsToggledOnForASpecificSegment_EvaluatesToTrueWhenSegmentIsSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [new("license", "trial")])
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
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [])
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
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [
                new("license", "trial"),
                new("region", "au"),
                new("region", "us"),
            ])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();

        // All specified
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial"), ("region", "us")])).Value
            .Should().BeTrue("Context has provided a value for each specified segment key on the toggle");
        
        // All specified, but one value does not match
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial"), ("region", "eu")])).Value
            .Should().BeFalse("Context must provide at least one matching value for each specified segment key on the toggle");

        // Superset specified
        context.Evaluate("testfeature", false,
                context: BuildContext([("license", "trial"), ("region", "us"), ("language", "english")])).Value.Should()
            .BeTrue("Context has provided a value for each specified segment key on the toggle");

        // Subset specified
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial")])).Value.Should()
            .BeFalse("Context must provide at least one matching value for each specified segment key on the toggle");

        // Invalid specified
        // Note that the default value is only returned if evaluation fails for an unexpected reason.
        // In this case, the default value is not returned, as we have a successful, but false, flag evaluation.
        context.Evaluate("testfeature", true, context: BuildContext([("other", "segment")])).Value.Should()
            .BeFalse("Context must provide at least one matching value for each specified segment key on the toggle");

        // None specified
        context.Evaluate("testfeature", true, context: null).Value.Should().BeFalse();
    }
    
    [Fact]
    public void
        WhenAFeatureIsToggledOnForASpecificSegment_ToleratesNullValuesInContext()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [new("license", "trial")])
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", false, context: BuildContext([("license", null)!])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: BuildContext([("other", "segment")])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: null).Value.Should().BeFalse();
    }
}