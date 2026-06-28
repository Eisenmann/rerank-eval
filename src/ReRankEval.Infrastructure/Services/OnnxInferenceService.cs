using Microsoft.Extensions.Logging;
using Microsoft.ML.OnnxRuntime;
using Microsoft.ML.OnnxRuntime.Tensors;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;
using System.Diagnostics;

namespace ReRankEval.Infrastructure.Services;

public sealed class OnnxInferenceService : IInferenceService
{
    private readonly ITokenizerService _tokenizer;
    private readonly ILogger<OnnxInferenceService> _logger;
    private InferenceSession? _session;
    private ModelEntry? _loadedModel;
    private bool _disposed;

    public OnnxInferenceService(ITokenizerService tokenizer, ILogger<OnnxInferenceService> logger)
    {
        _tokenizer = tokenizer;
        _logger = logger;
    }

    public void LoadModel(ModelEntry model, ExecutionProvider provider = ExecutionProvider.Cpu)
    {
        if (_loadedModel?.Id == model.Id) return;

        _session?.Dispose();
        var onnxPath = model.OnnxPath ?? throw new InvalidOperationException($"Model {model.HuggingFaceId} has no ONNX path.");

        var sessionOptions = CreateSessionOptions(provider);
        _session = new InferenceSession(onnxPath, sessionOptions);
        _loadedModel = model;

        var tokenizerPath = Path.Combine(model.LocalPath, "tokenizer.json");
        _tokenizer.Load(tokenizerPath);

        _logger.LogInformation("Loaded ONNX model {ModelId} with provider {Provider}", model.HuggingFaceId, provider);
    }

    public async Task<IReadOnlyList<float>> ScoreAsync(InferenceRequest request, CancellationToken ct = default)
    {
        var result = await ScoreWithTimingAsync(request, ct);
        return result.Scores;
    }

    public async Task<InferenceTimingResult> ScoreWithTimingAsync(
        InferenceRequest request, CancellationToken ct = default)
    {
        if (_session is null || _loadedModel is null)
            throw new InvalidOperationException("No model loaded. Call LoadModel first.");

        var tokSw = Stopwatch.StartNew();
        var pairs = request.Documents.Select(d => (request.Query, d)).ToList();
        var encodings = _tokenizer.EncodeBatch(pairs, _loadedModel.MaxSequenceLength);
        tokSw.Stop();

        double tensorMs = 0, sessionMs = 0, postMs = 0;
        var allScores = new List<float>(request.Documents.Count);

        for (var i = 0; i < encodings.Count; i += request.BatchSize)
        {
            ct.ThrowIfCancellationRequested();
            var batch = encodings.Skip(i).Take(request.BatchSize).ToList();
            var (batchScores, tMs, sMs, pMs) = await Task.Run(() => RunBatch(batch), ct);
            allScores.AddRange(batchScores);
            tensorMs += tMs;
            sessionMs += sMs;
            postMs += pMs;
        }

        return new InferenceTimingResult(allScores, tokSw.Elapsed.TotalMilliseconds, tensorMs, sessionMs, postMs);
    }

    private (List<float> Scores, double TensorMs, double SessionMs, double PostMs) RunBatch(List<TokenizerOutput> batch)
    {
        var tensorSw = Stopwatch.StartNew();
        var batchSize = batch.Count;
        var seqLen = batch[0].InputIds.Length;

        var inputIds = new long[batchSize * seqLen];
        var attentionMask = new long[batchSize * seqLen];
        var tokenTypeIds = new long[batchSize * seqLen];

        for (var i = 0; i < batchSize; i++)
        {
            var enc = batch[i];
            Array.Copy(enc.InputIds, 0, inputIds, i * seqLen, seqLen);
            Array.Copy(enc.AttentionMask, 0, attentionMask, i * seqLen, seqLen);
            if (enc.TokenTypeIds != null)
                Array.Copy(enc.TokenTypeIds, 0, tokenTypeIds, i * seqLen, seqLen);
        }

        var dims = new[] { batchSize, seqLen };
        var inputs = new List<NamedOnnxValue>
        {
            NamedOnnxValue.CreateFromTensor("input_ids",      new DenseTensor<long>(inputIds,      dims)),
            NamedOnnxValue.CreateFromTensor("attention_mask", new DenseTensor<long>(attentionMask, dims)),
            NamedOnnxValue.CreateFromTensor("token_type_ids", new DenseTensor<long>(tokenTypeIds,  dims))
        };
        tensorSw.Stop();

        var sessionSw = Stopwatch.StartNew();
        using var results = _session!.Run(inputs);
        var logits = results.First().AsEnumerable<float>().ToArray();
        sessionSw.Stop();

        var postSw = Stopwatch.StartNew();
        var scores = logits.Select(l => (float)(1.0 / (1.0 + Math.Exp(-l)))).ToList();
        postSw.Stop();

        return (scores, tensorSw.Elapsed.TotalMilliseconds, sessionSw.Elapsed.TotalMilliseconds, postSw.Elapsed.TotalMilliseconds);
    }

    private static SessionOptions CreateSessionOptions(ExecutionProvider provider)
    {
        var opts = new SessionOptions { GraphOptimizationLevel = GraphOptimizationLevel.ORT_ENABLE_ALL };
        try
        {
            switch (provider)
            {
                case ExecutionProvider.Cuda:     opts.AppendExecutionProvider_CUDA(0); break;
                case ExecutionProvider.CoreMl:   opts.AppendExecutionProvider_CoreML(0); break;
                case ExecutionProvider.DirectMl: opts.AppendExecutionProvider_DML(0); break;
                default:                         opts.AppendExecutionProvider_CPU(); break;
            }
        }
        catch { opts.AppendExecutionProvider_CPU(); }
        return opts;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _session?.Dispose();
        _tokenizer.Dispose();
        _disposed = true;
    }
}
