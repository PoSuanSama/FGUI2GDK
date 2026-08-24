# Unity Client Quality

## Change Discipline

- Keep Unity assets and their `.meta` files together; preserve GUID/fileID references.
- Use Agent Bridge for supported Editor queries and modifications. Read the installed bridge `AGENT.md` and discover commands through runtime `list_commands` before use.
- Never hand-edit Unity YAML when a discovered Bridge/API command can express the operation.
- Change generator inputs first and review generated output as derived state.
- Avoid unrelated imports, formatting, package upgrades, or vendor-library edits.

## Verification

Unity C# requires Editor compilation and Error-log evidence through Agent Bridge when available. `.asmdef`, package, macro, link/AOT, scene, prefab, and importer changes need matching import/build/reference checks. UI work needs representative aspect ratios, screenshots/readback, input flow, reopen/close behavior, and Error logs.

Always run the repository change guard and `git diff --check`. A .NET/IDE solution build is supplemental only and cannot establish Unity import, serialization, domain reload, Player defines, or runtime correctness.

If Bridge or Unity is unavailable, report the missing check explicitly; do not mark it passed by inference.
