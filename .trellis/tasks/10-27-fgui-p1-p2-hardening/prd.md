# FairyGUI 接入 P1+P2 缺陷修复

## Goal

解决 FairyGUI 接入审查中列出的结构性问题（P0 已完成）。四个阶段各自独立可交付：
包常驻与预热、删除管理器转发层、双链路统一、加载进度反馈。

## Requirements

### P1 包常驻与预热

- `FairyPackageManager` 支持白名单包在引用计数归零后不卸载。
- 预热与常驻是同一机制：零引用即卸载，因此「预热后立即释放」没有意义。
- 依赖包必须一并常驻，避免根包驻留而依赖被卸载的半可用状态。
- 默认白名单为空；由 `HotEntry` / ET `Init` 显式声明。
- `Shutdown` 必须清空常驻表，跨重初始化不残留。

### P2 删除转发层

- `FairyUIManager` 暴露 `IUIManager Manager`，删除一行式转发方法。
- 保留 FairyGUI 专有 API：`AddUIGroup`、`Initialize`、`Shutdown`、`OpenFairyUIFormAsync`、`UIFormTableProvider`、ET 关闭回调。
- 保留 `GetUIForm` / `GetAllLoadedUIForms` 的 Fairy 类型收窄适配器。

### P3 双链路统一

- ET 的 `FairyUIFormComponent` 直接实现 `IFairyUIPresenter`，删除 `FairyUIPresenterAdapter` 与 `FairyUIFormService.PresenterFactory`。
- 编辑器测试钩子不放在 Entity 上，改由 `FairyUIManager` 的 editor-only 重载承载。
- 前置验证：加方法后分析器若报错，跳过本阶段，不影响 P1/P2/P4。

### P4 加载进度反馈

- 新增 `OpenFairyUIFormAsync` 重载接受 `IProgress<FairyUIOpenProgress>`，不改既有签名。
- 进度单调递增；取消与失败不推送完成。

## Acceptance Criteria

- [ ] 白名单包在窗体全部关闭后仍为 `Ready`，重新打开不触发 `UIPackage.AddPackage`。
- [ ] `FairyPackageManager.GetDiagnostics()` 暴露 `IsPinned`，测试基线过滤常驻条目后回到空集。
- [ ] `FairyUIManager` 公开成员收敛到 12 个以内，且不存在仅做 `GetRequiredUIManager().XXX()` 的一行转发。
- [ ] ET 与 GameHot 两条链路的 Presenter 断言均通过，`FairyUIPresenterAdapter.cs` 已删除。
- [ ] 既有回归全部通过，`type=error` 日志 0 条，编译 0 error / 0 warning。

## Notes

- P0（`FairyUIException` 错误分类、主线程断言）已在同一批次完成。
- 不做 descriptor 缓存：收益仅省一次加载，且首次无法降到 1 次。
- 不做业务导航栈：属于新增功能，不是缺陷。

## Deferred items

以下问题已核实，但按「触发条件是否成立 / 最坏结果 / 有无运行时证据」三道闸判定**当前不进入修复队列**。每项都记录了不阻塞的理由，不是「还没修」。

- UI 单实例竞争窗口（`FairyUIManager` 的单实例检查早于后续多次 await）：需两个调用同帧发起才触发，正常业务不构造就不发生。
- `ProcedureCheckVersion` 跳过远端版本请求：有意简化，当前没有热更服务器；补版本请求属于新增能力，不是缺陷。
- `KChannel` 重组长度无总量上限：需公网暴露才构成风险，当前是单机 Demo。
- Router 接受客户端提供的目标地址：同上，取决于部署网络隔离。
- 协议 Opcode 按消息顺序递增：需新旧端混用才出问题，单人开发不存在。
- 管理后台跨进程 sender 与版本激活非原子：Demo 阶段不需要。
- `IScheduler` 只有 `Add` 没有 `Remove`：上游设计，scheduler 里的 id 残留不影响正确性。
- `ResourceContainer.Clear()` 不卸载资产：既有契约，调用方先 `UnloadAllAssets` 再 `Clear`。
- 强类型 UI 打开参数与不可变窗体句柄：是改进不是修复，当前属于过度设计。
- 端到端性能预算（UI 打开阶段、资源驻留、GC、构建大小）：缺真机测量数据，等有数据再做。

## 审查封板

本轮修复完成后**不再发起新的静态审查**。重新审查的触发条件只有三个：出现崩溃/卡顿/用户反馈等运行时证据、大版本迭代前、或上游 merge 后涉及本目录代码。收敛判据是「P0 清零 + 关键路径有回归 + P1 有明确排期决定 + P2/P3 有书面记录」，不是「零问题」。
