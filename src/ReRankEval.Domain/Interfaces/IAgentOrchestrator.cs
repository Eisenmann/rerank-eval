using ReRankEval.Domain.Models;

namespace ReRankEval.Domain.Interfaces;

public record AgentResponseChunk(string Text, bool IsFinal, AgentAction? ToolCall = null);

public interface IAgentOrchestrator
{
    Task<AgentSession> StartSessionAsync(CancellationToken ct = default);
    IAsyncEnumerable<AgentResponseChunk> SendMessageAsync(Guid sessionId, string userMessage, CancellationToken ct = default);
    Task<IReadOnlyList<AgentSession>> ListSessionsAsync(CancellationToken ct = default);
}
