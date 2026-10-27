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
