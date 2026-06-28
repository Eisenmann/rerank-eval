using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public partial class AgentViewModel : ObservableObject
{
    private readonly IAgentOrchestrator _orchestrator;
    private readonly IExperimentStore _store;

    [ObservableProperty] private string _userInput = "";
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private bool _isStreaming;
    [ObservableProperty] private string _streamingText = "";
    [ObservableProperty] private Guid _currentSessionId;
    [ObservableProperty] private AgentSession? _selectedSession;

    public ObservableCollection<AgentMessageItem> Messages { get; } = [];
    public ObservableCollection<AgentAction> ActionLog { get; } = [];
    public ObservableCollection<AgentSession> Sessions { get; } = [];

    public AgentViewModel(IAgentOrchestrator orchestrator, IExperimentStore store)
    {
        _orchestrator = orchestrator;
        _store = store;
        _ = InitAsync();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsSending) return;

        var text = UserInput;
        UserInput = "";
        IsSending = true;
        IsStreaming = true;
        StreamingText = "";
        Messages.Add(new AgentMessageItem(AgentRole.User, text, DateTime.Now));

        var sb = new System.Text.StringBuilder();
        try
        {
            await foreach (var chunk in _orchestrator.SendMessageAsync(CurrentSessionId, text))
            {
                if (chunk.ToolCall is not null)
                    ActionLog.Insert(0, chunk.ToolCall);

                if (!chunk.IsFinal && !string.IsNullOrEmpty(chunk.Text))
                {
                    sb.Append(chunk.Text);
                    StreamingText = sb.ToString();
                }

                if (chunk.IsFinal)
                {
                    var content = sb.Length > 0 ? sb.ToString() : chunk.Text;
                    if (!string.IsNullOrEmpty(content))
                        Messages.Add(new AgentMessageItem(AgentRole.Assistant, content, DateTime.Now));
                }
            }
        }
        catch (Exception ex)
        {
            Messages.Add(new AgentMessageItem(AgentRole.Tool, $"Error: {ex.Message}", DateTime.Now));
        }
        finally
        {
            IsStreaming = false;
            StreamingText = "";
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task NewSessionAsync()
    {
        Messages.Clear();
        ActionLog.Clear();
        IsStreaming = false;
        StreamingText = "";
        var session = await _orchestrator.StartSessionAsync();
        CurrentSessionId = session.Id;
        SelectedSession = session;
        Sessions.Insert(0, session);
    }

    [RelayCommand]
    private async Task SelectSessionAsync(AgentSession session)
    {
        if (session == null) return;
        SelectedSession = session;
        CurrentSessionId = session.Id;
        Messages.Clear();
        ActionLog.Clear();
        StreamingText = "";
        IsStreaming = false;

        var loaded = await _store.GetSessionAsync(session.Id);
        if (loaded != null)
        {
            foreach (var msg in loaded.Messages.OrderBy(m => m.Timestamp))
                Messages.Add(new AgentMessageItem(msg.Role, msg.Content, msg.Timestamp));
            foreach (var action in loaded.ExecutedActions.OrderByDescending(a => a.Timestamp))
                ActionLog.Add(action);
        }
    }

    private async Task InitAsync()
    {
        var all = await _orchestrator.ListSessionsAsync();
        foreach (var s in all.OrderByDescending(s => s.LastActiveAt))
            Sessions.Add(s);

        if (Sessions.Count > 0)
            await SelectSessionAsync(Sessions[0]);
        else
            await NewSessionAsync();
    }
}

public record AgentMessageItem(AgentRole Role, string Content, DateTime Timestamp);
