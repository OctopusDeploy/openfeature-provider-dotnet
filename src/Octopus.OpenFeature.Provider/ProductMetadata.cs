using System.Text.RegularExpressions;

namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Metadata about the application using the OpenFeature provider.
/// Used to populate header for telemetry.
/// </summary>
public class ProductMetadata
{
    // https://www.rfc-editor.org/rfc/rfc9110.html#name-tokens
    private static readonly Regex UnsupportedChars = new("[^a-zA-Z0-9!#$%&'*+\\-.^_`|~]", RegexOptions.Compiled);

    public string Name { get; }
    public string? Version { get; }

    /// <summary>
    /// Construct a ProductMetadata with the product name only.
    /// </summary>
    /// <param name="name">The name of the product. Unsupported characters will be removed.</param>
    public ProductMetadata(string name)
    {
        Name = Clean(name);
        Version = null;

        ValidateName();
    }

    /// <summary>
    /// Construct a ProductMetadata with the product name and version.
    /// </summary>
    /// <param name="name">The name of the product. Unsupported characters will be removed.</param>
    /// <param name="version">The version of the product. Unsupported characters will be removed.</param>
    public ProductMetadata(string name, string version)
    {
        Name = Clean(name);
        Version = Clean(version);

        ValidateName();
        ValidateVersion();
    }

    private static string Clean(string value) => UnsupportedChars.Replace(value, "");

    private void ValidateName()
    {
        if (Name.Length == 0)
        {
            throw new ArgumentException("Product name must contain at least one valid token character.");
        }
    }

    private void ValidateVersion()
    {
        if (Version?.Length == 0)
        {
            throw new ArgumentException("Product version must contain at least one valid token character.");
        }
    }
}
