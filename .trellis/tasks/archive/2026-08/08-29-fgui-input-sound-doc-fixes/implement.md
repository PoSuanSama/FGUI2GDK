# FairyGUI 输入/声音桥修复与文档口径对齐 — 实施记录

## R1 输入层

- [x] `FairyInputService` 提取 `FindTopForm()`,按 `UIGroup.Depth` → `DepthInUIGroup` → `serial` 三层判定最上层。
- [x] `CancelTopForm` 与 `FindTopFormNavRoot` 复用 `FindTopForm`,跨组口径一致。
- [x] 摇杆方向改 `leftStick.left/right/up/down.wasPressedThisFrame` 边沿触发,消除持续按住逐帧跳焦点。

## R2 声音桥

- [x] `FairySound.TryPlay` 未映射/失败/空名均返回 `true`(已处理),不再回退 FairyGUI 原生 AudioSource。
- [x] `volumeScale` 文档化为有意忽略(GDK 音量由 Luban DRUISound.Volume 统一控制)。

## R3 文档

- [x] `AGENTS.md`:Key Patterns 改 FairyGUI UI/FairyEntity;UI 创建流程改 IFairyUIPresenter / FairyUIFormComponent。
- [x] `Book/FairyGUI接入.md`:命名空间 `Game.Hot.FairyGUI` → `Game.FairyGUI`。

## 验证

- [x] 编译 gen 11 0 error。
- [x] 冒烟 `RunFairyInputSoundFixSmokeTest`(TryPlay 未映射不回退 + CancelTopForm 关闭窗体)通过。
- [x] 回归 `RunFairyUIManagerSmokeTest` 通过;停止 PlayMode 后 Error 0。

## 未覆盖

- CancelTopForm 跨组场景(Default + Pop 同开)的端到端冒烟未做,`FindTopForm` 三层比较逻辑经代码审查确认。
- ET 模式输入/声音冒烟未重跑(改动在 Game 程序集,ET 共用,符号已恢复 GameHot)。
