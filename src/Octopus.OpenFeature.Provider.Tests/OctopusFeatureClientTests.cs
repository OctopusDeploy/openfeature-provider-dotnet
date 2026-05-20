using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureClientTests
{
    [Fact]
    public void AddOctopusClientHeader_SetsXOctopusClientHeader()
    {
        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("MyProduct"));
        var client = new OctopusFeatureClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();

        client.AddOctopusClientHeader(httpClient);

        httpClient.DefaultRequestHeaders.Should().ContainKey("X-Octopus-Client");
    }

    [Fact]
    public void AddOctopusClientHeader_WithNameOnly_HeaderContainsProductNameAndProviderInformation()
    {
        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("MyProduct"));
        var client = new OctopusFeatureClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();
        var expectedVersion = typeof(OctopusFeatureClient).Assembly.GetName().Version?.ToString(3);

        client.AddOctopusClientHeader(httpClient);

        var headerValue = httpClient.DefaultRequestHeaders.GetValues("X-Octopus-Client").Single();
        headerValue.Should().Be($"MyProduct openfeature-provider-dotnet/{expectedVersion}");
    }

    [Fact]
    public void AddOctopusClientHeader_WithNameAndVersion_HeaderContainsProductAndProviderInformation()
    {
        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("MyProduct", "2024.1.0"));
        var client = new OctopusFeatureClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();
        var expectedVersion = typeof(OctopusFeatureClient).Assembly.GetName().Version?.ToString(3);

        client.AddOctopusClientHeader(httpClient);

        var headerValue = httpClient.DefaultRequestHeaders.GetValues("X-Octopus-Client").Single();
        headerValue.Should().Be($"MyProduct/2024.1.0 openfeature-provider-dotnet/{expectedVersion}");
    }

    [Fact]
    public void AddOctopusClientHeader_WithNameContainingUnsupportedChars_StripsCharsFromHeader()
    {
        // Note: More character checking tests are in ProductMetadataTests.cs
        
        var config = new OctopusFeatureConfiguration("test-id", new ProductMetadata("My Product"));
        var client = new OctopusFeatureClient(config, NullLogger.Instance);
        var httpClient = new HttpClient();

        client.AddOctopusClientHeader(httpClient);

        var headerValue = httpClient.DefaultRequestHeaders.GetValues("X-Octopus-Client").Single();
        headerValue.Should().StartWith("MyProduct ");
    }
}
