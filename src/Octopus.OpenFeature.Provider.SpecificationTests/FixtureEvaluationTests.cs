using FluentAssertions;
using FluentAssertions.Execution;
using OpenFeature;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.SpecificationTests;

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

        await Api.Instance.SetProviderAsync(new OctopusFeatureProvider(configuration));
        Api.Instance.SetContext(BuildContext(testCase.Configuration.Context));
        var client = Api.Instance.GetClient();

        // Act
        var result = await client.GetBooleanDetailsAsync(testCase.Configuration.Slug, testCase.Configuration.DefaultValue);
        await Api.Instance.ShutdownAsync();

        // Assert
        using var scope = new AssertionScope(testCase.Description);
        result.Value.Should().Be(testCase.Expected.Value);
        result.ErrorType.Should().Be(MapErrorCode(testCase.Expected.ErrorCode));
    }

    static EvaluationContext BuildContext(Dictionary<string, string>? context)
    {
        var builder = EvaluationContext.Builder();

        context ??= [];
        foreach (var (key, value) in context)
        {
            builder.Set(key, value);
        }

        return builder.Build();
    }

    static ErrorType MapErrorCode(string? errorCode) => errorCode switch
    {
        "PROVIDER_NOT_READY" => ErrorType.ProviderNotReady,
        "FLAG_NOT_FOUND" => ErrorType.FlagNotFound,
        "PARSE_ERROR" => ErrorType.ParseError,
        "TYPE_MISMATCH" => ErrorType.TypeMismatch,
        "TARGETING_KEY_MISSING" => ErrorType.TargetingKeyMissing,
        "INVALID_CONTEXT" => ErrorType.InvalidContext,
        "PROVIDER_FATAL" => ErrorType.ProviderFatal,
        "GENERAL" => ErrorType.General,
        null => ErrorType.None,
        _ => throw new ArgumentException($"Unknown error code: {errorCode}")
    };
}
