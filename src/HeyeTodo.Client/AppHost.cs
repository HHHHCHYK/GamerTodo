using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using HeyeTodo.Client.Application.Tasks;
using HeyeTodo.Client.Data;
using HeyeTodo.Client.Data.Repositories;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Auth;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Client.ViewModels;
using HeyeTodo.Shared.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace HeyeTodo.Client;

/// <summary>
/// Application-wide composition root. Resolves into a <see cref="IServiceProvider"/> exposed
/// on <see cref="Services"/>; consumers locate types via constructor injection / <c>GetRequiredService</c>.
/// </summary>
public static class AppHost
{
    public static IServiceProvider Services { get; private set; } = null!;

    public static void BootstrapCore()
    {
        var settings = SettingsStore.Load();
        var clientId = AppPaths.GetOrCreateClientId();

        var sc = new ServiceCollection();

        // ─── Singletons ──────────────────────────────────────
        sc.AddSingleton(settings);
        sc.AddSingleton<ISettingsService, SettingsService>();
        sc.AddSingleton(new TokenStore(AppPaths.TokenStorePath));
        sc.AddSingleton<ClientSession>();
        sc.AddSingleton<INavigationService, NavigationService>();

        // ─── HttpClient + ApiClient ──────────────────────────
        sc.AddHttpClient("api", http =>
        {
            http.Timeout = TimeSpan.FromSeconds(30);
        });
        sc.AddSingleton<ApiClient>(sp =>
        {
            var factory = sp.GetRequiredService<IHttpClientFactory>();
            var http = factory.CreateClient("api");
            return new ApiClient(http, sp.GetRequiredService<TokenStore>(), sp.GetRequiredService<ISettingsService>(), clientId);
        });
        sc.AddTransient<ProjectApiClient>();
        sc.AddTransient<TaskApiClient>();

        // ─── Local DB ────────────────────────────────────────
        sc.AddDbContextFactory<LocalDbContext>(o =>
            o.UseSqlite($"Data Source={AppPaths.LocalDbPath}"));
        sc.AddTransient<IProjectRepository, LocalProjectRepository>();
        sc.AddTransient<ITaskRepository, LocalTaskRepository>();
        sc.AddTransient<ITaskWorkspaceService, TaskWorkspaceService>();

        // ─── ViewModels ──────────────────────────────────────
        sc.AddSingleton<MainWindowViewModel>();
        sc.AddTransient<ShellViewModel>();
        sc.AddTransient<LoginViewModel>();
        sc.AddTransient<RegisterViewModel>();
        sc.AddTransient<RoleSelectionViewModel>();
        sc.AddTransient<TaskListViewModel>();
        sc.AddTransient<GanttViewModel>();
        sc.AddTransient<MiniGamesHubViewModel>();
        sc.AddTransient<SettingsViewModel>();
        sc.AddTransient<SplashViewModel>();

        Services = sc.BuildServiceProvider();
    }

    public static async Task WarmupAsync(SplashViewModel splash, CancellationToken ct = default)
    {
        splash.Status = LocalizationService.Instance["Splash.LoadingSettings"];
        await Task.Yield();

        splash.Status = LocalizationService.Instance["Splash.InitializingDatabase"];

        await Task.Run(() =>
        {
            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<IDbContextFactory<LocalDbContext>>().CreateDbContext();
            db.Database.EnsureCreated();
        }, ct);

        splash.Status = LocalizationService.Instance["Splash.Ready"];
    }
}

/// <summary>
/// In-memory session state (current user, active project, connection status).
/// View models subscribe to this via <see cref="PropertyChanged"/>.
/// </summary>
public sealed class ClientSession : System.ComponentModel.INotifyPropertyChanged
{
    private bool _isAuthenticated;
    private Guid? _userId;
    private string? _displayName;
    private RoleType _roles;
    private RoleType? _activeRoleContext;

    public bool IsAuthenticated
    {
        get => _isAuthenticated;
        set { _isAuthenticated = value; OnChanged(nameof(IsAuthenticated)); }
    }

    public Guid? UserId
    {
        get => _userId;
        set { _userId = value; OnChanged(nameof(UserId)); }
    }

    public string? DisplayName
    {
        get => _displayName;
        set { _displayName = value; OnChanged(nameof(DisplayName)); }
    }

    public RoleType Roles
    {
        get => _roles;
        set { _roles = value; OnChanged(nameof(Roles)); }
    }

    public RoleType? ActiveRoleContext
    {
        get => _activeRoleContext;
        set { _activeRoleContext = value; OnChanged(nameof(ActiveRoleContext)); }
    }

    public void Reset()
    {
        IsAuthenticated = false;
        UserId = null;
        DisplayName = null;
        Roles = RoleType.None;
        ActiveRoleContext = null;
    }

    public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    private void OnChanged(string name) =>
        PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));
}
