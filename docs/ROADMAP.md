# Roadmap

## M0 Foundation

- Solution-wide conventions (`.editorconfig`, `Directory.Build.props`, `.gitignore`)
- Shared contracts and enums (`RoleType`, task enums, sync DTOs)
- Server skeleton
  - ASP.NET Core API
  - EF Core + PostgreSQL
  - JWT auth skeleton
  - SignalR `SyncHub`
  - bilingual resource scaffolding
- Client skeleton
  - Avalonia shell and navigation
  - login / register / role-selection placeholders
  - runtime localization (`en` / `zh`)
  - local SQLite database
  - token store + API client skeleton
- Deployment assets
  - Docker Compose for self-hosted server + Postgres
  - Server Dockerfile
  - local `dotnet-ef` tool manifest

## M1 Auth + User Profile

- complete login / register flow end-to-end against server
- current-user endpoint integration on client
- skippable role selection persisted to server
- settings page
  - language switch
  - server base URL
  - role editing
  - planning mode selection

## M2 Tasks + List View

- project CRUD
- task CRUD
- local-first task repository over SQLite
- task list view with filters / sort / basic status transitions

## M3 Sync Engine

- outbox / inbox model
- push / pull APIs
- LWW conflict resolution + tombstones
- SignalR project subscription and live invalidation

## M4 Timeline View (deferred)

- timeline scheduling view removed from the current product scope
- keep task start / end dates in the task model for list editing and planning
- revisit a timeline experience only after the core task, sync, role, and planning flows are stable

## M5 Role Panels

- user role subset support (0..N roles)
- role-specific field schemas
- role-aware default columns / actions / dashboards

## M6 Planning

- rule-based planner (topological sort + priority weighting)
- server-proxy LLM driver
- client-key LLM driver

## M7 Mini-games Placeholder

- keep left-nav entry
- placeholder hub page only

## M8 Packaging

- Windows MSIX + zip
- macOS app bundle / dmg (unsigned in MVP)
- self-host deployment guide
