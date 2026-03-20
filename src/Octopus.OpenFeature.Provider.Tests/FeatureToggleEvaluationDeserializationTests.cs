using System.Text.Json;
using FluentAssertions;

namespace Octopus.OpenFeature.Provider.Tests;

public class FeatureToggleEvaluationDeserializationTests
{
    [Fact]
    public void ShouldDeserializeEnabledToggle()
    {
        var json = """
                   {
                       "name": "My Feature",
                       "slug": "my-feature",
                       "isEnabled": true,
                       "segments": []
                   }
                   """;

        var result = JsonSerializer.Deserialize<FeatureToggleEvaluation>(json, JsonSerializerOptions.Web);

        result!.Name.Should().Be("My Feature");
        result.Slug.Should().Be("my-feature");
        result.IsEnabled.Should().BeTrue();
        result.Segments.Should().BeEmpty();
    }

    [Fact]
    public void ShouldDeserializeDisabledToggle()
    {
        var json = """
                   {"name":"My Feature","slug":"my-feature","isEnabled":false,"segments":[]}
                   """;

        var result = JsonSerializer.Deserialize<FeatureToggleEvaluation>(json, JsonSerializerOptions.Web);

        result!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void ShouldDeserializeToggleWithMissingSegmentsFieldAsNull()
    {
        var json = """
                   {"name":"My Feature","slug":"my-feature","isEnabled":true}
                   """;

        var result = JsonSerializer.Deserialize<FeatureToggleEvaluation>(json, JsonSerializerOptions.Web);

        result!.Segments.Should().BeNull();
    }

    [Fact]
    public void ShouldDeserializeToggleWithSegments()
    {
        var json = """
                   {"name":"My Feature","slug":"my-feature","isEnabled":true,"segments":[{"key":"license-type","value":"free"},{"key":"country","value":"au"}]}
                   """;

        var result = JsonSerializer.Deserialize<FeatureToggleEvaluation>(json, JsonSerializerOptions.Web);

        result!.Segments.Should().HaveCount(2).And.Contain([
            new KeyValuePair<string, string>("license-type", "free"),
            new KeyValuePair<string, string>("country", "au")
        ]);
    }

    [Fact]
    public void ShouldDeserializeArrayOfToggles()
    {
        var json = """
                   [
                     {"name":"Feature A","slug":"feature-a","isEnabled":true,"segments":[]},
                     {"name":"Feature B","slug":"feature-b","isEnabled":false,"segments":[]}
                   ]
                   """;

        var result = JsonSerializer.Deserialize<FeatureToggleEvaluation[]>(json, JsonSerializerOptions.Web);

        result.Should().HaveCount(2);
        result![0].Slug.Should().Be("feature-a");
        result[0].IsEnabled.Should().BeTrue();
        result[1].Slug.Should().Be("feature-b");
        result[1].IsEnabled.Should().BeFalse();
    }
    
    [Fact]
    public void ShouldIgnoreExtraneousProperties()
    {
        var json = """
                   {
                       "name": "My Feature",
                       "slug": "my-feature",
                       "isEnabled": true,
                       "segments": [],
                       "foo": "bar",
                       "qux": 123,
                       "wux": {
                           "nested": "value"
                       }
                   }
                   """;

        var result = JsonSerializer.Deserialize<FeatureToggleEvaluation>(json, JsonSerializerOptions.Web);

        result!.Name.Should().Be("My Feature");
        result.Slug.Should().Be("my-feature");
        result.IsEnabled.Should().BeTrue();
        result.Segments.Should().BeEmpty();
    }
}
