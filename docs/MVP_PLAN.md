# HeyeTodo MVP Completion Plan

## Summary

The MVP goal is to ship a usable HeyeTodo build where the server can be deployed independently, desktop clients can sync remotely across devices, and users have basic project and task management.

Use the planned local-first approach: the client stores task data in local SQLite, supports offline edits, and syncs through an outbox/push/pull flow when a server account is connected.

Current repo assessment:

- The server already has useful foundations: auth, project/task APIs, sync push/pull, SignalR invalidation, EF Core, PostgreSQL, migrations, Dockerfile, and Docker Compose.
- The largest MVP gaps are shared contract completeness, client SQLite persistence, client auth/session handling, API client integration, and the sync coordinator.
- The existing client task panel is local-only and currently uses JSON file persistence rather than the roadmap's SQLite/outbox model.

## Key Changes

### 1. Restore Buildable Shared Contracts

- Add the missing shared contracts, enums, and sync DTOs referenced by the server under `src/HeyeTodo.Shared`.
- Keep shared types as the single protocol source for both client and server.
- Confirm `dotnet restore HeyeTodo.sln` and `dotnet build HeyeTodo.sln` pass before larger feature work.
- Align the current EF Core migrations with the entity model so a fresh server deployment can start cleanly.

### 2. Complete Server MVP Deployment

- Keep the server independently deployable with PostgreSQL through `deploy/docker-compose.yml`.
- Preserve Docker internal PostgreSQL networking with `Host=postgres;Port=5432`.
- Keep the host PostgreSQL mapping intentionally different: `0427:5432`.
- Document required production settings:
  - `ConnectionStrings__Default`
  - `Jwt__SigningKey`
  - `Jwt__Issuer`
  - `Jwt__Audience`
  - `Cors__AllowedOrigins__*`
- Verify the server root endpoint returns a basic health response.
- Verify production startup fails clearly when `Jwt__SigningKey` is missing, placeholder, or too short.

### 3. Add Client Local-First Persistence

- Add SQLite-backed local storage for:
  - projects
  - tasks
  - sync metadata
  - outbox changes
  - auth/session settings
- Stop using JSON file persistence as the primary task/project store.
- Preserve existing user data where practical by reading the old JSON state once and importing it into SQLite.
- Each local project/task mutation should:
  - update SQLite immediately
  - update the UI from local state
  - append a pending outbox change with entity type, operation, payload, `clientId`, and `updatedAt`
- Store a stable local `clientId`.
- Store `lastPulledServerVersion` for incremental pull.

### 4. Add Client Auth and Remote Sync

- Add configurable server base URL.
- Add login, register, logout, token refresh, and current-user loading.
- Add a typed API client that attaches:
  - `Authorization: Bearer <access token>`
  - `X-Client-Id: <clientId>`
- Add a `SyncCoordinator` that runs the same core sync flow for startup, manual refresh, periodic sync, and SignalR invalidation:
  - pull changes since `lastPulledServerVersion`
  - apply server changes into SQLite
  - push pending outbox changes
  - remove accepted outbox entries
  - apply returned conflicts using MVP conflict policy
  - pull once more after push to converge local state
- Use the existing `/ws/sync` hub to trigger a pull when another device invalidates a project.
- Surface basic sync status in the UI:
  - not logged in
  - saved locally
  - syncing
  - synced
  - sync failed

### 5. Complete Basic Task Management

- Keep MVP task management focused on:
  - project create/list/filter/delete
  - task create/edit/delete
  - title and description
  - project assignment
  - start and end date
  - assignee text or current available equivalent
  - priority
  - status
- Defer timeline/Gantt, advanced planning, role panels, team collaboration, complex dependency editing, and mini-game features.
- Keep the left navigation simple and focused on MVP-ready screens.

## Public Interfaces

Shared contracts should cover these areas:

- Auth:
  - register
  - login
  - refresh
  - logout
  - current user
- Projects:
  - list
  - create
  - update
  - delete
- Tasks:
  - list
  - create
  - update
  - status update
  - delete
- Sync:
  - push request/response
  - pull response
  - change entity type
  - change operation
  - conflict
  - sync metadata

Existing server routes should remain stable for MVP:

- `/api/auth/*`
- `/api/users/me`
- `/api/projects`
- `/api/tasks`
- `/api/sync/push`
- `/api/sync/pull`
- `/ws/sync`

## Conflict Policy

Use MVP-level last-write-wins conflict handling.

- If the server has a newer version, apply the server version locally.
- If the local change is newer, keep the outbox change and retry on the next sync.
- Tombstones should hide deleted projects/tasks locally after pull.
- No field-level merge is required for MVP.

## Test Plan

### Build Validation

Run:

```bash
dotnet restore HeyeTodo.sln
dotnet build HeyeTodo.sln
```

### Server Deployment Validation

Run:

```bash
docker compose -f deploy/docker-compose.yml up -d postgres server
```

Verify:

- the server container starts
- EF Core migrations apply on startup
- `GET /` returns service status
- registration succeeds
- login succeeds
- refresh succeeds
- authenticated project and task API calls succeed

### Sync Validation

Validate these end-to-end scenarios:

- Client A creates a project and task offline, then syncs them after login.
- Client B logs into the same account and pulls Client A's project/task.
- Client B changes a task status; Client A receives the update after refresh or SignalR invalidation.
- Client A and Client B edit the same task; the app resolves the conflict according to last-write-wins without crashing or duplicating tasks.
- Deleting a task on one client hides it on the other after sync.
- Deleting a project on one client hides the project and its tasks on the other after sync.

### Offline and Token Validation

Validate:

- the client remains usable while logged out
- local edits are not lost when sync fails
- expired access tokens refresh automatically
- failed refresh returns the app to a logged-out state without deleting local task data

## Assumptions

- MVP uses local-first SQLite plus outbox sync.
- The first sync target is one user's own devices, not shared team workspaces.
- Server deployment means server plus PostgreSQL; client installer packaging is separate.
- Conflict resolution is last-write-wins for MVP.
- Existing uncommitted work in the repository must not be reverted during implementation.
- No code changes should be committed unless explicitly requested.

## Implementation Progress - 2026-05-13

- Added shared auth, task, sync, planning contracts plus shared enums so client and server compile against one protocol layer.
- Added the server project to `HeyeTodo.sln`, so `dotnet build HeyeTodo.sln` now validates server, client, and shared projects together.
- Added a client SQLite local-first repository for projects, tasks, sync metadata, outbox changes, and auth/session settings.
- Updated the task panel to load from SQLite, import legacy JSON state once, persist local edits immediately, and enqueue sync changes.
- Added a minimal account/sync page for server URL, login, register, logout, and saved session status.
- Added a typed API client and sync coordinator for token refresh, pull, push, outbox cleanup, and remote project/task materialization.
- Current remaining MVP hardening: SignalR-triggered automatic pull, richer conflict UX, project deletion from the client UI, and full end-to-end sync testing against a running PostgreSQL/server stack.
