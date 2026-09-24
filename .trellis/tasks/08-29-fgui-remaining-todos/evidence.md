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
清理。Overlay pending 和 RuntimeInspector 的完整业务 OnClose 尚未分别验证。该提交当时尚未锁定
OnViewReady 完成、GF serial 尚未分配的窗口；后续精确时序证据见下一节。

这条证据不等同于 ET IL2CPP Player、100 次 ET child entity 计数矩阵或任意未来新 Component 的自动覆盖。

## 2026-09-24 ET serial 分配前 owner Destroy 精确竞态

在 `HEAD=df19728f` 的未提交测试切片上，通过实例级、仅 Editor 编译的单次打开钩子，稳定停在
业务 `OnViewReady` 派发完成且 GF 尚未分配 serial 的窗口：

- 最终双符号 Unity 编译 generation `15`：`errorCount=0`、`warningCount=0`；运行时发现的新方法
  `ET.FairyFiberLifecycleSmokeTest::RunFairyPreSerialOwnerDestroySmokeTest` 返回 `status=ok`。
- 钩子在 Unity 主线程同步执行；当时 Context/Form/Component serial 均为 `0`，owner 状态为
  `pending=1`、`owned=0`、`child=1`，业务按钮订阅和 Widget 已完成，而 `OnOpenCount=0`。
- 同步销毁 owner 后，Context lifetime 回调执行一次并观察到 `OnCloseCount=0`，随后业务
  `OnClose` 恰好执行一次；订阅、Widget 和 child 立即清理，打开任务最终以
  `OperationCanceledException` 结束。
- 回滚后 Component、Context、view、Widget、ET root component、GF loaded/loading、GRoot child、
  package diagnostics 和 `UIPackage.GetByName("Package1")` 的精确注册实例均回到测试基线；测试
  `finally` 恢复进入前的主 Demo 与运行时计数。
- 同一独占 Bridge 会话中，`ET.FairyInventorySmokeTest::RunFairyInventorySmokeTest` 和
  `ET.FairyFiberLifecycleSmokeTest::RunFairyFiberLifecycleSmokeTest` 均返回 `status=ok`；PlayMode
  的 `type=error, query=Fairy` 为 `matched=0`。全量错误仍命中已有 ET Server 端口占用/Router
  连带错误，不属于本 FairyGUI 切片。
- 退出 PlayMode 并清理上述既有日志后，`ET.FairyUIFormSkeletonSelfCheck::RunFairyUIFormSkeletonSelfCheck`
  返回 `status=ok`，Fairy 与全量 Error 查询均为 `matched=0`。
- 最后恢复默认符号，Unity 编译 generation `16` 为 0 error / 0 warning；`ProjectSettings` 无差异。

这条证据关闭了 OnViewReady 后、GF serial 前 owner Destroy 的精确竞态缺口；仍不等同于 Overlay
pending、RuntimeInspector 完整业务 OnClose、六类失败统一 100 次矩阵或 ET IL2CPP Player 证据。

## 2026-09-24 GameHot 打开失败与 Context 失效压力回归

在 `HEAD=93bef65d` 的测试切片上，将既有 GameHot 打开失败回归提升为逐类 100 次，并复用同一
运行时基线断言检查每次回滚：

- Unity 编译 generation `17`：`errorCount=0`、`warningCount=0`。
- 在 `Assets/FairyGUIDemo.unity` PlayMode 清理 24 条旧日志后，运行时重新发现的
  `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIOpenFailureCleanup` 返回 `status=ok`。
- `OnViewReady`、`OnOpen` 和 package Binder 准备失败各执行 100 次；每次都观察到原始失败，且
  loaded/loading UI、package diagnostics、GRoot child 数和 Package1 注册状态回到进入测试时的基线。
- 成功打开后关闭并访问已失效 Context 的路径执行 100 次；每次均确认 `IsAlive=false`，且
  `LifetimeToken` 立即抛出 `ObjectDisposedException`，关闭后运行时状态回到基线。
- PlayMode 内和停止 PlayMode 后的 Unity Console `type=error` 查询均为 `matched=0`。

