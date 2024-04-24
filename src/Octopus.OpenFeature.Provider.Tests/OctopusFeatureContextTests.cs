using FluentAssertions;
using FluentAssertions.Execution;

namespace Octopus.OpenFeature.Provider.Tests;

public class OctopusFeatureContextTests
{
    [Fact]
    public void GivenAFeature_WhenToggledOnForTheEnvironment_EvaluatesToTrue()
    {
        var environment = "environments-1";

        var manifest = new OctopusFeatureManifestBuilder()
            .WithFeature("testfeature")
            .ForEnvironment("environments-1")
            .Build();

        var context = new OctopusFeatureContext(
            manifest,
            new OctopusFeatureConfiguration(BuildIdentifier(environment)));

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", segment: null).Should().BeTrue();
        context.Evaluate("anotherfeature", segment: null).Should().BeFalse();
    }

    [Fact]
    public void GivenAToggle_WhenToggledOnForSpecificTenants_EvaluatesToTrue()
    {
        var environment = "environments-1";
        var tenants = new[] { "tenants-1", "tenants-2" };

        var manifest = new OctopusFeatureManifestBuilder()
            .WithFeature("testfeature")
            .ForTenants(tenants)
            .ForEnvironment("environments-1")
            .Build();

        var context = new OctopusFeatureContext(
            manifest,
            new OctopusFeatureConfiguration(BuildIdentifier(environment, tenants[0])));

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", segment: null).Should().BeTrue();
        context.Evaluate("anotherfeature", segment: null).Should().BeFalse();
    }


    [Fact]
    public void
        GivenAToggle_WhenToggledOnForAPercentageOfTenantsInAnEnvironment_EvaluatesToTrueForWithinTwoPercentOfSpecifiedPercentageOfTenants()
    {
        var environment = "environments-1";
        var tenants = Enumerable.Range(1, 1000).Select(i => $"tenants-{i}").ToArray();

        var percentage = 50;

        var manifest = new OctopusFeatureManifestBuilder()
            .WithFeature("testfeature")
            .ForPercentageOfTenants(percentage)
            .ForEnvironment("environments-1")
            .Build();

        int enabledCount = 0;
        foreach (var tenant in tenants)
        {
            var context = new OctopusFeatureContext(
                manifest,
                new OctopusFeatureConfiguration(BuildIdentifier(environment, tenant)));

            if (context.Evaluate("testfeature", segment: null))
            {
                enabledCount++;
            }
        }

        var actualPercentage = (enabledCount / (double)tenants.Length) * 100.0;

        using var scope = new AssertionScope();

        ((percentage - 2) < actualPercentage)
            .Should().BeTrue("Expected " + percentage + "%, but was " + actualPercentage + "%");

        ((percentage + 2) > actualPercentage).Should()
            .BeTrue("Expected " + percentage + "%, but was " + actualPercentage + "%");
    }

    [Fact]
    public void GivenAToggle_WhenToggledOnForSpecificSegments_EvaluatesToTrue()
    {
        var environment = "environments-1";
        var segments = new[] { "users/beta", "region/us" };

        var manifest = new OctopusFeatureManifestBuilder()
            .WithFeature("testfeature")
            .ForSegments(segments)
            .ForEnvironment("environments-1")
            .Build();

        var context = new OctopusFeatureContext(
            manifest,
            new OctopusFeatureConfiguration(BuildIdentifier(environment)));

        using var scope = new AssertionScope();
        context.Evaluate("testfeature", segment: "users/beta").Should().BeTrue();
        context.Evaluate("testfeature", segment: "region/us").Should().BeTrue();
        context.Evaluate("testfeature", segment: "tier/premium").Should().BeFalse();
    }

    [Fact]
    public void GivenAToggle_WhenToggledOnForSpecificTenants_AndToggledOnForSpecificSegments_EvaluatesToTrue()
    {
        var environment = "environments-1";
        var tenants = new[] { "tenants-1", "tenants-2" };
        var segments = new[] { "users/beta", "region/us" };

        var manifest = new OctopusFeatureManifestBuilder()
            .WithFeature("testfeature")
            .ForTenants(tenants)
            .ForSegments(segments)
            .ForEnvironment("environments-1")
            .Build();

        var contextTenantOne = new OctopusFeatureContext(
            manifest,
            new OctopusFeatureConfiguration(BuildIdentifier(environment, tenants[0])));

        var contextTenantTwo = new OctopusFeatureContext(
            manifest,
            new OctopusFeatureConfiguration(BuildIdentifier(environment, tenants[0])));

        var contextNoTenants = new OctopusFeatureContext(
            manifest,
            new OctopusFeatureConfiguration(BuildIdentifier(environment)));

        using var scope = new AssertionScope();

        // Any combination of valid tenant + valid segment should be true
        contextTenantOne.Evaluate("testfeature", segments[0]).Should().BeTrue();
        contextTenantOne.Evaluate("testfeature", segments[1]).Should().BeTrue();
        contextTenantTwo.Evaluate("testfeature", segments[0]).Should().BeTrue();
        contextTenantTwo.Evaluate("testfeature", segments[1]).Should().BeTrue();

        // Individual tenant / segment validations should be false, as the toggles are specific to tenant + segment combinations
        contextTenantOne.Evaluate("testfeature", segment: null).Should().BeFalse();
        contextTenantTwo.Evaluate("testfeature", segment: null).Should().BeFalse();
        contextNoTenants.Evaluate("testfeature", segments[0]).Should().BeFalse();
        contextNoTenants.Evaluate("testfeature", segments[1]).Should().BeFalse();

        // Evaluation of out of scope values returns false
        contextTenantOne.Evaluate("testfeature", "does/notexist").Should().BeFalse();
    }

    string BuildIdentifier(string environmentId) =>
        $"test-installation-id:test-project-id:{environmentId}";

    string BuildIdentifier(string environmentId, string tenantId) =>
        $"test-installation-id:test-project-id:{environmentId}:{tenantId}";
}