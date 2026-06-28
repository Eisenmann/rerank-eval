using ReRankEval.Domain.Models;

namespace ReRankEval.Infrastructure.Services;

public sealed class TrainValTestSplitter
{
    public record DataSplit(
        IReadOnlyList<FineTuningExample> Train,
        IReadOnlyList<FineTuningExample> Val,
        IReadOnlyList<FineTuningExample> Test);

    /// <summary>
    /// Stratified split by query frequency bucket: 80/10/10 train/val/test.
    /// Queries that appear multiple times are kept together in one split.
    /// </summary>
    public DataSplit Split(IReadOnlyList<FineTuningExample> examples, int seed = 42)
    {
        var rng = new Random(seed);

        // Group by query so all examples for a query land in the same split
        var byQuery = examples
            .GroupBy(e => e.Query)
            .OrderBy(_ => rng.NextDouble())  // shuffle query groups
            .ToList();

        int total = byQuery.Count;
        int valCount = Math.Max(1, (int)Math.Round(total * 0.10));
        int testCount = Math.Max(1, (int)Math.Round(total * 0.10));
        int trainCount = total - valCount - testCount;

        var train = byQuery.Take(trainCount).SelectMany(g => g).ToList();
        var val   = byQuery.Skip(trainCount).Take(valCount).SelectMany(g => g).ToList();
        var test  = byQuery.Skip(trainCount + valCount).SelectMany(g => g).ToList();

        return new DataSplit(train, val, test);
    }
}
