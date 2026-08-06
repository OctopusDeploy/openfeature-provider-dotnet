using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Octopus.OpenFeature.Provider.V4;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextProviderTests
{
    const string Slug = "test-feature";

    readonly OctopusFeatureConfiguration configuration = new("identifier", new ProductMetadata("test-agent"))
    {
        CacheDuration = TimeSpan.FromSeconds(1)
    };

    /// <summary>
    /// A response holding one server-resolved flag. These tests turn on the content hash and the value the
    /// flag resolves to, so the flag needs no rules of its own.
    /// </summary>
    static EvaluationResponse Response(bool value, byte[] contentHash)
        => new([
            new ServerSideEvaluation(
                Slug,
                value,
                reason: value ? "The flag is enabled for this environment." : "The flag is disabled for this environment.",
                evaluationKey: null,
                rules: null)
        ], contentHash);

    class MockOctopusFeatureClient(EvaluationResponse? evaluationResponse) : IOctopusFeatureClient
    {
        EvaluationResponse? evaluationResponse = evaluationResponse;

        public Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken)
        {
            return Task.FromResult(true);
        }

        public Task<EvaluationResponse?> GetServerSideEvaluations(CancellationToken cancellationToken)
        {
            return Task.FromResult(evaluationResponse);
        }

        public void ChangeEvaluations(EvaluationResponse? newEvaluationResponse = null)
        {
            this.evaluationResponse = newEvaluationResponse;
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

        var client = new MockOctopusFeatureClient(Response(value: true, contentHash));

        var provider = new OctopusFeatureContextProvider(configuration, client, NullLogger.Instance);
        await provider.Initialize();
        var context = provider.GetEvaluationContext();

        using var scope = new AssertionScope();
        context.Should().NotBeNull();
        context.ContentHash.Should().BeEquivalentTo(contentHash);
        context.Evaluate(Slug, context: null).Value.Should().BeTrue();
    }

    [Fact]
    public async Task WhenInitialized_RefreshesCacheAfterCacheDurationExpires()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        var client = new MockOctopusFeatureClient(Response(value: true, contentHash));

        // Initialize the provider
        var provider = new OctopusFeatureContextProvider(configuration, client, NullLogger.Instance);
        await provider.Initialize();

        // Validate the initial state
        using var scope = new AssertionScope();
        var context = provider.GetEvaluationContext();
        context.ContentHash.Should().BeEquivalentTo(contentHash);
        context.Evaluate(Slug, context: null).Value.Should().BeTrue();

        // Simulate a change in the available feature flags
        client.ChangeEvaluations(Response(value: false, [0x01, 0x02, 0x03, 0x05]));

        // Wait for the cache to expire
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Validate the updated evaluations are available
        context = provider.GetEvaluationContext();
        context.ContentHash.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x05 });
        context.Evaluate(Slug, context: null).Value.Should().BeFalse();
    }

    [Fact]
    public async Task WhenInitialized_AndRefreshFails_RetainsExistingContextAndLogsError()
    {
        var logger = new FakeLogger();

        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        var client = new MockOctopusFeatureClient(Response(value: true, contentHash));

        var provider = new OctopusFeatureContextProvider(configuration, client, logger);
        await provider.Initialize();

        // Simulate a failed fetch
        client.ChangeEvaluations(null);
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

            // Update client to return valid evaluations and wait for refresh
            client.ChangeEvaluations(Response(value: false, contentHash));
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

        // Initialize with a client that returns valid evaluations
        var client = new MockOctopusFeatureClient(Response(value: true, initialHash));
        var provider = new OctopusFeatureContextProvider(configuration, client, logger);
        await provider.Initialize();

        try
        {
            // Switch to a null client and wait for refresh to fail
            client.ChangeEvaluations(null);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert that failed refresh is logged and old context is retained
            logger.LatestRecord.Message.Should().StartWith("Failed to retrieve updated feature manifest");
            provider.GetEvaluationContext().ContentHash.Should().BeEquivalentTo(initialHash);

            // Update client to return valid evaluations again and wait for refresh
            client.ChangeEvaluations(Response(value: false, updatedHash));
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

    class ThrowsOnRefreshClient(EvaluationResponse initial) : IOctopusFeatureClient
    {
        public readonly string ErrorMessage = "Oops! Simulated refresh error";

        public Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken)
        {
            throw new Exception(ErrorMessage);
        }

        public Task<EvaluationResponse?> GetServerSideEvaluations(CancellationToken cancellationToken)
        {
            return Task.FromResult<EvaluationResponse?>(initial);
        }
    }

    [Fact]
    public async Task WhenAnExceptionIsThrownDuringRefresh_LogsErrorDetails()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];
        var logger = new FakeLogger();

        // Initialize with a client that will throw on refresh
        var client = new ThrowsOnRefreshClient(Response(value: true, contentHash));
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

        public Task<EvaluationResponse?> GetServerSideEvaluations(CancellationToken cancellationToken)
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
