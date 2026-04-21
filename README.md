# HeyeTodo

A cross-platform TODO and project-management application tailored for **indie game developers**.

| | |
|---|---|
| Client | Avalonia 12 (C#) — Windows & macOS |
| Server | ASP.NET Core 10 + EF Core + PostgreSQL |
| Realtime | SignalR (WebSocket) |
| Local Store | SQLite (local-first) |
| Auth | JWT (access + refresh) |

## Features (MVP)

- Multi-user registration & login, multi-device sync
- Local-first architecture: offline-complete, conflict-aware sync
- Task CRUD with **List** and **Gantt** views (both back the same data)
- User selectable roles (Producer / Designer / Artist / Programmer / Sound Designer).
  A user may pick zero, one, or multiple roles; UI surfaces adapt accordingly.
- Automatic reordering by priority + dependency
  - Rule-based engine (topological sort + priority weights)
  - Optional LLM assist (server-proxy mode and client-key mode)
- Mini-games section (placeholder in MVP, reserved entry)
- Bilingual UI (Chinese / English), system-language auto-detect

## Repository layout

```
HeyeTodo/
├── src/
│   ├── HeyeTodo.Shared/    Shared DTOs, enums, contracts
│   ├── HeyeTodo.Server/    ASP.NET Core backend (Api / Application / Domain / Infrastructure layered by namespace)
│   └── HeyeTodo.Client/    Avalonia desktop client
├── deploy/                 Docker / deployment assets
├── scripts/                Helper scripts (mac signing placeholder, etc.)
└── HeyeTodo.sln
```

## Getting started (dev)

### Prerequisites

- .NET SDK 10.x
- Docker Desktop (for Postgres via `deploy/docker-compose.yml`)
- `dotnet-ef` local tool (`dotnet tool restore`)

### Run server

```bash
dotnet tool restore
cd deploy
docker compose up -d postgres
cd ../src/HeyeTodo.Server
dotnet run
```

To apply database migrations manually:

```bash
dotnet dotnet-ef database update --project src/HeyeTodo.Server/HeyeTodo.Server.csproj --startup-project src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

### Run client

```bash
cd src/HeyeTodo.Client
dotnet run
```

## Milestones

See `docs/ROADMAP.md` (M0 through M8).

## Self-hosting

MVP is designed to support self-hosted deployment first.

1. Edit `deploy/docker-compose.yml` and replace the default JWT signing key.
2. Run `docker compose up -d` inside `deploy/`.
3. Apply migrations:

```bash
dotnet dotnet-ef database update --project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj --startup-project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

4. Point the desktop client at `http://localhost:8080` (or your reverse-proxied domain).

## License

TBD
