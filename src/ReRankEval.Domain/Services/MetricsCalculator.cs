using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Services;

public sealed class MetricsCalculator : IMetricsCalculator
{
    public double NdcgAtK(IReadOnlyList<int> relevances, int k)
    {
        var dcg = ComputeDcg(relevances, k);
        var ideal = ComputeIdealDcg(relevances, k);
        return ideal == 0 ? 0 : dcg / ideal;
    }

    public double MrrAtK(IReadOnlyList<int> relevances, int k)
    {
        var limit = Math.Min(k, relevances.Count);
        for (var i = 0; i < limit; i++)
        {
            if (relevances[i] > 0)
                return 1.0 / (i + 1);
        }
        return 0;
    }

    public double MapAtK(IReadOnlyList<int> relevances, int k)
    {
        var limit = Math.Min(k, relevances.Count);
        var totalRelevant = relevances.Take(limit).Count(r => r > 0);
        if (totalRelevant == 0) return 0;

        double sumPrecision = 0;
        int hits = 0;
        for (var i = 0; i < limit; i++)
        {
            if (relevances[i] > 0)
            {
                hits++;
                sumPrecision += (double)hits / (i + 1);
            }
        }
        return sumPrecision / totalRelevant;
    }

    public double PrecisionAtK(IReadOnlyList<int> relevances, int k)
    {
        var limit = Math.Min(k, relevances.Count);
        return limit == 0 ? 0 : (double)relevances.Take(limit).Count(r => r > 0) / limit;
    }

    public double RecallAtK(IReadOnlyList<int> relevances, int totalRelevant, int k)
    {
        if (totalRelevant == 0) return 0;
        var limit = Math.Min(k, relevances.Count);
        return (double)relevances.Take(limit).Count(r => r > 0) / totalRelevant;
    }

    public double SpearmanRho(IReadOnlyList<double> ranks1, IReadOnlyList<double> ranks2)
    {
        if (ranks1.Count != ranks2.Count || ranks1.Count == 0) return 0;
        var n = ranks1.Count;
        var d2Sum = ranks1.Zip(ranks2, (a, b) => (a - b) * (a - b)).Sum();
        return 1.0 - (6.0 * d2Sum) / (n * ((long)n * n - 1));
    }

    public double KendallTau(IReadOnlyList<double> ranks1, IReadOnlyList<double> ranks2)
    {
        var n = ranks1.Count;
        if (n < 2) return 0;

        long concordant = 0, discordant = 0;
        for (var i = 0; i < n - 1; i++)
        {
            for (var j = i + 1; j < n; j++)
            {
                var sign1 = Math.Sign(ranks1[j] - ranks1[i]);
                var sign2 = Math.Sign(ranks2[j] - ranks2[i]);
                if (sign1 * sign2 > 0) concordant++;
                else if (sign1 * sign2 < 0) discordant++;
            }
        }

        var total = (long)n * (n - 1) / 2;
        return total == 0 ? 0 : (double)(concordant - discordant) / total;
    }

    public IReadOnlyList<CalibrationBucket> CalibrationBuckets(
        IReadOnlyList<float> scores, IReadOnlyList<int> labels, int buckets = 10)
    {
        if (scores.Count != labels.Count || scores.Count == 0)
            return [];

        var result = new List<CalibrationBucket>(buckets);
        var step = 1.0 / buckets;

        for (var b = 0; b < buckets; b++)
        {
            var low = b * step;
            var high = (b + 1) * step;
            var indices = Enumerable.Range(0, scores.Count)
                .Where(i => scores[i] >= low && (scores[i] < high || b == buckets - 1))
                .ToList();

            var count = indices.Count;
            var fraction = count == 0 ? 0 : (double)indices.Count(i => labels[i] > 0) / count;
            result.Add(new CalibrationBucket(low, high, fraction, count));
        }

        return result;
    }

    public Dictionary<int, double> ComputeAll(IReadOnlyList<int> relevances, IReadOnlyList<int> kValues)
    {
        var result = new Dictionary<int, double>();
        var totalRelevant = relevances.Count(r => r > 0);

        foreach (var k in kValues)
        {
            result[k] = NdcgAtK(relevances, k);
        }

        return result;
    }

    private static double ComputeDcg(IReadOnlyList<int> relevances, int k)
    {
        var limit = Math.Min(k, relevances.Count);
        double dcg = 0;
        for (var i = 0; i < limit; i++)
        {
            if (relevances[i] > 0)
                dcg += (Math.Pow(2, relevances[i]) - 1) / Math.Log2(i + 2);
        }
        return dcg;
    }

    private static double ComputeIdealDcg(IReadOnlyList<int> relevances, int k)
    {
        var sorted = relevances.OrderByDescending(r => r).ToList();
        return ComputeDcg(sorted, k);
    }
}
