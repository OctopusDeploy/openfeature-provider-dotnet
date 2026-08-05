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
}
