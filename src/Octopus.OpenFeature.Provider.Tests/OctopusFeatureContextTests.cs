using FluentAssertions;
using FluentAssertions.Execution;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextTests
{
    [Fact]
    public void GivenASetOfFeatureToggles_EvaluatesToFalseIfFeatureIsNotContainedWithinSet()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles);
        
        context.Evaluate("anotherfeature", context: null).Should().BeFalse();
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
    public void GivenASetOfFeatureToggles_WhenAFeatureIsToggledOnForASpecificSegment_EvaluatesToTrueWhenSegmentIsSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, new Dictionary<string, string> { { "license", "trial" }})
        ], []);

        var context = new OctopusFeatureContext(featureToggles);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", context: BuildContext([("license", "trial")])).Should().BeTrue();
        context.Evaluate("testfeature", context: BuildContext([("other", "segment")])).Should().BeFalse();
        context.Evaluate("testfeature", context: null).Should().BeFalse();
    }
    
    [Fact]
    public void GivenASetOfFeatureToggles_WhenFeatureIsNotToggledOnForSpecificSegments_EvaluatesToTrueRegardlessOfSegmentSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", context: BuildContext([("license", "trial")])).Should().BeTrue();
        context.Evaluate("testfeature", context: null).Should().BeTrue();
    }
    
    [Fact]
    public void GivenASetOfFeatureToggles_WhenAFeatureIsToggledOnForMultipleSpecificSegments_EvaluatesToTrueWhenAllSegmentsAreSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, new Dictionary<string, string>
            {
                { "license", "trial" },
                { "region", "us" }
            })
        ], []);

        var context = new OctopusFeatureContext(featureToggles);

        using var scope = new AssertionScope();
        
        // All specified
        context.Evaluate("testfeature", context: BuildContext([("license", "trial"), ("region", "us")])).Should().BeTrue();
        
        // Superset specified
        context.Evaluate("testfeature", context: BuildContext([("license", "trial"), ("region", "us"), ("language", "english")])).Should().BeTrue();

        // Subset specified
        context.Evaluate("testfeature", context: BuildContext([("other", "segment")])).Should().BeFalse();
        
        // None specified
        context.Evaluate("testfeature", context: null).Should().BeFalse();
    }
}