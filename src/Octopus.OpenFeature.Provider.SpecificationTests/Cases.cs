using System.Collections;
using System.Collections.Concurrent;
using System.Text.Json;
using Xunit.Abstractions;

namespace Octopus.OpenFeature.Provider.SpecificationTests;

public class Cases : IEnumerable<object[]>
{
    public IEnumerator<object[]> GetEnumerator()
    {
        var jsonFiles = Directory.EnumerateFiles(SpecificationCase.FixtureDirectory, "*.json").Order();

        foreach (var jsonFile in jsonFiles)
        {
            var fixture = SpecificationCase.LoadFixture(Path.GetFileName(jsonFile));

            for (var caseIndex = 0; caseIndex < fixture.Cases.Length; caseIndex++)
            {
                yield return [new SpecificationCase(Path.GetFileName(jsonFile), caseIndex)];
            }
        }
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}

/// <summary>
/// A single case within a specification fixture, identified by the fixture it came from rather than
/// by its contents. That keeps the case serialisable, so xUnit reports each one as its own test, and
/// lets <see cref="ToString" /> name the test after the fixture and the case description.
/// </summary>
public class SpecificationCase : IXunitSerializable
{
    public const string FixtureDirectory = "Fixtures";

    static readonly ConcurrentDictionary<string, Fixture> Fixtures = new();

    string fileName = string.Empty;
    int caseIndex;

    public SpecificationCase()
    {
    }

    public SpecificationCase(string fileName, int caseIndex)
    {
        this.fileName = fileName;
        this.caseIndex = caseIndex;
    }

    public string Response => LoadFixture(fileName).Response.GetRawText();

    public FixtureCase Case => LoadFixture(fileName).Cases[caseIndex];

    public static Fixture LoadFixture(string fileName) => Fixtures.GetOrAdd(fileName, static name =>
    {
        var json = File.ReadAllText(Path.Combine(FixtureDirectory, name));
        return JsonSerializer.Deserialize<Fixture>(json, JsonSerializerOptions.Web)!;
    });

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(fileName), fileName);
        info.AddValue(nameof(caseIndex), caseIndex);
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        fileName = info.GetValue<string>(nameof(fileName));
        caseIndex = info.GetValue<int>(nameof(caseIndex));
    }

    public override string ToString() => $"{Path.GetFileNameWithoutExtension(fileName)} - {Case.Description}";
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
    Dictionary<string, string>? Context
);

public record FixtureExpected(
    bool Value,
    string? ErrorCode = null
);
