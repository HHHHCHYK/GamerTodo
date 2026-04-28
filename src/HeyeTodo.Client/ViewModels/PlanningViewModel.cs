using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Application.Planning;
using HeyeTodo.Client.Application.Tasks;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Client.Infrastructure.Logging;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class PlanningViewModel : ViewModelBase
{
    private readonly ITaskWorkspaceService _workspace;
    private readonly IPlanningApplicationService _planning;
    private readonly ISettingsService _settings;
    private readonly IClientLogger _logger;
    private readonly ClientSession _session;

    public ObservableCollection<ProjectItemViewModel> Projects { get; } = new();
    public ObservableCollection<PlanningSuggestionViewModel> Suggestions { get; } = new();
    public ObservableCollection<PlanningIssueViewModel> Issues { get; } = new();

    [ObservableProperty] private ProjectItemViewModel? _selectedProject;
    [ObservableProperty] private string _prompt = string.Empty;
    [ObservableProperty] private string _driverLabel = string.Empty;
    [ObservableProperty] private string? _summary;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public PlanningViewModel(ITaskWorkspaceService workspace, IPlanningApplicationService planning, ISettingsService settings, IClientLogger logger)
    {
        _workspace = workspace;
        _planning = planning;
        _settings = settings;
        _logger = logger;
        _session = AppHost.Services.GetRequiredService<ClientSession>();
        DriverLabel = BuildDriverLabel();
        _settings.Changed += OnSettingsChanged;
        _ = LoadProjectsAsync();
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        await LoadProjectsAsync();
    }

    [RelayCommand]
    private async Task GenerateAsync()
    {
        if (_session.UserId is null)
        {
            ErrorMessage = LocalizationService.Instance["Tasks.Error.NoSession"];
            return;
        }

        ErrorMessage = null;
        Summary = null;
        Suggestions.Clear();
        Issues.Clear();
        IsBusy = true;
        try
        {
            var result = await _planning.PlanAsync(_session.UserId.Value, SelectedProject?.Id, Prompt);
            Summary = result.Summary;
            DriverLabel = BuildDriverLabel(result.Driver);
            Suggestions.ReplaceWith(result.Suggestions.Select(x => new PlanningSuggestionViewModel(x)));
            Issues.ReplaceWith(result.Issues.Select(x => new PlanningIssueViewModel(x)));
            await _logger.LogOperationAsync("Planning", "Generate", ClientLogLevel.Information, "Planning suggestions generated.", new Dictionary<string, object?>
            {
                ["projectId"] = SelectedProject?.Id,
                ["driver"] = result.Driver,
                ["suggestionCount"] = Suggestions.Count,
                ["issueCount"] = Issues.Count,
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _logger.LogUserOperationExceptionAsync("PlanningGenerate", ex, new Dictionary<string, object?>
            {
                ["projectId"] = SelectedProject?.Id,
                ["planningMode"] = _settings.Current.PlanningMode,
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task LoadProjectsAsync()
    {
        if (_session.UserId is null)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var currentProjectId = SelectedProject?.Id;
            var projects = await _workspace.ListProjectsAsync(_session.UserId.Value);
            Projects.ReplaceWith(projects.Select(x => new ProjectItemViewModel(x)));
            SelectedProject = currentProjectId is not null
                ? Projects.FirstOrDefault(x => x.Id == currentProjectId.Value)
                : Projects.FirstOrDefault();
            await _logger.LogOperationAsync("Planning", "LoadProjects", ClientLogLevel.Information, "Planning projects loaded.", new Dictionary<string, object?>
            {
                ["projectCount"] = Projects.Count,
                ["selectedProjectId"] = SelectedProject?.Id,
            });
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            await _logger.LogUserOperationExceptionAsync("PlanningLoadProjects", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void OnSettingsChanged(object? sender, SettingsChangedEventArgs e)
    {
        if (e.PlanningModeChanged || e.PlanningDriverChanged)
        {
            DriverLabel = BuildDriverLabel();
        }
    }

    private string BuildDriverLabel(string? actualDriver = null)
    {
        var mode = actualDriver ?? _settings.Current.PlanningMode;
        return string.Format(LocalizationService.Instance["Planning.Driver"], mode);
    }
}

public sealed class PlanningSuggestionViewModel
{
    public int Rank { get; }
    public Guid TaskId { get; }
    public string Score { get; }
    public string DateRange { get; }
    public string Reason { get; }

    public PlanningSuggestionViewModel(HeyeTodo.Shared.Contracts.Planning.PlanningSuggestion suggestion)
    {
        Rank = suggestion.Rank;
        TaskId = suggestion.TaskId;
        Score = suggestion.Score.ToString("0.##");
        DateRange = suggestion.SuggestedStartDate is null || suggestion.SuggestedEndDate is null
            ? "-"
            : $"{suggestion.SuggestedStartDate:yyyy-MM-dd} → {suggestion.SuggestedEndDate:yyyy-MM-dd}";
        Reason = suggestion.Reason;
    }
}

public sealed class PlanningIssueViewModel
{
    public string Code { get; }
    public string Message { get; }

    public PlanningIssueViewModel(HeyeTodo.Shared.Contracts.Planning.PlanningIssue issue)
    {
        Code = issue.Code;
        Message = issue.Message;
    }
}
