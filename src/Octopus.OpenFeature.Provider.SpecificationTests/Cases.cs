using System.Collections;
using System.Text.Json;

namespace Octopus.OpenFeature.Provider.SpecificationTests;

public interface IFixture<T>
{
    JsonElement Response { get; }
    T[] Cases { get; }
}

public abstract class Cases<TFixture, TCase>(string directory) : IEnumerable<object[]> where TFixture : IFixture<TCase>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var jsonFiles = Directory.EnumerateFiles(directory, "*.json");

        foreach (var jsonFile in jsonFiles)
        {
            var json = File.ReadAllText(jsonFile);
            var data = JsonSerializer.Deserialize<TFixture>(json, JsonSerializerOptions.Web)!;

            var responseJson = data.Response.GetRawText();

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

public class BooleanCases() : Cases<Fixture, FixtureCase>("Fixtures/boolean");

public record Fixture(
    JsonElement Response,
    FixtureCase[] Cases
) : IFixture<FixtureCase>;

public record FixtureCase(
    string Description,
    FixtureConfiguration Configuration,
    FixtureExpected Expected
);

public record FixtureConfiguration(string Slug,
    bool DefaultValue,
    Dictionary<string, string>? Context
);

public record FixtureExpected(
    bool Value,
    string? ErrorCode = null
);

public class NonBooleanCases() : Cases<NonBooleanFixture, NonBooleanFixtureCase>("Fixtures/non-boolean");

public record NonBooleanFixture(
    JsonElement Response,
    NonBooleanFixtureCase[] Cases
) : IFixture<NonBooleanFixtureCase>;

public record NonBooleanFixtureCase(
    string Description,
    NonBooleanFixtureConfiguration Configuration,
    NonBooleanFixtureExpected Expected
);

public record NonBooleanFixtureConfiguration(
    string Slug,
    string FlagType
);

public record NonBooleanFixtureExpected(
    JsonElement Value,
    string ErrorCode
);
