using System;
using System.IO;
using System.Linq;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Threading.Tasks;
using Avalonia.Controls;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Shared.Enums;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly INavigationService _navigationService;
    private readonly ApiClient _api;
    private readonly ClientSession _session;
    private readonly IClientLogger _logger;

    [ObservableProperty]
    private ViewModelBase _current = null!;

#if DEBUG
    [ObservableProperty]
    private string? _debugInfo;

    [ObservableProperty]
    private bool _isDebugInfoVisible;
#endif

    public MainWindowViewModel(INavigationService navigationService, ApiClient api, ClientSession session, IClientLogger logger)
    {
        _navigationService = navigationService;
        _api = api;
        _session = session;
        _logger = logger;
        navigationService.Navigated += viewModel => Current = viewModel;

        _ = InitializeAsync(navigationService, api, session);
    }

    private async Task InitializeAsync(INavigationService navigationService, ApiClient api, ClientSession session)
    {
        if (!api.IsAuthenticated)
        {
            session.Reset();
            navigationService.NavigateTo<LoginViewModel>();
            return;
        }

        var user = await api.GetCurrentUserAsync();
        if (user is null)
        {
            await api.LogoutAsync();
            session.Reset();
            navigationService.NavigateTo<LoginViewModel>();
            return;
        }

        session.IsAuthenticated = true;
        session.UserId = user.Id;
        session.DisplayName = user.DisplayName;
        session.Roles = user.Roles;
        session.ActiveRoleContext = user.ActiveRoleContext;

        if (user.Roles == RoleType.None)
        {
            navigationService.NavigateTo<RoleSelectionViewModel>();
            return;
        }

        navigationService.NavigateTo<ShellViewModel>();
    }

#if DEBUG
    [RelayCommand]
    private void ToggleDebugInfo()
    {
        IsDebugInfoVisible = !IsDebugInfoVisible;
        if (IsDebugInfoVisible)
        {
            RefreshDebugInfo();
        }
    }

    [RelayCommand]
    private void RefreshDebugInfo()
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Time: {DateTimeOffset.Now:O}");
        builder.AppendLine($"Current: {Current?.GetType().Name ?? "<null>"}");
        builder.AppendLine($"ApiAuthenticated: {_api.IsAuthenticated}");
        builder.AppendLine($"SessionAuthenticated: {_session.IsAuthenticated}");
        builder.AppendLine($"UserId: {_session.UserId?.ToString() ?? "<null>"}");
        builder.AppendLine($"Roles: {_session.Roles}");
        builder.AppendLine($"ActiveRole: {_session.ActiveRoleContext?.ToString() ?? "<null>"}");

        if (Current is ShellViewModel shell)
        {
            builder.AppendLine($"ShellNavItems: {shell.NavItems.Count}");
            builder.AppendLine($"ShellSelectedNav: {shell.SelectedNav?.Label ?? "<null>"}");
            builder.AppendLine($"ShellContent: {shell.Content?.GetType().Name ?? "<null>"}");
        }

        builder.AppendLine($"LogPath: {_logger.LogFilePath}");
        builder.AppendLine($"DbPath: {AppPaths.LocalDbPath}");
        builder.AppendLine($"DbExists: {File.Exists(AppPaths.LocalDbPath)}");
        builder.AppendLine($"ShellViewResolved: {ResolveViewName(Current)}");
        if (Current is ShellViewModel shellVm)
        {
            builder.AppendLine($"ShellContentViewResolved: {ResolveViewName(shellVm.Content)}");
        }

        if (File.Exists(_logger.LogFilePath))
        {
            builder.AppendLine("LogTail:");
            foreach (var line in File.ReadLines(_logger.LogFilePath).TakeLast(20))
            {
                builder.AppendLine(line);
            }
        }
        else
        {
            builder.AppendLine("LogTail: <no log file>");
        }

        DebugInfo = builder.ToString();
    }

    private static string ResolveViewName(object? viewModel)
    {
        if (viewModel is null)
        {
            return "<null>";
        }

        var name = viewModel.GetType().FullName!.Replace("ViewModel", "View", StringComparison.Ordinal);
        var type = Type.GetType(name);
        return type is null ? $"Not Found: {name}" : type.Name;
    }
#endif
}
