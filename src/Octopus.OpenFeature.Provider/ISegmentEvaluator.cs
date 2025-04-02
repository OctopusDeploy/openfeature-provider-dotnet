using Semver;

namespace Octopus.OpenFeature.Provider;

interface ISegmentEvaluator
{
    bool CanEvaluate(string segmentValue, string contextValue);
    bool Evaluate(string segmentValue, string contextValue);
}

class SemanticVersionSegmentEvaluator : ISegmentEvaluator
{
    public bool CanEvaluate(string segmentValue, string contextValue)
    {
        return SemVersion.TryParse(segmentValue, out _) && SemVersion.TryParse(contextValue, out _);
    }

    public bool Evaluate(string segmentValue, string contextValue)
    {
        var segmentVersion = SemVersion.Parse(segmentValue);
        var contextVersion = SemVersion.Parse(contextValue);

        return SemVersion.CompareSortOrder(segmentVersion, contextVersion) <= 0;
    }
}

class DefaultSegmentEvaluator : ISegmentEvaluator
{
    public bool CanEvaluate(string segmentValue, string contextValue)
    {
        return true;
    }

    public bool Evaluate(string segmentValue, string contextValue)
    {
        return contextValue.Equals(segmentValue, StringComparison.OrdinalIgnoreCase);
    }
}