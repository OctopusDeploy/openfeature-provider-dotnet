using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using OpenFeature.Constant;
using OpenFeature.Model;
using WireMock.RequestBuilders;
using WireMock.ResponseBuilders;
using WireMock.Server;

namespace Octopus.OpenFeature.Provider.IntegrationTests;

public class FixtureEvaluationTests
{
    public static TheoryData<FixtureTestData> GetTestCases()
    {
        var data = new TheoryData<FixtureTestData>();

        var fixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        foreach (var dir in Directory.GetDirectories(fixtureDir))
        {
            var togglesJson = File.ReadAllText(Path.Combine(dir, "toggles.json"));
            var casesJson = File.ReadAllText(Path.Combine(dir, "cases.json"));
            var cases = JsonSerializer.Deserialize<FixtureCase[]>(casesJson, JsonSerializerOptions.Web)
                ?? throw new InvalidOperationException($"Failed to deserialize cases in {dir}");

            foreach (var testCase in cases)
            {
                data.Add(new FixtureTestData(togglesJson, testCase));
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public async Task Evaluate(FixtureTestData testData)
    {
        using var server = WireMockServer.Start();

        server
            .Given(Request.Create().WithPath("/api/featuretoggles/v3/").UsingGet())
            .RespondWith(Response.Create()
                .WithStatusCode(200)
                .WithHeader("ContentHash", Convert.ToBase64String([0x01]))
                .WithBody(testData.TogglesJson));

        var configuration = new OctopusFeatureConfiguration("test-identifier", new ProductMetadata("test-agent"))
        {
            ServerUri = new Uri(server.Url!)
        };

        var provider = new OctopusFeatureProvider(configuration);
        await provider.InitializeAsync(EvaluationContext.Empty);

        var evaluationContext = BuildContext(testData.Case.Configuration.Context);

        var result = await provider.ResolveBooleanValueAsync(
            testData.Case.Configuration.Slug,
            testData.Case.Configuration.DefaultValue,
            evaluationContext);

        await provider.ShutdownAsync();

        using var scope = new AssertionScope(testData.Case.Description);
        result.Value.Should().Be(testData.Case.Expected.Value);

        if (testData.Case.Expected.ErrorCode is { } errorCode)
        {
            result.ErrorType.Should().Be(MapErrorCode(errorCode));
        }
        else
        {
            result.ErrorType.Should().Be(ErrorType.None);
        }
    }

    static EvaluationContext? BuildContext(Dictionary<string, string>? context)
    {
        if (context is null)
        {

            return null;
        }


        var builder = EvaluationContext.Builder();
        foreach (var (key, value) in context)
        {

            builder.Set(key, value);
        }


        return builder.Build();
    }

    static ErrorType MapErrorCode(string errorCode) => errorCode switch
    {
        "FLAG_NOT_FOUND" => ErrorType.FlagNotFound,
        _ => ErrorType.General
    };
}

public class FixtureTestData(string togglesJson, FixtureCase @case)
{
    public string TogglesJson { get; } = togglesJson;
    public FixtureCase Case { get; } = @case;

    public override string ToString() => Case.Description;
}
