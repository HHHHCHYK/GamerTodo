# M0 P1 Fix Plan

This plan addresses the eight P1 items identified in `M0-REVIEW.md` without
starting any M1 work. All P0 items are already clean; all P2 items are
explicitly deferred.

## Goals

Bring the codebase to a "ready for M1 without debt" state:

- Cross-platform visuals (F1)
- Minimal password hygiene (F2)
- Hardened CORS (F3)
- Secure dev secrets (F4)
- Non-blocking startup (F5)
- Testable navigation (F6)
- i18n-aware server errors (F7)
- Developer-friendly API browsing (F8)

---

## F1 · Cross-platform navigation icons

**Problem.** `ShellView.axaml` uses Segoe Fluent Icons PUA codepoints
(`\uE8FD / \uE9D9 / \uE7FC / \uE713`). macOS and Linux render tofu blocks.

**Chosen approach.** `Projektanker.Icons.Avalonia` + FontAwesome provider.

Alternatives rejected:
- `FluentIcons.Avalonia` — heavier package, similar capability
- Inline SVG per icon — higher maintenance for little gain

**Steps.**

1. Add to `HeyeTodo.Client.csproj`:
   - `Projektanker.Icons.Avalonia`
   - `Projektanker.Icons.Avalonia.FontAwesome`
2. Register the provider in `Program.cs` via
   `.WithIcons(b => b.Register<FontAwesomeIconProvider>())`.
3. Rename `NavItem.Glyph` → `NavItem.IconKey`; values become FontAwesome
   names such as `fa-solid fa-list-check`, `fa-solid fa-chart-gantt`,
   `fa-solid fa-gamepad`, `fa-solid fa-gear`.
4. Swap the `TextBlock` + `FontFamily` line in `ShellView.axaml` for
   `<i:Icon Value="{Binding IconKey}" />` (importing the
   `Projektanker.Icons.Avalonia` namespace).
5. Icons should inherit the enclosing `Foreground`.

**Verification.** Run on both Windows and macOS; each nav entry shows a
proper icon. No PUA strings remain in source.

**Rollback.** If the package is not Avalonia 12 compatible, switch to
`FluentIcons.Avalonia`; if that also fails, inline four SVGs.

---

## F2 · Password handling minimum bar

**Problem.** Avalonia 12 ships no `PasswordBox`; we use
`TextBox.PasswordChar`. `LoginViewModel.Password` is a plain `string` that
lives for the VM lifetime.

**Chosen approach.** Keep `TextBox + PasswordChar="*"`; tighten the VM.

Alternatives rejected:
- FluentAvalonia PasswordBox — adds a dependency for little incremental win
- SecureString hand-rolled control — scope creep

**Steps.**

1. In `LoginViewModel.LoginAsync`: wrap the call in `try/finally`, clear
   `Password = string.Empty` inside `finally` on both success and failure.
2. Same in `RegisterViewModel.RegisterAsync`.
3. In `ApiClient.LoginAsync` / `RegisterAsync`, avoid keeping the caller's
   password string in any field; pass directly into the request DTO and let
   GC reclaim it.
4. Leave `PasswordChar="*"` in XAML. Revisit when Avalonia or FluentAvalonia
   ship a canonical `PasswordBox`.

**Verification.** After login attempts, the bound `Password` observable is
empty. Covered by a VM-level unit test when the test project lands.

---

## F3 · CORS policy tightened

**Problem.** `Program.cs` currently uses
`SetIsOriginAllowed(_ => true).AllowCredentials()`.

**Steps.**

1. Introduce `Cors:AllowedOrigins` as `string[]` in `appsettings.json`.
2. Default values for Development:
   `["http://localhost:5254", "http://localhost:7040"]`.
3. In `Program.cs`:

   ```csharp
   var allowed = builder.Configuration
       .GetSection("Cors:AllowedOrigins")
       .Get<string[]>() ?? Array.Empty<string>();

   builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
       .WithOrigins(allowed)
       .AllowAnyHeader()
       .AllowAnyMethod()
       .AllowCredentials()));
   ```

4. Log `ILogger.LogWarning` at startup if `allowed` is empty so operators
   see the misconfiguration.
5. `deploy/docker-compose.yml` — add a commented example:
   `Cors__AllowedOrigins__0=https://heyetodo.example.com`.
