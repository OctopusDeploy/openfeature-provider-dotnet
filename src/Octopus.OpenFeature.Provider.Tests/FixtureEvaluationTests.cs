using System.Text.Json;
using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests;

public class FixtureEvaluationTests
{
    public static TheoryData<FixtureTestData> GetTestCases()
    {
        var data = new TheoryData<FixtureTestData>();

        var fixtureDir = Path.Combine(AppContext.BaseDirectory, "Fixtures");
        foreach (var file in Directory.GetFiles(fixtureDir, "*.json"))
        {
            var json = File.ReadAllText(file);
            var fixture = JsonSerializer.Deserialize<FixtureFile>(json, JsonSerializerOptions.Web)
                ?? throw new InvalidOperationException($"Failed to deserialize fixture file: {file}");

            foreach (var testCase in fixture.Cases)
            {
                data.Add(new FixtureTestData(fixture.Toggles, testCase));
            }

        }

        return data;
    }

    [Theory]
    [MemberData(nameof(GetTestCases))]
    public void Evaluate(FixtureTestData testData)
    {
        var toggles = new FeatureToggles(testData.Toggles, []);
        var featureContext = new OctopusFeatureContext(toggles, NullLoggerFactory.Instance);

        var evaluationContext = BuildContext(testData.Case.Configuration.Context);

        var result = featureContext.Evaluate(
            testData.Case.Configuration.Slug,
            testData.Case.Configuration.DefaultValue,
            evaluationContext);

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

public class FixtureTestData(FeatureToggleEvaluation[] toggles, FixtureCase @case)
{
    public FeatureToggleEvaluation[] Toggles { get; } = toggles;
    public FixtureCase Case { get; } = @case;

    // Controls the name shown in Test Explorer for each theory row
    public override string ToString() => Case.Description;
}
