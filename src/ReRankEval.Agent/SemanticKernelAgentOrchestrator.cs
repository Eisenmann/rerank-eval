using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;
using ReRankEval.Agent.Plugins;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.Agent;

public sealed class SemanticKernelAgentOrchestrator : IAgentOrchestrator
{
    private readonly IExperimentStore _store;
    private readonly IModelRegistry _modelRegistry;
    private readonly IHFHubClient _hubClient;
    private readonly IAnalysisService _analysis;
    private readonly IFineTuningService _fineTuning;
    private readonly IAppSettingsService _settingsService;
    private readonly ICredentialStore _credentialStore;
    private readonly ILogger<SemanticKernelAgentOrchestrator> _logger;
    private readonly string _appDataDir;

    // In-memory chat histories per session
    private readonly ConcurrentDictionary<Guid, ChatHistory> _histories = new();

    public SemanticKernelAgentOrchestrator(
        IExperimentStore store,
        IModelRegistry modelRegistry,
        IHFHubClient hubClient,
        IAnalysisService analysis,
        IFineTuningService fineTuning,
        IAppSettingsService settingsService,
        ICredentialStore credentialStore,
        ILogger<SemanticKernelAgentOrchestrator> logger,
        string appDataDir)
    {
        _store = store;
        _modelRegistry = modelRegistry;
        _hubClient = hubClient;
        _analysis = analysis;
        _fineTuning = fineTuning;
        _settingsService = settingsService;
        _credentialStore = credentialStore;
        _logger = logger;
        _appDataDir = appDataDir;
    }

    public async Task<AgentSession> StartSessionAsync(CancellationToken ct = default)
    {
        var session = new AgentSession { Title = $"Session {DateTime.UtcNow:yyyy-MM-dd HH:mm}" };
        return await _store.SaveSessionAsync(session, ct);
    }

    public async IAsyncEnumerable<AgentResponseChunk> SendMessageAsync(
        Guid sessionId,
        string userMessage,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await _store.SaveAgentMessageAsync(new AgentMessage
        {
            SessionId = sessionId,
            Role = AgentRole.User,
            Content = userMessage
        }, ct);

        Kernel? kernel;
        try
        {
            kernel = await BuildKernelAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to build SK kernel");
            kernel = null;
        }

        if (kernel == null)
        {
            const string notConfigured = "AI Agent is not configured. Please add your API key in **Settings** (sidebar).";
            await _store.SaveAgentMessageAsync(new AgentMessage
            {
                SessionId = sessionId,
                Role = AgentRole.Assistant,
                Content = notConfigured
            }, ct);
            yield return new AgentResponseChunk(notConfigured, IsFinal: true);
            yield break;
        }

        var history = _histories.GetOrAdd(sessionId, _ => CreateSystemHistory());
        history.AddUserMessage(userMessage);

        var pendingActions = new ConcurrentQueue<AgentAction>();
        var filter = new ToolCallFilter(sessionId, pendingActions);
        kernel.FunctionInvocationFilters.Add(filter);

        var channel = System.Threading.Channels.Channel.CreateUnbounded<AgentResponseChunk>();

        // Run streaming in background task so we can yield outside try/catch
        var streamTask = StreamToChannelAsync(kernel, history, sessionId, channel.Writer, ct);

        await foreach (var chunk in channel.Reader.ReadAllAsync(ct))
        {
            // Drain any tool calls queued by the filter
            while (pendingActions.TryDequeue(out var action))
                yield return new AgentResponseChunk("", IsFinal: false, ToolCall: action);

            yield return chunk;
        }

        // Drain remaining tool calls after stream ends
        while (pendingActions.TryDequeue(out var action))
            yield return new AgentResponseChunk("", IsFinal: false, ToolCall: action);

        kernel.FunctionInvocationFilters.Remove(filter);

        await streamTask;
    }

    public Task<IReadOnlyList<AgentSession>> ListSessionsAsync(CancellationToken ct = default) =>
        _store.ListSessionsAsync(ct);

    private async Task StreamToChannelAsync(
        Kernel kernel,
        ChatHistory history,
        Guid sessionId,
        System.Threading.Channels.ChannelWriter<AgentResponseChunk> writer,
        CancellationToken ct)
    {
        var sb = new StringBuilder();
        try
        {
            var chatService = kernel.Services.GetService(typeof(IChatCompletionService)) as IChatCompletionService;
            if (chatService == null)
            {
                await writer.WriteAsync(new AgentResponseChunk("No chat service available.", IsFinal: true), ct);
                return;
            }

#pragma warning disable SKEXP0001
            var settings = new OpenAIPromptExecutionSettings
            {
                FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
            };
#pragma warning restore SKEXP0001

            await foreach (var chunk in chatService.GetStreamingChatMessageContentsAsync(
                history, settings, kernel, ct))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    sb.Append(chunk.Content);
                    await writer.WriteAsync(new AgentResponseChunk(chunk.Content, IsFinal: false), ct);
                }
            }

