using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public interface ITokenizerService : IDisposable
{
    void Load(string tokenizerJsonPath);
    TokenizerOutput Encode(string query, string document, int maxLength = 512);
    IReadOnlyList<TokenizerOutput> EncodeBatch(IReadOnlyList<(string Query, string Document)> pairs, int maxLength = 512);
}
