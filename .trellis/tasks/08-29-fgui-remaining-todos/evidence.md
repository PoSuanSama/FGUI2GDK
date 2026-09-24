# 当前验证证据

本记录从验证基线 `6ed43a50` 开始收录实际结果；后续小节分别记录其执行基线。
历史 HANDOFF 或旧提交中的运行结果不自动继承为本任务证据。

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

## 2026-09-24 编辑器分辨率矩阵

使用运行时发现的 `set_game_view_resolution`，在同一 PlayMode 会话依次执行
`ValidateFairyUIMixedDepthAndSafeArea`，四组尺寸均返回 `status=ok`，且每组后错误日志均为
`matched=0`：

- `1280×720`；
- `1920×1080`；
- `1080×1920`；
- `800×1280`。

最后按 Bridge 返回的 restore token 恢复原始 `4K UHD (3840×2160)` Game View 选择。
该证据覆盖 Unity Editor 的横向/纵向分辨率变化，不替代真机安全区、旋转传感器和输入验证。

## 2026-09-24 ET owner Destroy 与 Fiber Remove

在 `HEAD=f41403ca` 的未提交修复上切换 Standalone 为 `UNITY_ET + UNITY_GAMEHOT`，使用
`Assets/FairyGUIDemoET.unity` 验证 ET Entity/System 生命周期：

- 修复前，`ET.FairyInventorySmokeTest::RunFairyInventorySmokeTest` 失败并报告
  `Destroying UIComponent left the owned FairyGUI demo serial open.`。定位到 ET `Entity.Dispose()`
  先递归销毁 child、后派发 owner `Destroy`，导致 `UIComponentSystem.Destroy` 关闭 GF host 时，
  per-open Component 已经 disposed，业务 `OnClose` 派发被跳过。
- 修复为每个已注册的具体 Fairy form Component 生成精确类型 `DestroySystem`。child 销毁时先回滚
  `OnViewReady` 已建立的订阅，再关闭已有 GF serial；`UIComponent.Destroy` 继续承担 pending CTS
  和 owned serial 的兜底清理。`FairyUIPresenterAdapter.OnOpen` 同时记录 Context 对应的 form。
- 双符号 Unity 编译 generation `4`：`errorCount=0`、`warningCount=0`。
- `ET.FairyInventorySmokeTest::RunFairyInventorySmokeTest` 返回 `status=ok`，覆盖同资源三实例、
  幂等 serial 关闭、pending open 取消、owner Destroy、Demo Widget 回收和 replacement owner 基线。
- `ET.FairyFiberLifecycleSmokeTest::RunFairyFiberLifecycleSmokeTest` 返回 `status=ok`，覆盖真实
  `FiberManager.Remove` 后窗体、Widget 与 loaded form 数回到基线。
- 停止 PlayMode 后，`ET.FairyUIFormSkeletonSelfCheck::RunFairyUIFormSkeletonSelfCheck` 返回
  `status=ok`；随后 `type=error` 查询为 `matched=0`。
- 提交前在新 Bridge session 再次切换双符号，编译 generation `1` 为 0 error / 0 warning；
  包含五种精确 Destroy System 断言的最新 Skeleton self-check 返回 `status=ok`，Fairy 错误日志为 0。
- 两条 PlayMode 回归后的 `type=error, query=Fairy` 查询均为 `matched=0`。全量错误查询仍会命中
  仓库既有的 `ET.Server.RouterComponentSystem.Update` 空引用，与本次 FairyGUI 变更无关。
- 最后调用 `RestoreDefaultSymbols`，默认符号重编译 generation `5`：`errorCount=0`、
  `warningCount=0`；提交前复核的默认符号 generation `2` 同样为 0 error / 0 warning，
  ProjectSettings 未留下差异。

Skeleton self-check 证明当前五种已注册 ET Fairy form Component 都有精确类型 Destroy System；运行时
owner Destroy 实际覆盖 Demo、ItemDetail 和 Fiber Demo，Inventory 流程另覆盖 pending/owned serial
清理。Overlay pending 和 RuntimeInspector 的完整业务 OnClose 尚未分别验证。本轮也没有用同步闩锁
锁定 OnViewReady 完成、GF serial 尚未分配的窗口并断言业务 OnClose 恰好一次。

这条证据不等同于 ET IL2CPP Player、100 次 ET child entity 计数矩阵或任意未来新 Component 的自动覆盖。

## 证据边界

本轮结果不能证明以下验收项：

- 六类失败统一执行 100 次后的资源/ET child entity 基线；本轮已分别覆盖包 descriptor 资源加载失败、生成绑定类型错配，以及单轮 ET owner Destroy/Fiber Remove；
- OnViewReady 完成、GF serial 尚未分配时 owner Destroy 的精确竞态与业务 OnClose 恰好一次；
- 真机旋转/安全区、输入矩阵和重复 add/remove；本轮已覆盖 Unity Editor 的四组分辨率矩阵；
- 并发多 package 或语言切换；当前仓库只有一个运行时 Package1；
- 场景重载、域/热更重载、重复 PlayMode 后旧 PlayerLoop/ResourceManager 引用清零；
- ET IL2CPP Player、真机安全区/输入、URP 色觉方案和性能 Profiler 基线；
- 版本检查/资源更新 UX 的产品决策。

这些项目继续保持未完成状态，不将本轮结果表述为全部验收通过。
