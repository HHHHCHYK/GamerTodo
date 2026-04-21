# Roadmap (M0 – M8)

Total estimated effort: ~10.5 person-weeks. Order is strict; each milestone
assumes the previous one is complete.

---

## M0 · Foundation · 1 week

Scope: scaffolding that everything else stands on.

- Solution-wide conventions: `.editorconfig`, `Directory.Build.props`,
  `.gitignore`
- Shared contracts: `RoleType` (flags), task enums, sync DTOs, auth DTOs
- Server skeleton
  - ASP.NET Core Web API entrypoint
  - EF Core + PostgreSQL (`AppDbContext`, initial migration)
  - JWT auth skeleton (`AuthService`, `TokenService`, `PasswordHasher`,
    `RefreshToken` rotation)
  - SignalR `SyncHub`
  - Localization scaffolding (`IStringLocalizer<SharedResource>`, zh + en
    resx)
- Client skeleton
  - Avalonia shell + navigation
  - Login / Register / Role-selection views (skippable)
  - Mini-games placeholder hub
  - Local SQLite database (`LocalDbContext`)
  - `TokenStore` (DPAPI on Windows)
  - `ApiClient` with refresh-retry
  - Runtime localization (`LocalizationService`, `TExtension`)
- Deploy assets: `docker-compose.yml`, `Dockerfile`, `dotnet-tools.json`,
  README/ROADMAP docs

Exit criteria:
- `dotnet build HeyeTodo.sln` returns 0 warnings 0 errors
- Initial EF migration generated
- Server starts (given Postgres is up) and serves placeholder root
- Client starts, shows login page, auto-detects system language

---

## M1 · Auth & User Profile · 1 week

Scope: make the authentication + profile pipeline production-ready end-to-end.

See `M1-PLAN.md` once it lands. High-level:

- Finish client-server auth wire-up
- `/api/users/me` and role/language PATCH endpoints
- Skippable role selection persisted to server
- Settings page (account, appearance, roles, connection)
- Language auto-detection verification + prompt on mismatch
- First unit test projects (server + client)

Exit criteria:
- Fresh user can register → skip or complete role picker → reach Shell →
  modify settings → logout → re-login, all offline-resilient
- `dotnet test` runs green

---

## M2 · Tasks & List View · 1.5 weeks

- Project CRUD (server + client)
- Task CRUD (server + client)
- Local-first task repository over SQLite (writes local, marks `IsDirty`)
- List view: filter, sort, quick status transitions
- Generic (role-agnostic) task fields only; role-specific fields ride
  `RoleFieldsJson`

Exit criteria: a user can create a project, add tasks, change status, and see
them after relaunching the app even without network.

---

## M3 · Sync Engine · 2 weeks

- Outbox / inbox queues on client
- `POST /api/sync/push` + `GET /api/sync/pull` endpoints
- Field-level LWW conflict resolution + tombstones
- SignalR `SyncHub` subscription per project, live invalidation events
- Background sync worker on client (reconnect, exponential backoff)

Exit criteria: two devices logged into the same account converge to the same
state within a few seconds; offline edits replay cleanly on reconnect.

---

## M4 · Gantt · 2 weeks

- Shared `TasksViewModel` feeds both list and Gantt (single data source)
- Self-drawn Avalonia `GanttCanvas` (fallback option: embedded WebView +
  frappe-gantt if canvas effort overruns)
- Zoomable timeline (day / week / month)
- Drag to move, resize edges to change duration
- Dependency lines; edits write through to `TaskDependency`
- Two-way sync: changes in Gantt are immediately reflected in list view

Exit criteria: a user can manage tasks equivalently in either view.

---

## M5 · Role Panels · 1 week

- `IRolePanelProvider` abstraction with 5 concrete providers + one "generic"
  fallback
- Per-role: default view, visible columns, quick actions, dashboard widgets,
  `RoleFieldsJson` schema
- Top-bar role-context switcher (only for users with multiple roles)
- Hide panels for roles the user did not select
- Generic view for users with 0 roles

Exit criteria: switching role context alters surfaces without touching the
underlying task data.

---

## M6 · Planning · 1 week

- `IPlanningService` abstraction
- `RuleBasedPlanner`: topological sort + priority / deadline weighting
- `LLMPlannerViaServer`: client → server → provider; server owns API key
- `LLMPlannerLocalKey`: client → provider directly with user's key
- LLM returns priority scores with rationale; local topological pass produces
  the final legal order

Exit criteria: "Auto-plan" button reorders tasks; LLM failures gracefully fall
back to rule-based output.

---

## M7 · Mini-games Placeholder · 0.5 week

- Keep left-nav entry
- "Coming soon" hub page (bilingual)
- `IMiniGameModule` interface reserved for future plug-ins (no implementation)

Exit criteria: entry visible, page renders, no stubs leak into other flows.

---

## M8 · Packaging · 0.5 week

- Windows: MSIX + portable zip (both unsigned in MVP)
- macOS: `.app` + `.dmg` (unsigned). README documents
  `xattr -d com.apple.quarantine`
- Placeholder signing script kept at `scripts/mac/sign.sh`
- Self-hosted deployment guide (env vars, reverse-proxy recipe, backup
  recommendations)

Exit criteria: binary artifacts build on a clean machine; a fresh user can
install and reach the login screen.

---

## Risk register

| Risk | Mitigation |
|---|---|
| Avalonia self-drawn Gantt effort overruns M4 | Deliver read-only version first; editing slips to v1.1 |
| Offline conflict edge cases balloon | Only guarantee field-level LWW in MVP; complex cases bounce back to the user as "resolve manually" |
| macOS notarization complexity | Skipped in MVP; documented in M8 |
| LLM violates dependency constraints | LLM returns scores only; local topological sort enforces legality |
| Self-hosted onboarding friction | Provide one-command `docker compose up`, first-run wizard in future |
