# openfeature-dotnet

The OctopusDeploy dotnet [OpenFeature provider
](https://openfeature.dev/docs/reference/concepts/provider/)

# Usage

```c#
var clientIdentifier = Environment.GetEnvironmentVariable("Octopus__Features__ClientIdentifier");

var provider = new OctopusFeatureProvider(new OctopusFeatureConfiguration(clientIdentifier));

await OpenFeature.Api.Instance.SetProviderAsync(provider);

var client = OpenFeature.Api.Instance.GetClient();

if (await client.GetBooleanValue("to-the-moon-feature", false))
{
  Console.WriteLine("🚀🚀🚀");
}
```
