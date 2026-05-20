using FluentAssertions;

namespace Octopus.OpenFeature.Provider.Tests;

public class ProductMetadataTests
{
    [Fact]
    public void CleanProductName_WithValidChars_ReturnsNameUnchanged()
    {
        var metadata = new ProductMetadata("OctopusDeploy");

        metadata.CleanProductName.Should().Be("OctopusDeploy");
    }

    [Fact]
    public void CleanProductName_WithCommonUnsupportedChars_StripsThemOut()
    {
        // Characters that may be used but are not RFC 9110 tchars
        var metadata = new ProductMetadata("My ,Product (v2.0)/release@2024:final");

        metadata.CleanProductName.Should().Be("MyProductv2.0release2024final");
    }

    [Fact]
    public void CleanProductName_WithHyphen_PreservesIt()
    {
        var metadata = new ProductMetadata("My-Product");

        metadata.CleanProductName.Should().Be("My-Product");
    }

    [Fact]
    public void CleanProductVersion_WhenNoVersionProvided_ReturnsNull()
    {
        var metadata = new ProductMetadata("MyProduct");

        metadata.CleanProductVersion.Should().BeNull();
    }

    [Fact]
    public void CleanProductVersion_WithValidChars_ReturnsVersionUnchanged()
    {
        var metadata = new ProductMetadata("MyProduct", "2024.1.0");

        metadata.CleanProductVersion.Should().Be("2024.1.0");
    }

    [Fact]
    public void CleanProductVersion_WithUnsupportedChars_StripsThemOut()
    {
        var metadata = new ProductMetadata("MyProduct", "2024.1 (beta)");

        metadata.CleanProductVersion.Should().Be("2024.1beta");
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
