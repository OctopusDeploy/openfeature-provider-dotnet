using FluentAssertions;
using FluentAssertions.Execution;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextTests
{
    [Fact]
    public void GivenAFeatureToggleEvaluation_WhenToggledOnForSpecificSegments_EvaluatesToTrueWhenSegmentIsSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", "testfeature", true, ["license/trial"])
        ], []);

        var context = new OctopusFeatureContext(featureToggles);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", segment: "license/trial").Should().BeTrue();
        context.Evaluate("anotherfeature", segment: null).Should().BeFalse();
    }
}