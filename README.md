# HeyeTodo

A cross-platform TODO and project-management application tailored for **indie game developers**.

> Also available in: [中文](README.zh.md)

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
- Task CRUD with **List** and **Gantt** views (both backed by the same data model)
- User-selectable roles: Producer / Designer / Artist / Programmer / Sound Designer
  - A user may pick zero, one, or multiple roles; UI surfaces adapt accordingly
- Automatic task reordering by priority and dependency
  - Rule-based engine (topological sort + priority weights)
  - Optional LLM assist (server-proxy mode and client-key mode)
- Mini-games section (placeholder in MVP, reserved entry)
- Bilingual UI (Chinese / English) with system-language auto-detect

## Repository layout

```
HeyeTodo/
├── src/
│   ├── HeyeTodo.Shared/    Shared DTOs, enums, contracts
│   ├── HeyeTodo.Server/    ASP.NET Core backend (Api / Application / Domain / Infrastructure)
│   └── HeyeTodo.Client/    Avalonia desktop client
├── deploy/                 Docker / deployment assets
├── scripts/                Helper scripts (mac signing placeholder, etc.)
└── HeyeTodo.sln
```

## Getting started (dev)

### Prerequisites

- .NET SDK 10.x
- Docker Desktop (for PostgreSQL via `deploy/docker-compose.yml`)
- `dotnet-ef` local tool — restored via `dotnet tool restore`

### Run the server

```bash
dotnet tool restore
dotnet user-secrets --project src/HeyeTodo.Server set "Jwt:SigningKey" "<your-random-32+-byte-key>"
cd deploy
docker compose up -d postgres
cd ../src/HeyeTodo.Server
dotnet run
```

Generate a random signing key in PowerShell:

```powershell
$key = [Convert]::ToBase64String((1..48 | ForEach-Object { [byte](Get-Random -Max 256) }))
dotnet user-secrets --project src/HeyeTodo.Server set "Jwt:SigningKey" $key
```

Apply database migrations manually (if needed):

```bash
dotnet dotnet-ef database update \
  --project src/HeyeTodo.Server/HeyeTodo.Server.csproj \
  --startup-project src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

The interactive API reference (Scalar) is available at `http://localhost:5254/scalar/v1` when running in Development mode.

### Run the client

```bash
cd src/HeyeTodo.Client
dotnet run
```

## Packaging

Build scripts live under `artifacts/scripts/` (added in milestone M8).

### Windows — portable zip

```powershell
pwsh ./artifacts/scripts/publish-windows.ps1 -Version 0.1.0
```

Output:

| Path | Description |
|------|-------------|
| `artifacts/releases/client-win-x64` | Self-contained publish directory |
| `artifacts/releases/HeyeTodo-client-win-x64-<version>.zip` | Portable zip package |

> The package is unsigned in MVP.

### Windows — MSIX

```powershell
pwsh ./artifacts/scripts/publish-windows-msix.ps1 -Version 0.1.0
```

Requirements: Windows SDK `makeappx.exe` must be available in `PATH`.

Output: `artifacts/releases/HeyeTodo-client-win-x64-<version>.msix` (unsigned)

> Sign the package separately before broad distribution if your environment requires trusted installation.

### macOS — app bundle and dmg

Run on macOS:

```bash
bash ./artifacts/scripts/publish-macos.sh Release osx-arm64 0.1.0
```

Output:

| Path | Description |
|------|-------------|
| `artifacts/releases/HeyeTodo.app` | App bundle |
| `artifacts/releases/HeyeTodo-osx-arm64-<version>.dmg` | Disk image |

If `hdiutil` is unavailable, the script produces the `.app` bundle and skips dmg creation.

Because MVP packages are unsigned, macOS may quarantine the app after download. Clear the quarantine attribute before first launch:

```bash
xattr -d com.apple.quarantine /Applications/HeyeTodo.app
```

A placeholder signing integration point is at `scripts/mac/sign.sh`.

## Milestones

See [`docs/ROADMAP.md`](docs/ROADMAP.md) for the full milestone plan (M0 through M8).

## Self-hosting

HeyeTodo is designed for self-hosted deployment first.

### Minimum requirements

- Docker Engine or Docker Desktop
- A hostname or reverse-proxy entry pointing to the API container
- A JWT signing key of at least 32 random bytes
- Persistent storage volume for PostgreSQL data

### Quick start

1. Edit `deploy/docker-compose.yml` and replace the placeholder JWT signing key.
2. Set `Cors__AllowedOrigins__0` (add more entries as needed) to the exact origin the desktop client will use.
3. Run `docker compose up -d` from inside `deploy/`.
4. Verify the server is reachable at `http://localhost:8080` or through your reverse proxy.
5. In the desktop client, open **Settings** and point the base URL at your server.

### Manual database migration

The server applies EF Core migrations automatically on startup. To run them manually:

```bash
dotnet dotnet-ef database update \
  --project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj \
  --startup-project ../src/HeyeTodo.Server/HeyeTodo.Server.csproj
```

### Environment variables

| Variable | Description |
|----------|-------------|
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Development` |
| `ASPNETCORE_URLS` | Listening URL(s) for the server |
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__Issuer` | JWT issuer claim |
| `Jwt__Audience` | JWT audience claim |
| `Jwt__SigningKey` | HMAC signing key (≥ 32 bytes, keep secret) |
| `Jwt__AccessTokenMinutes` | Access token lifetime in minutes |
| `Jwt__RefreshTokenDays` | Refresh token lifetime in days |
| `Cors__AllowedOrigins__0` | First allowed CORS origin |
| `Cors__AllowedOrigins__1` | Second allowed CORS origin (optional) |

### Reverse proxy notes

When placing the API behind Nginx, Caddy, Traefik, or another reverse proxy:

- Forward HTTP traffic to the container on port `8080`
- Preserve WebSocket upgrades for the `/ws/sync` path
- Keep the external origin aligned with `Cors__AllowedOrigins__*`
- Terminate TLS at the proxy for production deployments

### Backup recommendations

Back up at minimum:

- PostgreSQL data volume `heyetodo-postgres-dev-data`
- Deployment configuration values, especially the JWT signing key and CORS settings

Stop the stack before replacing database files or restoring a Docker volume snapshot to avoid data corruption.

## Contributing

Issues and pull requests are welcome. Please open an issue first to discuss significant changes.

## License

TBD
