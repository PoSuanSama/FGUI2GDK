# 验证证据

## 2026-10-27 P1/P2/P3/P4 全量交付

基线：Unity `6000.3.22f1`，双符号 `UNITY_GAMEHOT;UNITY_ET`。每阶段独立编译 + 回归，全部通过。

### P1 包常驻与预热

- `PackageState.PinCount` + `FairyPackageDiagnostic.IsPinned`；`FairyPackageLease.MarkPinned` / `UnpinAndDispose`。
- `Release` 增加 `PinCount > 0` 提前返回；三处归零卸载逻辑未改动。
- 新增 `WarmPackageAsync` / `ReleaseWarmPackage` / `IsPackageWarm` / `ClearWarmPackages`，经 `FairyUIManager` 暴露 public 门面。
- `FairyUIManager.Shutdown` 在包管理器关闭前调用 `ClearWarmPackages`；`FairyPackageManager.Shutdown` 兜底清零 `PinCount`。
- `HotEntry` / `FairyGUIBootstrap` 各加 `WarmPackages` 白名单（默认空，无加载开销）。
- 测试基线 pin-aware：`FairyPackageDiagnosticsMatch` 双侧过滤 `IsPinned`，`UIPackage.GetByName` 断言按常驻状态放宽。

新增 `ValidateFairyPackagePinLifecycle`：warm → 重复 warm 幂等 → 打开关闭 → 包仍驻留 → unwarm → 重复 unwarm 返回 false → 诊断回基线。

### P2 删除转发层

- 新增 `public IUIManager Manager`；25 处 `GetRequiredUIManager().` 统一为 `Manager.`。
- 删除 3 个零调用方法（`IsValidUIForm`、`SetUIFormInstanceLocked`、`SetUIFormInstancePriority`）与 5 个静态事件及其 `?.Invoke`。
- `FairyUIManagerSmokeTest` 的事件订阅改走 `Manager`。
- 22 处生产调用迁移到 `Manager`（`HotEntry`、`FairyInventoryFlow`、`FairyDialogFlow`、`ProcedureMenu`、`FairyInputService`、`UIComponentSystem`、`FairyGUIBootstrap` 等）。
- 高频转发方法保留为一行门面（`=> Manager.XXX`）；138 处调用点未迁移，避免误伤。

### P3 双链路统一

- 前置验证：`FairyUIFormComponent` 加方法后 `Share.Analyzer` 未报 ET0006，方案可行。
- `FairyUIFormComponent : Entity, IDestroy, IFairyUIPresenter`，9 个生命周期方法搬入，`OnClose` 末尾 `Dispose()`。
- 测试钩子改道：新增 `FairyUIManager.AfterViewReadyForTesting`（`#if UNITY_EDITOR`，触发一次后自动清除），替代 Adapter 的 per-open 钩子。
- `UIComponentSystem.CreateComponentPresenter` 直接返回 Component；`PresenterFactory` 的兜底 Dispose 改为 `(presenter as FairyUIFormComponent)?.Dispose()`。
- 删除 `FairyUIPresenterAdapter.cs`（115 行）。
- 14 处测试引用改写：`FairyUIPresenterAdapter` → `FairyUIFormComponent`，`.Component` 访问收敛为组件自身，`!= null` 改为 `!IsDisposed`。

### P4 加载进度反馈

- 新增 `FairyUIOpenStage`（Descriptor/Package/Localization/View/Asset/Completed）与 `FairyUIOpenProgress`。
- `OpenFairyUIFormAsync` 增加 `IProgress<FairyUIOpenProgress>` 重载；`FairyUIFormService` 同步门面。
- 阶段权重 0.05/0.1/0.55/0.6/0.7，GF 资源回调映射到 `0.7 + 0.3 * progress`，局部与请求共用单调序列，取消/失败不推 Completed。

### 回归结果

编译：每阶段 0 error / 0 warning。

| 场景 | 方法 | 结果 |
| --- | --- | --- |
| FairyGUIDemo | `ValidateFairyPackagePinLifecycle` | ok |
| FairyGUIDemo | `ValidateFairyPackageManagerLifecycle` | ok |
| FairyGUIDemo | `ValidateFairyUIFormLifecycleCycles` | ok |
| FairyGUIDemo | `ValidateFairyUIManagerShutdownReinitialize` | ok |
| FairyGUIDemo | `ValidateFairyUIOpenFailureCleanup` | ok |
| FairyGUIDemo | `ValidateFairyUIBindingTypeMismatchCleanup` | ok |
| FairyGUIDemoET | `RunFairyFiberLifecycleSmokeTest` | ok |
| FairyGUIDemoET | `RunFairyInventorySmokeTest` | ok |
| FairyGUIDemoET | `RunFairyInputSmokeTest` | ok |
| FairyGUIDemoET | `RunFairyPreSerialOwnerDestroySmokeTest` | ok |
| FairyGUIDemoET | `RunFairyBootstrapShutdownReinitializeSmokeTest` | ok |
| FairyGUIDemoET | `RunFairyRunnerGameObjectDestroyReinitializeSmokeTest` | ok |
| FairyGUIDemoET | `RunFairyOwnerLifecyclePressureSmokeTest` | ok |

`type=error` 日志：两个场景均为 0 条。

### 观察与边界

- 测试间存在既有状态污染：`ValidateFairyPackagePinLifecycle` 紧跟生命周期测试会因 `GameEntry.Resource` 被清空而失败，干净会话通过。属既有隔离问题，非本轮引入。
- ET 场景启动约 50 秒；`isPlayingOrWillChangePlaymode` 在该仓库 Bridge 上可能长期为 true，属状态缓存，不影响测试执行。
- 未验证：真机、IL2CPP、性能基线。P1 的「预热后首次打开耗时 ≤ 50%」需在真机或带资源计时的场景下补测。
