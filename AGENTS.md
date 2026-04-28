# AGENTS.md

## Project overview

HeyeTodo is a cross-platform TODO and project-management app for indie game developers. It is a .NET 10 solution with three main projects:

- `src/HeyeTodo.Shared`: shared DTOs, enums, contracts, and sync metadata.
- `src/HeyeTodo.Server`: ASP.NET Core backend using EF Core, PostgreSQL, JWT authentication, and SignalR.
- `src/HeyeTodo.Client`: Avalonia desktop client with local-first SQLite persistence and server sync.

## Repository map

- `HeyeTodo.sln`: solution entry point.
- `src/HeyeTodo.Shared`: shared code for client and server.
- `src/HeyeTodo.Server`: API, application services, domain logic, persistence, auth, localization, sync, and planning server proxy code.
- `src/HeyeTodo.Client`: Avalonia views/view models, services, local persistence, sync coordinator, and client settings.
- `deploy/docker-compose.yml`: PostgreSQL and self-hosted deployment assets.
- `docs/ROADMAP.md`: milestone plan.
- `start-test-env.bat` / `start-test-env.sh`: local environment startup scripts.
- `stop-test-env.bat`: Windows local environment shutdown script.

## Development commands

Run from the repository root unless noted otherwise:

```bash
dotnet tool restore
dotnet restore HeyeTodo.sln
dotnet build HeyeTodo.sln
```

Run the server:

```bash
dotnet user-secrets --project src/HeyeTodo.Server set "Jwt:SigningKey" "<random-32+-byte-key>"
docker compose -f deploy/docker-compose.yml up -d postgres
dotnet run --project src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

Run the client:

```bash
dotnet run --project src/HeyeTodo.Client/HeyeTodo.Client.csproj
```

Apply EF Core migrations when needed:

```bash
dotnet dotnet-ef database update --project src/HeyeTodo.Server/HeyeTodo.Server.csproj --startup-project src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

There are no dedicated test projects yet. Use `dotnet build HeyeTodo.sln` as the minimum validation step after code changes.

## Local environment conventions

- PostgreSQL starts through Docker Compose.
- Host PostgreSQL port is `0427`, mapped to container port `5432`.
- Server HTTP development port is `5254`.
- Docker Compose server containers must use Docker internal PostgreSQL networking: `Host=postgres;Port=5432`. Do not replace it with the host-mapped port.
- Use `start-test-env.bat` / `stop-test-env.bat` on Windows and `start-test-env.sh` on macOS.

## Coding guidelines for agents

- Make small, targeted changes that follow existing project structure, naming, and style.
- Do not add frameworks or packages unless already used by the codebase or explicitly required.
- Put shared contracts and DTOs in `HeyeTodo.Shared` when used by both client and server.
- Keep server-only infrastructure in `HeyeTodo.Server/Infrastructure` and application orchestration in `HeyeTodo.Server/Application`.
- Keep Avalonia UI concerns in the client project; do not leak UI-specific types into shared or server code.
- Preserve the local-first client architecture: local SQLite remains authoritative for offline work, and sync flows through the existing sync coordinator/outbox/inbox pattern.
- Add clear code comments for non-obvious logic, important business rules, sync conflict behavior, security-sensitive flows, and cross-project contracts. Prefer concise comments that explain why the code exists or why an approach is used; avoid comments that merely repeat what the code says.
- Never log secrets, JWT signing keys, refresh tokens, access tokens, or user-sensitive data.
- Do not commit changes unless explicitly requested.

## Validation expectations

After meaningful code changes, run:

```bash
dotnet build HeyeTodo.sln
```

When changing EF Core models or migrations, also validate migration behavior with `dotnet dotnet-ef database update` against a local PostgreSQL container.

When changing startup scripts or Docker configuration, verify related port mappings and keep the host port and container-internal port intentionally different where applicable.
