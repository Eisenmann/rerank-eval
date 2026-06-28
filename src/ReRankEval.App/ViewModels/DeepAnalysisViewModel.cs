using Avalonia;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using System.Collections.ObjectModel;
using System.Text;

namespace ReRankEval.App.ViewModels;

// ── Shared selector row ───────────────────────────────────────────────

public record ModelResultRow(Guid ModelResultId, string Label, double Ndcg10);

// ── Calibration chart dot ────────────────────────────────────────────

public record CalibDot(double CanvasX, double CanvasY, string Tooltip, double DiagX, double DiagY);

// ── Latency bar segment ──────────────────────────────────────────────

public record LatencyModelBar(
    string Label,
    double TokBarH, double TokBarTop,
    double TensorBarH, double TensorBarTop,
    double SessionBarH, double SessionBarTop,
    double PostBarH, double PostBarTop,
    double BarX, double BarWidth,
    double LabelX, double TotalMs);

// ── Main ViewModel ────────────────────────────────────────────────────

public partial class DeepAnalysisViewModel : ObservableObject
{
    private readonly IAnalysisService _analysis;
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _registry;

    // ── Common selectors ─────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<EvaluationRun> _runs = [];
    [ObservableProperty] private EvaluationRun? _selectedRun;
    [ObservableProperty] private ObservableCollection<ModelResultRow> _modelRows = [];
    [ObservableProperty] private ModelResultRow? _selectedModelRow;
    [ObservableProperty] private string _statusMessage = string.Empty;

    // ── Error analysis ────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<QueryResult> _worstQueries = [];
    [ObservableProperty] private string _errorSearchText = string.Empty;
    [ObservableProperty] private bool _hasWorstQueries;

    // ── Calibration ───────────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<CalibDot> _calibDots = [];
    [ObservableProperty] private bool _hasCalibData;
    [ObservableProperty] private ObservableCollection<DomainBreakdownRow> _domainRows = [];
    [ObservableProperty] private bool _hasDomainData;

    // ── Rank correlation ─────────────────────────────────────────────

    [ObservableProperty] private ObservableCollection<ModelCorrelation> _correlations = [];
    [ObservableProperty] private bool _hasCorrelations;

    // ── Latency profile + A/B test ────────────────────────────────────

    [ObservableProperty] private ObservableCollection<LatencyModelBar> _latencyBars = [];
    [ObservableProperty] private bool _hasLatencyData;

    // A/B test calculator fields
    [ObservableProperty] private string _controlNdcg = "0.750";
    [ObservableProperty] private string _treatmentNdcg = "0.770";
    [ObservableProperty] private string _alphaLevel = "0.05";
    [ObservableProperty] private string _power = "0.80";
    [ObservableProperty] private string _abResult = "Enter values above and click Calculate";

    // Canvas layout
    private const double CalibW = 300, CalibH = 220;
    private const double CalibPadL = 35, CalibPadB = 30, CalibPadT = 10;

    private const double LatencyCanvasH = 200;
    private const double LatencyAxisY = 170;
    private const double LatencyMaxH = 150;

