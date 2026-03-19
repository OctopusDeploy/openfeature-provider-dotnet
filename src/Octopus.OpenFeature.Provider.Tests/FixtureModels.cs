namespace Octopus.OpenFeature.Provider.Tests;

public record FixtureFile(
    FeatureToggleEvaluation[] Toggles,
    FixtureCase[] Cases);

public record FixtureCase(
    string Description,
    FixtureConfiguration Configuration,
    FixtureExpected Expected);

public record FixtureConfiguration(
    string Slug,
    Dictionary<string, string>? Context,
    bool DefaultValue);

public record FixtureExpected(
    bool Value,
    string? ErrorCode = null);
