# 当前验证证据

本记录只收录验证基线 `6ed43a50` 的实际结果。历史 HANDOFF 或旧提交中的运行结果不自动继承为本任务证据。

## 2026-09-24 GameHot FairyGUI Editor Bridge

验证基线（执行以下 Agent Bridge 调用时）：

- Git `HEAD`: `6ed43a50` (`main`；验证开始时工作树干净，领先 `origin/main` 4 个提交)。
- Unity: `6000.3.22f1`。
- 场景: `Assets/FairyGUIDemo.unity`，通过已发现的 Agent Bridge 进入 PlayMode。
- Bridge `commandsVersion`: `58952b8569078412`。
- 执行前清理 Unity Console：清理 24 条旧日志。

已通过的 AgentCallable：

| 方法 | 当前证明范围 |
| --- | --- |
| `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIOpenFailureCleanup` | `OnViewReady`、`OnOpen`、绑定准备失败回滚，以及关闭后 Context 失效 |
| `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIMixedDepthAndSafeArea` | full-screen/safe-area 混合层级、refocus、非法/越界/NaN 安全区，以及关闭重开 |
| `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIFormLifecycleCycles` | 预取消、对象池旧 owner token、多实例 serial 隔离、后台线程取消和 100 次生命周期循环 |
| `Game.Editor.FairyGUIDemoAgent::ValidateFairyPackageHashContract` | manifest catalog 对空 bytes 的 SHA-256 接受和错误 bytes 拒绝 |
| `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIManagerShutdownReinitialize` | 关闭期间取消打开、原地 Shutdown/reinitialize、UIGroup/Stage/package 恢复 |

上述五个方法均返回 `status=ok`。停止 PlayMode 后再次检查 Unity Console，`type=error` 查询结果为 `matched=0`。

## 2026-09-24 资源失败回归切片

在同一 Unity/Bridge 环境中对本轮资源失败切片重新编译并验证：

- 编译 generation `76`：`errorCount=0`、`warningCount=0`。
- 先重启 `Assets/FairyGUIDemo.unity` PlayMode，使 domain reload 后的 FairyGUI 引导重新完成。
- `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIResourceFailureCleanup` 返回 `status=ok`。
- 该方法在包 descriptor 资源加载点注入 Editor-only 失败 seam，连续打开 `FairyDemoUIId` 100 次；每次均观察到原始资源失败异常，并核对 loaded/loading UI、package diagnostics、GRoot child 数和 Package1 注册状态回到未加载基线。
- 验证结束后恢复 loader override 并执行 `FairyPackageManager.Shutdown()`；Unity Console `type=error` 查询为 `matched=0`。

这条证据覆盖 **FairyGUI 包 descriptor 资源加载失败**，不等同于 UI descriptor 缺失、外部纹理失败或 binding type mismatch。

## 2026-09-24 生成绑定类型错配回归切片

在同一 Unity/Bridge 环境中对生成绑定错配切片重新编译并验证：

- 编译 generation `77`：`errorCount=0`、`warningCount=0`。
- 重启 `Assets/FairyGUIDemo.unity` PlayMode，并清理 Unity Console 的 24 条旧日志。
- `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIBindingTypeMismatchCleanup` 返回 `status=ok`。
- 该方法保留生成 `Package1Binder`，仅将 `UIDialog.URL` 临时映射到 `GComponent`，连续打开 Dialog 100 次；每次均观察到 `FairyGUI binding type mismatch`，并核对 loaded/loading UI、package diagnostics、GRoot child 数和 Package1 注册状态回到基线。
- `finally` 恢复原 `PreparePackage` 并重新执行生成 Binder；验证后的 Unity Console `type=error` 查询为 `matched=0`。

这条证据覆盖真实生成绑定类型错配回滚，不等同于 ET child entity 计数、外部纹理失败或 Player/设备验证。

## 证据边界

本轮结果不能证明以下验收项：

- 六类失败统一执行 100 次后的资源/ET child entity 基线；本轮已分别覆盖包 descriptor 资源加载失败和生成绑定类型错配；
- 旋转/分辨率矩阵和重复 add/remove；
- 并发多 package 或语言切换；当前仓库只有一个运行时 Package1；
- 场景重载、域/热更重载、重复 PlayMode 后旧 PlayerLoop/ResourceManager 引用清零；
- ET IL2CPP Player、真机安全区/输入、URP 色觉方案和性能 Profiler 基线；
- 版本检查/资源更新 UX 的产品决策。

这些项目继续保持未完成状态，不将本轮结果表述为全部验收通过。
