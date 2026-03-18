using System.Net.Http.Headers;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextProviderTests
{
    readonly OctopusFeatureConfiguration configuration = new("identifier", new ProductMetadata("test-agent"))
    {
        CacheDuration = TimeSpan.FromSeconds(1)
    };

    class MockOctopusFeatureClient(FeatureToggles? featureToggles) : IOctopusFeatureClient
    {
        FeatureToggles? featureToggles = featureToggles;

        public Task<FeatureToggles?> GetLatestManifest(EntityTagHeaderValue? eTag, CancellationToken cancellationToken)
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
        context.ETag.Should().BeNull();
    }

    [Fact]
    public async Task WhenInitialized_ProvidesRetrievedEvaluationContext()
    {
        var client = new MockOctopusFeatureClient(new FeatureToggles(
            [new FeatureToggleEvaluation("Test Feature", "test-feature", true, [])],
            new("\"01-02-03-04\"")));

        var provider = new OctopusFeatureContextProvider(configuration, client, NullLogger.Instance);
        await provider.Initialize();
        var context = provider.GetEvaluationContext();

        using var scope = new AssertionScope();
        context.Should().NotBeNull();
        context.ETag.Should().NotBeNull();
        context.ETag!.Tag.Should().Be("\"01-02-03-04\"");
        context.Evaluate("test-feature", false, context: null).Value.Should().BeTrue();
    }

    [Fact]
    public async Task WhenInitialized_RefreshesCacheAfterCacheDurationExpires()
    {
        var client = new MockOctopusFeatureClient(new FeatureToggles(
            [new FeatureToggleEvaluation("Test Feature", "test-feature", true, [])],
            new("\"01-02-03-04\"")));

        // Initialize the provider
        var provider = new OctopusFeatureContextProvider(configuration, client, NullLogger.Instance);
        await provider.Initialize();

        // Validate the initial state
        using var scope = new AssertionScope();
        var context = provider.GetEvaluationContext();
        context.ETag.Should().NotBeNull();
        context.ETag!.Tag.Should().Be("\"01-02-03-04\"");
        context.Evaluate("test-feature", false, context: null).Value.Should().BeTrue();

        // Simulate a change in the available feature toggles
        client.ChangeToggles(new FeatureToggles(
            [new FeatureToggleEvaluation("Test Feature", "test-feature", false, [])],
            new("\"01-02-03-05\"")));

        // Wait for the cache to expire
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Validate the updated toggles are available
        context = provider.GetEvaluationContext();
        context.ETag.Should().NotBeNull();
        context.ETag!.Tag.Should().Be("\"01-02-03-05\"");
        context.Evaluate("test-feature", false, context: null).Value.Should().BeFalse();
    }

    class AlwaysFailsFeatureClient : IOctopusFeatureClient
    {
        public Task<FeatureToggles?> GetLatestManifest(EntityTagHeaderValue? eTag, CancellationToken cancellationToken)
        {
            throw new Exception("Oops!");
        }
    }

    [Fact]
    public async Task WhenFeatureEvaluationRetrievalFails_LogsError()
    {
        var client = new AlwaysFailsFeatureClient();
        var logger = new FakeLogger();
        var provider = new OctopusFeatureContextProvider(configuration, client, logger);

        await provider.Initialize();

        using var scope = new AssertionScope();
        provider.GetEvaluationContext().ETag.Should().BeNull();
        logger.LatestRecord.Level.Should().Be(LogLevel.Error);
        logger.LatestRecord.Message.Should().StartWith("Failed to retrieve feature manifest");

        await provider.Shutdown();
    }
}
