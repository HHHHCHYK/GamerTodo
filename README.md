# HeyeTodo

A TODO and project-management tool tailor-made for **indie game developers**. Cross-platform, local-first, and built around how game teams actually work.

> Also available in: [中文](README.zh.md)

| | |
|---|---|
| Client | Avalonia 12 (C#) — Windows & macOS |
| Server | ASP.NET Core 10 + EF Core + PostgreSQL |
| Realtime | SignalR (WebSocket) |
| Local Store | SQLite (local-first) |
| Auth | JWT (access + refresh) |

## Features

- **Multi-user with real sync** — register once, log in on any device, and your tasks follow you. SignalR keeps every client up-to-date in real time.
- **Local-first, always available** — all your data lives in a local SQLite store. You can work offline without interruption, and changes sync automatically when you reconnect.
- **List view** — filter, sort, search, and manage tasks the classic way. Create projects, add tasks with priorities and dates, and advance them through a full status pipeline (Backlog → Todo → In Progress → Blocked → Review → Done / Cancelled).
- **Role panels** — pick your role (Producer, Designer, Artist, Programmer, Sound Designer) and the UI adapts. Each role brings its own fields, actions, and dashboard widgets that make sense for that discipline.
- **Smart planning** — a rule-based engine reorders tasks by priority and dependency chains. Optionally hook in an LLM (server-proxy or your own API key) for AI-assisted planning suggestions.
- **Mini-games** — a reserved space for small relaxation games (coming in a future release).
- **Bilingual UI** — automatically follows your system language, with manual override available in settings.

## Download & Install

Grab the latest release from [GitHub Releases](https://github.com/your-org/HeyeTodo/releases).

| Platform | Package | Notes |
|---|---|---|
| Windows | `HeyeTodo-client-win-x64-<version>.zip` | Portable — unzip and run |
| Windows | `HeyeTodo-client-win-x64-<version>.msix` | MSIX package |
| macOS | `HeyeTodo-osx-arm64-<version>.dmg` | Drag to Applications |

On macOS, after downloading you may need to clear the quarantine flag:

```bash
xattr -d com.apple.quarantine /Applications/HeyeTodo.app
```

> HeyeTodo works best with a server. You can use a shared team server set up by your team, or [self-host your own](#self-hosting). If you are just trying it out, the client can also point at `http://localhost:5254` while running the server locally.

## Using HeyeTodo

### First launch

When you open HeyeTodo for the first time, you will land on the login screen. You have two options:

- **Register** — pick an email and password, set a display name, and you are in.
- **Log in** — if you already have an account, just sign in.

After logging in for the first time, HeyeTodo asks which roles you carry on your project:

- Producer
- Designer
- Artist
- Programmer
- Sound Designer

You can pick none, one, or several — and change your mind later in Settings. Your choice shapes which panels and fields appear in the app.

### The sidebar

Once inside, the left sidebar gives you quick access to every area:

| Section | What it does |
|---|---|
| **Tasks** | List view for creating and managing tasks |
| **Role panels** | Role-specific dashboards, fields, and actions |
| **Planning** | Auto-ordering and AI-assisted planning |
| **Mini-games** | Relaxation corner (placeholder for now) |
| **Settings** | Language, server URL, roles, and planning mode |

### Managing tasks

The **Tasks** view is your daily command center.

- **Create a project** first — every task lives inside a project. Click "New project", give it a name and optional description, and save.
- **Add tasks** — select a project from the dropdown, then click "New task". Fill in the title, an optional description, start/end dates, estimated hours, priority, and status.
- **Work through statuses** — click "Next status" on any task to advance it through the pipeline: Backlog → Todo → In Progress → Blocked → Review → Done (or Cancelled).
- **Filter and sort** — narrow down by status or priority. Sort by recently updated, priority, title, or status.
- **Search** — use the search box to find tasks by keyword.
- **Include completed** — toggle visibility of done and cancelled tasks.

> Tasks save locally first, then sync to the server. If you are offline, you will see a "Saved locally" notice — everything will sync once you reconnect.

### Role panels

The **Role panels** view adapts to the roles you selected. If you have not picked any roles yet, head to Settings first.

For each active role, you get three tabs:

- **Dashboard** — an at-a-glance summary of what matters most for your role.
- **Role actions** — one-click actions that move tasks through your workflow (for example, a Programmer can "Start implementation" or "Request code review").
- **Role fields** — discipline-specific metadata you can attach to tasks.

| Role | Example fields |
|---|---|
| Producer | Milestone, Risk, Owner note |
| Designer | Feature area, Spec link, Acceptance notes |
| Artist | Asset type, Reference, Delivery path |
| Programmer | Code area, Branch or PR, Test plan |
| Sound Designer | Cue name, Mood, Mix notes |

Select a project and a task in the top bar to start editing role-specific fields and running role actions.

### Planning

The **Planning** view helps you sort out what to work on next.

By default, HeyeTodo uses a **rule-based engine** that orders tasks using topological sort (respecting dependencies) combined with priority weighting. This works offline and requires no configuration.

You can also enable AI-assisted planning in Settings:

- **Server-managed AI** — the server handles the LLM calls. No API key needed on your end.
- **Client API key** — bring your own OpenAI-compatible API key. You control the endpoint and model.

In either mode, you can type optional instructions (e.g. "focus on combat system tasks first") before generating a plan. The app then shows suggestions and any issues it detected.

### Settings

Reach Settings from the bottom of the sidebar.

- **Language** — follow your system setting, or force English / Chinese.
- **Server base URL** — point the client at your team's server. Defaults to `http://localhost:5254`.
- **Roles** — add or remove roles at any time. The UI adapts immediately.
- **Planning mode** — choose between rules-only, server AI, or client API key.
- **Logout** — available at the bottom of the settings page.

## Architecture highlights

- **Local-first** — SQLite is the authoritative store. You always have your data, online or offline.
- **Conflict-aware sync** — uses an outbox/inbox model with LWW (last-write-wins) conflict resolution.
- **Role-driven UI** — the interface surfaces relevant fields and actions based on your chosen roles.
- **Bilingual by design** — all strings live in `.resx` files with English and Chinese variants.

## Repository layout

```
HeyeTodo/
├── src/
│   ├── HeyeTodo.Shared/    Shared DTOs, enums, contracts
│   ├── HeyeTodo.Server/    ASP.NET Core backend
│   └── HeyeTodo.Client/    Avalonia desktop client
├── deploy/                 Docker Compose & deployment assets
├── artifacts/scripts/      Build & packaging scripts
└── HeyeTodo.sln
```

## Development

See [AGENTS.md](.agents/README.md) for detailed development setup instructions.

## Self-hosting

HeyeTodo is designed for self-hosted deployment.

### Quick start

1. Edit `deploy/docker-compose.yml` and replace the default JWT signing key with a random 32+ byte key.
2. Set `Cors__AllowedOrigins__0` to the origin your desktop client will use.
3. Run `docker compose up -d` from the `deploy/` directory.
4. The server will be available at `http://localhost:8080`.
5. In the desktop client, open **Settings** and set the server base URL to your server address.

The server applies EF Core migrations automatically on startup.

### Environment variables

| Variable | Description |
|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` or `Development` |
| `ASPNETCORE_URLS` | Listening URLs |
| `ConnectionStrings__Default` | PostgreSQL connection string |
| `Jwt__Issuer` | JWT issuer |
| `Jwt__Audience` | JWT audience |
| `Jwt__SigningKey` | HMAC signing key (≥ 32 bytes) |
| `Jwt__AccessTokenMinutes` | Access token lifetime (minutes) |
| `Jwt__RefreshTokenDays` | Refresh token lifetime (days) |
| `Cors__AllowedOrigins__0` | First allowed CORS origin |
| `Cors__AllowedOrigins__1` | Second allowed CORS origin (optional) |

### Reverse proxy

When placing the API behind Nginx, Caddy, Traefik, or similar:

- Forward HTTP traffic to the container on port `8080`
- Preserve WebSocket upgrades for the `/ws/sync` path
- Ensure the external origin matches `Cors__AllowedOrigins__*`
- Terminate TLS at the proxy in production

### Backup

Back up at minimum:

- PostgreSQL data volume `heyetodo-postgres-dev-data`
- Deployment configuration values (JWT signing key, CORS settings)

Stop the stack before replacing database files or restoring a Docker volume snapshot.

## Contributing

Issues and pull requests are welcome. Please open an issue first to discuss significant changes.

## License

TBD
