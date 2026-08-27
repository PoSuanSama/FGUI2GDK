# FairyGUI 新手引导系统

## Goal

用 FairyGUI 实现新手引导系统，替代 UXTool 的引导能力，命名不再沿用 UXTool。

## Requirements

- R1 新增 `FairyGuideStep` 步骤模型。
- R2 新增 `FairyGuide` 控制器，支持开始、下一步、关闭、状态事件。
- R3 不依赖 UXTool / UGUI，只依赖 FairyGUI GObject/GComponent。

## Acceptance Criteria

- [ ] Unity 编译 0 错误、0 警告。
- [ ] 支持多步骤引导顺序。
- [ ] 不引用旧 UXTool 类型。
