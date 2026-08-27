# FairyGUI UIWidget 迁移

## Goal

用 FairyGUI 组件化 Widget 替代旧 UGUI UIWidget 体系，提供可复用、可嵌套、可管理生命周期的 FGUI Widget 基础层。

## Requirements

- R1 新增 `IFairyUIWidget` 与 `FairyUIWidget` 抽象。
- R2 新增 `FairyUIWidgetContainer`，管理 Widget 的打开/关闭/暂停/恢复/置顶/深度。
- R3 Widget 不依赖 UGUI，只依赖 FairyGUI GComponent。
- R4 提供 GameHot / ET 共用基础层。

## Acceptance Criteria

- [ ] Unity 编译 0 错误、0 警告。
- [ ] 不再引用旧 `AUIWidget` / `UIWidgetContainer`。
- [ ] Widget 生命周期 API 覆盖 open/close/pause/resume/cover/reveal/refocus/update。
