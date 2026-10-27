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

## 2026-09-24 FairyInputService 同帧重初始化

在 `HEAD=8eeb5129` 的独立运行时切片上，修复 UniTask PlayerLoop item 必须等下一次
`MoveNext=false` 才移除时，`Shutdown()` 后同帧 `Initialize()` 会让旧条目借用新
`m_Initialized=true` 状态继续轮询的问题：

- 每次 Initialize 现在创建独立 `PlayerLoopRegistration`；Shutdown 立即失活并摘除当前
  registration。旧条目即使下一帧才被 runner 调度，也只能返回 `false`，不会读取新会话状态。
- 重复 Initialize 保持幂等；`PlayerLoopHelper.AddAction` 抛错时会回滚 registration 和初始化状态。
  代次与 poll 计数仅在 `UNITY_EDITOR` 下提供给聚焦回归，不进入 Player 运行时接口。
- Unity 默认符号编译 generation `30`：`errorCount=0`、`warningCount=0`。
- 在 `Assets/FairyGUIDemo.unity` 稳定 PlayMode 中清理 24 条启动日志后，运行时重新发现的
  `Game.Editor.FairyInputLifecycleAgent::ValidateImmediateShutdownReinitialize` 返回 `status=ok`。
- 探针在同一帧执行 Shutdown→Initialize，确认 registration generation 恰好增加一次；再次
  Initialize 不改变 generation。排空旧条目后，连续三个完整 Update 各只增加一次 poll；再次
  Shutdown 后等待三个完整帧，poll 计数保持不变，并在 finally 恢复进入测试前的初始化状态。
- 探针后与停止 PlayMode 后的全量 `type=error` 查询均为 `matched=0`。
- `Test-FairyGUITools.ps1` 为 `success=true, assertions=157`；GDK project、descriptor、manifest、
  localization、binder registry 只读校验、任务上下文、变更守卫和 `git diff --check` 均通过。

这条证据关闭了同一运行时会话内输入 PlayerLoop 重复注册的确定性缺口；跨场景、domain reload、
禁用 domain reload 的重复 PlayMode，以及旧 ResourceManager/Stage/静态委托清零仍需独立矩阵。

## 2026-09-24 ET FairyGUI Bootstrap shutdown reset/reinitialize

在 `HEAD=6d988581` 的未提交代码切片上，给 ET Runner 完整关闭后的 ModelView 静态状态增加一次性
通知。Runner 在 `World.Dispose()` 和 `FairyUIManager.Shutdown()` 后通知；ET Bootstrap 只移除自己
占有的 `PreparePackage` 回调，并清理 Component factory、UI bridge 和初始化代次。

- 双符号 `UNITY_ET + UNITY_GAMEHOT` 编译 generation `41`：`errorCount=0`、`warningCount=0`。
- `Assets/FairyGUIDemoET.unity` PlayMode 中，`ET.FairyFiberLifecycleSmokeTest::RunFairyBootstrapShutdownReinitializeSmokeTest`
  返回 `status=ok`。探针先关闭已有 ET Demo，再执行 FairyUIManager shutdown 与 ET shutdown-completed 通知；
  断言 factory、ET-owned package callback、bridge delegates、table provider、GF loaded/loading forms 和
  package diagnostics 清空；随后连续 Initialize 两次，由新的 Fiber/UIComponent 打开并关闭 Demo form，
  再核对 owner、GRoot、package 和 GF 计数恢复到基线。`finally` 恢复进入测试前的主 Demo。
- 同一 PlayMode 的 Unity Console Fairy Error 查询为 `matched=0`（扫描 46 条日志）。
- 通过 `RestoreDefaultSymbols` 恢复客户端平台符号，再执行 `Game/Define Symbol/Refresh`；默认符号编译
  generation `42`：`errorCount=0`、`warningCount=0`，Unity PlayMode 已停止。
- 模式恢复后 Luban、资源规则和 `link.xml` 均回到 Git 基线，没有把模式切换副作用纳入提交。
- `validate_changes.py` 已通过，检查 6 个代码变更路径为 0 error / 0 warning。

探针直接调用 FairyUIManager shutdown 和完成通知来覆盖 ET reset/reinitialize 契约；它没有销毁实际 ET
Runner GameObject，因此 Unity 对生产 `Runner.OnDestroy` 接线的实际调用仍待单独验证。真实
`CodeLoader.ReloadAsync()` 后 HotfixView bridge 重绑、跨场景/domain reload 和禁用 domain reload 的重复
PlayMode 也未由此证据覆盖。

## 2026-10-27 打开事务收敛（收尾）

将打开事务从「GF 状态机 + 自造 pending 状态机」收敛为单一通道，用删除而非追加完成收尾。

