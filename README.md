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
dotnet user-secrets --project src/HeyeTodo.Server set "Jwt:SigningKey" "<your-random-32+-byte-key>"
cd deploy
docker compose up -d postgres
cd ../src/HeyeTodo.Server
dotnet run
```

PowerShell example for a development signing key:

```powershell
$key = [Convert]::ToBase64String((1..48 | ForEach-Object { [byte](Get-Random -Max 256) }))
dotnet user-secrets --project src/HeyeTodo.Server set "Jwt:SigningKey" $key
```

To apply database migrations manually:

```bash
dotnet dotnet-ef database update --project src/HeyeTodo.Server/HeyeTodo.Server.csproj --startup-project src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

Open `http://localhost:5254/scalar/v1` for an interactive API reference in Development.

### Run client

```bash
cd src/HeyeTodo.Client
dotnet run
```

## Packaging

M8 adds build scripts under `artifacts/scripts/`.

### Windows portable zip

```powershell
pwsh ./artifacts/scripts/publish-windows.ps1 -Version 0.1.0
```

Output:

- publish directory: `artifacts/releases/client-win-x64`
- zip package: `artifacts/releases/HeyeTodo-client-win-x64-<version>.zip`

This package is unsigned in MVP.

### Windows MSIX

```powershell
pwsh ./artifacts/scripts/publish-windows-msix.ps1 -Version 0.1.0
```

Requirements:

- Windows SDK `makeappx.exe` available in PATH

Output:

- unsigned MSIX package: `artifacts/releases/HeyeTodo-client-win-x64-<version>.msix`

Notes:

- The generated package is unsigned by design in MVP.
- Sign it separately before broad distribution if your environment requires trusted installation.

### macOS app bundle and dmg

Run this on macOS:

```bash
bash ./artifacts/scripts/publish-macos.sh Release osx-arm64 0.1.0
```

Output:

- app bundle: `artifacts/releases/HeyeTodo.app`
- dmg package: `artifacts/releases/HeyeTodo-osx-arm64-<version>.dmg`

If `hdiutil` is unavailable, the script still produces the `.app` bundle and skips dmg creation.

Because MVP packages are unsigned, a fresh macOS machine may quarantine the app after download. Clear the quarantine attribute before first launch:

```bash
xattr -d com.apple.quarantine /Applications/HeyeTodo.app
```

A placeholder signing integration point remains at `scripts/mac/sign.sh`.

## Milestones

See `docs/ROADMAP.md` (M0 through M8).

## Self-hosting

MVP is designed to support self-hosted deployment first.

### Minimum environment

- Docker Engine / Docker Desktop
- A host name or reverse-proxy entry for the API
- A strong JWT signing key with at least 32 bytes
- Persistent storage for PostgreSQL data

### Quick start

1. Edit `deploy/docker-compose.yml` and replace the default JWT signing key.
2. Set `Cors__AllowedOrigins__0` (and more entries if needed) to the exact desktop/web origin that will reach the server.
3. Run `docker compose up -d` inside `deploy/`.
4. Confirm the server is reachable on `http://localhost:8080` or through your reverse proxy.
5. Point the desktop client at that base URL in Settings.

### Optional manual migration

The server applies EF Core migrations on startup. If you still want to apply them manually:

```bash
dotnet dotnet-ef database update --project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj --startup-project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

### Recommended environment variables

The self-hosted server currently reads these from `deploy/docker-compose.yml`:

- `ASPNETCORE_ENVIRONMENT`
- `ASPNETCORE_URLS`
- `ConnectionStrings__Default`
- `Jwt__Issuer`
- `Jwt__Audience`
- `Jwt__SigningKey`
- `Jwt__AccessTokenMinutes`
- `Jwt__RefreshTokenDays`
- `Cors__AllowedOrigins__0`
- `Cors__AllowedOrigins__1`

### Reverse proxy notes

When placing the API behind Nginx, Caddy, Traefik, or another reverse proxy:

- forward HTTP traffic to the container on port `8080`
- preserve WebSocket upgrades for `/ws/sync`
- keep the external origin aligned with `Cors__AllowedOrigins__*`
- prefer HTTPS in front of the proxy for production use

### Backup recommendations

At minimum, back up:

- PostgreSQL data volume `heyetodo-postgres-dev-data`
- deployment configuration values, especially JWT and CORS settings

For safe restore operations, stop the stack before replacing database files or restoring the Docker volume snapshot.

## License

TBD
