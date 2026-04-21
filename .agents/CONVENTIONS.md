# Conventions

## Languages

- All code comments, commit messages, identifiers: English
- User-facing strings: resx resources only (never hard-coded)
- Chat / docs with the product owner: Chinese by default

## Code style

- `.editorconfig` is authoritative
- File-scoped namespaces
- `Nullable` enabled solution-wide (via `Directory.Build.props`)
- `ImplicitUsings` enabled
- Async methods suffixed `Async`
- Records for immutable DTOs, classes for entities

## Project layout (server)

Layered inside a single `HeyeTodo.Server` project for MVP speed:

```
HeyeTodo.Server/
├── Api/            Controllers + SignalR hubs
├── Application/    Application services (orchestration, DTO mapping)
├── Domain/         Entities
├── Infrastructure/ Persistence, auth plumbing, localization
├── Resources/      *.resx bundles
└── Program.cs      Composition root
```

Splitting into independent projects is a future refactor, not a blocker.

## Project layout (client)

```
HeyeTodo.Client/
├── Assets/
├── Data/                     Local EF Core entities + DbContext
├── Infrastructure/
│   ├── Auth/                 TokenStore
│   ├── Localization/         LocalizationService + TExtension
│   ├── Networking/           ApiClient
│   ├── AppPaths.cs
│   └── AppSettings.cs
├── Resources/                Strings.*.resx
├── ViewModels/
├── Views/                    XAML + code-behind
├── AppHost.cs                DI root
├── App.axaml[.cs]            Avalonia application
├── Program.cs                Entry point
└── ViewLocator.cs
```

## Naming

- Views: `<Feature>View.axaml` / `.axaml.cs`
- ViewModels: `<Feature>ViewModel.cs`, inherit `ViewModelBase`
- Entities (server domain): singular, e.g. `Project`, `TodoTask`
- Client local mirrors: `Local<Name>`, e.g. `LocalTask`
- Resource keys
  - Client: `Area.Key` (dot-separated). Example `Auth.Login`, `Nav.Tasks`
  - Server: `Area_Key` (snake). Example `Auth_InvalidCredentials`

## Async + threading

- Server: everything async all the way down
- Client: UI thread only touches ObservableProperty setters; IO goes through
  `Task.Run` wrappers or native async

## Dependency injection

- Server: built-in `IServiceCollection`
- Client: `Microsoft.Extensions.DependencyInjection`
- ViewModels: constructor injection
- **Avoid** `AppHost.Services.GetRequiredService<T>()` inside ViewModels once
  the navigation service lands (F6 in M0 P1 fixes). The root `App` code may
  still use it.

## Commits

- Subject line in English, imperative, ≤72 chars
- Each milestone fix / feature commits independently; do not mix unrelated
  changes

## Testing

- xUnit + `WebApplicationFactory` for server
- xUnit for client domain logic; Avalonia UI tests optional
- Test projects live under `tests/` (to be created in M1.6)

## Security

- Passwords hashed with PBKDF2-SHA256, 100k iterations (subject to uplift)
- Tokens encrypted at rest on Windows via DPAPI, plaintext with 0600 perms on
  Unix (Keychain / Secret Service backends future work)
- Dev-time secrets live in user-secrets, never in git-tracked appsettings
  (see M0 P1 fix F4)
