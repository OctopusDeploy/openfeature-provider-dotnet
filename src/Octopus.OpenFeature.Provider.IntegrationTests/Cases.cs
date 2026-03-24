using System.Collections;
using System.Text.Json;

namespace Octopus.OpenFeature.Provider.IntegrationTests;

public class Cases : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var jsonFiles = Directory.EnumerateFiles("Fixtures", "*.json");

        foreach (var jsonFile in jsonFiles)
        {
            var json = File.ReadAllText(jsonFile);
            var data = JsonSerializer.Deserialize<Fixture>(json, JsonSerializerOptions.Web)!;

            var responseJson = data.Response.GetRawText();
            var file = Path.GetFileName(jsonFile);

            foreach (var c in data.Cases)
            {
                yield return new object[] { responseJson, c };
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

public record Fixture(
    JsonElement Response,
    FixtureCase[] Cases
);

public record FixtureCase(
    string Description,
    FixtureConfiguration Configuration,
    FixtureExpected Expected
);

public record FixtureConfiguration(string Slug,
    bool DefaultValue,
    Dictionary<string, string> Context
);

public record FixtureExpected(
    bool Value,
    string? ErrorCode = null
)
;