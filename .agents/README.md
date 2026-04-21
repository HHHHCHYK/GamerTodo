# HeyeTodo — Agent Working Notes

This folder stores the long-lived planning artifacts that guide any AI agent or
human contributor working on HeyeTodo.

| File | Purpose |
|---|---|
| `README.md` | This index |
| `OVERVIEW.md` | Product goals, tech stack, architecture summary |
| `ROADMAP.md` | Full M0–M8 milestone plan |
| `M0-REVIEW.md` | Review findings of the M0 deliverable |
| `M0-P1-FIX-PLAN.md` | Detailed plan for fixing the P1 items surfaced in M0-REVIEW |
| `DECISIONS.md` | Product / architecture decisions already confirmed with the user |
| `CONVENTIONS.md` | Code / naming / process conventions for contributors |

## How agents should use this folder

1. Before starting any task, read `OVERVIEW.md` + `ROADMAP.md` + `DECISIONS.md`.
2. If the task maps to a specific milestone, read that milestone's section in
   `ROADMAP.md`.
3. Any architectural decision taken during a task must be appended to
   `DECISIONS.md` with a dated entry.
4. Review findings should be written as dedicated `Mx-REVIEW.md` files; fix
   plans go next to them as `Mx-<TOPIC>-FIX-PLAN.md`.
5. Never delete historical notes; append, correct, or supersede them.
