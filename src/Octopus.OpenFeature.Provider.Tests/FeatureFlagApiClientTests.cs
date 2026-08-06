using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Octopus.OpenFeature.Provider.Tests;

public class FeatureFlagApiClientTests
{
    [Fact]
    public void AddOctopusClientHeader_SetsXOctopusClientHeader()
    {
        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("MyProduct"));
        var client = new FeatureFlagApiClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();

        client.AddOctopusClientHeader(httpClient);

        httpClient.DefaultRequestHeaders.Should().ContainKey("X-Octopus-Client");
    }

    [Fact]
    public void AddOctopusClientHeader_WithNameOnly_HeaderContainsProductNameAndProviderInformation()
    {
        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("MyProduct"));
        var client = new FeatureFlagApiClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();
        var expectedVersion = typeof(FeatureFlagApiClient).Assembly.GetName().Version?.ToString(3);

        client.AddOctopusClientHeader(httpClient);

        var headerValue = httpClient.DefaultRequestHeaders.GetValues("X-Octopus-Client").Single();
        headerValue.Should().Be($"MyProduct openfeature-provider-dotnet/{expectedVersion}");
    }

    [Fact]
    public void AddOctopusClientHeader_WithNameAndVersion_HeaderContainsProductAndProviderInformation()
    {
        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("MyProduct", "2024.1.0"));
        var client = new FeatureFlagApiClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();
        var expectedVersion = typeof(FeatureFlagApiClient).Assembly.GetName().Version?.ToString(3);

        client.AddOctopusClientHeader(httpClient);

        var headerValue = httpClient.DefaultRequestHeaders.GetValues("X-Octopus-Client").Single();
        headerValue.Should().Be($"MyProduct/2024.1.0 openfeature-provider-dotnet/{expectedVersion}");
    }

    [Fact]
    public void AddOctopusClientHeader_WithNameContainingUnsupportedChars_StripsCharsFromHeader()
    {
        // Note: More character checking tests are in ProductMetadataTests.cs

        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("My Product"));
        var client = new FeatureFlagApiClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();
        var expectedVersion = typeof(FeatureFlagApiClient).Assembly.GetName().Version?.ToString(3);

        client.AddOctopusClientHeader(httpClient);

        var headerValue = httpClient.DefaultRequestHeaders.GetValues("X-Octopus-Client").Single();
        headerValue.Should().Be($"MyProduct openfeature-provider-dotnet/{expectedVersion}");
    }

    const string CheckPath = "/api/feature-flags/check/v4/";
    const string EvaluationsPath = "/api/feature-flags/evaluations/v4/";

    static FeatureFlagApiClient ClientFor(WireMockServer server)
    {
        var configuration = new OctopusFeatureConfiguration("test-id", new ProductMetadata("MyProduct"))
        {
            ServerUri = new Uri(server.Url!)
        };

        return new FeatureFlagApiClient(configuration, NullLogger.Instance);
    }

    static IEnumerable<string> RequestedPaths(WireMockServer server)
        => server.LogEntries.Select(entry => entry.RequestMessage!.Path);

    [Fact]
    public async Task HaveFeaturesChanged_RequestsTheV4CheckEndpoint()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath(CheckPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"contentHash":"{{Convert.ToBase64String([0x01, 0x02])}}"}"""));

        var haveFeaturesChanged = await ClientFor(server).HaveFeaturesChanged([0x03, 0x04], CancellationToken.None);

        using var scope = new AssertionScope();
        RequestedPaths(server).Should().Equal(CheckPath);
        haveFeaturesChanged.Should().BeTrue();
    }

    [Fact]
    public async Task HaveFeaturesChanged_WhenTheContentHashIsUnchanged_ReportsNoChange()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath(CheckPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithBody($$"""{"contentHash":"{{Convert.ToBase64String([0x01, 0x02])}}"}"""));

        var haveFeaturesChanged = await ClientFor(server).HaveFeaturesChanged([0x01, 0x02], CancellationToken.None);

        haveFeaturesChanged.Should().BeFalse();
    }

    [Fact]
    public async Task GetServerSideEvaluations_RequestsTheV4EvaluationsEndpoint()
    {
        using var server = WireMockServer.Start();
        server
            .Given(Request.Create().WithPath(EvaluationsPath).UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("Content-Type", "application/json")
                .WithHeader("ContentHash", Convert.ToBase64String([0x01, 0x02]))
                .WithBody("""[{"slug":"test-feature","value":true,"reason":"The flag is enabled for this environment."}]"""));

        var evaluationResponse = await ClientFor(server).GetServerSideEvaluations(CancellationToken.None);

        using var scope = new AssertionScope();
        RequestedPaths(server).Should().Equal(EvaluationsPath);
        evaluationResponse.Should().NotBeNull();
        evaluationResponse!.ContentHash.Should().Equal([0x01, 0x02]);
        evaluationResponse.Evaluations.Select(evaluation => evaluation.Slug).Should().Equal("test-feature");
    }
}
