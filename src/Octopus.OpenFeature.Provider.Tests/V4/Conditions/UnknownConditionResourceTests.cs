using FluentAssertions;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class UnknownConditionResourceTests
{
    [Fact]
    public void AnUnrecognisedType_NeverMatches()
    {
        // A newer server sending a condition this version has never heard of is not a bad response. The
        // capability is treated as "not met", so the condition fails its own rule and the rest of the flag
        // still evaluates.
        new UnknownConditionResource("some-future-condition")
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    [Fact]
    public void NoTypeAtAll_ThrowsAParseError()
    {
        // No server version emits a condition without a type, so this fails the flag instead of quietly
        // failing one rule.
        var matches = () => new UnknownConditionResource(type: null)
            .Matches(Contexts.ForRules(Contexts.TargetingKey));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be(Contexts.MalformedMessage("a condition with no type"));
    }
}