6. README — document configuration under a "Self-hosting" subsection.

**Verification.** Manual: a request from an unknown origin is rejected; the
official client from `http://localhost:5254` still works. SignalR handshake
continues to succeed because the client's origin is whitelisted.

---

## F4 · Developer secrets via user-secrets

**Problem.** `Jwt:SigningKey` sits in plaintext in `appsettings.json`.

**Steps.**

1. Confirm existing `<UserSecretsId>heyetodo-server-dev-secrets</UserSecretsId>`
   in the server csproj.
2. In `Program.cs`, explicitly:
   `builder.Configuration.AddUserSecrets<Program>(optional: true);` so the
   intent is obvious and works under any host configuration.
3. In `appsettings.json`, change `Jwt:SigningKey` to an obvious placeholder
   such as `REPLACE-ME-VIA-USER-SECRETS`.
4. In `appsettings.Development.json`, remove any `Jwt` section.
5. Startup validation: if the resolved signing key starts with `REPLACE-ME`
   or is shorter than 32 bytes, log a warning in Development and throw in
   Production.
6. README — add a one-liner:

   ```
   dotnet user-secrets --project src/HeyeTodo.Server \
     set "Jwt:SigningKey" "$(openssl rand -base64 48)"
   ```
7. Update `deploy/docker-compose.yml` comment to emphasize that
   `Jwt__SigningKey` MUST be replaced before deploying.

**Verification.** Clone, run without setting any secret → startup logs a
warning and (in Production) refuses to start. After running the
`user-secrets set` command, Development server starts clean.

---

## F5 · Non-blocking startup

**Problem.** `AppHost.Bootstrap()` performs synchronous disk IO
(`SettingsStore.Load`, `Database.EnsureCreated()`) on the UI thread during
`OnFrameworkInitializationCompleted`.

**Steps.**

1. Split into two phases:
   - `BootstrapCore()` — synchronous; constructs DI container, applies
     culture. Must stay under ~10ms.
   - `WarmupAsync()` — async; loads settings / tokens, runs
     `EnsureCreated`, returns `Task`.
2. Introduce a minimal `SplashWindow` (just `App.Title`, a spinner, a
   status line) shown immediately.
3. In `App.OnFrameworkInitializationCompleted`:
   1. Call `AppHost.BootstrapCore()` synchronously.
   2. Show `SplashWindow` as `desktop.MainWindow`.
   3. `await AppHost.WarmupAsync()`; on success, create `MainWindow` with
      its VM, set as `desktop.MainWindow`, close the splash.
   4. On exception, show "Retry / Quit" buttons on the splash with the
      exception message (localized when possible).
4. Keep disk IO on `Task.Run` wrappers; no need for true async file APIs.
5. Ensure DI container registration still happens inside `BootstrapCore` so
   VMs keep their constructor-injection contract.

**Verification.** On a slow disk, the user sees the splash immediately and
the main window appears once warmup finishes. No UI freeze during startup.

**Rollback.** If swapping `MainWindow` mid-lifetime proves fragile on
Avalonia 12, keep a single window instance whose content starts as a splash
view and swaps to `ShellView` after warmup.

---

## F6 · Introduce `INavigationService`

**Problem.** `ShellViewModel`, `LoginViewModel`, `RegisterViewModel`,
`RoleSelectionViewModel` all poke `AppHost.Services` or the `MainSwitcher`
helper. This makes them untestable without the real DI container.

**Steps.**

1. Define the interface in `Infrastructure/Navigation/INavigationService.cs`:

   ```csharp
   public interface INavigationService
   {
       void NavigateTo<TVm>() where TVm : ViewModelBase;
       void Navigate(ViewModelBase vm);
       event Action<ViewModelBase>? Navigated;
   }
   ```

2. Implement `NavigationService`:
   - Constructor-injected `IServiceProvider`
   - `NavigateTo<TVm>` resolves via DI then raises `Navigated`
   - `Navigate(vm)` accepts an already-constructed VM (useful for already
     configured VMs like a pre-populated settings page)
3. Register `sc.AddSingleton<INavigationService, NavigationService>()` in
   `AppHost`.
4. `MainWindowViewModel` now:
   - Takes `INavigationService` in its constructor
   - Subscribes: `nav.Navigated += vm => Current = vm;`
   - Picks the initial view in its constructor: login vs shell, based on
     `ApiClient.IsAuthenticated`.
