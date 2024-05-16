using FluentAssertions;
using FluentAssertions.Execution;

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
        
        context.Evaluate("anotherfeature", segment: null).Should().BeFalse();
    }
    
    [Fact]
    public void GivenASetOfFeatureToggles_WhenAFeatureIsToggledOnForSpecificSegments_EvaluatesToTrueWhenSegmentIsSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, ["license/trial"])
        ], []);

        var context = new OctopusFeatureContext(featureToggles);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", segment: "license/trial").Should().BeTrue();
        context.Evaluate("testfeature", segment: "other/segment").Should().BeFalse();
        context.Evaluate("testfeature", segment: null).Should().BeFalse();
    }
    
    [Fact]
    public void GivenASetOfFeatureToggles_WhenFeatureIsNotToggledOnForSpecificSegments_EvaluatesToTrueRegardlessOfSegmentSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, [])
        ], []);

        var context = new OctopusFeatureContext(featureToggles);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", segment: "license/trial").Should().BeTrue();
        context.Evaluate("testfeature", segment: null).Should().BeTrue();
    }
}