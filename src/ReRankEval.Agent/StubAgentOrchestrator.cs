using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Agent;

/// <summary>
/// Stub agent orchestrator — Phase 4 will replace with Semantic Kernel implementation.
/// </summary>
public sealed class StubAgentOrchestrator : IAgentOrchestrator
{
    private readonly IExperimentStore _store;

    public StubAgentOrchestrator(IExperimentStore store)
    {
        _store = store;
    }

    public async Task<AgentSession> StartSessionAsync(CancellationToken ct = default)
    {
        var session = new AgentSession { Title = $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}" };
        return await _store.SaveSessionAsync(session, ct);
    }

    public async IAsyncEnumerable<AgentResponseChunk> SendMessageAsync(
        Guid sessionId, string userMessage,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        await _store.SaveAgentMessageAsync(new AgentMessage
        {
            SessionId = sessionId,
            Role = AgentRole.User,
            Content = userMessage
        }, ct);

        var reply = "AI Agent is not yet configured. Please set up a Semantic Kernel provider in Settings (Phase 4).";

        await _store.SaveAgentMessageAsync(new AgentMessage
        {
            SessionId = sessionId,
            Role = AgentRole.Assistant,
            Content = reply
        }, ct);

        yield return new AgentResponseChunk(reply, IsFinal: true);
    }

    public Task<IReadOnlyList<AgentSession>> ListSessionsAsync(CancellationToken ct = default) =>
        _store.ListSessionsAsync(ct);
}
