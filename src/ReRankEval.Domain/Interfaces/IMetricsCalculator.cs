using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public record CalibrationBucket(double ScoreLow, double ScoreHigh, double ActualRelevanceFraction, int Count);

public interface IMetricsCalculator
{
    double NdcgAtK(IReadOnlyList<int> relevances, int k);
    double MrrAtK(IReadOnlyList<int> relevances, int k);
    double MapAtK(IReadOnlyList<int> relevances, int k);
    double PrecisionAtK(IReadOnlyList<int> relevances, int k);
    double RecallAtK(IReadOnlyList<int> relevances, int totalRelevant, int k);
    double SpearmanRho(IReadOnlyList<double> ranks1, IReadOnlyList<double> ranks2);
    double KendallTau(IReadOnlyList<double> ranks1, IReadOnlyList<double> ranks2);
    IReadOnlyList<CalibrationBucket> CalibrationBuckets(IReadOnlyList<float> scores, IReadOnlyList<int> labels, int buckets = 10);
    Dictionary<int, double> ComputeAll(IReadOnlyList<int> relevances, IReadOnlyList<int> kValues);
}
