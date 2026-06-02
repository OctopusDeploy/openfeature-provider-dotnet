using System.Collections;
using System.Text.Json;

namespace Octopus.OpenFeature.Provider.SpecificationTests;

public class NonBooleanCases : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var jsonFiles = Directory.EnumerateFiles("Fixtures/non-boolean", "*.json");

        foreach (var jsonFile in jsonFiles)
        {
            var json = File.ReadAllText(jsonFile);
            var data = JsonSerializer.Deserialize<NonBooleanFixture>(json, JsonSerializerOptions.Web)!;

            var responseJson = data.Response.GetRawText();

            foreach (var c in data.Cases)
            {
                yield return new object[] { responseJson, c };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
}

public record NonBooleanFixture(
    JsonElement Response,
    NonBooleanFixtureCase[] Cases
);

public record NonBooleanFixtureCase(
    string Description,
    NonBooleanFixtureConfiguration Configuration,
    NonBooleanFixtureExpected Expected
);

public record NonBooleanFixtureConfiguration(string Slug, string FlagType);

public record NonBooleanFixtureExpected(JsonElement Value, string ErrorCode);
