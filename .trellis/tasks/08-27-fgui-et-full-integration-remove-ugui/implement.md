# Implement — FGUI ET 完整接入并移除 UGUI 栈

## 有序实施清单（分阶段，每阶段可编译可回滚）

### 阶段 1：新增 FairyGUI 原生窗口层（不破坏现状）
1. [x] 调研 `GameFramework.UI.UIManager`：语义层经 `GameFrameworkEntry` 独立驱动，`IUIForm.Handle` 为 object。
2. [x] 新增 `FairyUIForm`、`FairyUIGroupHelper`、`FairyUIFormHelper`、`FairyUIManager`、`FairyUIFormPendingRegistry`（放 `Game`）。
3. [x] 桥接异步包加载：给 `FairyPackageManager` 增加 lease 版 `WaitForPendingAssetsAsync` 重载。
4. [x] 接线（新增 `FairyUIManagerSmokeTest`，`FairyUIManager.Initialize` 已幂等化）：初始化 `FairyUIManager` + `AddUIGroup`，写最小 `[AgentCallable]` 打开/关闭冒烟走新入口。

验证：`dotnet build Kit.sln` + Unity 编译（四个新类已编译通过）；接线后再做最小冒烟。

### 阶段 2：切换运行时入口 + 下沉基础设施
4. `FairyUIFormService` 改为调用 `FairyUIManager`；同时移除 `GameEntry.prefab` 上 `UIComponent` 的 UGUI 组注册（或跳过其 AddUIGroup），让 `FairyUIManager.AddUIGroup` 接管 Default/Pop 组。
5. 把 `IFairyUIPresenter` / `FairyUIPresenterAttribute` / 注册表 / 描述符解析 / 绑定类下沉到共享层（Game 或独立 FairyGUI 绑定程序集）。
6. 修复 CSName 强类型化（问题 2）。

验证：GameHot 模式冒烟测试（背包/多实例/置顶/覆盖/拖拽）通过，且不再碰 `GameEntry.UI`/`UIFormLogic`。

### 阶段 3：ET 侧接入
7. ET 启动流程注入 Presenter 扫描注册（扫 `Game.ET.Code.HotfixView`）。
8. ET `UIComponent` 改为经共享 `FairyUIManager` 打开界面；在 ET 侧完整复刻 4 个 Presenter（Demo/背包/详情/遮罩）到 `Game.ET.Code.HotfixView`。
9. 统一双份 `UI.xlsx`（问题 4）。

验证：切 ET 模式，`list_agent_methods` 能调 ET 验证方法，打开 FGUI 界面成功；编译 0 错误。

#### 2026-08-28 阶段 3 功能 owner 进展

- [x] `UIComponent` 持有 pending open 和 owned serial/CTS；HotfixView System 按固定 Destroy 顺序清理。
- [x] EntryEvent 先创建 owner，再经 owner API 打开 Demo；ET Flow/Presenter 不再直接无主打开/关闭。
- [x] ET 最终 generation 141 编译 0 error/0 warning；ET inventory/三实例/Destroy/pending/shutdown 冒烟通过。
- [x] 验证后恢复 `UNITY_GAMEHOT`，最终 generation 142 编译 0 error/0 warning。
- [x] Presenter 热更分层方案已批准并落地骨架（提交 `ca02b491`）：采用原 UGFUIForm/UGFSystemSingleton
      同构的 Entity/System dispatcher（design.md §8 决策），ET0004/CS1061 已消解，派发自检通过。
- [ ] 五个有状态 Presenter/Flow/Bootstrap 按骨架逐个迁移（状态进 Component、行为进 HotfixView System、
      打开链改 `FairyUIPresenterAdapter`），全部完成后删除 `UIComponentFairyUIBridge` 委托桥。
- [ ] ET LockStep、Widget parent destroy 与真实 Fiber Remove 验证。
- [ ] 接入复盘（HANDOFF §19）新增缺口：Widget 生命周期级联、GF 实例锁/优先级/事件透出、
      窗体 EventContainer/ResourceContainer 上下文。

### 阶段 4：删除 UGUI 栈 + 编排
10. 删除 Game.Hot 与 ET 的 UGUI 界面代码/prefab、`UnityGameFramework.Runtime.UI` 绑定层、旧宿主胶水（`FairyUIFormLogic/Host/UIGroupContainer/GDKUIFormHelper`）。
11. 删除 `AssetName` 的 prefab 校验分支（问题 3），新增一键编排脚本 `luban export → 描述符生成 → 资源刷新`（问题 5）。

验证：全量编译 + GameHot/ET 两模式冒烟测试；`rg` 确认无 `GameEntry.UI` / UGUI 界面残留；工具自测 `Test-FairyGUITools.ps1` 通过。

## 验证命令
- `dotnet build Kit.sln`
- Unity Bridge：`recompile` → `get_compile_result`；`search_logs type=error`
- GameHot 冒烟：`invoke_agent_method Game.Hot.Editor.FairyInventorySmokeTest::RunFairyInventorySmokeTest`
- ET 冒烟：切 `UNITY_ET` 后调用 ET 验证方法
- `python .agents/skills/gdk-development-workflow/scripts/validate_changes.py`
- `& Tools/FairyGUI/Test-FairyGUITools.ps1`

## 风险文件 / 回滚点
- `UnityGameFramework.Runtime.UI` 整个目录删除前，先确认 `Entity/Scene/Procedure` 等不引用 `GameEntry.UI`（依赖闭包检查）。
- `GameFramework.UI.UIManager` 的异步加载契约与 `IUIFormHelper` 同步契约的桥接是最高风险点，阶段 1 必须先验证。
- 删除 prefab 与界面代码要连同 `.meta`、`UI.xlsx` 行、`UIFormId` 常量一起原子化处理。

## 阶段 1 已确认的关键发现
- F1：IUIManager 是单例，SetObjectPoolManager 会无条件重建 "UI Instance Pool"，二次调用抛 "Already exist object pool"（被 TypeNamePair.ToString 的嵌套 ZString bug 掩盖为 NestedStringBuilderCreationException）。FairyUIManager.Initialize 已改为幂等（HasObjectPool 预检后仅首次建池）。
- F2：GameEntry.prefab 的 UIComponent 已在 PlayMode 注册 "Default"/"Pop" 等 GDKUIGroupHelper 组；FairyUIManager 需要同名 FairyUIGroupHelper 组，而 IUIManager 无 remove-group API。阶段 1 端到端冒烟因此被组名冲突阻塞，须在阶段 2 切换入口时同步移除/跳过 UIComponent 的 UGUI 组注册，或阶段 4 删除 UIComponent 后自然消除。
## task.py start 前检查
- [x] 热更边界已定：选 A，Presenter 热更，ET 侧完整复刻背包演示
- [x] design.md 的"待验证点"已通过阶段 1 调研消除或明确（design.md §8 已记录 F1/F2 与 dispatcher 决策）
- [x] 分支策略与提交拆分方案已定（分支 `fgui/et-owner-dispatcher`，按 HANDOFF §16 逻辑批次提交：
      `a306b572` P0-1 / `3de99143` P0-2 owner / `ca02b491` dispatcher 骨架）
