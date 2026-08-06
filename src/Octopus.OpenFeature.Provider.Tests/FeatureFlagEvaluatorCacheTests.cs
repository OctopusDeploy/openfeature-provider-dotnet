using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Octopus.OpenFeature.Provider.V4;

namespace Octopus.OpenFeature.Provider.Tests;

public class FeatureFlagEvaluatorCacheTests
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

    class MockFeatureFlagApiClient(EvaluationResponse? evaluationResponse) : IFeatureFlagApiClient
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
    public void WhenInstantiated_ProvidesAnEmptyEvaluator()
    {
        var cache = new FeatureFlagEvaluatorCache(configuration, new MockFeatureFlagApiClient(null), NullLogger.Instance);

        var evaluator = cache.GetEvaluator();

        using var scope = new AssertionScope();
        evaluator.Should().NotBeNull();
        evaluator.ContentHash.Length.Should().Be(0);
    }

    [Fact]
    public async Task WhenInitialized_ProvidesTheRetrievedEvaluator()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        var client = new MockFeatureFlagApiClient(Response(value: true, contentHash));

        var cache = new FeatureFlagEvaluatorCache(configuration, client, NullLogger.Instance);
        await cache.Initialize();
        var evaluator = cache.GetEvaluator();

        using var scope = new AssertionScope();
        evaluator.Should().NotBeNull();
        evaluator.ContentHash.Should().BeEquivalentTo(contentHash);
        evaluator.Evaluate(Slug, context: null).Value.Should().BeTrue();
    }

    [Fact]
    public async Task WhenInitialized_RefreshesCacheAfterCacheDurationExpires()
    {
        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        var client = new MockFeatureFlagApiClient(Response(value: true, contentHash));

        // Initialize the cache
        var cache = new FeatureFlagEvaluatorCache(configuration, client, NullLogger.Instance);
        await cache.Initialize();

        // Validate the initial state
        using var scope = new AssertionScope();
        var evaluator = cache.GetEvaluator();
        evaluator.ContentHash.Should().BeEquivalentTo(contentHash);
        evaluator.Evaluate(Slug, context: null).Value.Should().BeTrue();

        // Simulate a change in the available feature flags
        client.ChangeEvaluations(Response(value: false, [0x01, 0x02, 0x03, 0x05]));

        // Wait for the cache to expire
        await Task.Delay(TimeSpan.FromSeconds(5));

        // Validate the updated evaluations are available
        evaluator = cache.GetEvaluator();
        evaluator.ContentHash.Should().BeEquivalentTo(new byte[] { 0x01, 0x02, 0x03, 0x05 });
        evaluator.Evaluate(Slug, context: null).Value.Should().BeFalse();
    }

    [Fact]
    public async Task WhenInitialized_AndRefreshFails_RetainsTheExistingEvaluatorAndLogsError()
    {
        var logger = new FakeLogger();

        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        var client = new MockFeatureFlagApiClient(Response(value: true, contentHash));

        var cache = new FeatureFlagEvaluatorCache(configuration, client, logger);
        await cache.Initialize();

        // Simulate a failed fetch
        client.ChangeEvaluations(null);
        // Wait for the cache to expire and refresh loop to run
        await Task.Delay(TimeSpan.FromSeconds(5));

        try
        {
            var evaluator = cache.GetEvaluator();

            using var scope = new AssertionScope();
            logger.LatestRecord.Message.Should().StartWith("Failed to retrieve updated feature manifest");
            evaluator.ContentHash.Should().BeEquivalentTo(contentHash);
        }
        finally
        {
            await cache.Shutdown();
        }
    }

    [Fact]
    public async Task WhenInitialFetchReturnsNothing_AndRefreshSucceeds_TheEvaluatorIsPopulated()
    {
        var logger = new FakeLogger();

        byte[] contentHash = [0x01, 0x02, 0x03, 0x04];

        // Initialize with a null client so that first fetch fails
        var client = new MockFeatureFlagApiClient(null);
        var cache = new FeatureFlagEvaluatorCache(configuration, client, logger);
        await cache.Initialize();

        try
        {
            // Check that the evaluator is empty
            cache.GetEvaluator().ContentHash.Length.Should().Be(0);

            // Update client to return valid evaluations and wait for refresh
            client.ChangeEvaluations(Response(value: false, contentHash));
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert that the evaluator is now correctly populated
            cache.GetEvaluator().ContentHash.Should().BeEquivalentTo(contentHash);
        }
        finally
        {
            await cache.Shutdown();
        }
    }

    [Fact]
    public async Task WhenRefreshReturnsNothing_AndSubsequentRefreshSucceeds_TheEvaluatorIsUpdated()
    {
        var logger = new FakeLogger();

        byte[] initialHash = [0x01, 0x02, 0x03, 0x04];
        byte[] updatedHash = [0x01, 0x02, 0x03, 0x05];

        // Initialize with a client that returns valid evaluations
        var client = new MockFeatureFlagApiClient(Response(value: true, initialHash));
        var cache = new FeatureFlagEvaluatorCache(configuration, client, logger);
        await cache.Initialize();

        try
        {
            // Switch to a null client and wait for refresh to fail
            client.ChangeEvaluations(null);
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert that failed refresh is logged and the old evaluator is retained
            logger.LatestRecord.Message.Should().StartWith("Failed to retrieve updated feature manifest");
            cache.GetEvaluator().ContentHash.Should().BeEquivalentTo(initialHash);

            // Update client to return valid evaluations again and wait for refresh
            client.ChangeEvaluations(Response(value: false, updatedHash));
            await Task.Delay(TimeSpan.FromSeconds(5));

            // Assert that the evaluator is now correctly populated
            var evaluator = cache.GetEvaluator();
            evaluator.ContentHash.Should().BeEquivalentTo(updatedHash);
        }
        finally
        {
            await cache.Shutdown();
        }
    }

    class ThrowsOnRefreshClient(EvaluationResponse initial) : IFeatureFlagApiClient
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
        var cache = new FeatureFlagEvaluatorCache(configuration, client, logger);
        await cache.Initialize();

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
            await cache.Shutdown();
        }
    }

    class AlwaysFailsFeatureFlagApiClient : IFeatureFlagApiClient
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
        var client = new AlwaysFailsFeatureFlagApiClient();
        var logger = new FakeLogger();
        var cache = new FeatureFlagEvaluatorCache(configuration, client, logger);

        await cache.Initialize();

        using var scope = new AssertionScope();
        cache.GetEvaluator().ContentHash.Length.Should().Be(0);
        logger.LatestRecord.Level.Should().Be(LogLevel.Error);
        logger.LatestRecord.Message.Should().StartWith("Failed to retrieve feature manifest");

        await cache.Shutdown();
    }
}
