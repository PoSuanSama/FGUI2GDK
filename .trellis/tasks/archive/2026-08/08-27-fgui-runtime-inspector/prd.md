# FairyGUI RuntimeInspector 迁移

## Goal

用 FairyGUI 实现运行时调试面板，替代旧 RuntimeInspectorForm，并接入现有 FairyUIManager。

## Requirements

- R1 新增 FairyGUI RuntimeInspector 界面资源并发布到工程。
- R2 新增 `FairyRuntimeInspectorPresenter`，实现 `IFairyUIPresenter`。
- R3 通过 `FairyUIManager` 打开/关闭 RuntimeInspector，支持运行时查看框架/UI 状态。
- R4 删除旧 `RuntimeInspectorForm` 的 UGUI 占位实现。

## Acceptance Criteria

- [ ] ET 和 GameHot 模式均能打开 RuntimeInspector。
- [ ] 编译 0 错误、0 警告。
- [ ] 旧 `RuntimeInspectorForm` 不再继承 UGUI 基类或已被删除。