这条证据补齐 GameHot `OnViewReady`、`OnOpen`、绑定准备失败和关闭后 Context 失效的逐类压力覆盖；
资源 descriptor 失败与生成绑定类型错配的 100 次证据见前文。owner cancel/Destroy 的 ET child
entity 统一 100 次矩阵仍未完成。

## 2026-09-24 ET owner 取消与销毁压力矩阵

在 `HEAD=60374b1a` 的独立测试切片上，新增 Editor-only
`ET.FairyFiberLifecycleSmokeTest::RunFairyOwnerLifecyclePressureSmokeTest`，未修改生产运行时 API：

- 双符号 `UNITY_ET + UNITY_GAMEHOT` 的最终 Unity 编译 generation `27` 为 `0 error / 0 warning`；
  恢复默认符号后的 generation `28` 同样为 `0 error / 0 warning`，`ProjectSettings` 无差异。
- 在 `Assets/FairyGUIDemoET.unity` 的稳定 PlayMode 会话中清空启动日志后，该 AgentCallable
  连续两次完整运行均返回 `status=ok`；最终证据轮覆盖 100 次 pending open owner Dispose，及
  100 次成功打开后的销毁，其中 owner Dispose 与真实 `FiberManager.Remove` 各 50 次。
- pending 路径逐次停在业务 `OnViewReady` 完成、GF serial 尚未分配的边界，断言 owner
  `pending=1 / owned=0 / child=1`，随后同步取消 lifetime、执行一次业务 `OnClose`，最终以
  `OperationCanceledException` 结束。
- 成功打开路径逐次断言 owner `pending=0 / owned=1 / child=1`、GF serial、Context、View 与
  Widget 完整建立；销毁后逐次确认 Component 已 disposed、`OnCloseCount=1`、Context API 失效、
  View disposed、Widget 回收，owner/fiber 与 GF loaded/loading 状态回到基线。
- 每次迭代都比较 GRoot child、`FairyInventoryFlow.OpenDetailCount`、Package1 精确 `UIPackage`
  实例，以及 package diagnostics 的 Name/Status/**Generation**/引用数/资源数/错误字段。
  测试为复用单实例 Demo 会主动关闭再恢复入口窗体，因此最终恢复原业务状态时允许这一次有意的
  package 卸载/重载，只要求注册状态、引用/资源计数和其他诊断字段恢复；迭代内部仍执行精确实例
  与 generation 门禁。
- 稳定证据轮后的 `type=error, query=Fairy` 为 `matched=0`。全量 Error 查询只命中仓库既有的
  `ET.Server.RouterComponentSystem.Update` 空引用（本机 Router 端口已被其他 Unity/ET 进程占用），
  不在本 FairyGUI 切片调用栈中。
- `Test-FairyGUITools.ps1` 为 `success=true, assertions=157`；GDK project、descriptor、manifest、
  localization 与 binder registry 五项只读校验全部通过，binder `changed=false`；任务上下文、
  变更守卫和 `git diff --check` 通过。

这条证据补齐 ET owner cancel/Destroy 的逐类 100 次 child entity 与资源基线覆盖。GameHot 的
OnViewReady、OnOpen、绑定和资源失败压力测试仍不直接创建 ET child，因此尚不构成六类失败在
同一个 ET 矩阵中的统一覆盖。

## 证据边界

本轮结果不能证明以下验收项：

- 六类失败统一执行 100 次后的 ET child entity 基线；GameHot OnViewReady、OnOpen、绑定准备失败、Context 关闭失效，以及包 descriptor 资源失败、生成绑定类型错配均已有逐类 100 次证据；ET owner cancel/Destroy 也已有逐类 100 次 child/entity 与资源基线，但前四类尚未在 ET Component 路径复跑；
- 真机旋转/安全区、输入矩阵和重复 add/remove；本轮已覆盖 Unity Editor 的四组分辨率矩阵；
- 并发多 package 或语言切换；当前仓库只有一个运行时 Package1；
- 场景重载、域/热更重载、重复 PlayMode 后旧 PlayerLoop/ResourceManager 引用清零；
- ET IL2CPP Player、真机安全区/输入、URP 色觉方案和性能 Profiler 基线；
- 版本检查/资源更新 UX 的产品决策。

这些项目继续保持未完成状态，不将本轮结果表述为全部验收通过。
