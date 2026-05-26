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

        public Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken)
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
            [new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)],
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
    public async Task WhenInitialized_RefreshesCacheAfterCacheDurationExpires()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        var client = new MockOctopusFeatureClient(new FeatureToggles(
            [new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)],
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
            [new FeatureToggleEvaluation("test-feature", false, "evaluation-key", [], 100)],
            [0x01, 0x02, 0x03, 0x05]));

        // Wait for the cache to expire
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Validate the updated toggles are available
        context = provider.GetEvaluationContext();
        context.ContentHash.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x05 });
        context.Evaluate("test-feature", false, context: null).Value.Should().BeFalse();
    }

    [Fact]
    public async Task WhenInitialized_AndRefreshFails_RetainsExistingContextAndLogsError()
    {
        var logger = new FakeLogger();

        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        var client = new MockOctopusFeatureClient(new FeatureToggles(
            [new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)],
            contentHash
        ));

        var provider = new OctopusFeatureContextProvider(configuration, client, logger);
        await provider.Initialize();

        // Simulate a failed fetch
        client.ChangeToggles(null);
        // Wait for the cache to expire and refresh loop to run
        await Task.Delay(TimeSpan.FromSeconds(5));

        try
        {
            var context = provider.GetEvaluationContext();

            using var scope = new AssertionScope();
            logger.LatestRecord.Message.Should().StartWith("Failed to retrieve updated feature manifest");
            context.ContentHash.Should().BeEquivalentTo(contentHash);
        }
        finally
        {
            await provider.Shutdown();
        }
    }

    [Fact]
    public async Task WhenInitialFetchReturnsNothing_AndRefreshSucceeds_ContextIsPopulated()
    {
        var logger = new FakeLogger();

        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        // Initialize with a null client so that first fetch fails
        var client = new MockOctopusFeatureClient(null);
        var provider = new OctopusFeatureContextProvider(configuration, client, logger);
        await provider.Initialize();

        try
        {
            // Check that the context is empty
            provider.GetEvaluationContext().ContentHash.Length.Should().Be(0);

            // Update client to return valid toggles and wait for refresh
            client.ChangeToggles(new FeatureToggles([new FeatureToggleEvaluation("test-feature", false, "evaluation-key", [], 100)], contentHash));
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert that the context is now correctly populated
            provider.GetEvaluationContext().ContentHash.Should().BeEquivalentTo(contentHash);
        }
        finally
        {
            await provider.Shutdown();
        }
    }

    [Fact]
    public async Task WhenRefreshReturnsNothing_AndSubsequentRefreshSucceeds_ContextIsUpdated()
    {
        var logger = new FakeLogger();

        byte[] initialHash = [0x01, 0x02, 0x03, 0x04];
        byte[] updatedHash = [0x01, 0x02, 0x03, 0x05];

        // Initialize with a client that returns valid toggles
        var client = new MockOctopusFeatureClient(new FeatureToggles([new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)], initialHash));
        var provider = new OctopusFeatureContextProvider(configuration, client, logger);
        await provider.Initialize();

        try
        {
            // Switch to a null client and wait for refresh to fail
            client.ChangeToggles(null);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert that failed refresh is logged and old context is retained
            logger.LatestRecord.Message.Should().StartWith("Failed to retrieve updated feature manifest");
            provider.GetEvaluationContext().ContentHash.Should().BeEquivalentTo(initialHash);

            // Update client to return valid toggles again and wait for refresh
            client.ChangeToggles(new FeatureToggles([new FeatureToggleEvaluation("test-feature", false, "evaluation-key", [], 100)], updatedHash));
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert that the context is now correctly populated
            var context = provider.GetEvaluationContext();
            context.ContentHash.Should().BeEquivalentTo(updatedHash);
        }
        finally
        {
            await provider.Shutdown();
        }
    }

    class ThrowsOnRefreshClient(FeatureToggles initial) : IOctopusFeatureClient
    {
        public readonly string ErrorMessage = "Oops! Simulated refresh error";

        public Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken)
        {
            throw new Exception(ErrorMessage);
        }

        public Task<FeatureToggles?> GetFeatureToggleEvaluationManifest(CancellationToken cancellationToken)
        {
            return Task.FromResult<FeatureToggles?>(initial);
        }
    }

    [Fact]
    public async Task WhenAnExceptionIsThrownDuringRefresh_LogsErrorDetails()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];
        var logger = new FakeLogger();

        // Initialize with a client that will throw on refresh
        var client = new ThrowsOnRefreshClient(
            new FeatureToggles([new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)], contentHash)
        );
        var provider = new OctopusFeatureContextProvider(configuration, client, logger);
        await provider.Initialize();

        // Wait for cache to clear and refresh attempt to occur
        await Task.Delay(TimeSpan.FromSeconds(5));

        try
        {
            logger.Collector.GetSnapshot()
                .Should().Contain(r => r.Message.Contains("Failed to retrieve updated feature manifest")
                    && r.Exception != null
                    && r.Exception.Message.Contains(client.ErrorMessage)
                );
        }
        finally
        {
            await provider.Shutdown();
        }
    }

    class AlwaysFailsFeatureClient : IOctopusFeatureClient
    {
        public Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<FeatureToggles?> GetFeatureToggleEvaluationManifest(CancellationToken cancellationToken)
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
        provider.GetEvaluationContext().ContentHash.Length.Should().Be(0);
        logger.LatestRecord.Level.Should().Be(LogLevel.Error);
        logger.LatestRecord.Message.Should().StartWith("Failed to retrieve feature manifest");

        await provider.Shutdown();
    }
}
