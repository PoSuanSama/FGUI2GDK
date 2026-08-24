# GDK Cross-Layer Change Guide

## Map The Full Flow

For a cross-layer change, write the concrete path before implementation. Common GDK flows include:

```text
Excel/Proto input -> exporter/generator -> generated C#/bytes -> server/client loader -> runtime consumer
UI configuration -> generated UIForm ID -> prefab/resource rule -> UI logic -> Procedure/ET system
Request message -> ET handler -> component/service -> response message -> client system/view
```

At every arrow, identify the owner, data shape, validation point, failure contract, and compatibility requirement.

## Boundaries To Check

- `Design/` inputs versus generated files under `Config/` and Unity `Generate/`/resource paths.
- DotNet server versus Unity client/shared ET assemblies.
- GameHot code versus non-hot Loader code.
- ET Model/ModelView state versus Hotfix/HotfixView systems.
- C# component/view code versus prefab, scene, `.meta`, resource rule, and generated ID.
- Async operation versus the UI/entity/scene/fiber lifecycle that owns cancellation and cleanup.

## Contract Rules

- Validate untrusted requests, paths, URLs, and configuration at their entry boundary.
- Keep protocol opcodes, serialized fields, Luban IDs/defaults, and resource paths backward compatible unless migration is approved.
- Make one layer own conversion and normalization; consumers should receive typed values.
- Update source inputs and derived outputs together, but never repair only the generated output.
- Define mixed-version and rollback behavior for shared client/server or persisted changes.

## Verification Checklist

- Source and generated diffs agree.
- Client and server consumers compile where applicable.
- Unity resources retain GUID/fileID references and import without Error logs.
- Success, invalid input, cancellation, repeat lifecycle, and shutdown paths are covered.
- UI/visual changes are checked at representative aspect ratios.
- Missing Unity/Bridge/build evidence is reported rather than inferred from a weaker check.
