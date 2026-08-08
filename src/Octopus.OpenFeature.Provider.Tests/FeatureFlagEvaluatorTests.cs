using FluentAssertions;
using FluentAssertions.Execution;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Logging.Testing;
using Octopus.OpenFeature.Provider.V4;
using OpenFeature.Constant;
using OpenFeature.Error;

namespace Octopus.OpenFeature.Provider.Tests;

public class FeatureFlagEvaluatorTests
{
    static ServerSideEvaluation Flag(string slug, bool value)
        => new(
            slug,
            value,
            reason: value ? "The flag is enabled for this environment." : "The flag is disabled for this environment.",
            evaluationKey: null,
            rules: null);

    static FeatureFlagEvaluator EvaluatorFor(params ServerSideEvaluation[] evaluations)
        => EvaluatorFor(NullLoggerFactory.Instance, evaluations);

    static FeatureFlagEvaluator EvaluatorFor(ILoggerFactory loggerFactory, params ServerSideEvaluation[] evaluations)
        => new(new EvaluationResponse(evaluations, []), loggerFactory);

    [Fact]
    public void Evaluate_WhenTheFlagIsInTheResponse_ReturnsTheFlagsEvaluation()
    {
        var evaluator = EvaluatorFor(Flag("test-feature", true));

        var result = evaluator.Evaluate("test-feature", context: null);

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.Reason.Should().Be("The flag is enabled for this environment.");
    }

    [Fact]
    public void Evaluate_WhenTheSlugDiffersOnlyInCase_ReturnsTheFlagValueAndTheFlagsOwnSlug()
    {
        var evaluator = EvaluatorFor(Flag("test-feature", true));

        var result = evaluator.Evaluate("Test-Feature", context: null);

        using var scope = new AssertionScope();
        result.Value.Should().BeTrue();
        result.FlagKey.Should().Be("test-feature");
    }

    [Fact]
    public void Evaluate_WhenTheFlagKeyIsNotASlug_ThrowsFlagNotFound()
    {
        var evaluator = EvaluatorFor(Flag("this-is-clearly-not-a-slug", true));

        var evaluate = () => evaluator.Evaluate("This is clearly not a slug!", context: null);

        evaluate.Should().Throw<FlagNotFoundException>().Which.ErrorType.Should().Be(ErrorType.FlagNotFound);
    }

    [Fact]
    public void Evaluate_WhenTheFlagIsNotInTheResponse_ThrowsFlagNotFound()
    {
        var evaluator = EvaluatorFor(Flag("testfeature", false));

        var evaluate = () => evaluator.Evaluate("anotherfeature", context: null);

        evaluate.Should().Throw<FlagNotFoundException>().Which.ErrorType.Should().Be(ErrorType.FlagNotFound);
    }

    [Fact]
    public void Evaluate_WhenTheSameMissingSlugIsEvaluatedRepeatedly_LogsOneWarning()
    {
        var fakeLogger = new FakeLogger();
        var evaluator = EvaluatorFor(new SingleLoggerFactory(fakeLogger), Flag("known-feature", true));

        for (var i = 0; i < 10; i++)
        {
            var evaluate = () => evaluator.Evaluate("missing-slug", context: null);
            evaluate.Should().Throw<FlagNotFoundException>();
        }

        fakeLogger.Collector.GetSnapshot().Should().ContainSingle(r => r.Level == LogLevel.Warning);
    }

    class SingleLoggerFactory(ILogger logger) : ILoggerFactory
    {
        public ILogger CreateLogger(string categoryName) => logger;
        public void AddProvider(ILoggerProvider provider) { }
        public void Dispose() { }
    }
}