    public DeepAnalysisViewModel(IAnalysisService analysis, IExperimentStore store, IModelRegistry registry)
    {
        _analysis = analysis;
        _store = store;
        _registry = registry;
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        StatusMessage = "Loading runs…";
        try
        {
            var runs = await _store.ListRunsAsync();
            Runs.Clear();
            foreach (var r in runs.Where(r => r.Status == RunStatus.Completed))
                Runs.Add(r);
            StatusMessage = $"{Runs.Count} completed run(s)";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    partial void OnSelectedRunChanged(EvaluationRun? value)
    {
        ModelRows.Clear();
        HasWorstQueries = false;
        HasCalibData = false;
        HasCorrelations = false;
        HasLatencyData = false;
        WorstQueries.Clear();
        CalibDots.Clear();
        Correlations.Clear();
        LatencyBars.Clear();
        DomainRows.Clear();
        HasDomainData = false;

        if (value is null) return;
        _ = LoadModelRowsAsync(value.Id);
    }

    partial void OnSelectedModelRowChanged(ModelResultRow? value)
    {
        if (value is null) return;
        _ = LoadWorstQueriesAsync(value.ModelResultId);
        _ = LoadCalibrationAsync(value.ModelResultId);
        _ = LoadDomainAsync(value.ModelResultId);
    }

    private async Task LoadModelRowsAsync(Guid runId)
    {
        var results = await _store.GetModelResultsAsync(runId);
        var models = await _registry.ListAsync();
        var labelMap = models.ToDictionary(m => m.Id, m => m.HuggingFaceId);

        ModelRows.Clear();
        foreach (var r in results)
        {
            var label = labelMap.TryGetValue(r.ModelId, out var hf) ? hf : r.ModelId.ToString()[..8];
            ModelRows.Add(new ModelResultRow(r.Id, label, r.NdcgAt.GetValueOrDefault(10)));
        }

        await LoadCorrelationsAsync(runId);
        await LoadLatencyBarsAsync(runId, results.ToList(), labelMap);
    }

    // ── Error analysis ────────────────────────────────────────────────

    private async Task LoadWorstQueriesAsync(Guid modelResultId)
    {
        StatusMessage = "Loading worst queries…";
        WorstQueries.Clear();
        HasWorstQueries = false;
        try
        {
            var results = await _analysis.GetWorstQueriesAsync(modelResultId, take: 50);
            foreach (var q in ApplySearch(results))
                WorstQueries.Add(q);
            HasWorstQueries = WorstQueries.Count > 0;
            StatusMessage = $"{WorstQueries.Count} worst queries loaded";
        }
        catch (Exception ex) { StatusMessage = $"Error: {ex.Message}"; }
    }

    private IEnumerable<QueryResult> ApplySearch(IEnumerable<QueryResult> source)
    {
        if (string.IsNullOrWhiteSpace(ErrorSearchText)) return source;
        return source.Where(q => q.QueryText.Contains(ErrorSearchText, StringComparison.OrdinalIgnoreCase));
    }

    partial void OnErrorSearchTextChanged(string value)
    {
        if (SelectedModelRow is null) return;
        _ = LoadWorstQueriesAsync(SelectedModelRow.ModelResultId);
    }

    [RelayCommand]
    private async Task ExportWorstCsvAsync(string filePath)
    {
        if (!HasWorstQueries) return;
        var sb = new StringBuilder("QueryText,NDCG@10,MRR@10,LatencyMs\n");
        foreach (var q in WorstQueries)
            sb.AppendLine($"\"{q.QueryText.Replace("\"", "\"\"")}\",{q.NdcgAt10:F4},{q.MrrAt10:F4},{q.LatencyMs:F1}");
        await File.WriteAllTextAsync(filePath, sb.ToString());
    }

    // ── Calibration ───────────────────────────────────────────────────

    private async Task LoadCalibrationAsync(Guid modelResultId)
    {
        HasCalibData = false;
        CalibDots.Clear();
        try
        {
            var buckets = await _analysis.GetCalibrationDataAsync(modelResultId, buckets: 10);
            if (buckets.Count == 0) return;

            double usableW = CalibW - CalibPadL - 10;
            double usableH = CalibH - CalibPadT - CalibPadB;

            foreach (var b in buckets)
            {
                double midScore = (b.ScoreLow + b.ScoreHigh) / 2;
                double cx = CalibPadL + midScore * usableW;
                double cy = CalibPadT + (1 - b.ActualRelevanceFraction) * usableH;
                double diagX = CalibPadL + midScore * usableW;
                double diagY = CalibPadT + (1 - midScore) * usableH;
                CalibDots.Add(new CalibDot(cx, cy, $"Score {midScore:F2} → rel {b.ActualRelevanceFraction:F2} (n={b.Count})", diagX, diagY));
            }
            HasCalibData = CalibDots.Count > 0;
        }
        catch { /* no data */ }
    }

    private async Task LoadDomainAsync(Guid modelResultId)
    {
        DomainRows.Clear();
        HasDomainData = false;
        try
        {
            var rows = await _analysis.GetDomainBreakdownAsync(modelResultId);
            foreach (var r in rows) DomainRows.Add(r);
            HasDomainData = DomainRows.Count > 0;
        }
        catch { /* no data */ }
    }

    // ── Rank correlation ─────────────────────────────────────────────

    private async Task LoadCorrelationsAsync(Guid runId)
    {
        Correlations.Clear();
        HasCorrelations = false;
        try
        {
            var corrs = await _analysis.GetRankCorrelationsAsync(runId);
            foreach (var c in corrs) Correlations.Add(c);
            HasCorrelations = Correlations.Count > 0;
        }
        catch { /* no data */ }
    }

