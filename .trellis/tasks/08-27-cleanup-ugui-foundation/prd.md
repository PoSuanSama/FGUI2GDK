# 第一批清理 UGUI 基础层代码

## Goal

删除仍保留的 UGUI Form/Widget 基础层与 `UnityGameFramework.Runtime.UI` 绑定层，使 FairyGUI 成为唯一 UI 宿主，同时不删除待定 FGUI 迁移的五块业务。

## Requirements

- R1 删除 `UnityGameFramework.Runtime.UI` 绑定层目录及 `.meta`。
- R2 删除 Game/UI/Common 下 UGUI Form/Widget 基类与辅助类。
- R3 删除 ET `UGFUIForm` / `UGFUIWidget` 体系及旧 UIWidget 测试代码。
- R4 精简 `UGFSystemSingleton`，只保留 `UGFEntity` 分发。
- R5 删除 `GameEntry.UI` 属性与初始化，并确认 `GameFramework.prefab` 无 UI 节点。
- R6 清理 Editor 侧 UGUI UI 生成工具与模板。
- R7 保持以下五项暂不删除，供后续 FGUI 迁移：GF Entity、HPBar、UXTool、RuntimeInspector、UIEntity/UIWidget。

## Acceptance Criteria

- [ ] Unity ET 编译 0 错误、0 警告。
- [ ] Unity GameHot 编译 0 错误、0 警告。
- [ ] ET FairyGUI 背包冒烟通过。
- [ ] `rg` 不再出现 `UGFUIForm`、`UGFUIWidget`、`AUIForm`、`AExUIForm`、`UIFormLogic`、`GameEntry.UI` 等已删除符号。
- [ ] 五块待定业务目录仍存在且未被删除。
