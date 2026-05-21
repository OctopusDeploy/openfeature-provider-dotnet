using FluentAssertions;

namespace Octopus.OpenFeature.Provider.Tests;

public class ProductMetadataTests
{
    [Fact]
    public void Constructor_WithValidNameChars_SetsNameUnchanged()
    {
        var metadata = new ProductMetadata("OctopusDeploy");

        metadata.Name.Should().Be("OctopusDeploy");
    }

    [Fact]
    public void Constructor_WithCommonUnsupportedCharsInName_StripsThemOut()
    {
        // Characters that may be used but are not RFC 9110 tchars
        var metadata = new ProductMetadata("My ,Product (v2.0)/release@2024:final");

        metadata.Name.Should().Be("MyProductv2.0release2024final");
    }

    [Fact]
    public void Constructor_WithHyphenInName_PreservesIt()
    {
        var metadata = new ProductMetadata("My-Product");

        metadata.Name.Should().Be("My-Product");
    }

    [Fact]
    public void Constructor_WhenNoVersionProvided_SetsNull()
    {
        var metadata = new ProductMetadata("MyProduct");

        metadata.Version.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithValidCharsInVersion_SetsVersionUnchanged()
    {
        var metadata = new ProductMetadata("MyProduct", "2024.1.0");

        metadata.Version.Should().Be("2024.1.0");
    }

    [Fact]
    public void Constructor_WithUnsupportedCharsInVersion_StripsThemOut()
    {
        var metadata = new ProductMetadata("MyProduct", "2024.1 (beta)");

        metadata.Version.Should().Be("2024.1beta");
    }

    [Fact]
    public void Constructor_WhenNameBecomesEmptyAfterCleaning_ThrowsArgumentException()
    {
        var act = () => new ProductMetadata("   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Product name*");
    }

    [Fact]
    public void Constructor_WhenVersionBecomesEmptyAfterCleaning_ThrowsArgumentException()
    {
        var act = () => new ProductMetadata("MyProduct", "   ");

        act.Should().Throw<ArgumentException>()
            .WithMessage("*Product version*");
    }
}