- 基线 `f5749bb4` 上重构；Unity `6000.3.22f1`。
- `FairyOpenRequest` 经 GF `userData` 从 `OpenUIForm` 原样传到 `OnInit`、`OnOpen` 与结果事件，删除 `FairyUIFormPendingRegistry`（319 行）及其 4 个字典。
- 打开完成改为 `UniTaskCompletionSource` + `OpenUIFormSuccess/Failure` 事件，删除 `while(true)` 轮询、失败表与同步/异步双分支。
- `ThrowIfOpenInvalidated` 从 9 处收敛到 2 个边界；`OnInit` 失败由 `FairyUIForm` 自行释放，替代 `AdoptedForm` 跨对象兜底。
- 删除无调用者的 `FairyPackageManager.GetLastError` 与 `s_LastErrors`。

双符号 `UNITY_GAMEHOT;UNITY_ET` 与纯 `UNITY_GAMEHOT` 两次 Unity 编译均为 `0 error / 0 warning`；恢复默认符号后同样为 `0 error / 0 warning`，`ProjectSettings` 与 `link.xml` 无差异。

已通过的 AgentCallable（`Assets/FairyGUIDemo.unity`）：

| 方法 | 覆盖 |
| --- | --- |
| `ValidateFairyUIFormLifecycleCycles` | 预取消、池化 owner token、多实例 serial、后台线程取消、100 次循环 |
| `ValidateFairyUIOpenFailureCleanup` | OnViewReady / OnOpen / 绑定准备失败逐类 100 次，关闭后 Context 失效 |
| `ValidateFairyUIResourceFailureCleanup` | descriptor 资源加载失败 100 次回滚 |
| `ValidateFairyUIBindingTypeMismatchCleanup` | 生成绑定类型错配 100 次回滚 |
| `ValidateFairyUIMixedDepthAndSafeArea` | full-screen/safe-area 混合层级与非法安全区 |
| `ValidateFairyUIManagerShutdownReinitialize` | 关闭期间取消打开、原地 Shutdown/reinitialize |
| `Game.Hot.Editor.FairyUIManagerSmokeTest::RunFairyUIManagerSmokeTest` | GameHot 生产 Presenter 注册表 |

ET 链路（`Assets/FairyGUIDemoET.unity`）：

| 方法 | 覆盖 |
| --- | --- |
| `ET.FairyInventorySmokeTest::RunFairyInventorySmokeTest` | 同资源三实例、幂等关闭、pending 取消、owner Destroy |
| `ET.FairyFiberLifecycleSmokeTest::RunFairyFiberLifecycleSmokeTest` | 真实 `FiberManager.Remove` 后窗体/Widget/loaded form 回基线 |

`type=error, query=Fairy` 查询为 `matched=0`。全量错误仅命中仓库既有的 ET Server 端口占用（30300–30303）与 `RouterComponentSystem.Update` 空引用，与本次变更无关。

本轮观察：`ValidateFairyUIOpenFailureCleanup` 紧跟 `ValidateFairyUIFormLifecycleCycles` 执行时，会因 package diagnostics 残留失败；在干净 PlayMode 会话中通过。测试之间存在状态污染，属既有测试隔离问题，不在本轮修复范围。

未验证：ET IL2CPP Player、真机旋转/安全区/输入、URP 色觉、性能基线、domain reload 与重复 PlayMode 矩阵。这些已移交发布验证任务，不作为框架收尾条件。

## 2026-10-27 P0：错误分类与主线程断言

- 新增 `FairyUIException` + `FairyUIError`（17 个错误码）。FairyGUI 运行时目录 **80 处**抛出点全部改用带错误码的异常，`throw new GameFrameworkException` 归零。
- `FairyUIException.Message` 保留原始文案并加 `[ErrorCode]` 前缀，既有 `exception.Message.Contains(...)` / `ToString().Contains(...)` 断言不受影响。
- 新增 `[Conditional("UNITY_EDITOR")] AssertMainThread`：`FairyUIManager` 经 `GetRequiredUIManager([CallerMemberName])` 一次覆盖 30 个转发方法，另在 `Initialize` / `Shutdown` 显式断言；`FairyPackageManager` 在 `AcquireAsync` / `Shutdown` 断言。非 Editor 构建由编译器移除，零开销。
- 编译 0 error / 0 warning；`ValidateFairyUIFormLifecycleCycles`、`ValidateFairyUIOpenFailureCleanup`、`ET.FairyInventorySmokeTest` 全部通过，运行期 error 日志 0 条。

## 证据边界

框架收尾以 `prd.md` 的四条可观测契约为判定标准：打开成功、打开失败无残留、关闭后 Context 立即失效、连续 3 次仍成立。2026-10-27 轮次已覆盖 GameHot 与 ET 两条链路，并保留既有 100 次压力回归作为额外证据。

以下项目不再作为框架收尾的验收条件，已移交独立的发布验证任务：

- 真机旋转、安全区与输入矩阵；
- ET IL2CPP Player、domain reload、场景重载与重复 PlayMode 矩阵；
- URP 色觉方案与性能 Profiler 基线；
- 版本检查/资源更新 UX 的产品决策。

这些项目未执行，也不在本任务中标记为通过。
