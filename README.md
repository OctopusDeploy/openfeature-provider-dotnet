# Octopus Deploy OpenFeature provider for .NET  

The OctopusDeploy .NET [OpenFeature provider
](https://openfeature.dev/docs/reference/concepts/provider/)

## About Octopus Deploy 

[Octopus Deploy](https://octopus.com) is a sophisticated, best-of-breed continuous delivery (CD) platform for modern software teams. Octopus offers powerful release orchestration, deployment automation, and runbook automation, while handling the scale, complexity and governance expectations of even the largest organizations with the most complex deployment challenges.

## Supported .NET Versions
This SDK is currently built for .NET 8.0, meaning it will run on .NET 8.0 and above. 

If you require support for additional versions, please raise an issue.

## Getting Started

### Installation

```
dotnet add package OpenFeature  
dotnet add package Octopus.OpenFeature
```

### Usage 

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

For information on using the OpenFeature client please refer to the [OpenFeature Documentation](https://docs.openfeature.dev/docs/reference/concepts/evaluation-api/).