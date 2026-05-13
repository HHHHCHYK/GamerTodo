using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using HeyeTodo.Client.Services;
using HeyeTodo.Shared.Contracts.Auth;

namespace HeyeTodo.Client.ViewModels;

public sealed partial class AccountViewModel : ViewModelBase
{
    private readonly HeyeTodoApiClient _api;
    private readonly IClientSessionStore _sessionStore;

    public AccountViewModel(HeyeTodoApiClient api, IClientSessionStore sessionStore)
    {
        _api = api;
        _sessionStore = sessionStore;
        _ = LoadAsync();
    }

    [ObservableProperty]
    private string _serverBaseUrl = "http://localhost:5254";

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    private string _displayName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = "未登录";

    [ObservableProperty]
    private bool _isBusy;

    [RelayCommand]
    private async Task Login()
    {
        await RunAuthAsync(async () =>
        {
            var auth = await _api.LoginAsync(ServerBaseUrl, Email, Password);
            StatusMessage = $"已登录：{auth.User.DisplayName}";
        });
    }

    [RelayCommand]
    private async Task Register()
    {
        await RunAuthAsync(async () =>
        {
            var auth = await _api.RegisterAsync(ServerBaseUrl, new RegisterRequest(Email, Password, DisplayName, HeyeTodo.Shared.Enums.RoleType.None));
            StatusMessage = $"已注册并登录：{auth.User.DisplayName}";
        });
    }

    [RelayCommand]
    private async Task Logout()
    {
        await _sessionStore.ClearTokensAsync();
        StatusMessage = "已退出登录，本地任务不会删除";
    }

    private async Task LoadAsync()
    {
        var session = await _sessionStore.LoadAsync();
        ServerBaseUrl = string.IsNullOrWhiteSpace(session.ServerBaseUrl) ? ServerBaseUrl : session.ServerBaseUrl;
        StatusMessage = session.IsAuthenticated ? "已保存登录状态" : "未登录";
    }

    private async Task RunAuthAsync(Func<Task> action)
    {
        if (string.IsNullOrWhiteSpace(ServerBaseUrl) || string.IsNullOrWhiteSpace(Email) || string.IsNullOrWhiteSpace(Password))
        {
            StatusMessage = "请填写服务器地址、邮箱和密码";
            return;
        }

        IsBusy = true;
        StatusMessage = "正在连接服务器";
        try
        {
            await action();
        }
        catch (Exception exception) when (exception is HttpRequestException or InvalidOperationException or TaskCanceledException)
        {
            StatusMessage = $"认证失败：{exception.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }
}
