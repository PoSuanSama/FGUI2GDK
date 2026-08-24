# Unity Client Directory Structure

## Choose The Runtime First

- `Unity/Assets/Scripts/Game/Hot/Code/`: HybridCLR GameHot business code.
- `Unity/Assets/Scripts/Game/Hot/Loader/`: non-hot GameHot bootstrap; it must not depend on behavior available only after hot code loads.
- `Unity/Assets/Scripts/Game/ET/Code/`: ET Model, ModelView, Hotfix, HotfixView, shared, client, and server partitions.
- `Unity/Assets/Scripts/Game/ET/Loader/`: non-hot ET loading and UGF integration.
- `Unity/Assets/Scripts/Game/Editor/`: Editor-only project tooling.
- `Unity/Assets/Scripts/Library/`: framework/third-party code; extend from `Game/` unless vendor modification is required.

GameHot uses GF/MonoBehaviour-style logic, demonstrated by `Game/Hot/Code/Base/HotEntry.cs`, `UI/`, `Entity/`, and `Procedure/`. ET uses data/components in Model/ModelView and systems/behavior in Hotfix/HotfixView.

## Assets And Inputs

- Runtime/Editor assets live under `Unity/Assets/Res/` with their `.meta` files.
- Luban and Proto inputs live under `Design/`; generated outputs remain derived.
- UI prefabs and entity prefabs follow the locations documented in `AGENTS.md` and `Book/UI开发.md`.

Do not duplicate one feature across ET and GameHot unless the requirement explicitly needs both modes.
