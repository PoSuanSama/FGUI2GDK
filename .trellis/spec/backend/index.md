# GDK Server Guidelines

This section covers the ET 8.1 server and server-side tools under `DotNet/`.

## Pre-Development Checklist

Read the guides that match the change:

- [Directory Structure](./directory-structure.md) for ownership and hot-reload boundaries.
- [Data and Configuration](./database-guidelines.md) for LiteDB, MongoDB, Luban, or persistence.
- [Error Handling](./error-handling.md) for handlers, services, and asynchronous failures.
- [Logging](./logging-guidelines.md) for ET/NLog diagnostics.
- [Quality](./quality-guidelines.md) before implementation and handoff.

Also load `.agents/skills/gdk-development-workflow/SKILL.md`; it is the repository-wide authority when these concise notes do not cover a case.

## Quality Check

- The change stays inside the correct `Core`, `Model`, `Hotfix`, `Loader`, or tool boundary.
- Protocol/config/generated changes start from their source inputs.
- Async ownership, error responses, logs, and shutdown cleanup are explicit.
- Run the focused .NET build/test plus the GDK change guard; do not infer Unity correctness from it.
