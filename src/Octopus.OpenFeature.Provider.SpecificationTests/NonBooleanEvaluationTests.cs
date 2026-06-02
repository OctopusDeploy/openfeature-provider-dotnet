using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using OpenFeature;
using OpenFeature.Constant;

namespace Octopus.OpenFeature.Provider.SpecificationTests;

[Collection("OpenFeatureApi")]
public class NonBooleanEvaluationTests(Server server) : IClassFixture<Server>
{
    [Theory]
    [ClassData(typeof(NonBooleanCases))]
    public async Task Evaluate(string testResponse, NonBooleanFixtureCase testCase)
    {
        var clientIdentifier = server.Configure(testResponse);
        var configuration = new OctopusFeatureConfiguration(clientIdentifier, new ProductMetadata("test-agent"))
        {
            ServerUri = new Uri(server.Url)
        };

        await Api.Instance.SetProviderAsync(new OctopusFeatureProvider(configuration));
        var client = Api.Instance.GetClient();

        var (errorType, value) = await EvaluateFlag(client, testCase.Configuration.Slug, testCase.Configuration.FlagType, testCase.Expected.Value);
        await Api.Instance.ShutdownAsync();

        using var scope = new AssertionScope(testCase.Description);
        errorType.Should().Be(MapErrorCode(testCase.Expected.ErrorCode));
        value.Should().Be(testCase.Expected.Value.ToString());
    }

    static async Task<(ErrorType, string)> EvaluateFlag(FeatureClient client, string slug, string flagType, JsonElement expectedValue) =>
        flagType switch
        {
            "string" => await client.GetStringDetailsAsync(slug, expectedValue.GetString()!) is var r
                ? (r.ErrorType, r.Value)
                : default,
            "integer" => await client.GetIntegerDetailsAsync(slug, expectedValue.GetInt32()) is var r
                ? (r.ErrorType, r.Value.ToString())
                : default,
            "double" => await client.GetDoubleDetailsAsync(slug, expectedValue.GetDouble()) is var r
                ? (r.ErrorType, r.Value.ToString())
                : default,
            _ => throw new ArgumentException($"Unknown flag type: {flagType}")
        };

    static ErrorType MapErrorCode(string errorCode) => errorCode switch
    {
        "FLAG_NOT_FOUND" => ErrorType.FlagNotFound,
        "TYPE_MISMATCH" => ErrorType.TypeMismatch,
        _ => throw new ArgumentException($"Unknown error code: {errorCode}")
    };
}