            var fullText = sb.ToString();
            if (!string.IsNullOrEmpty(fullText))
                history.AddAssistantMessage(fullText);

            await _store.SaveAgentMessageAsync(new AgentMessage
            {
                SessionId = sessionId,
                Role = AgentRole.Assistant,
                Content = fullText
            }, ct);

            await writer.WriteAsync(new AgentResponseChunk("", IsFinal: true), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during agent streaming");
            var errorText = $"Error: {ex.Message}";
            await _store.SaveAgentMessageAsync(new AgentMessage
            {
                SessionId = sessionId,
                Role = AgentRole.Assistant,
                Content = errorText
            }, CancellationToken.None);
            await writer.WriteAsync(new AgentResponseChunk(errorText, IsFinal: true), CancellationToken.None);
        }
        finally
        {
            writer.TryComplete();
        }
    }

    private async Task<Kernel?> BuildKernelAsync(CancellationToken ct)
    {
        var settings = await _settingsService.GetSettingsAsync(ct);
        var apiKey = await _credentialStore.LoadAsync($"llm:{settings.LlmProvider}", ct);

        if (string.IsNullOrWhiteSpace(apiKey) && settings.LlmProvider != "Ollama")
            return null;

        var builder = Kernel.CreateBuilder();

        switch (settings.LlmProvider)
        {
            case "OpenAI":
                builder.AddOpenAIChatCompletion(settings.ModelId, apiKey!);
                break;

            case "Azure OpenAI":
                if (string.IsNullOrWhiteSpace(settings.AzureEndpoint) ||
                    string.IsNullOrWhiteSpace(settings.AzureDeploymentName))
                    return null;
                builder.AddAzureOpenAIChatCompletion(
                    settings.AzureDeploymentName, settings.AzureEndpoint, apiKey!);
                break;

            case "Ollama":
                // Ollama exposes an OpenAI-compatible API on localhost:11434
                var ollamaKey = string.IsNullOrWhiteSpace(apiKey) ? "ollama" : apiKey;
                builder.AddOpenAIChatCompletion(settings.ModelId, ollamaKey);
                break;

            default:
                return null;
        }

        builder.Plugins.AddFromObject(
            new ModelManagementPlugin(_modelRegistry, _hubClient), "ModelManagement");
        builder.Plugins.AddFromObject(
            new EvaluationPlugin(_store, _modelRegistry), "Evaluation");
        builder.Plugins.AddFromObject(
            new DatasetPlugin(_store), "Dataset");
        builder.Plugins.AddFromObject(
            new MetricsAnalysisPlugin(_analysis, _store, _modelRegistry), "MetricsAnalysis");
        builder.Plugins.AddFromObject(
            new FineTuningPlugin(_fineTuning, _store, _modelRegistry), "FineTuning");
        builder.Plugins.AddFromObject(
            new ReportingPlugin(_store, _modelRegistry, _appDataDir), "Reporting");

        return builder.Build();
    }

    private static ChatHistory CreateSystemHistory() => new(
        """
        You are an AI assistant embedded in RerankEval, a desktop application for evaluating
        and fine-tuning information-retrieval reranker models. You have access to tools that let
        you list models, run evaluations, analyze metrics, manage fine-tuning, and generate reports.
        Be concise and use Markdown tables and headings to present data clearly.
        """);
}

/// <summary>
/// Captures Semantic Kernel function invocations and queues them as AgentActions.
/// </summary>
internal sealed class ToolCallFilter : IFunctionInvocationFilter
{
    private readonly Guid _sessionId;
    private readonly ConcurrentQueue<AgentAction> _queue;

    public ToolCallFilter(Guid sessionId, ConcurrentQueue<AgentAction> queue)
    {
        _sessionId = sessionId;
        _queue = queue;
    }

    public async Task OnFunctionInvocationAsync(
        FunctionInvocationContext context,
        Func<FunctionInvocationContext, Task> next)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var paramsJson = JsonSerializer.Serialize(
            context.Arguments.ToDictionary(kv => kv.Key, kv => kv.Value?.ToString()));

        try
        {
            await next(context);
            sw.Stop();

            _queue.Enqueue(new AgentAction
            {
                SessionId = _sessionId,
                ToolName = $"{context.Function.PluginName}.{context.Function.Name}",
                ParametersJson = paramsJson,
                ResultJson = context.Result?.GetValue<object>()?.ToString(),
                Status = ActionStatus.Succeeded,
                Duration = sw.Elapsed
            });
        }
        catch
        {
            sw.Stop();
            _queue.Enqueue(new AgentAction
            {
                SessionId = _sessionId,
                ToolName = $"{context.Function.PluginName}.{context.Function.Name}",
                ParametersJson = paramsJson,
                Status = ActionStatus.Failed,
                Duration = sw.Elapsed
            });
            throw;
        }
    }
}
