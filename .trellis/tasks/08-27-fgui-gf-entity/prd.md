# FairyGUI GF Entity 迁移

## Goal

用 FairyGUI 实体表现层替代旧 UGFEntity/GF Entity 表现体系，提供场景实体对象的 FGUI 抽象与管理容器。

## Requirements

- R1 新增 `IFairyEntity` 接口。
- R2 新增 `FairyEntity` 基类。
- R3 新增 `FairyEntityContainer`，管理实体显示/隐藏/挂载/更新。
- R4 实体表现不依赖 UGUI，只依赖 FairyGUI GObject。

## Acceptance Criteria

- [ ] Unity 编译 0 错误、0 警告。
- [ ] 不再引用旧 `UGFEntity` / `AEntity` 作为新实体表现层依赖。
- [ ] 覆盖 Show/Hide/Recycle/Attach/Detach/Update 生命周期。
