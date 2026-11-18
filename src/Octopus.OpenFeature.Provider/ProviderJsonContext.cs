using System.Text.Json;
using System.Text.Json.Serialization;
using Octopus.OpenFeature.Provider.V4;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider;

/// <summary>
/// Source-generated serialization metadata for every type the provider reads from the Octopus API. Using it instead of
/// reflection keeps the provider working when reflection-based serialization is disabled, as it is under Native AOT
/// and trimming. The web defaults match the <see cref="JsonSerializerOptions.Web" /> options used previously.
/// </summary>
[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ServerSideEvaluation[]))]
[JsonSerializable(typeof(FeatureFlagApiClient.FeatureCheck))]
[JsonSerializable(typeof(PercentageByContextCondition))]
[JsonSerializable(typeof(ContextAttributeIsOneOfCondition))]
[JsonSerializable(typeof(ContextAttributeIsNotOneOfCondition))]
internal sealed partial class ProviderJsonContext : JsonSerializerContext;
