using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using OpenFeature.Constant;
using OpenFeature.Model;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Octopus.OpenFeature.Provider.IntegrationTests;

public class FixtureEvaluationTests(Server server) : IClassFixture<Server>
{
    [Theory]
    [ClassData(typeof(Cases))]
    public async Task Evaluate(string testResponse, FixtureCase testCase)
    {
        var clientIdentifier = server.Configure(testResponse);
        var configuration = new OctopusFeatureConfiguration(clientIdentifier, new ProductMetadata("test-agent"))
        {
            ServerUri = new Uri(server.Url)
        };

        var provider = new OctopusFeatureProvider(configuration);
        await provider.InitializeAsync(BuildContext(testCase.Configuration.Context));

        // Act
        var result = await provider.ResolveBooleanValueAsync(
            testCase.Configuration.Slug,
            testCase.Configuration.DefaultValue
        );
        await provider.ShutdownAsync();

        // Assert
        using var scope = new AssertionScope(testCase.Description);
        result.Value.Should().Be(testCase.Expected.Value);
        result.ErrorType.Should().Be(MapErrorCode(testCase.Expected.ErrorCode));
    }

    static EvaluationContext BuildContext(Dictionary<string, string> context)
    {
        var builder = EvaluationContext.Builder();
        foreach (var (key, value) in context)
        {
            builder.Set(key, value);
        }

        return builder.Build();
    }

    static ErrorType MapErrorCode(string? errorCode) => errorCode switch
    {
        "FLAG_NOT_FOUND" => ErrorType.FlagNotFound,
        null => ErrorType.None,
        _ => ErrorType.General
    };
}
