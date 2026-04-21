using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Networking;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class LoginViewModel : ViewModelBase
{
    private readonly ApiClient _api;
    private readonly ClientSession _session;

    [ObservableProperty] private string _email = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string? _errorMessage;
    [ObservableProperty] private bool _isBusy;

    public LoginViewModel(ApiClient api, ClientSession session)
    {
        _api = api;
        _session = session;
    }

    [RelayCommand]
    private async Task LoginAsync()
    {
        ErrorMessage = null;
        IsBusy = true;
        try
        {
            var r = await _api.LoginAsync(Email.Trim(), Password);
            if (r is null)
            {
                ErrorMessage = "Login failed";
                return;
            }
            _session.IsAuthenticated = true;
            _session.UserId = r.User.Id;
            _session.DisplayName = r.User.DisplayName;

            // After login: if user hasn't chosen any roles yet, show role selection (skippable).
            var shell = AppHost.Services.GetRequiredService<ShellViewModel>();
            // TODO(M1): route to RoleSelectionViewModel when Roles == None.
            MainSwitcher.Switch(shell);
        }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private void GoToRegister()
    {
        var vm = AppHost.Services.GetRequiredService<RegisterViewModel>();
        MainSwitcher.Switch(vm);
    }
}

/// <summary>
/// Helper that mutates the top-level <see cref="MainWindowViewModel.Current"/> content.
/// Kept very small so unit tests can replace it.
/// </summary>
internal static class MainSwitcher
{
    public static void Switch(ViewModelBase vm)
    {
        var main = AppHost.Services.GetRequiredService<MainWindowViewModel>();
        main.Current = vm;
    }
}
