# FairyGUI GF 能力透出复核与补齐

## Goal

复核 `FairyUIManager` 对 GF `IUIManager` 能力的透出是否与 HANDOFF §19.1 的缺口清单一致，并补齐尚未透出的对象池调参 4 属性与三个 GF 事件桥，使 FairyGUI 接入层对 GF UI 语义层的暴露达到与原 UGUI 实现对等。

## Background and Confirmed Facts

HANDOFF §19.1 曾列出 GF `IUIManager` 契约的能力缺口，并在 §19.5 声称 `dd72f24c` 已透出大部分。对当前代码(`FairyUIManager.cs`)核验后，实际状态如下：

**已透出(需复核正确性,不需重做):**

| 能力 | GF 契约(`IUIManager.cs`) | FairyUIManager |
| --- | --- | --- |
| 实例锁/优先级 | `SetUIFormInstanceLocked`/`SetUIFormInstancePriority`(:373,380) | `FairyUIManager.cs:199-206` |
| 批量关闭 | `CloseAllLoadedUIForms`/`CloseAllLoadingUIForms`(:342,353) | `FairyUIManager.cs:214-218` |
| 诊断 | `IsValidUIForm`/`UIGroupCount`/`GetAllUIGroups`(:123-235) | `FairyUIManager.cs:208-212` |
| 事件桥 | `OpenUIFormFailure`/`CloseUIFormComplete` | `FairyUIManager.cs:31,36,75-76,470-477` |

**仍缺(本任务补齐):**

| 缺口 | GF 契约(`IUIManager.cs`) | 影响 |
| --- | --- | --- |
| 对象池调参 4 属性 | `InstanceAutoReleaseInterval`(:31)、`InstanceCapacity`(:40)、`InstanceExpireTime`(:49)、`InstancePriority`(:58) | 无法按 UI 组/全局调优窗体对象池,影响内存与 GC |
| GF 事件桥 3 个 | `OpenUIFormSuccess`(:67)、`OpenUIFormUpdate`(:77)、`OpenUIFormDependencyAsset`(:82) | 打开成功/进度/依赖资产加载无法被业务订阅,只能靠轮询兜底 |

注意:`OpenUIFormFailure`(:72) 与 `CloseUIFormComplete`(:87) 已桥接;`SetUIFormInstancePriority`(:380) 是「实例优先级」,与对象池的「`InstancePriority`(:58) 池默认优先级」是不同契约,前者已透出、后者未透出,不可混淆。

## Requirements

### R1. 复核已透出能力的正确性

- 逐一核对实例锁/优先级、批量关闭、诊断、Failure/CloseComplete 事件桥的参数透传与空安全(未初始化时抛稳定 `GameFrameworkException` 而非空引用),与本任务新补内容统一。

### R2. 透出对象池调参

- 在 `FairyUIManager` 透出 `InstanceAutoReleaseInterval`、`InstanceCapacity`、`InstanceExpireTime`、`InstancePriority` 四个属性,直通 `GetRequiredUIManager()` 的对应契约。
- 明确作用域:GF 的对象池属性是 UIManager 级还是按 UIGroup 级,按契约如实暴露,不臆造分组参数。
- 属性读写走 `GetRequiredUIManager()`,未初始化时保持既有稳定异常契约。

### R3. 桥接剩余三个 GF 事件

- 在 `FairyUIManager` 静态事件层桥接 `OpenUIFormSuccess`、`OpenUIFormUpdate`、`OpenUIFormDependencyAsset`,与既有 `OpenUIFormFailure`/`CloseUIFormComplete` 同构(订阅 `m_UIManager` 事件→转发静态事件)。
- 事件参数类型与 GF 契约一致(`OpenUIFormSuccessEventArgs`/`OpenUIFormUpdateEventArgs`/`OpenUIFormDependencyAssetEventArgs`),不丢失 sender/args。
- `Dispose`/重建 Initialize 时对称退订,避免重复订阅或泄漏。

### R4. 验证

- 单测/冒烟覆盖:对象池四属性读写回读一致;触发一次打开成功/进度/依赖资产事件被转发;重复 Initialize 后事件不重复订阅。
- GameHot/ET 冒烟回归不因新增透出而破坏。

## Acceptance Criteria

- [ ] AC01:对象池四属性 `InstanceAutoReleaseInterval`/`InstanceCapacity`/`InstanceExpireTime`/`InstancePriority` 已在 `FairyUIManager` 透出且读写直通 GF,回读一致。
- [ ] AC02:`OpenUIFormSuccess`/`OpenUIFormUpdate`/`OpenUIFormDependencyAsset` 三事件已桥接为静态事件,参数契约与 GF 一致。
- [ ] AC03:Initialize 重建/Dispose 后事件订阅对称,无重复转发或泄漏(可经计数断言)。
- [ ] AC04:实例锁/优先级、批量关闭、诊断、Failure/CloseComplete 等已透出能力复核无误,未初始化时异常契约稳定。
- [ ] AC05:GameHot/ET 冒烟回归通过,Error 0。
- [ ] AC06:HANDOFF §19.1 缺口清单已更新为「已补齐」并附本任务修订。

## Out of Scope

- 不改 GF `IUIManager` 供应商核心接口,只做 `FairyUIManager` 层的透出与事件桥。
- 不改已验证的共享宿主公共 API(owner token、打开链、包租约)。
- 不迁移生产 UGUI 页面、不处理阶段 3 边界项。

## Key Decisions

- 透出遵循「薄转发」:`FairyUIManager` 只直通 GF 契约,不引入第二套缓存或语义。
- 事件桥与既有 Failure/CloseComplete 同构,不另造事件系统;静态事件命名与 GF 原事件对齐。
- `SetUIFormInstancePriority`(实例级)与 `InstancePriority`(对象池默认级)区分清楚,不合并。
