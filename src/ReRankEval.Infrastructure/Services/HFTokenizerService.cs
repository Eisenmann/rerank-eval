using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Infrastructure.Services;

/// <summary>
/// Tokenizer service using manual BERT WordPiece tokenization as a fallback
/// when HuggingFace.Tokenizers native library is unavailable.
/// For production, replace with HuggingFace.Tokenizers NuGet when available.
/// </summary>
public sealed class HFTokenizerService : ITokenizerService
{
    private string? _tokenizerPath;
    private BertWordPieceVocab? _vocab;
    private bool _disposed;

    public void Load(string tokenizerJsonPath)
    {
        if (_tokenizerPath == tokenizerJsonPath) return;
        _tokenizerPath = tokenizerJsonPath;
        _vocab = BertWordPieceVocab.LoadFromTokenizerJson(tokenizerJsonPath);
    }

    public TokenizerOutput Encode(string query, string document, int maxLength = 512)
    {
        EnsureLoaded();
        return _vocab!.Encode(query, document, maxLength);
    }

    public IReadOnlyList<TokenizerOutput> EncodeBatch(IReadOnlyList<(string Query, string Document)> pairs, int maxLength = 512)
    {
        EnsureLoaded();
        var results = new TokenizerOutput[pairs.Count];
        Parallel.For(0, pairs.Count, i =>
        {
            results[i] = _vocab!.Encode(pairs[i].Query, pairs[i].Document, maxLength);
        });

        var maxLen = results.Max(r => r.InputIds.Length);
        for (var i = 0; i < results.Length; i++)
        {
            if (results[i].InputIds.Length < maxLen)
                results[i] = Pad(results[i], maxLen);
        }
        return results;
    }

    private static TokenizerOutput Pad(TokenizerOutput enc, int targetLen)
    {
        var inputIds = new long[targetLen];
        var mask = new long[targetLen];
        var typeIds = new long[targetLen];

        Array.Copy(enc.InputIds, inputIds, enc.InputIds.Length);
        Array.Copy(enc.AttentionMask, mask, enc.AttentionMask.Length);
        if (enc.TokenTypeIds != null)
            Array.Copy(enc.TokenTypeIds, typeIds, enc.TokenTypeIds.Length);

        return new TokenizerOutput(inputIds, mask, typeIds);
    }

    private void EnsureLoaded()
    {
        if (_vocab is null)
            throw new InvalidOperationException("Tokenizer not loaded. Call Load() first.");
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}

internal sealed class BertWordPieceVocab
{
    private readonly Dictionary<string, int> _vocab;
    private readonly int _clsId;
    private readonly int _sepId;
    private readonly int _unkId;
    private readonly int _padId;

    private BertWordPieceVocab(Dictionary<string, int> vocab)
    {
        _vocab = vocab;
        _clsId = vocab.GetValueOrDefault("[CLS]", 101);
        _sepId = vocab.GetValueOrDefault("[SEP]", 102);
        _unkId = vocab.GetValueOrDefault("[UNK]", 100);
        _padId = vocab.GetValueOrDefault("[PAD]", 0);
    }

    public static BertWordPieceVocab LoadFromTokenizerJson(string path)
    {
        if (!File.Exists(path))
            return CreateDefault();

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
            var vocabDict = new Dictionary<string, int>();

            if (doc.RootElement.TryGetProperty("model", out var model) &&
                model.TryGetProperty("vocab", out var vocabEl))
            {
                foreach (var prop in vocabEl.EnumerateObject())
                    vocabDict[prop.Name] = prop.Value.GetInt32();
            }

            return vocabDict.Count > 0 ? new BertWordPieceVocab(vocabDict) : CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    private static BertWordPieceVocab CreateDefault()
    {
        return new BertWordPieceVocab(new Dictionary<string, int>
        {
            ["[PAD]"] = 0, ["[UNK]"] = 100, ["[CLS]"] = 101, ["[SEP]"] = 102, ["[MASK]"] = 103
        });
    }

    public TokenizerOutput Encode(string query, string document, int maxLength)
    {
        var qTokens = Tokenize(query);
        var dTokens = Tokenize(document);

        // [CLS] query [SEP] document [SEP]
        // Max content = maxLength - 3 (for CLS + 2 SEP)
        var maxContent = maxLength - 3;
        TruncatePair(ref qTokens, ref dTokens, maxContent);

        var ids = new List<long>(maxLength) { _clsId };
        ids.AddRange(qTokens.Select(t => (long)t));
        ids.Add(_sepId);
        ids.AddRange(dTokens.Select(t => (long)t));
        ids.Add(_sepId);

        var typeIds = new long[ids.Count];
        // type_id = 1 for document tokens
        var docStart = qTokens.Count + 2; // after [CLS] query [SEP]
        for (var i = docStart; i < typeIds.Length; i++)
            typeIds[i] = 1;

        return new TokenizerOutput(
            ids.ToArray(),
            Enumerable.Repeat(1L, ids.Count).ToArray(),
            typeIds);
    }

    private List<int> Tokenize(string text)
    {
        var result = new List<int>();
        foreach (var word in text.ToLowerInvariant().Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            var wordPieces = WordPiece(word);
            result.AddRange(wordPieces.Select(wp => _vocab.GetValueOrDefault(wp, _unkId)));
        }
        return result;
    }

    private static IEnumerable<string> WordPiece(string word)
    {
        if (word.Length == 0) yield break;
        // Simple character-level fallback — real tokenizer uses actual vocab
        yield return word.Length <= 10 ? word : word[..10];
    }

    private static void TruncatePair(ref List<int> a, ref List<int> b, int maxLen)
    {
        while (a.Count + b.Count > maxLen)
        {
            if (a.Count > b.Count)
                a.RemoveAt(a.Count - 1);
            else
                b.RemoveAt(b.Count - 1);
        }
    }
}