    // ── Latency bars ──────────────────────────────────────────────────

    private Task LoadLatencyBarsAsync(
        Guid runId,
        List<ModelEvalResult> results,
        Dictionary<Guid, string> labelMap)
    {
        LatencyBars.Clear();
        HasLatencyData = false;
        if (results.Count == 0) return Task.CompletedTask;

        double maxTotal = results.Max(r => r.TokenizationMeanMs + r.TensorCreationMeanMs + r.SessionRunMeanMs + r.PostprocessingMeanMs);
        if (maxTotal <= 0) maxTotal = results.Max(r => r.LatencyMeanMs);
        if (maxTotal <= 0) return Task.CompletedTask;

        double barWidth = Math.Min(60, Math.Max(20, (480.0 - 30) / results.Count - 8));
        double startX = 40;

        for (int i = 0; i < results.Count; i++)
        {
            var r = results[i];
            var label = labelMap.TryGetValue(r.ModelId, out var hf) ? ShortLabel(hf) : r.ModelId.ToString()[..6];

            double totalMs = r.TokenizationMeanMs + r.TensorCreationMeanMs + r.SessionRunMeanMs + r.PostprocessingMeanMs;
            if (totalMs <= 0) totalMs = r.LatencyMeanMs;

            double scale = totalMs > 0 ? LatencyMaxH / maxTotal : 0;

            double tokH     = r.TokenizationMeanMs    * scale;
            double tensorH  = r.TensorCreationMeanMs  * scale;
            double sessH    = r.SessionRunMeanMs       * scale;
            double postH    = r.PostprocessingMeanMs   * scale;

            if (tokH + tensorH + sessH + postH < 1 && totalMs > 0)
            {
                sessH = totalMs * scale;
            }

            double barX = startX + i * (barWidth + 8);

            LatencyBars.Add(new LatencyModelBar(
                Label: label,
                TokBarH:    tokH,    TokBarTop:    LatencyAxisY - tokH - tensorH - sessH - postH,
                TensorBarH: tensorH, TensorBarTop: LatencyAxisY - tensorH - sessH - postH,
                SessionBarH: sessH,  SessionBarTop: LatencyAxisY - sessH - postH,
                PostBarH:   postH,   PostBarTop:    LatencyAxisY - postH,
                BarX:    barX,
                BarWidth: barWidth,
                LabelX:  barX,
                TotalMs: totalMs));
        }

        HasLatencyData = LatencyBars.Count > 0;
        return Task.CompletedTask;
    }

    private static string ShortLabel(string hfId)
    {
        var parts = hfId.Split('/');
        var name = parts.Last();
        return name.Length > 12 ? name[..12] : name;
    }

    // ── A/B test calculator ───────────────────────────────────────────

    [RelayCommand]
    private void CalculateSampleSize()
    {
        if (!double.TryParse(ControlNdcg,   out var p0) ||
            !double.TryParse(TreatmentNdcg, out var p1) ||
            !double.TryParse(AlphaLevel,    out var alpha) ||
            !double.TryParse(Power,         out var power))
        {
            AbResult = "Invalid input — enter decimal numbers (e.g. 0.75)";
            return;
        }

        double delta = Math.Abs(p1 - p0);
        if (delta < 1e-6)
        {
            AbResult = "Control and treatment NDCG must differ";
            return;
        }

        double zAlpha = ZScore(1 - alpha / 2);
        double zBeta  = ZScore(power);
        double pBar   = (p0 + p1) / 2;
        double n = Math.Ceiling(2 * Math.Pow(zAlpha + zBeta, 2) * pBar * (1 - pBar) / (delta * delta));

        AbResult = $"~{(int)n:N0} queries per arm  (α={alpha:F2}, power={power:F0%}, δ={delta:F4})";
    }

    // Inverse normal CDF approximation (Abramowitz & Stegun 26.2.17)
    private static double ZScore(double p)
    {
        if (p <= 0 || p >= 1) return 0;
        var t = Math.Sqrt(-2 * Math.Log(p <= 0.5 ? p : 1 - p));
        var c = new[] { 2.515517, 0.802853, 0.010328 };
        var d = new[] { 1.432788, 0.189269, 0.001308 };
        var z = t - (c[0] + c[1] * t + c[2] * t * t) / (1 + d[0] * t + d[1] * t * t + d[2] * t * t * t);
        return p <= 0.5 ? -z : z;
    }
}
