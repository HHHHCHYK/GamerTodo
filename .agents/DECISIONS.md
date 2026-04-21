# Confirmed Decisions

Each entry is a decision that has been explicitly accepted by the product
owner. Agents must not silently reverse these; propose + discuss in a new
entry if a change is needed.

## D-001 · Client UI framework — Avalonia 12 (C#)

- Date: project kickoff
- Choice: Avalonia UI on top of .NET 10
- Alternatives considered: Tauri + React, .NET MAUI, Electron, Flutter Desktop
- Rationale: stay 100% C# end-to-end, native performance, good cross-platform
  coverage for Windows + macOS

## D-002 · Server stack — ASP.NET Core 10 + EF Core + PostgreSQL

- Rationale: mainstream, first-class cross-platform containerization, flex
  deploy story

## D-003 · Sync model — local-first with offline-complete usage + conflict merge

- Client maintains a full local SQLite replica
- Writes land locally, are marked dirty, and flush through an outbox
- Server resolves conflicts with field-level Last-Write-Wins plus tombstones
- Cross-CRDT designs rejected as over-engineering for MVP

## D-004 · Deployment — user-hosted (self-host) only for MVP

- We ship Docker Compose + Dockerfile
- No official SaaS instance in MVP
- Each client installation stores a user-configurable server URL

## D-005 · LLM integration — both proxy and BYO-key supported

- `IPlanningService` abstraction with two drivers
- Rule-based engine always available as fallback

## D-006 · Gantt rendering — self-drawn canvas inside Avalonia

- Fallback option approved: embed WebView + frappe-gantt if canvas scope
  explodes during M4

## D-007 · Role model — user-level subset, not project-level assignment

- `User.Roles` is a `[Flags]` bitmask; zero, one, or many
- First-run flow shows a skippable role picker
- Panels / columns / quick-actions adapt to selected roles
- No role-based access control in MVP; roles affect UI only

## D-008 · SignalR selected over raw WebSocket

- Ships with ASP.NET Core, first-party
- MessagePack protocol enabled for bandwidth

## D-009 · macOS code signing skipped for MVP

- No Apple Developer subscription required to ship v1
- `scripts/mac/sign.sh` exists as an integration point for later
- README will instruct users to `xattr -d com.apple.quarantine`

## D-010 · Mini-games is placeholder only

- Preserve nav entry, render "Coming soon"
- Keep `IMiniGameModule` interface for plug-in style extensions later

## D-011 · i18n — Chinese + English from day one

- Client: `resx` + `LocalizationService` + `{loc:T Key=...}` markup
- Server: `IStringLocalizer` against `SharedResource.resx` / `.zh.resx`
- Resource keys use `Area.SubArea` dotted style on client, `Area_Name` snake
  case on server (server resx tooling friendliness)
- Auto-detect system UI language on first run (zh-* → zh, else en); remember
  user's explicit override

## D-012 · First-run role selection is skippable

- Skip leaves the user with `RoleType.None`
- Shell falls back to a generic view when no role is chosen
- Settings page always allows editing the role set later

## D-013 · OpenAPI tooling — .NET 10 built-in `Microsoft.AspNetCore.OpenApi`

- Swashbuckle removed in M0 after `GetSwagger` TypeLoadException on .NET 10
- UI story for developer browsing deferred to M0 P1 fix plan (evaluate Scalar
  vs. waiting for Swashbuckle compatibility)

## D-014 · Avalonia `PasswordBox` is not used in MVP

- Avalonia 12 does not ship one; we use `TextBox` + `PasswordChar="*"`
- Password fields are cleared immediately after submit to minimize lifetime
- Revisit once upstream Avalonia or FluentAvalonia stabilizes a control
