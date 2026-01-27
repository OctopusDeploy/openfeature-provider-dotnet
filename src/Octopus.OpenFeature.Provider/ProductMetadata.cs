using System.Net.Http.Headers;

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
        ProductHeaderValue = new ProductHeaderValue(productName);
    }

    public ProductMetadata(string productName, string productVersion)
    {
        ProductHeaderValue = new ProductHeaderValue(productName, productVersion);
    }

    public ProductHeaderValue ProductHeaderValue { get; }
}
