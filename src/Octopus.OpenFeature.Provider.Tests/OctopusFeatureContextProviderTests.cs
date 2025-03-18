using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextProviderTests
{
    readonly OctopusFeatureConfiguration configuration = new("identifier")
    {
        CacheDuration = TimeSpan.FromSeconds(1)
    };
    
    class MockOctopusFeatureClient(FeatureToggles? featureToggles) : IOctopusFeatureClient
    {
        FeatureToggles? featureToggles = featureToggles;

        public Task<bool> HaveFeaturesChanged(OctopusFeatureContext? currentContext, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<FeatureToggles?> GetFeatureToggleEvaluationManifest(CancellationToken cancellationToken)
        {
            return Task.FromResult(featureToggles);
        }

        public void ChangeToggles(FeatureToggles? featureToggles = null)
        {
            this.featureToggles = featureToggles;
        }
    }

    [Fact]
    public void WhenInstantiated_ProvidesAnEmptyEvaluationContext()
    {
        var provider = new OctopusFeatureContextProvider(configuration, new MockOctopusFeatureClient(null), NullLogger.Instance);
        
        var context = provider.GetEvaluationContext();

        using var scope = new AssertionScope();
        context.Should().NotBeNull();
        context.ContentHash.Length.Should().Be(0);
    }

    [Fact]
    public async Task WhenInitialized_ProvidesRetrievedEvaluationContext()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];
        
        var client = new MockOctopusFeatureClient(new FeatureToggles(
            [new FeatureToggleEvaluation("Test Feature", "test-feature", true, [])],
            contentHash));
        
        var provider = new OctopusFeatureContextProvider(configuration, client, NullLogger.Instance);
        await provider.Initialize();
        var context = provider.GetEvaluationContext();

        using var scope = new AssertionScope();
        context.Should().NotBeNull();
        context.ContentHash.Should().BeEquivalentTo(contentHash);
        context.Evaluate("test-feature", false, context: null).Value.Should().BeTrue();
    }

    [Fact]
    public async Task WhenInitialized_RefreshesCacheOnceCacheDurationExpires()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];
        
        var client = new MockOctopusFeatureClient(new FeatureToggles(
            [new FeatureToggleEvaluation("Test Feature", "test-feature", true, [])], 
            contentHash));
        
        // Initialize the provider
        var provider = new OctopusFeatureContextProvider(configuration, client, NullLogger.Instance);
        await provider.Initialize();
        
        // Validate the initial state
        using var scope = new AssertionScope();
        var context = provider.GetEvaluationContext();
        context.ContentHash.Should().BeEquivalentTo(contentHash);
        context.Evaluate("test-feature", false, context: null).Value.Should().BeTrue();
        
        // Simulate a change in the available feature toggles
        client.ChangeToggles(new FeatureToggles(
            [new FeatureToggleEvaluation("Test Feature", "test-feature", false, [])],
            [0x01, 0x02, 0x03, 0x05]));

        // Wait for the cache to expire
        await Task.Delay(TimeSpan.FromSeconds(5));
        
        // Validate the updated toggles are available
        context = provider.GetEvaluationContext();
        context.ContentHash.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x05 });
        context.Evaluate("test-feature", false, context: null).Value.Should().BeFalse();
    }
}