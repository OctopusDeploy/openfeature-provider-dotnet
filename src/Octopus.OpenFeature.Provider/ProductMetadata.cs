namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Metadata about the application using the OpenFeature provider. Used to build 
/// the User-Agent sent in HTTP requests.
/// </summary>
/// <param name="productName">The name of the product</param>
/// <param name="productVersion">The version of the product</param>
public class ProductMetadata
{
    public ProductMetadata(string productName)
    {
        ProductName = productName;
        UserAgentString = productName;
    }

    public ProductMetadata(string productName, string productVersion)
    {
        ProductName = productName;
        ProductVersion = productVersion;
        UserAgentString = $"{productName}/{productVersion}";
    }

    public string ProductName { get; }

    public string? ProductVersion { get; }

    public string UserAgentString { get; }
}
