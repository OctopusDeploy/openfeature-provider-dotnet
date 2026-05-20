using System.Text.RegularExpressions;

namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Metadata about the application using the OpenFeature provider.
/// Used to populate header for telemetry.
/// </summary>
public class ProductMetadata
{
    // https://www.rfc-editor.org/rfc/rfc9110.html#name-tokens
    private static readonly Regex UnsupportedChars = new("[^a-zA-Z0-9!#$%&'*+-.^_`|~]", RegexOptions.Compiled);

    private string ProductName { get; }
    private string? ProductVersion { get; }

    internal string CleanProductName => UnsupportedChars.Replace(ProductName, "");
    internal string? CleanProductVersion => ProductVersion == null ? null : UnsupportedChars.Replace(ProductVersion, "");

    /// <summary>
    /// Construct a ProductMetadata with the product name only
    /// </summary>
    /// <param name="productName">The name of the product</param>
    public ProductMetadata(string productName)
    {
        ProductName = productName;
        ProductVersion = null;

        if (CleanProductName.Length == 0)
        {
            throw new ArgumentException("Product name must contain at least one valid token character.");
        }
    }

    /// <summary>
    /// Construct a ProductMetadata with the product name and version
    /// </summary>
    /// <param name="productName">The name of the product</param>
    /// <param name="productVersion">The version of the product</param>
    public ProductMetadata(string productName, string productVersion)
    {
        ProductName = productName;
        ProductVersion = productVersion;

        if (CleanProductName.Length == 0)
        {
            throw new ArgumentException("Product name must contain at least one valid token character.");
        }

        if (CleanProductVersion?.Length == 0)
        {
            throw new ArgumentException("Product version must contain at least one valid token character.");
        }
    }
}
