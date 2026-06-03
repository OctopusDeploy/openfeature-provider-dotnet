using FluentAssertions;
using OpenFeature.Error;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureProviderTests
{
    readonly OctopusFeatureConfiguration configuration = new("identifier", new ProductMetadata("test-agent"));

    class FakeOctopusFeatureClient(FeatureToggles? featureToggles) : IOctopusFeatureClient
    {
        public Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<FeatureToggles?> GetFeatureToggleEvaluationManifest(CancellationToken cancellationToken) => Task.FromResult(featureToggles);
    }

    async Task<OctopusFeatureProvider> CreateInitializedProvider(FeatureToggles toggles)
    {
        var provider = new OctopusFeatureProvider(configuration, new FakeOctopusFeatureClient(toggles));
        await provider.InitializeAsync(EvaluationContext.Builder().Build());
        return provider;
    }

    [Fact]
    public async Task ResolveStringValueAsync_WhenFlagExists_ThrowsTypeMismatchException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles(
            [new FeatureToggleEvaluation("my-flag", true, "key", [], 100)], []));

        var act = () => provider.ResolveStringValueAsync("my-flag", "default");

        await act.Should().ThrowAsync<TypeMismatchException>();
        await provider.ShutdownAsync();
    }

    [Fact]
    public async Task ResolveStringValueAsync_WhenFlagDoesNotExist_ThrowsFlagNotFoundException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles([], []));

        var act = () => provider.ResolveStringValueAsync("unknown-flag", "default");

        await act.Should().ThrowAsync<FlagNotFoundException>();
        await provider.ShutdownAsync();
    }

    [Fact]
    public async Task ResolveIntegerValueAsync_WhenFlagExists_ThrowsTypeMismatchException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles(
            [new FeatureToggleEvaluation("my-flag", true, "key", [], 100)], []));

        var act = () => provider.ResolveIntegerValueAsync("my-flag", 0);

        await act.Should().ThrowAsync<TypeMismatchException>();
        await provider.ShutdownAsync();
    }

    [Fact]
    public async Task ResolveIntegerValueAsync_WhenFlagDoesNotExist_ThrowsFlagNotFoundException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles([], []));

        var act = () => provider.ResolveIntegerValueAsync("unknown-flag", 0);

        await act.Should().ThrowAsync<FlagNotFoundException>();
        await provider.ShutdownAsync();
    }

    [Fact]
    public async Task ResolveDoubleValueAsync_WhenFlagExists_ThrowsTypeMismatchException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles(
            [new FeatureToggleEvaluation("my-flag", true, "key", [], 100)], []));

        var act = () => provider.ResolveDoubleValueAsync("my-flag", 0.0);

        await act.Should().ThrowAsync<TypeMismatchException>();
        await provider.ShutdownAsync();
    }

    [Fact]
    public async Task ResolveDoubleValueAsync_WhenFlagDoesNotExist_ThrowsFlagNotFoundException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles([], []));

        var act = () => provider.ResolveDoubleValueAsync("unknown-flag", 0.0);

        await act.Should().ThrowAsync<FlagNotFoundException>();
        await provider.ShutdownAsync();
    }

    [Fact]
    public async Task ResolveStructureValueAsync_WhenFlagExists_ThrowsTypeMismatchException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles(
            [new FeatureToggleEvaluation("my-flag", true, "key", [], 100)], []));

        var act = () => provider.ResolveStructureValueAsync("my-flag", new Value());

        await act.Should().ThrowAsync<TypeMismatchException>();
        await provider.ShutdownAsync();
    }

    [Fact]
    public async Task ResolveStructureValueAsync_WhenFlagDoesNotExist_ThrowsFlagNotFoundException()
    {
        var provider = await CreateInitializedProvider(new FeatureToggles([], []));

        var act = () => provider.ResolveStructureValueAsync("unknown-flag", new Value());

        await act.Should().ThrowAsync<FlagNotFoundException>();
        await provider.ShutdownAsync();
    }
}
