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

public class SpecificationCase : IXunitSerializable
{
    public const string FixtureDirectory = "Fixtures";

    static readonly ConcurrentDictionary<string, Fixture> Fixtures = new();

    private string FileName { get; set; } = string.Empty;
    private int CaseIndex { get; set; }

    public SpecificationCase()
    {
    }

    public SpecificationCase(string fileName, int caseIndex)
    {
        this.FileName = fileName;
        this.CaseIndex = caseIndex;
    }

    public string Response => LoadFixture(FileName).Response.GetRawText();

    public FixtureCase Case => LoadFixture(FileName).Cases[CaseIndex];

    public static Fixture LoadFixture(string fileName) => Fixtures.GetOrAdd(
        fileName,
        static name =>
        {
            var json = File.ReadAllText(Path.Combine(FixtureDirectory, name));
            return JsonSerializer.Deserialize<Fixture>(json, JsonSerializerOptions.Web)!;
        }
    );

    public void Serialize(IXunitSerializationInfo info)
    {
        info.AddValue(nameof(FileName), FileName);
        info.AddValue(nameof(CaseIndex), CaseIndex);
    }

    public void Deserialize(IXunitSerializationInfo info)
    {
        FileName = info.GetValue<string>(nameof(FileName));
        CaseIndex = info.GetValue<int>(nameof(CaseIndex));
    }

    public override string ToString() => $"{Path.GetFileNameWithoutExtension(FileName)} - {Case.Description}";
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

public record FixtureConfiguration(
    string Slug,
    bool DefaultValue,
    Dictionary<string, string>? Context
);

public record FixtureExpected(
    bool Value,
    string? ErrorCode = null
);
