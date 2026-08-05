using FluentAssertions;
using Octopus.OpenFeature.Provider.V4.Conditions;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.Tests.V4.Conditions;

public class UnknownConditionTests
{
    [Fact]
    public void AnUnrecognisedType_NeverMatches()
    {
        new UnknownCondition("some-future-condition")
            .Matches(Contexts.ForRules(Contexts.TargetingKey)).Should().BeFalse();
    }

    [Fact]
    public void NoTypeAtAll_ThrowsAParseError()
    {
        // No server version emits a condition without a type, unlike one with a type we do not know.
        var matches = () => new UnknownCondition(type: null)
            .Matches(Contexts.ForRules(Contexts.TargetingKey));

        matches.Should().Throw<ParseErrorException>()
            .Which.Message.Should().Be("A condition has no type.");
    }
}
