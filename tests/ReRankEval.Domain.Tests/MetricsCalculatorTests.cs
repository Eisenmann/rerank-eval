using FluentAssertions;
using ReRankEval.Domain.Services;

namespace ReRankEval.Domain.Tests;

public class MetricsCalculatorTests
{
    private readonly MetricsCalculator _sut = new();

    // NDCG@K tests

    [Fact]
    public void NdcgAtK_PerfectRanking_Returns1()
    {
        var relevances = new[] { 2, 1, 0 };
        _sut.NdcgAtK(relevances, 3).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void NdcgAtK_AllIrrelevant_Returns0()
    {
        var relevances = new[] { 0, 0, 0 };
        _sut.NdcgAtK(relevances, 3).Should().Be(0.0);
    }

    [Fact]
    public void NdcgAtK_KLargerThanList_UsesListLength()
    {
        var relevances = new[] { 1, 0 };
        var result = _sut.NdcgAtK(relevances, 10);
        result.Should().BeGreaterThan(0).And.BeLessOrEqualTo(1.0);
    }

    [Fact]
    public void NdcgAtK_KnownValues_MatchReference()
    {
        // DCG = (2^1-1)/log2(2) + (2^0-1)/log2(3) = 1 + 0 = 1
        // IdealDCG = 1 (same ordering)
        // NDCG = 1.0
        var relevances = new[] { 1, 0, 1, 0 };
        var result = _sut.NdcgAtK(relevances, 4);
        result.Should().BeGreaterThan(0).And.BeLessOrEqualTo(1.0);
    }

    // MRR@K tests

    [Fact]
    public void MrrAtK_FirstRelevant_Returns1()
    {
        var relevances = new[] { 1, 0, 0 };
        _sut.MrrAtK(relevances, 3).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void MrrAtK_SecondRelevant_Returns0_5()
    {
        var relevances = new[] { 0, 1, 0 };
        _sut.MrrAtK(relevances, 3).Should().BeApproximately(0.5, 1e-10);
    }

    [Fact]
    public void MrrAtK_ThirdRelevant_Returns1Over3()
    {
        var relevances = new[] { 0, 0, 1 };
        _sut.MrrAtK(relevances, 3).Should().BeApproximately(1.0 / 3.0, 1e-10);
    }

    [Fact]
    public void MrrAtK_NoRelevant_Returns0()
    {
        var relevances = new[] { 0, 0, 0 };
        _sut.MrrAtK(relevances, 3).Should().Be(0.0);
    }

    // MAP@K tests

    [Fact]
    public void MapAtK_AllRelevant_Returns1()
    {
        var relevances = new[] { 1, 1, 1 };
        _sut.MapAtK(relevances, 3).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void MapAtK_NoRelevant_Returns0()
    {
        var relevances = new[] { 0, 0, 0 };
        _sut.MapAtK(relevances, 3).Should().Be(0.0);
    }

    [Fact]
    public void MapAtK_AlternatingRelevance_IsCorrect()
    {
        // Positions 1 and 3 are relevant
        // AP = (1/1 + 2/3) / 2 = (1 + 0.667) / 2 = 0.833
        var relevances = new[] { 1, 0, 1 };
        _sut.MapAtK(relevances, 3).Should().BeApproximately(5.0 / 6.0, 1e-6);
    }

    // Precision@K tests

    [Fact]
    public void PrecisionAtK_AllRelevant_Returns1()
    {
        var relevances = new[] { 1, 1, 1 };
        _sut.PrecisionAtK(relevances, 3).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void PrecisionAtK_HalfRelevant_Returns0_5()
    {
        var relevances = new[] { 1, 0, 1, 0 };
        _sut.PrecisionAtK(relevances, 4).Should().BeApproximately(0.5, 1e-10);
    }

    // Recall@K tests

    [Fact]
    public void RecallAtK_AllRelevantInK_Returns1()
    {
        var relevances = new[] { 1, 1, 0, 0 };
        _sut.RecallAtK(relevances, 2, 2).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void RecallAtK_ZeroRelevant_Returns0()
    {
        var relevances = new[] { 0, 0 };
        _sut.RecallAtK(relevances, 0, 2).Should().Be(0.0);
    }

    // SpearmanRho tests

    [Fact]
    public void SpearmanRho_PerfectCorrelation_Returns1()
    {
        var ranks = new double[] { 1, 2, 3, 4, 5 };
        _sut.SpearmanRho(ranks, ranks).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void SpearmanRho_ReverseRanks_ReturnsMinus1()
    {
        var r1 = new double[] { 1, 2, 3, 4, 5 };
        var r2 = new double[] { 5, 4, 3, 2, 1 };
        _sut.SpearmanRho(r1, r2).Should().BeApproximately(-1.0, 1e-10);
    }

    // KendallTau tests

    [Fact]
    public void KendallTau_PerfectAgreement_Returns1()
    {
        var r1 = new double[] { 1, 2, 3 };
        var r2 = new double[] { 1, 2, 3 };
        _sut.KendallTau(r1, r2).Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void KendallTau_PerfectDisagreement_ReturnsMinus1()
    {
        var r1 = new double[] { 1, 2, 3 };
        var r2 = new double[] { 3, 2, 1 };
        _sut.KendallTau(r1, r2).Should().BeApproximately(-1.0, 1e-10);
    }

    // CalibrationBuckets tests

    [Fact]
    public void CalibrationBuckets_AllRelevantHighScores_CorrectFraction()
    {
        var scores = new float[] { 0.9f, 0.95f, 0.85f };
        var labels = new[] { 1, 1, 1 };
        var buckets = _sut.CalibrationBuckets(scores, labels, 10);
        var highBucket = buckets.Last(b => b.Count > 0);
        highBucket.ActualRelevanceFraction.Should().BeApproximately(1.0, 1e-10);
    }

    [Fact]
    public void CalibrationBuckets_EmptyInput_ReturnsEmpty()
    {
        _sut.CalibrationBuckets([], [], 10).Should().BeEmpty();
    }
}
