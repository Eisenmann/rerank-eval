using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ReRankEval.Domain.Interfaces;
using ReRankEval.Domain.Models;

namespace ReRankEval.App.ViewModels;

public partial class AgentViewModel : ObservableObject
{
    private readonly IAgentOrchestrator _orchestrator;

    [ObservableProperty] private string _userInput = "";
    [ObservableProperty] private bool _isSending;
    [ObservableProperty] private Guid _currentSessionId;

    public ObservableCollection<AgentMessageItem> Messages { get; } = [];
    public ObservableCollection<AgentAction> ActionLog { get; } = [];

    public AgentViewModel(IAgentOrchestrator orchestrator)
    {
        _orchestrator = orchestrator;
        _ = StartSessionAsync();
    }

    [RelayCommand]
    private async Task SendAsync()
    {
        if (string.IsNullOrWhiteSpace(UserInput) || IsSending) return;

        var text = UserInput;
        UserInput = "";
        IsSending = true;

        Messages.Add(new AgentMessageItem(AgentRole.User, text, DateTime.Now));

        try
        {
            var responseText = new System.Text.StringBuilder();
            await foreach (var chunk in _orchestrator.SendMessageAsync(CurrentSessionId, text))
            {
                if (chunk.ToolCall is not null)
                    ActionLog.Insert(0, chunk.ToolCall);

                responseText.Append(chunk.Text);

                if (chunk.IsFinal)
                    Messages.Add(new AgentMessageItem(AgentRole.Assistant, responseText.ToString(), DateTime.Now));
            }
        }
        catch (Exception ex)
        {
            Messages.Add(new AgentMessageItem(AgentRole.Tool, $"Error: {ex.Message}", DateTime.Now));
        }
        finally
        {
            IsSending = false;
        }
    }

    [RelayCommand]
    private async Task NewSessionAsync()
    {
        Messages.Clear();
        ActionLog.Clear();
        await StartSessionAsync();
    }

    private async Task StartSessionAsync()
    {
        var session = await _orchestrator.StartSessionAsync();
        CurrentSessionId = session.Id;
    }
}

public record AgentMessageItem(AgentRole Role, string Content, DateTime Timestamp);
