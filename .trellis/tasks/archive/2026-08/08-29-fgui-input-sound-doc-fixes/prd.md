# FairyGUI 输入/声音桥修复与文档口径对齐

## Goal

修复三个 FairyGUI 收尾遗留问题：输入层的顶层窗口判断错误、声音桥对 GDK 的绕过、以及 AGENTS.md/接入文档的漂移。

## Background and Confirmed Facts

### R1 背景：输入层顶层窗口判断（问题 #4）

`FairyInputService.cs` 的 `CancelTopForm()`（`FairyInputService.cs:203-239`）用 `form.DepthInUIGroup` 判断「最上层窗体」，但**忽略了 `UIGroup.Depth`**：Default 组（depth 0）内 `DepthInUIGroup` 很高的窗体，会被误判为比 Pop 组（depth 100）内 `DepthInUIGroup` 较低的窗体更上层，导致返回键关闭了错误分组里的窗口。同理 `FindTopFormNavRoot()`（`:139`）无焦点时取「最上层」也未按组深度。手柄摇杆持续按住时 `TryMoveFocus` 每帧移动焦点，无边沿/重复节流。

### R2 背景：声音桥绕过 GDK（问题 #5）

`FairySound.TryPlay()`（`FairySound.cs:63-95`）未命中映射时返回 `false`（`:79`），调用方 Stage 补丁回退 FairyGUI 原生 AudioSource 路径——绕过 GDK 声音组与音量/静音设置，与「所有 UI 声音统一由 GDK Sound 持有」冲突。`volumeScale` 参数被忽略（`:90-93` 注释明确不叠加）。

### R3 背景：文档工具说明漂移（问题 #9）

- `AGENTS.md:173` 仍指导 CodeBind、`:208` 仍写 `StarForceUIForm`，而实际 UI 开发已是 FairyGUI Presenter / ET Component-System 流程。
- `Book/FairyGUI接入.md:111,114` 写旧命名空间 `Game.Hot.FairyGUI`，实际官方生成绑定为 `Game.FairyGUI`。

## Requirements

### R1. 输入层顶层判断与节流

- `CancelTopForm` 按「UIGroup.Depth 降序 → 组内 DepthInUIGroup 降序 → serial 降序」判定最上层，跨组正确。
- `FindTopFormNavRoot` 无焦点时取真正顶层窗体的导航根，判定口径与 `CancelTopForm` 一致。
- 手柄摇杆方向移动加节流（边沿触发或重复延迟），持续按住不每帧移动焦点。

### R2. 声音桥不绕过 GDK

- 未命中映射的声音不再回退 FairyGUI 原生 AudioSource；返回「已处理」语义（静默跳过或经 GDK 默认声音），不破坏「声音统一由 GDK Sound 持有」。
- `volumeScale` 处理明确：叠加到 GDK 音量或记录为有意忽略的文档化决策，不留未处理参数。

### R3. 文档口径对齐

- `AGENTS.md` 移除 CodeBind / `StarForceUIForm` 的 UI 开发指导，改为 FairyGUI Presenter / ET Component-System 流程。
- `Book/FairyGUI接入.md` 命名空间 `Game.Hot.FairyGUI` → `Game.FairyGUI`，与官方生成绑定一致。

## Acceptance Criteria

- [ ] AC01:`CancelTopForm` 跨组正确关闭真正最上层窗体（含 UIGroup.Depth 判定），单测覆盖跨组场景。
- [ ] AC02:`FindTopFormNavRoot` 无焦点时返回真正顶层导航根，口径与 `CancelTopForm` 一致。
- [ ] AC03:手柄摇杆方向移动有节流，持续按住不逐帧移动焦点。
- [ ] AC04:未映射声音不再回退 FairyGUI 原生 AudioSource，统一由 GDK Sound 处理。
- [ ] AC05:`volumeScale` 被处理或明确文档化为有意忽略，无未使用参数。
- [ ] AC06:`AGENTS.md` 无 CodeBind/StarForceUIForm 残留，改为 FairyGUI 流程。
- [ ] AC07:`Book/FairyGUI接入.md` 命名空间 `Game.FairyGUI` 与实现一致。
- [ ] AC08:编译 0 error；输入/声音冒烟回归通过；`validate_changes.py` 与 `git diff --check` 通过。

## Out of Scope

- 不处理 #2（启动/更新降级）、#3（fullScreen 生成端）、#6（ET Player）、#7（服务桥/无障碍）、#8（验证证据）——这些另立 todo。
- 不删除 `link.xml` 的 UnityEngine.UI（已单独验证 URP 不依赖，另议删除 + IL2CPP 确认）。
- 不改变已验证的共享宿主公共 API、打开链、包租约。

## Key Decisions

- R2 未映射声音的处理方式（静默跳过 vs GDK 默认声音）与 `volumeScale` 语义，需在实施时基于「声音统一由 GDK 持有」原则明确，避免破坏既有映射行为。
- R1 顶层判定复用 GF 的 UIGroup.Depth 语义，不另建第二套深度模型。
