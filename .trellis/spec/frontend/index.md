# GDK Unity Client Guidelines

This section covers Unity client code, GameHot, ET client/view integration, UI, entities, procedures, and assets.

## Pre-Development Checklist

- [Directory Structure](./directory-structure.md): choose GameHot vs ET and hot vs loader ownership.
- [Components And UI](./component-guidelines.md): follow GameFramework and ET component patterns.
- [Lifecycle And Async](./hook-guidelines.md): pair setup/cleanup and cancellation.
- [State Management](./state-management.md): Procedures, ET entities, and owned containers.
- [C# Type Safety](./type-safety.md): assemblies, generated IDs, and boundary typing.
- [Unity Quality](./quality-guidelines.md): resource and Editor validation requirements.

Always load the GDK workflow Skill before Unity implementation, resource work, review, or validation.

## Quality Check

- Ownership, lifecycle, and assembly boundaries match adjacent code.
- Every Unity asset change includes its `.meta`; generated outputs have source-input changes.
- Unity compilation/log/import checks use Agent Bridge when available.
- UI changes include representative GameView and interaction evidence.