5. Rewrite `ShellViewModel`, `LoginViewModel`, `RegisterViewModel`,
   `RoleSelectionViewModel` to take `INavigationService` by constructor;
   replace every `AppHost.Services.GetRequiredService<...>()` and every
   `MainSwitcher.Switch(...)` with `nav.NavigateTo<...>()`.
6. Delete `MainSwitcher`.
7. Verify via grep: no ViewModel references `AppHost.Services`.

**Verification.** Manual run-through of the flows: app start → login →
register → role selection → shell → logout → login. Behaviour is
unchanged. Grep confirms the service-locator usage is gone from VMs.

---

## F7 · Server error localization

**Problem.** `AuthService` returns plain English literals.

**Steps.**

1. Inject `IStringLocalizer<SharedResource>` into `AuthService`.
2. Replace literals:
   - `"Invalid credentials."` → `_loc["Auth_InvalidCredentials"]`
   - `"Email already registered."` → `_loc["Auth_EmailAlreadyRegistered"]`
   - `"Email and password are required."` →
     `_loc["Auth_EmailAndPasswordRequired"]`
   - `"Refresh token invalid or expired."` → `_loc["Auth_RefreshInvalid"]`
3. `ServiceResult<T>.Fail` signature unchanged; consumers receive already
   localized strings.
4. Confirm `app.UseHeyeLocalization()` is in the pipeline before controller
   mapping (already true in M0).
5. Client work (sending `Accept-Language`) is deferred to M1.2 but not
   blocked by this fix.
6. Extend `src/HeyeTodo.Server/HeyeTodo.Server.http` with a pair of
   deliberately-failing login requests that set `Accept-Language: zh` and
   `Accept-Language: en` respectively, so the localization flow is
   demonstrable.

**Verification.** `curl -H "Accept-Language: zh" ...` yields the Chinese
message; `Accept-Language: en` yields the English one.

---

## F8 · OpenAPI UI

**Problem.** `MapOpenApi` serves JSON only. No in-browser exploration for
developers.

**Chosen approach.** Add `Scalar.AspNetCore` for an interactive reference.

Alternatives:
- Retry Swashbuckle — known incompatibility with .NET 10 at the version we
  tried; leave as future work if Scalar proves insufficient.
- Do nothing — acceptable but friction-heavy.

**Steps.**

1. Add package reference `Scalar.AspNetCore` to the server project.
2. In `Program.cs` Development branch:

   ```csharp
   if (app.Environment.IsDevelopment())
   {
       app.MapOpenApi();
       app.MapScalarApiReference();
   }
   ```

3. README — "visit `http://localhost:5254/scalar/v1` for an interactive
   API reference during development".
4. Production branch unchanged — UI only surfaces in Development.

**Verification.** Running the server locally and opening `/scalar/v1`
renders the UI with both controllers enumerated.

---

## Execution order & time estimate

| Order | Item | Est. |
|---|---|---|
| 1 | F4 · user-secrets cleanup | 20 min |
| 2 | F3 · CORS hardening | 20 min |
| 3 | F7 · AuthService i18n wiring | 40 min |
| 4 | F6 · `INavigationService` refactor | 1h30 |
| 5 | F5 · splash + async warmup | 1h |
| 6 | F1 · icon library integration | 45 min |
| 7 | F2 · password hygiene tweaks | 20 min |
| 8 | F8 · Scalar integration | 15 min |
| 9 | full `dotnet build` + manual smoke | 30 min |

Total ≈ 5 hours. Fits comfortably inside one work day.

## Cross-cutting verification checklist

- [ ] `dotnet build HeyeTodo.sln` → 0 errors, 0 warnings
- [ ] Server starts against a running Postgres, `/scalar/v1` works
- [ ] Rejects CORS from unknown origin; allows the configured origin
- [ ] `curl` login with bad password returns localized message per
      `Accept-Language`
- [ ] Client boots on Windows and (if available) macOS; icons render
- [ ] No VM touches `AppHost.Services` any more (grep clean)
- [ ] Password field is empty after login attempt
- [ ] Cold start shows splash; warm start is nearly instant

## Out of scope for this fix pass

- Tests project creation (planned for M1.6)
- Client-side `Accept-Language` header emission (M1.2)
- Any M1 functional features
