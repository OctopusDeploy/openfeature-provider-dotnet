using FluentAssertions;
using Octopus.OpenFeature.Provider.V4.Conditions;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class UnknownConditionResourceTests
{
    [Theory]
    [InlineData("some-future-condition")]
    [InlineData(null)]
    public void NeverMatches(string? type)
    {
        // A capability a newer server understands and this client does not is treated as "not met".
        new UnknownConditionResource(type).Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    [Fact]
    public void AnUnrecognisedType_IsWellFormed()
    {
        // A newer server sending a condition this version has never heard of is not a bad response. The
        // condition fails its own rule and the rest of the flag still evaluates.
        new UnknownConditionResource("some-future-condition").Validate().Should().BeNull();
    }

    [Fact]
    public void NoTypeAtAll_IsMalformed()
    {
        // No server version emits a condition without a type, so this fails the whole flag instead.
        new UnknownConditionResource(type: null).Validate().Should().Be("a condition with no type");
    }
}
