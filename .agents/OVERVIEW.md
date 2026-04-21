# HeyeTodo Overview

## What is HeyeTodo

A cross-platform TODO and lightweight project-management application tailored
for **indie game developers**. The product pitches itself on three
differentiators:

1. **Role-aware UI.** A single user can carry any subset of
   {Producer, Designer, Artist, Programmer, Sound Designer} roles. The
   surfaces (columns, quick actions, dashboards) adapt to those roles.
2. **List + Gantt parity.** Both views read and write the same underlying
   task records. No duplicated data, no sync quirks inside the app.
3. **Auto-ordering by priority and dependency.** Mixes a deterministic
   rule-based topological sort with optional LLM priority scoring.
4. **Mini-games hub (deferred).** A dedicated left-nav entry is reserved for
   small "take a break" games; only a placeholder ships in MVP.

## Form factor

- **Clients:** Windows and macOS desktop apps (Avalonia 12, C#)
- **Server:** self-hosted ASP.NET Core 10 service backed by PostgreSQL
- **Sync style:** local-first; each client keeps a full SQLite mirror and
  reconciles via outbox/inbox against the server, with field-level LWW
  conflict resolution plus tombstones
- **Transport:** HTTPS for REST, SignalR (WebSocket + MessagePack) for
  realtime invalidation

## Architectural sketch

```
┌──────────────────────────────┐           ┌────────────────────────────────┐
│  Avalonia Client (Win/Mac)   │           │    ASP.NET Core Server         │
│                              │           │                                │
│  Views / ViewModels          │           │    Controllers + SyncHub       │
│           │                  │           │              │                 │
│  Navigation / Session        │           │    Application services        │
│           │                  │  HTTPS    │              │                 │
│  Domain services             │ ◄──────►  │    Repositories (EF / Npgsql)  │
│           │                  │  WSS      │              │                 │
│  Sync engine (outbox/inbox)  │           │    PostgreSQL                  │
│           │                  │           │                                │
│  Local SQLite                │           │    Resources / localization    │
└──────────────────────────────┘           └────────────────────────────────┘
```

## Data model at a glance

- `User` — identity + `Roles` bitmask + `ActiveRoleContext` + preferred
  language
- `Project` — owner-scoped container for tasks and dependencies
- `TodoTask` — title, description, status, priority, timeline, assignee,
  role-specific JSON, sync metadata
- `TaskDependency` — FS/SS/FF/SF relationship between two tasks
- `RefreshToken` — hashed, per-client rotation

All syncable entities extend `SyncableEntity` / `LocalSyncable` carrying
`ServerVersion`, `UpdatedAt`, `UpdatedBy`, `ClientId`, `DeletedAt`.

## Non-goals for MVP

- Team-level RBAC (roles are UX only)
- SaaS hosting (self-host only)
- Mobile / web clients
- Real-time multi-cursor collaboration
- Full CRDT-based merging

## First-run user experience

1. Auto-detect system UI language (zh-* → zh, else en)
2. Show login; offer register
3. On registration, open a skippable role picker
4. On completion, land in the Shell with Tasks, Gantt, Mini-games, Settings

## Source of truth for planning

- Long-term plan: `.agents/ROADMAP.md`
- Confirmed decisions: `.agents/DECISIONS.md`
- Coding rules: `.agents/CONVENTIONS.md`
- Per-milestone reviews: `.agents/M{n}-REVIEW.md`
- Per-milestone fix plans: `.agents/M{n}-<topic>-FIX-PLAN.md`
