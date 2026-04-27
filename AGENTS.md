# AGENTS.md

## Project overview

HeyeTodo is a cross-platform TODO and project-management application for indie game developers.

The solution is based on .NET 10 and contains three main projects:

- `src/HeyeTodo.Shared`: shared DTOs, enums, contracts, and sync metadata.
- `src/HeyeTodo.Server`: ASP.NET Core backend using EF Core, PostgreSQL, JWT authentication, and SignalR.
- `src/HeyeTodo.Client`: Avalonia desktop client using a local-first SQLite store and sync with the server.

## Repository map

- `HeyeTodo.sln`: solution entry point.
- `src/HeyeTodo.Shared`: code shared by client and server.
- `src/HeyeTodo.Server`: API, application services, domain logic, persistence, authentication, localization, sync, and planning server proxy code.
- `src/HeyeTodo.Client`: Avalonia views, view models, services, local persistence, sync coordinator, and client settings.
- `deploy/docker-compose.yml`: PostgreSQL and self-hosted server deployment assets.
- `docs/ROADMAP.md`: milestone plan.
- `start-test-env.bat`: Windows local environment startup script.
- `start-test-env.sh`: macOS local environment startup script.
- `stop-test-env.bat`: Windows local environment shutdown script.

## Development commands

Run these commands from the repository root unless a command says otherwise.

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

Apply database migrations manually when needed:

```bash
dotnet dotnet-ef database update --project src/HeyeTodo.Server/HeyeTodo.Server.csproj --startup-project src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

There are currently no dedicated test projects in the solution. Use `dotnet build HeyeTodo.sln` as the minimum validation step after code changes.

## Local environment conventions

- PostgreSQL is started through Docker Compose.
- The local host PostgreSQL port is currently `0427`, mapped to container port `5432`.
- Server HTTP development port is `5254`.
- The Docker Compose server container connects to PostgreSQL through Docker internal networking using `Host=postgres;Port=5432`; do not replace this with the host-mapped port.
- For Windows quick start, use `start-test-env.bat` and `stop-test-env.bat`.
- For macOS quick start, use `start-test-env.sh`.

## Coding guidelines for agents

- Prefer small, targeted changes that follow existing project structure and naming.
- Do not introduce new frameworks or packages unless the codebase already uses them or the change explicitly requires them.
- Keep shared contracts and DTOs in `HeyeTodo.Shared` when they are used by both client and server.
- Keep server-only infrastructure in `HeyeTodo.Server/Infrastructure` and application orchestration in `HeyeTodo.Server/Application`.
- Keep Avalonia UI concerns in the client project and avoid leaking UI-specific types into shared or server code.
- Preserve the local-first client architecture: local SQLite remains authoritative for offline work, and sync should flow through the existing sync coordinator/outbox/inbox pattern.
- Avoid logging secrets, JWT signing keys, refresh tokens, access tokens, or user-sensitive data.
- Do not commit changes unless explicitly requested.

## Validation expectations

After meaningful code changes, run:

```bash
dotnet build HeyeTodo.sln
```

When changing EF Core models or migrations, also validate migration behavior with the `dotnet dotnet-ef database update` command against a local PostgreSQL container.

When changing startup scripts or Docker configuration, check the related port mappings and ensure the host port and container-internal port are intentionally different where applicable.
