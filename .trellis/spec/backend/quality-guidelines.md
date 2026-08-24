# Server Quality

## Implementation

- Follow existing ET analyzers, namespaces, attributes, and lifecycle interfaces.
- Use `Cysharp.Threading.Tasks.UniTask`; do not introduce ETTask or fire-and-forget work without error ownership.
- Keep validation at trust boundaries and make filesystem/process/network operations bounded and cancellable.
- Prefer an existing component, service, generator, or helper over a parallel abstraction.
- Treat public APIs, protocols, serialized data, Luban schemas, and IDs as compatibility boundaries.

Representative patterns: typed handlers in `DotNet/Hotfix/Server/Admin/`, service ownership in `DotNet/Hotfix/Server/Admin/Services/`, and fiber composition in `FiberInit_Admin.cs`.

## Verification

Run the narrowest build/test that proves the changed server surface, expanding to `dotnet build DotNet/DotNet.sln` or `dotnet build Kit.sln` for shared contracts. Run:

```powershell
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check
```

A successful .NET build proves type/analyzer checks only. It does not prove Unity import, compilation, serialization, or runtime behavior.
