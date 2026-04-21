# M0 Review

Date of review: M0 exit checkpoint.

## Build status

- `dotnet build HeyeTodo.sln` → 0 errors, 0 warnings
- `HeyeTodo.Shared.dll`, `HeyeTodo.Server.dll`, `HeyeTodo.Client.dll` all
  produced
- Initial EF migration `20260421070850_InitialCreate` generated successfully
- Server runtime smoke: boots to the point of DB connection attempt; fails
  only because PostgreSQL is not running locally (expected)

## Deliverable checklist

| Layer | Artifact | Status |
|---|---|---|
| Shared | `RoleType [Flags]`, `TaskStatus`, `TaskPriority`, `DependencyType`, `SyncMeta`, Auth/Tasks/Sync contracts | OK |
| Server Domain | `AppUser`, `RefreshToken`, `SyncableEntity`, `Project`, `TodoTask`, `TaskDependency` | OK |
| Server Infrastructure | `AppDbContext`, `AppDbContextDesignFactory`, `JwtOptions`, `PasswordHasher`, `TokenService`, `LocalizationSetup`, `SharedResource` (+ zh) | OK |
| Server Application | `AuthService`, `ServiceResult` | OK |
| Server Api | `AuthController`, `UsersController`, `SyncHub` | OK |
| Server Program | JWT + SignalR + CORS + OpenAPI + auto-migrate on startup | OK |
| Client Infrastructure | `AppPaths`, `AppSettings`/`SettingsStore`, `TokenStore` (DPAPI-on-Windows), `ApiClient` (auto-refresh + retry), `LocalizationService`, `TExtension` | OK |
| Client Data | `LocalDbContext` + `LocalProject` / `LocalTask` / `LocalDependency` | OK |
| Client ViewModels | `MainWindow`, `Shell`, `Login`, `Register`, `RoleSelection`, 5 placeholder VMs | OK |
| Client Views | `MainWindow`, `ShellView`, `LoginView`, `RegisterView`, `RoleSelectionView`, `MiniGamesHubView`, `TaskListView`, `GanttView`, `SettingsView` | OK |
| Resources | `Strings.resx` + `Strings.zh.resx` | OK |
| Deploy | `deploy/docker-compose.yml`, `src/HeyeTodo.Server/Dockerfile` | OK |
| Docs / tooling | `README.md`, `docs/ROADMAP.md`, `.editorconfig`, `Directory.Build.props`, `.config/dotnet-tools.json` | OK |

## Findings

### P0 — none

No blocking issues. M0 may be declared complete for the purpose of starting
M1.

### P1 — fix before or alongside M1

1. **F1 · Navigation icons rely on Segoe Fluent Icons PUA codepoints.**
   On macOS/Linux they render as tofu boxes. Needs a cross-platform icon
   strategy.
2. **F2 · Avalonia 12 has no native `PasswordBox`.** We bind `TextBox.Text`
   to a plain-string `Password` property. Functionally adequate but the
   plaintext lives in the VM until the view model is GC'd.
3. **F3 · CORS policy is fully open.** Current configuration:
   `AllowAnyHeader / AllowAnyMethod / SetIsOriginAllowed(_ => true) /
   AllowCredentials`. Unsafe outside MVP self-host scenarios.
4. **F4 · Dev JWT signing key is stored in plaintext.** `appsettings.json`
   carries `Jwt:SigningKey` as an obvious placeholder string. Needs to move
   to user-secrets for Development and environment variables for Production.
5. **F5 · `AppHost.Bootstrap` blocks the UI thread.** It does sync disk IO
   (settings load, `Database.EnsureCreated()`). On slow disks this stalls
   the first frame by 100–300ms with no visual feedback.
6. **F6 · `ShellViewModel` reaches into `AppHost.Services` directly.**
   Service locator usage makes unit testing painful. Needs an
   `INavigationService` abstraction.
7. **F7 · Server error messages are hard-coded English.**
   `AuthService` returns literal strings instead of resolving via
   `IStringLocalizer<SharedResource>`. Infrastructure is already in place,
   consumers are not wired.
8. **F8 · No OpenAPI browsing UI.** Swashbuckle was removed in M0 after a
   `TypeLoadException` against .NET 10; only the JSON endpoint remains.
   Developers must rely on curl / REST Client until this is addressed.

### P2 — defer, not urgent

9. Migrations path is `Infrastructure/Persistence/Migrations`. Fine for one
   DbContext; revisit if we ever introduce a second.
10. Client `LocalDbContext` uses `Database.EnsureCreated()` instead of a
    migration pipeline. Acceptable for MVP; revisit before M3 when schema
    churn accelerates.
11. `ClientId` file is stored as plaintext in app-data. Not sensitive, but
    could be unified with `TokenStore` encryption.
12. No unit test projects exist yet. Planned to land in M1.6.

## Verdict

M0 passes. All P1 items are scheduled against a dedicated fix pass
(`M0-P1-FIX-PLAN.md`) before starting the bulk of M1.
