using FluentAssertions;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureProviderTests
{
    readonly OctopusFeatureConfiguration configuration = new("identifier", new ProductMetadata("test-agent"));

    class FakeOctopusFeatureClient(EvaluationResponse? evaluationResponse) : IOctopusFeatureClient
    {
        public Task<bool> HaveFeaturesChanged(byte[] contentHash, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<EvaluationResponse?> GetServerSideEvaluations(CancellationToken cancellationToken) => Task.FromResult(evaluationResponse);
    }

    static ServerSideEvaluation Flag(string slug)
        => new(slug, value: true, reason: "The flag is enabled for this environment.", evaluationKey: null, rules: null);

    static EvaluationResponse Response(params ServerSideEvaluation[] evaluations) => new(evaluations, []);

    async Task<FeatureClient> CreateClientWith(EvaluationResponse evaluationResponse)
    {
        var provider = new OctopusFeatureProvider(configuration, new FakeOctopusFeatureClient(evaluationResponse));
        await Api.Instance.SetProviderAsync(provider);
        return Api.Instance.GetClient();
    }

    [Fact]
    public async Task GetStringDetailsAsync_WhenFlagExists_ReturnTypeMismatchError()
    {
        var client = await CreateClientWith(Response(Flag("my-flag")));

        var result = await client.GetStringDetailsAsync("my-flag", "default");
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.TypeMismatch);
    }

    [Fact]
    public async Task GetStringDetailsAsync_WhenFlagDoesNotExist_ReturnsFlagNotFoundError()
    {
        var client = await CreateClientWith(Response());

        var result = await client.GetStringDetailsAsync("unknown-flag", "default");
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
    }

    [Fact]
    public async Task GetIntegerDetailsAsync_WhenFlagExists_ReturnTypeMismatchError()
    {
        var client = await CreateClientWith(Response(Flag("my-flag")));

        var result = await client.GetIntegerDetailsAsync("my-flag", 0);
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.TypeMismatch);
    }

    [Fact]
    public async Task GetIntegerDetailsAsync_WhenFlagDoesNotExist_ReturnsFlagNotFoundError()
    {
        var client = await CreateClientWith(Response());

        var result = await client.GetIntegerDetailsAsync("unknown-flag", 0);
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
    }

    [Fact]
    public async Task GetDoubleDetailsAsync_WhenFlagExists_ReturnTypeMismatchError()
    {
        var client = await CreateClientWith(Response(Flag("my-flag")));

        var result = await client.GetDoubleDetailsAsync("my-flag", 0.0);
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.TypeMismatch);
    }

    [Fact]
    public async Task GetDoubleDetailsAsync_WhenFlagDoesNotExist_ReturnsFlagNotFoundError()
    {
        var client = await CreateClientWith(Response());

        var result = await client.GetDoubleDetailsAsync("unknown-flag", 0.0);
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
    }

    [Fact]
    public async Task GetObjectDetailsAsync_WhenFlagExists_ReturnTypeMismatchError()
    {
        var client = await CreateClientWith(Response(Flag("my-flag")));

        var result = await client.GetObjectDetailsAsync("my-flag", new Value());
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.TypeMismatch);
    }

    [Fact]
    public async Task GetObjectDetailsAsync_WhenFlagDoesNotExist_ReturnsFlagNotFoundError()
    {
        var client = await CreateClientWith(Response());

        var result = await client.GetObjectDetailsAsync("unknown-flag", new Value());
        await Api.Instance.ShutdownAsync();

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
    }
}
