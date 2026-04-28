using System;
using System.Threading;
using System.Threading.Tasks;
using System.Net.Http;
using HeyeTodo.Client.Application.Tasks;
using HeyeTodo.Client.Application.Planning;
using HeyeTodo.Client.Application.Sync;
using HeyeTodo.Client.Data;
using HeyeTodo.Client.Data.Repositories;
using HeyeTodo.Client.Infrastructure;
using HeyeTodo.Client.Infrastructure.Auth;
using HeyeTodo.Client.Infrastructure.Localization;
using HeyeTodo.Client.Infrastructure.Logging;
using HeyeTodo.Client.Infrastructure.Navigation;
using HeyeTodo.Client.Infrastructure.Networking;
using HeyeTodo.Client.ViewModels;
using HeyeTodo.Client.Views;
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
        sc.AddSingleton<IClientLogger, FileClientLogger>();
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
            return new ApiClient(http, sp.GetRequiredService<TokenStore>(), sp.GetRequiredService<ISettingsService>(), sp.GetRequiredService<IClientLogger>(), clientId);
        });
        sc.AddTransient<ProjectApiClient>();
        sc.AddTransient<TaskApiClient>();
        sc.AddSingleton<SyncCursorStore>();
        sc.AddSingleton<SyncOutboxStore>();
        sc.AddSingleton<SyncInboxStore>();
        sc.AddSingleton<SignalRSyncClient>();
        sc.AddTransient<SyncApiClient>();

        // ─── Local DB ────────────────────────────────────────
        sc.AddDbContextFactory<LocalDbContext>(o =>
            o.UseSqlite($"Data Source={AppPaths.LocalDbPath}"));
        sc.AddTransient<IProjectRepository, LocalProjectRepository>();
        sc.AddTransient<ITaskRepository, LocalTaskRepository>();
        sc.AddTransient<IDependencyRepository, LocalDependencyRepository>();
        sc.AddSingleton<ISyncCoordinator, SyncCoordinator>();
        sc.AddTransient<ITaskWorkspaceService, TaskWorkspaceService>();
        sc.AddSingleton<IPlanningDriver, RulePlanningDriver>();
        sc.AddSingleton<IPlanningDriver, ServerProxyPlanningDriver>();
        sc.AddSingleton<IPlanningDriver, ClientKeyPlanningDriver>();
        sc.AddSingleton<IPlanningApplicationService, PlanningApplicationService>();

        // ─── ViewModels ──────────────────────────────────────
        sc.AddSingleton<MainWindow>();
        sc.AddSingleton<MainWindowViewModel>();
        sc.AddTransient<ShellViewModel>();
        sc.AddTransient<LoginViewModel>();
        sc.AddTransient<RegisterViewModel>();
        sc.AddTransient<RoleSelectionViewModel>();
        sc.AddTransient<TaskListViewModel>();
        sc.AddTransient<GanttViewModel>();
        sc.AddTransient<RolePanelsViewModel>();
        sc.AddTransient<PlanningViewModel>();
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
            var logger = scope.ServiceProvider.GetRequiredService<IClientLogger>();
            logger.LogInfoAsync("Client started.").GetAwaiter().GetResult();
            db.Database.EnsureCreated();
            EnsureLocalSyncSchema(db);
        }, ct);

        splash.Status = LocalizationService.Instance["Splash.Ready"];
    }

    private static void EnsureLocalSyncSchema(LocalDbContext db)
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Outbox" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Outbox" PRIMARY KEY,
                "OwnerId" TEXT NOT NULL,
                "EntityType" INTEGER NOT NULL,
                "Operation" INTEGER NOT NULL,
                "EntityId" TEXT NOT NULL,
                "PayloadJson" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "UpdatedBy" TEXT NOT NULL,
                "ClientId" TEXT NOT NULL,
                "EnqueuedAt" TEXT NOT NULL,
                "AcknowledgedAt" TEXT NULL,
                "ConflictReason" TEXT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_Outbox_OwnerId_AcknowledgedAt" ON "Outbox" ("OwnerId", "AcknowledgedAt");
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_Outbox_OwnerId_EntityType_EntityId" ON "Outbox" ("OwnerId", "EntityType", "EntityId");
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS "Inbox" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_Inbox" PRIMARY KEY,
                "OwnerId" TEXT NOT NULL,
                "EntityType" INTEGER NOT NULL,
                "Operation" INTEGER NOT NULL,
                "EntityId" TEXT NOT NULL,
                "ServerVersion" INTEGER NOT NULL,
                "PayloadJson" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "UpdatedBy" TEXT NOT NULL,
                "ClientId" TEXT NOT NULL,
                "ReceivedAt" TEXT NOT NULL,
                "AppliedAt" TEXT NULL
            );
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE INDEX IF NOT EXISTS "IX_Inbox_OwnerId_ServerVersion" ON "Inbox" ("OwnerId", "ServerVersion");
            """);
        db.Database.ExecuteSqlRaw("""
            CREATE UNIQUE INDEX IF NOT EXISTS "IX_Inbox_OwnerId_EntityType_EntityId_ServerVersion" ON "Inbox" ("OwnerId", "EntityType", "EntityId", "ServerVersion");
            """);
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
