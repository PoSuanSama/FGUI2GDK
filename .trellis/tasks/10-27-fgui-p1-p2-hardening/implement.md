# 实施计划

按 P1 → P2 → P3 → P4 顺序执行，每阶段独立可交付、可回滚。

## 阶段 1：包常驻与预热

### 改动

- [x] `PackageState` 增加 `PinCount`；`FairyPackageDiagnostic` 增加 `IsPinned`
- [x] `FairyPackageLease.MarkPinned()`：对闭包内每个 state `PinCount++`
- [x] `FairyPackageLease.Dispose()`：先 `PinCount--`，再走原逆序 `Release`
- [x] `FairyPackageManager.Release` 增加 `state.PinCount > 0` 提前返回
- [x] 新增 `WarmPackageAsync` / `ReleaseWarmPackage` / `IsPackageWarm` / `ClearWarmPackages`
- [x] `FairyUIManager.Shutdown` 在包管理器关闭前调用 `ClearWarmPackages`
- [x] `FairyPackageManager.Shutdown` 兜底清零 `PinCount`
- [x] `HotEntry` / ET `Init` 增加 `WarmPackages` 白名单（默认空）
- [x] 测试基线适配：`FairyPackageDiagnosticsMatch` 双侧过滤 pinned、`FairyGUIDemoAgent` 基线循环改 pin-aware
- [x] 新增 AgentCallable `ValidateFairyPackagePinLifecycle`

### 验证

编译 0/0；`ValidateFairyPackagePinLifecycle`、`OpenFairyDemoForm`、`ValidateFairyPackageManagerLifecycle`、`ValidateFairyUIFormLifecycleCycles`、`ValidateFairyUIManagerShutdownReinitialize` 全部通过。

## 阶段 2：删除转发层

### 改动

- [x] 新增 `public IUIManager Manager => GetRequiredUIManager();`
- [x] 12 个生产调用文件改走 `Manager`
- [x] `FairyUIManagerSmokeTest` 的对象池属性与事件订阅改走 `Manager`
- [x] 删除 3 个零调用方法（IsValidUIForm / SetUIFormInstanceLocked / SetUIFormInstancePriority）与 5 个静态事件
- [x] 高频转发方法保留为一行门面（`=> Manager.XXX`），138 处调用点不迁移，避免误伤
- [x] 保留 `AddUIGroup` / `Initialize` / `Shutdown` / `OpenFairyUIFormAsync` / ET 关闭回调 / `UIFormTableProvider`
- [x] 保留 `GetUIForm`(×2) / `GetAllLoadedUIForms` 的类型收窄适配器

### 验证

编译 0/0；`RunFairyGFPassthroughSmokeTest`、`ValidateFairyUIOpenFailureCleanup`（干净会话单跑）、`ValidateFairyUIResourceFailureCleanup`、`ValidateFairyUIBindingTypeMismatchCleanup`。

## 阶段 3：双链路统一

### 前置验证

先在 `FairyUIFormComponent` 试加 1 个方法编译，确认 `Share.Analyzer` 不报 ET0006。不通过则跳过本阶段。

### 改动

- [x] `FairyUIFormComponent : Entity, IDestroy, IFairyUIPresenter`，9 个方法搬入，`OnClose` 末尾 `this.Dispose()`
- [x] `UIComponentSystem` 创建点改 `try/finally` 兜底 Dispose
- [x] 删除 `FairyUIFormService.PresenterFactory`
- [x] 删除 `FairyUIPresenterAdapter.cs`
- [x] 15 处测试断言改为 `Presenter is FairyUIFormComponent c && !c.IsDisposed`
- [x] 新增 `FairyUIFormTestAccess.GetComponent<T>(this FairyUIForm)` 收敛断言写法

### 验证

编译 0/0；`RunFairyFiberLifecycleSmokeTest`、`RunFairyInventorySmokeTest`、`RunFairyInputSmokeTest`、`RunFairyPreSerialOwnerDestroySmokeTest`、`RunFairyBootstrapShutdownReinitializeSmokeTest`、`RunFairyUIFormSkeletonSelfCheck`。

## 阶段 4：加载进度反馈

### 改动

- [x] 新增 `FairyUIOpenStage` 枚举与 `FairyUIOpenProgress` 结构
- [x] `FairyOpenRequest` 增加 `Progress` 字段
- [x] `OpenFairyUIFormAsync` 增加 `IProgress<FairyUIOpenProgress>` 重载
- [x] `OnOpenUIFormUpdate` 映射进度，单调钳制，完成推 1.0

### 验证

编译 0/0；打开/失败/取消三组用例，观察进度序列单调。

## 通用门禁

- [x] Unity 编译 0 error / 0 warning
- [x] `type=error` 日志 0 条
- [x] `git diff --check` 通过
- [x] 每阶段结束更新 `evidence.md`

## 审查门禁

- [x] 没有把生成输出作为事实来源直接编辑。
- [x] 每个 async await 都有 owner/cancellation 决策。
- [x] 每个资源/容器都有唯一所有者和释放路径。
- [x] 常驻包有明确的退出路径（`ReleaseWarmPackage` / `ClearWarmPackages`）。
