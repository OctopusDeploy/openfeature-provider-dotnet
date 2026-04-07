using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging.Abstractions;
using OpenFeature.Constant;
using OpenFeature.Model;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextTests
{
    [Fact]
    public void EvaluatesToTrue_IfFeatureIsContainedWithinTheSet_AndFeatureIsEnabled()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("test-feature", false, context: null);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public void WhenEvaluatedWithCasingDifferences_EvaluationIsInsensitiveToCase()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("Test-Feature", false, context: null);

        result.Value.Should().BeTrue();
    }

    [Fact]
    public void EvaluatesToFalse_IfFeatureIsContainedWithinTheSet_AndFeatureIsNotEnabled()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", false, "evaluation-key", [], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("test-feature", false, context: null);

        result.Value.Should().BeFalse();
    }

    [Fact]
    public void GivenAFlagKeyThatIsNotASlug_ReturnsFlagNotFound_AndEvaluatesToDefaultValue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("this-is-clearly-not-a-slug", true, "evaluation-key", [], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("This is clearly not a slug!", defaultValue: true, context: null);

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
        result.Value.Should().BeTrue();
    }

    [Fact]
    public void EvaluatesToDefaultValue_IfFeatureIsNotContainedWithinSet()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var result = context.Evaluate("anotherfeature", defaultValue: true, context: null);

        result.ErrorType.Should().Be(ErrorType.FlagNotFound);
        result.Value.Should().BeTrue();
    }

    EvaluationContext BuildContext(IEnumerable<(string key, string value)> values, string? targetingKey = null)
    {
        var builder = EvaluationContext.Builder();
        foreach (var (key, value) in values)
        {
            builder.Set(key, value);
        }
        if (targetingKey != null)
        {
            builder.SetTargetingKey(targetingKey);
        }
        return builder.Build();
    }

    [Fact]
    public void
        WhenAFeatureIsToggledOnForASpecificSegment_EvaluatesToTrueWhenSegmentIsSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [new("license", "trial")], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial")])).Value.Should().BeTrue();
        context.Evaluate("testfeature", false, context: BuildContext([("other", "segment")])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: null).Value.Should().BeFalse();
    }

    [Fact]
    public void
        WhenFeatureIsNotToggledOnForSpecificSegments_EvaluatesToTrueRegardlessOfSegmentSpecified()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial")])).Value.Should().BeTrue();
        context.Evaluate("testfeature", false, context: null).Value.Should().BeTrue();
    }

    [Fact]
    public void WhenAFeatureIsToggledOnForMultipleSegments_EvaluatesCorrectly()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation(
                "testfeature", true, "evaluation-key", [
                    new("license", "trial"),
                    new("region", "au"),
                    new("region", "us"),
                ],
                100
            )
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();

        // A matching context value is present for each toggled segment
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial"), ("region", "us")])).Value
            .Should().BeTrue("when there is a matching context value for each toggled segment, the toggle should be enabled");

        // A context value is present for each toggled segment, but it is not toggled on for one of the supplied values
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial"), ("region", "eu")])).Value
            .Should().BeFalse("when there is a matching context value for each toggled segment, but the context value does not match the toggled segment, the toggle should be disabled");

        // A matching context value is present for each toggled segment, and an additional segment is present in the provided context values
        context.Evaluate("testfeature", false,
                context: BuildContext([("license", "trial"), ("region", "us"), ("language", "english")])).Value.Should()
            .BeTrue("when extra context values are present, the toggle should still be enabled");

        // A context value is present for only one of the two toggled segments
        context.Evaluate("testfeature", false, context: BuildContext([("license", "trial")])).Value.Should()
            .BeFalse("when the context does not contain a value for all toggled segments, the toggle should be disabled");

        // No context values are present for the two toggled segment
        // Note that the default value is only returned if evaluation fails for an unexpected reason.
        // In this case, the default value is not returned, as we have a successful, but false, flag evaluation.
        context.Evaluate("testfeature", true, context: BuildContext([("other", "segment")])).Value.Should()
            .BeFalse("when the context does not contain a value for all toggled segments, the toggle should be disabled");

        // None specified
        context.Evaluate("testfeature", true, context: null).Value.Should().BeFalse("when no context values are present, and the feature is toggled on for a segment, the toggle should be disabled");
    }

    [Fact]
    public void
        WhenAFeatureIsToggledOnForASpecificSegment_ToleratesNullValuesInContext()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("testfeature", true, "evaluation-key", [new("license", "trial")], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", false, context: BuildContext([("license", null)!])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: BuildContext([("other", "segment")])).Value.Should().BeFalse();
        context.Evaluate("testfeature", false, context: null).Value.Should().BeFalse();
    }

    [Fact]
    public void WhenTargetingKeyFallsWithinRolloutPercentage_AndFeatureIsNotToggledForSegments_ResolvesToTrue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 13)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // Key resolves to 13 => segment is within rollout percentage
        var evaluationContext = BuildContext([], targetingKey: "targeting-key");
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeTrue("segment is within rollout percentage");
    }

    [Fact]
    public void WhenTargetingKeyFallsOutsideRolloutPercentage_AndFeatureIsNotToggledForSegments_ResolvesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 12)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // Key resolves to 13 > 12 => segment is outside of rollout percentage
        var evaluationContext = BuildContext([], targetingKey: "targeting-key");
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeFalse("segment is outside of rollout percentage");
    }

    [Fact]
    public void WhenTargetingKeyFallsWithinRolloutPercentage_AndSegmentMatchesRequiredSegments_EvaluatesToTrue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [new("license", "trial")], 13)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // Key resolves to 13 => segment is within rollout percentage
        var evaluationContext = BuildContext([("license", "trial")], targetingKey: "targeting-key");

        using var scope = new AssertionScope();
        context.Evaluate("test-feature", false, evaluationContext).Value.Should()
            .BeTrue("segment matches required segment and falls within rollout percentage");
    }

    [Fact]
    public void WhenTargetingKeyFallsWithinRolloutPercentage_AndSegmentValueDoesNotMatchRequiredSegment_EvaluatesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [new("license", "enterprise")], 99)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // Key resolves to 13 => segment is within rollout percentage
        var evaluationContext = BuildContext([("license", "trial")], targetingKey: "targeting-key");

        using var scope = new AssertionScope();
        context.Evaluate("test-feature", false, evaluationContext).Value.Should()
            .BeFalse("segment value does not match required segment");
    }

    [Fact]
    public void WhenTargetingKeyFallsOutsideRolloutPercentage_AndSegmentValueDoesNotMatchRequiredSegment_EvaluatesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [new("license", "enterprise")], 12)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        // Key resolves to 13 > 12 => segment is outside of rollout percentage
        var evaluationContext = BuildContext([("license", "trial")], targetingKey: "targeting-key");

        using var scope = new AssertionScope();
        context.Evaluate("test-feature", false, evaluationContext).Value.Should()
            .BeFalse("segment is outside of rollout percentage");
    }

    [Fact]
    public void WhenNoTargetingKey_RolloutIsLessThanOneHundredPercent_ResolvesToFalse()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 99)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var evaluationContext = BuildContext([], targetingKey: null);
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeFalse("no targeting key and rollout is less than 100%");
    }

    [Fact]
    public void WhenNoTargetingKey_RolloutIsEqualToOneHundredPercent_ResolvesToTrue()
    {
        var featureToggles = new FeatureToggles([
            new FeatureToggleEvaluation("test-feature", true, "evaluation-key", [], 100)
        ], []);

        var context = new OctopusFeatureContext(featureToggles, NullLoggerFactory.Instance);

        var evaluationContext = BuildContext([], targetingKey: null);
        var result = context.Evaluate("test-feature", false, evaluationContext);

        result.Value.Should().BeTrue("no targeting key and rollout is 100%");
    }

    // These cases verify that GetNormalizedNumber produces the same bucketing values as the equivalent
    // implementations in other Octopus OpenFeature provider libraries (e.g. Java). The expected values
    // are derived from the reference MurmurHash3 little-endian algorithm and are duplicated verbatim
    // across all libraries. DO NOT modify the input arguments or expected values — doing so would mask
    // a real divergence in evaluation behaviour between libraries and defeat the purpose of this test.
    [Theory]
    [InlineData("ef70b4c0-1773-44a3-9b95-f239ae97d9db", "780c4b16-a510-49fa-a2b2-bbd1c38dbe31", 48)]
    [InlineData("055665f0-fbb3-484e-8ef1-52125425b7b2", "6a33c211-5af8-4c34-ba82-4d79846cb045", 85)]
    [InlineData("8e63257f-07f1-43b6-871f-4e8fcaa1718e", "0a8dc2d9-fb7c-42a9-9bfd-200f65717c5f", 47)]
    [InlineData("b8a1abcd-1a69-46c7-8da4-f9fc3c6da5d7", "30433819-7096-420f-9b9f-ff555504a5dd", 79)]
    [InlineData("277582f0-93f5-4c2c-888e-44f94ecc6c7f", "3238dad5-c7d4-460e-868d-f8de4770a1e8", 97)]
    [InlineData("41f60be0-7cef-4aa3-aaed-cf4a4599a084", "user-0007", 41)]
    [InlineData("rollout", "fd7f2fc4-7ecc-46a0-9ee1-073bac59d6dd", 7)]
    [InlineData("83a52df2-49a6-47ec-89f1-b6106a2ca9a3", "a43ec79d-db51-4371-8827-cb5e7beac345", 90)]
    [InlineData("7b89296c-6dcb-4c50-8857-7eb1924770d3", "5932b247-0569-4069-9688-867975502818", 60)]
    [InlineData("7aeff54e-808c-46a6-9f49-2fba47a1fca7", "48c16871-3872-4ddc-a828-c3dd26f30f0b", 90)]
    [InlineData("b08969ee-f0d8-45e3-b763-c84ebdde03f1", "6b9bb2f6-535a-4e07-b6df-fce8112d9d11", 68)]
    [InlineData("5bc8fbbc-bde5-4099-8164-d8399f767c45", "545c10b7-eb4d-4a28-a1a6-e4dc0b46adec", 79)]
    [InlineData("5d357ffe-4423-460d-9b0e-da407f5e8e61", "2b263e0c-e7b2-4237-beb7-aa5d9f5ddbf2", 93)]
    [InlineData("8d62d777-8090-44bd-96a7-4dbe3e572e0f", "b5e8480a-d3dd-4a47-b108-781d1e644773", 60)]
    [InlineData("688bf8ca-c418-4fcd-9db8-4aa91325af10", "11eb4678-7889-4121-898d-83332035bce5", 28)]
    [InlineData("8623121d-e0bb-437a-9459-4d8b75673fca", "dff8494d-3bb3-4b25-9f81-20c447f0e17e", 76)]
    [InlineData("c33f4584-b23b-41d8-893c-d01609de8895", "48b0083a-3c39-4aba-9b9f-3e14c7608387", 85)]
    [InlineData("e008364f-306d-4b3d-bce8-0083255d4b38", "tenant-001", 84)]
    [InlineData("cc73abe9-9eba-409f-822a-87dc52e17fd9", "3df9d4e4-bda3-42f7-9b7c-85bf909d05d8", 11)]
    [InlineData("2507759b-36af-471e-ad2e-f1c113d1e9e3", "a", 13)]
    [InlineData("d7f20e07-ed42-42ed-84bb-895c608099f6", "f97f4108-4e1a-4c75-942f-8ea30236beb7", 9)]
    [InlineData("48f165d5-7b00-47f4-b81e-f86f5c8cc1ab", "8700cfed-571a-41b0-a32e-a7fbb2bec49f", 18)]
    [InlineData("46f7c9ea-b38c-445a-bad9-8a70a603e9e1", "user-0004", 32)]
    [InlineData("f7ae9466-2c20-48f7-9732-3b35156199d0", "2d885f55-3d4e-42ba-a6b5-3b4cb3362773", 82)]
    [InlineData("7450bc56-6fc6-4ccb-95b5-582a736a9625", "user-0008", 97)]
    [InlineData("b39cfd4b-8abe-4d78-8520-10116895cea8", "b86759d1-3ba6-4a4a-839f-64bfbf5c8eed", 2)]
    [InlineData("4cdac1b3-1894-45b6-b00a-65ccd081a3d4", "org-001", 74)]
    [InlineData("6b0404f2-b094-40b8-ab01-a1c12a3a2107", "d34923a2-4d4b-4d7e-a96d-7e83b47a99c9", 93)]
    [InlineData("fbc1af89-7d69-4938-84f7-18fd250c67f3", "org-003", 4)]
    [InlineData("8bb01460-217f-471c-be0a-e8fa1ceac2cc", "org-002", 37)]
    [InlineData("21636369-8b52-4b4a-97b7-50923ceb3ffd", "xxxxxxxxxxxxxx", 7)]
    [InlineData("bdd640fb-0667-4ad1-9c80-317fa3b1799d", "c927b3d7-c9df-4be2-8065-5831f2396945", 29)]
    [InlineData("705e3831-2331-4265-babf-7430e9e4817a", "87bb47cb-7f00-4264-8706-b39b55d9e4b8", 15)]
    [InlineData("bd023447-34aa-43ff-b278-e0a594ac807a", "tenant-002", 24)]
    [InlineData("cd28037c-1888-4b25-898c-cb7caf2a6a52", "user-0010", 51)]
    [InlineData("06a3f5be-62a9-401b-8279-530735b8cfae", "user-0003", 15)]
    [InlineData("eb85403c-7e8d-4475-9962-1895e98f559d", "xxxxxxxx", 71)]
    [InlineData("8dbb5b2a-6e20-4f8e-9001-a6625a1298a1", "1a8d7c50-c0d3-4ac5-88a4-3616b02b0cd8", 41)]
    [InlineData("e60922ca-8aba-43ed-a4d9-4a354580711b", "7dfb4d2e-17f5-413e-ace3-8d733ed6ff1a", 2)]
    [InlineData("6513270e-269e-4d37-b2a7-4de452e6b438", "org-005", 65)]
    [InlineData("c393fd0e-1cc6-4be5-b836-46bf0324aac3", "0ebd0956-dd97-4698-83c7-520f5783b05f", 60)]
    [InlineData("72e63ac7-a953-4322-9f70-d5dc2e675fc7", "xxxxxxxxx", 92)]
    [InlineData("07158ab7-95f3-4183-9b69-13cd87684f34", "bc087a02-3415-49ca-a6cd-1c580bad0d02", 79)]
    [InlineData("21b8c26b-c023-43ab-95da-cb8f8c773fe6", "c50c6475-83c3-4a03-b99d-b98358c913cb", 59)]
    [InlineData("e8d79f49-af6d-414c-8a6f-188a424e617b", "2f3e9786-a457-4917-b52b-e67cdbd76923", 70)]
    [InlineData("377e6ff8-8e83-4961-ae84-3b2c7e96ba87", "tenant-005", 50)]
    [InlineData("02f16d3d-8f4c-49e7-992b-0703f7467ac9", "e895eba8-39a6-4ad3-a3aa-7533875fd5a9", 86)]
    [InlineData("323d3ab0-f35c-48a9-bce5-e9d717208331", "tenant-003", 88)]
    [InlineData("d95bafc8-f2a4-427b-9cf4-bb99f4bea973", "9aa4e5cd-db41-4f65-9532-f707456606fe", 98)]
    [InlineData("993955be-5888-4f39-937c-56af8c5187c1", "76f939de-30a5-46cc-980d-e48610541d3f", 32)]
    [InlineData("d2e270f9-1fc6-4186-aebc-12938bfb2fcf", "ecd4771a-15e0-4c75-9c36-af0e659ba9df", 25)]
    [InlineData("fe1b1434-3b10-4980-950c-aef9618a9261", "6f5bb29e-1705-4cec-9f02-2d65cbbdc47d", 66)]
    [InlineData("dark-launch", "xxxxxxxxxxxx", 25)]
    [InlineData("0a3aee49-6666-4879-938d-da71e3658966", "505f886a-8692-491e-a530-3537fa5dcfb4", 13)]
    [InlineData("db5b5fab-8f4d-4e27-9da1-494c73cf256d", "xxxxxxxxxx", 44)]
    [InlineData("3bb427c1-a1da-459d-aad1-245c92010b38", "f10cc4f3-ae39-489a-81cf-e9d7627bffe1", 76)]
    [InlineData("374f469f-e3e0-4eda-9a6b-b5639dfcfbd4", "xxxxxxxxxxxxx", 58)]
    [InlineData("ee34cf80-4b49-41f1-b1b9-016371f4a4e4", "2b57b54f-05fa-4c58-a83e-395b8e9a0d48", 66)]
    [InlineData("4dad2986-ce83-4960-aa06-e9ab85a0bcc1", "9b58e9a0-ab47-4b99-a8b5-8dcf8187afc1", 78)]
    [InlineData("9af49740-96f5-40ee-9e25-02420ae59635", "user-0006", 6)]
    [InlineData("e539a78b-c8ef-4346-8b12-ae6ead581e57", "7dfa7c26-d794-4374-8ad7-57b6173beda4", 88)]
    [InlineData("e3e70682-c209-4cac-a29f-6fbed82c07cd", "ed146fe6-b963-4511-aa7b-4732d636b6ce", 38)]
    [InlineData("cd613e30-d8f1-4adf-91b7-584a2265b1f5", "193988fd-b97b-4177-bb55-68426f35f0bb", 97)]
    [InlineData("10adf348-2c4b-4d89-9344-7cbaed90dafc", "xxxxxxxxxxx", 67)]
    [InlineData("32833106-536e-45df-80b2-d002cc92d33d", "abcd", 10)]
    [InlineData("c15521b1-b3dc-450a-9daa-37e51b591d75", "a814e7ff-b207-453b-8167-7ccdeb6c1acc", 41)]
    [InlineData("de04cece-83e9-40e4-9c51-e692dc1729ca", "6347afc2-8444-4e8e-8f6d-a0d8ed54c5d8", 44)]
    [InlineData("c12776e4-6dd4-41b2-abce-fab3a3b48c4a", "user-0002", 68)]
    [InlineData("d1f36a09-9d74-4646-b388-d25833183edb", "user-0005", 86)]
    [InlineData("85750621-02fb-4d4f-b57f-bc5af71a1bfc", "abcde", 30)]
    [InlineData("feature-toggle", "tenant-004", 57)]
    [InlineData("7b24b13f-17ba-4194-b6ae-4b1d3423bf15", "ab", 19)]
    [InlineData("e149bd09-0df5-4245-84b0-6badfa7576c5", "201af639-f64b-4b83-8594-4e9cdac12538", 25)]
    [InlineData("5532e8ba-3083-449e-b945-e4b665c1d4b4", "2f3adacf-3c50-4499-8960-90bdf5d7e2fd", 39)]
    [InlineData("8e91579a-21c3-439e-90c1-91728c541241", "2ca20ef1-a0d9-47e9-8d6e-5c0baeb6ce87", 71)]
    [InlineData("c6601970-e9ff-441d-8899-3a14bb459fdf", "c3565f1d-1bda-4d13-afc1-3e734ce48c7d", 99)]
    [InlineData("c963cfe0-afae-4a3b-b909-6a04e7d80068", "user-0009", 94)]
    [InlineData("ec7d4222-6f41-4481-8fde-580f122088a5", "user-0001", 6)]
    [InlineData("988a0c48-5979-41d1-b000-368d2534c02d", "org-004", 10)]
    [InlineData("bd0558b4-9828-4a61-883e-dd4112cc16df", "xxxxxxxxxxxxxxx", 7)]
    [InlineData("a689ee27-eec1-43b6-95a8-f48f39643825", "097ac32f-0f04-4b42-9dcc-9e88dd29c0f4", 21)]
    [InlineData("9530fcd9-d6fd-4d9b-a203-2801b65c1c28", "abc", 9)]
    [InlineData("3e1c26d3-23ef-423e-a848-f808f54d35bf", "f4e663c3-d0eb-4c6c-a8c2-bf7e6628ac93", 32)]
    [InlineData("75bd5125-db54-435f-9802-db897f041728", "780c4b16-a510-49fa-a2b2-bbd1c38dbe31", 64)]
    [InlineData("8cd272e0-909e-4060-8425-07646bc9947a", "6a33c211-5af8-4c34-ba82-4d79846cb045", 23)]
    [InlineData("experiment-a", "0a8dc2d9-fb7c-42a9-9bfd-200f65717c5f", 93)]
    [InlineData("eeee3183-69ca-47e7-9826-00e9111f4efd", "30433819-7096-420f-9b9f-ff555504a5dd", 76)]
    [InlineData("b3fb08f3-4b48-438b-9e51-a921e8e6a305", "3238dad5-c7d4-460e-868d-f8de4770a1e8", 84)]
    [InlineData("14a03569-d26b-4496-92e5-dfe8cb1855fe", "user-0007", 65)]
    [InlineData("9cecdeee-a156-4927-9ff6-3c0179e58218", "fd7f2fc4-7ecc-46a0-9ee1-073bac59d6dd", 2)]
    [InlineData("568cec2b-740a-4798-96f1-813481854f8a", "a43ec79d-db51-4371-8827-cb5e7beac345", 73)]
    [InlineData("6018366c-f658-47a7-9ed3-4fe53a096533", "5932b247-0569-4069-9688-867975502818", 83)]
    [InlineData("87751d4c-a850-4e2c-84dc-da6a797d76de", "48c16871-3872-4ddc-a828-c3dd26f30f0b", 38)]
    [InlineData("4a37fa2d-f2d7-440f-8785-9faeecc3f80c", "6b9bb2f6-535a-4e07-b6df-fce8112d9d11", 68)]
    [InlineData("a8d42934-33e7-48a0-a81f-9b0cbf4e7af6", "545c10b7-eb4d-4a28-a1a6-e4dc0b46adec", 34)]
    [InlineData("9c6ab710-4a08-4720-8ede-24428a013fda", "2b263e0c-e7b2-4237-beb7-aa5d9f5ddbf2", 11)]
    [InlineData("b406dd29-9b57-4d64-8490-5c0914c25b99", "b5e8480a-d3dd-4a47-b108-781d1e644773", 28)]
    [InlineData("checkout-v2", "11eb4678-7889-4121-898d-83332035bce5", 21)]
    [InlineData("4462ebfc-5f91-4ef0-9cfb-ac6e7687a66e", "dff8494d-3bb3-4b25-9f81-20c447f0e17e", 43)]
    [InlineData("63bf9de9-f33f-4a58-b698-0fbe5edcccc1", "48b0083a-3c39-4aba-9b9f-3e14c7608387", 11)]
    [InlineData("test", "az", 1)]
    [InlineData("bucket", "j", 1)]
    [InlineData("test", "y", 100)]
    [InlineData("flag", "c", 100)]
    public void GetNormalizedNumber_MatchesExpectedValue(string evaluationKey, string targetingKey, int expected)
    {
        OctopusFeatureContext.GetNormalizedNumber(evaluationKey, targetingKey).Should().Be(expected);
    }
}

