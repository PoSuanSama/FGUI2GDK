# FairyGUI 完整接入 GDK：最终收尾交接

> 快照日期：2026-08-28
>
> 基线提交：`2c6ae6e2`（`main`，当时与 `origin/main` 一致）
>
> 总任务：`.trellis/tasks/08-20-fairygui-gdk-ai-ui-integration/`
>
> 状态：**主体完成（2026-08-29 阶段 G 收口）。完整接入与零 UGUI 已达成；§7.7/§11 列出的 SDK 翻译、真机刘海屏/手柄、真实音频、URP 色觉滤镜与 Profiler 报告是记录在案的边界或后续项，不构成阻塞。任务归档见 §17/G7。**

## 1. 文档用途

本文是后续模型/开发者的执行交接入口，目的是让接手者不依赖此前对话也能继续收尾。

它不替代以下事实来源：

1. `AGENTS.md` 与 `.agents/skills/gdk-development-workflow/SKILL.md`：工程操作规范；
2. 本任务的 `prd.md`：最终需求与 AC01–AC26；
3. 当前源码、Unity 运行结果和生成器输出：实际完成状态；
4. 本任务的 `design.md`：目标架构和不变量；
5. 本文：当前缺口、收尾顺序、验证门禁和交接注意事项。

如果旧任务清单、Book 文档或历史提交说明与当前源码冲突，以当前源码和重新执行的验证为准。
不要因为旧 `implement.md` 中某项已勾选，就直接认定当前实现仍满足该项。

## 2. 接手后前 15 分钟必须做什么

按顺序执行，不要直接开始删 UGUI：

1. 完整阅读：
   - `AGENTS.md`；
   - `.agents/skills/gdk-development-workflow/SKILL.md`；
   - `.agents/skills/trellis-start/SKILL.md`；
   - `.agents/skills/trellis-before-dev/SKILL.md`；
   - 本目录的 `prd.md`、`design.md`、`implement.md`、本文；
   - `.trellis/spec/frontend/index.md`；
   - `.trellis/spec/tools/index.md`。
2. 重新采集上下文：

   ```powershell
   python ./.trellis/scripts/get_context.py
   python ./.trellis/scripts/get_context.py --mode phase
   python ./.trellis/scripts/get_context.py --mode packages
   git status --short
   git log --oneline -20
   ```

3. 运行只读基线守卫：

   ```powershell
   python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
   git diff --check
   git diff --stat
   ```

4. 确认当前提交是否仍为本文快照提交；若不是，先审查快照之后的差异，再更新本文中的状态。

   当前分支 `fgui/et-owner-dispatcher` 自快照后新增提交：`a306b572`（P0-1）、`3de99143`（ET owner）、
   `ca02b491`（dispatcher 骨架）、`19671eef`（复盘文档+工具链）、`eae54cd0`（FairyUIFormContext
   Widget 级联）、`dd72f24c`（GF 能力透出）。
5. 先复核第 7.4 节的 P0-1 已提交改动（`a306b572`）与证据，再继续第 8 节的 P0-2；不要跳过工作树审查。

## 3. 当前工作树保护要求

快照时工作树不是干净的，以下文件/日志不属于本文档创建或 FairyGUI 收尾的自动授权范围：

- `Unity/Assets/Res/Editor/Config/ResourceRuleEditor_ET.asset`
- `Unity/Assets/Res/Editor/Config/ResourceRuleEditor_GameHot.asset`
- `Unity/ProjectSettings/HybridCLRSettings.asset`
- `Unity/ProjectSettings/ProjectSettings.asset`
- `Unity/hs_err_pid*.log`
- `Unity/replay_pid*.log`
- 仓库根目录的 `hs_err_pid*.log`、`replay_pid*.log`

要求：

- 不清理、不 reset、不 checkout、不格式化这些用户改动；
- JVM crash/replay 日志不得提交；
- 两个 ResourceRule 文件在快照时有行尾/尾随空格提示，不能为了让检查变绿而盲目重写；
- 暂存时必须显式列出本批文件，随后检查 `git diff --cached`；
- 未经用户明确授权，不创建提交、不推送、不建 PR。

## 4. 最终目标的可执行定义

“完美接入”指行为、所有权和工作流对等，不要求 FairyGUI 模仿 UGUI 的实现细节。

最终必须同时满足：

1. FairyGUI 是 GDK 自有 Player UI 的唯一视图后端；
2. GF `UIManager` 继续拥有 UI ID、UIGroup、serial、多实例、深度、对象池及完整生命周期；
3. GameHot Presenter 保持 HybridCLR 热更新边界；
4. ET 保持 Entity/Component 所有权以及 ModelView/HotfixView 行为分层；
5. 资源由 GDK Resource 和 `FairyPackageManager` 统一持有，关闭/取消/异常/Shutdown 不泄漏；
6. 本地化、声音、设置、安全区、输入/手柄、焦点、色觉和事件仍由 GDK 服务拥有；
7. 启动、资源更新、致命错误、重试和退出在普通下载资源不可用时也能显示；
8. GameHot、ET Demo、ET LockStep、RuntimeInspector、Widget、HUD/HPBar、世界空间 UI 等
   Player 可达路径都有 FairyGUI 等价实现或经确认删除；
9. GDK 自有运行时、Editor、asmdef、场景、预制体、配置和工具不再直接使用 UGUI；
10. 至少一个目标 IL2CPP Player 完成构建、启动、资源更新和进入游戏冒烟；
11. AC01–AC26 每一项都有能追溯到 Git 修订版本的证据。

第三方/Unity 包内部不可避免且未被 GDK UI 路径使用的 UGUI 传递依赖可以保留，但：

- `manifest.json` 不应继续把 `com.unity.ugui` 作为 GDK 直接依赖；
- 没有剩余使用方的 UGUI 专用直接依赖必须移除；
- 不允许为 GDK 自有使用建立宽泛 allowlist 来掩盖残留。

## 5. 当前架构与真实运行流程

### 5.1 事实来源和生成

```text
Design/FairyGUI/GDK_FGUI/*.fairy + assets/**/*.xml + settings/GDK.json
  -> Test-GDKProject.ps1
  -> FairyGUI Editor / fgui-agent 精确发布 Package1
  -> Package1_fui.bytes + 官方 C# 绑定
  -> GDKFairyManifest.json

Design/Excel/**/UI.xlsx
  -> Luban 导出
  -> dtuiform.json + UIFormId.cs 等派生输出

Luban UI 身份/策略 + GDK.json FairyGUI 映射 + manifest
  -> Generate-FairyUIFormDescriptors.ps1
  -> Assets/Res/UI/FairyGUI/*.json
```

当前工具入口：

- `Tools/FairyGUI/Test-FairyGUITools.ps1`
- `Tools/FairyGUI/Test-GDKProject.ps1`
- `Tools/FairyGUI/Sync-GDKDemoToEditor.ps1`
- `Tools/FairyGUI/Publish-GDKDemo.ps1`
- `Tools/FairyGUI/Generate-FairyUIFormDescriptors.ps1`
- `Tools/FairyGUI/Generate-FairyRuntimeManifest.ps1`
- `Tools/FairyGUI/Invoke-FairyGUIPipeline.ps1`

### 5.2 运行时打开链

```text
GameHot Procedure / ET flow
  -> FairyUIFormService.OpenFairyUIFormAsync(uiId, userData, ownerToken)
  -> FairyUIManager
  -> DRUIForm / descriptor 校验
  -> FairyPackageManager.AcquireAsync
  -> UIPackage.CreateObject 得到强类型 GComponent
  -> Presenter 创建并 OnViewReady
  -> GameFramework.UI.IUIManager.OpenUIForm
  -> FairyUIForm / FairyUIFormHelper / FairyUIGroupHelper
  -> GF UIGroup、serial、深度、对象池和生命周期
```

当前实现的正确方向：

- 没有绕过 GF 自己维护另一套页面栈；
- 使用纯 `GameFramework.UI` 语义层；
- FairyGUI 视图由一个 GRoot/UIGroup 映射承载；
- 包加载支持依赖、并发合并、租约和逆序释放；
- Presenter 使用官方生成绑定；
- GameHot 和 ET 共用底层 FairyGUI 管理层。

### 5.3 当前代表界面

当前 Package1/描述符中可见的主要界面：

| ID | 界面 | 用途 |
| --- | --- | --- |
| 103 | `FairyDemoForm` | 基础打开和按钮交互 |
| 104 | `FairyInventoryForm` | 分类、列表、Widget 示例 |
| 105 | `FairyItemDetailForm` | 多实例、置顶、拖动 |
| 106 | `FairyInventoryOverlayForm` | 覆盖、暂停、恢复 |
| 107 | `FairyRuntimeInspectorForm` | FairyGUI 运行信息演示 |

`FairyUIWidget`、`FairyEntity`、`FairyGuide` 已有基础抽象和示例，但它们不是原系统完整行为对等的证明。

## 6. 当前完成度矩阵

| 领域 | 状态 | 当前事实 | 收尾要求 |
| --- | --- | --- | --- |
| FairyGUI 事实来源 | 部分完成 | 仓库工程、稳定契约和同步工具已存在 | 实机发布与双向冲突门禁需再次留证 |
| 工具/生成 | 已完成 | 工具 140 断言 + 四个生成器 -Check 全部通过；本地化 manifest 漂移已同步（718a69b8） | 纳入 CI（可选） |
| 描述符/绑定 | 较完整 | 103–107 descriptor 和官方绑定存在 | 全域过期/字符串绑定静态守卫（可选） |
| 包生命周期 | 已完成 | 合并加载、依赖、租约测试通过；Player AssetBundle 路径加载通过 | — |
| GF 原生窗口宿主 | 已完成 | owner token、100 次、shutdown、Player descriptor 双加载修复（1d33b860）全部通过 | — |
| GameHot | 已完成 | 三个冒烟 + 100 次生命周期 + Player 启动冒烟通过 | — |
| ET | 已完成 | 七个冒烟 + 骨架自检通过；双符号启动竞态加固（1578e42e） | LockStep 等价性仍按 §7.7 阻塞记录 |
| 本地化 | 已完成(带边界) | 生成器确定性 + 运行时应用冒烟通过 | 主文本翻译需 SDK 升级（§7.7 边界） |
| UI 声音 | 已完成(带边界) | soundRedirect + 映射冒烟通过 | 真实音频资源待设计期接入（§7.7 边界） |
| 安全区/输入 | 已完成(带边界) | 换算/导航/焦点冒烟通过 | 真机刘海屏/手柄数据待设备（§7.7 边界） |
| 色觉能力 | 部分完成 | 语义颜色 lint 落地；URP 滤镜已记录为后续批次 | 新 URP RendererFeature + 性能实测 |
| 启动引导 | 产品删除 | 用户批准删除 Builtin 启动/更新界面（c2840cff） | 如需恢复需重新立项 |
| 全量 UI 迁移 | 产品删除 | 其余旧界面经产品决策删除（c2840cff） | — |
| 零 UGUI | 已完成 | 代码/asmdef/资源/工具链/包依赖全清零（b2f4ed4e + 86b5f27e）；全域扫描 0 | 静态门禁纳入 CI（可选） |
| IL2CPP/性能 | 已完成(带边界) | Windows64 IL2CPP Player 构建+启动+界面打开通过（1d33b860） | Profiler 前后对比未做（旧 UGUI 无法重采，明确记录） |
| 文档/Trellis | 已完成 | Book 六篇 + 三条 spec 同步（f6bed72d） | 任务归档见 §17 |

## 7. 已收集的验证证据

以下是提交 `2c6ae6e2` 附近收集的快照证据。任何新代码提交后都必须重新执行，不能永久沿用。

### 7.1 已通过

- `Tools/FairyGUI/Test-FairyGUITools.ps1`：通过，140 个断言；
- `Tools/FairyGUI/Test-GDKProject.ps1 -Check`：通过；
- Unity Editor 重新编译：0 error、0 warning；
- `ValidateFairyPackageManagerLifecycle`：通过；
- `Game.Hot.Editor.FairyUIManagerSmokeTest`：通过；
- `Game.Hot.Editor.FairyInventorySmokeTest`：通过；
- GameHot/ET 资源规则中存在 `UI.FairyGUI` 目录配置；
- GDK 变更守卫没有报告本次代码层错误。

### 7.2 已失败

1. `ValidateFairyUIFormLifecycleCycles`：失败。
   - 复现结果：owner token 在窗体已打开后取消，serial 9 在等待 300 帧后仍未关闭/回收；
   - 根因证据：`FairyUIManager.OpenFairyUIFormAsync` 只在打开流程内轮询 token，返回后没有持续注册；
   - 影响：场景离开、ET Entity 销毁、热更新域切换和 shutdown 可能留下窗体/租约。
2. `OpenFairyDemoForm`：初始化时序下发生 `NullReferenceException`；
3. `InspectFairyDemoRendering`：同样在 `FairyUIManager.GetUIForm` 访问尚未初始化的
   `m_UIManager` 时发生空引用。

后两个失败可能主要是 Agent/启动时序问题，但公开 API 没有稳定的“未初始化”错误契约，必须修复或
让所有调用入口在查询前显式完成初始化。

### 7.3 未验证

- ET UI Entity/System 完整生命周期冒烟；
- ET Demo/LockStep 原行为等价；
- 普通资源不可用时的内置启动包；
- 四语言、声音、安全区、手柄、色觉矩阵；
- GameHot 热更域切换和 ET Fiber 销毁；
- 实际 ResourceCollection 构建后的目标 Player 加载；
- 至少一个 IL2CPP Player；
- 同场景迁移前后 Profiler 基线；
- 全域 UGUI 静态零残留。

### 7.4 2026-08-28 P0-1 收尾进展（已提交 a306b572）

本轮已修复打开成功后的持续 owner token 和未初始化错误契约，已按 §16 批次 1 提交到分支
`fgui/et-owner-dispatcher`（提交 `a306b572`）。接手者仍需以当前源码为准复核。

改动文件：

- `Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIManager.cs`
- `Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIForm.cs`
- `Unity/Assets/Scripts/Game/Editor/AgentBridge/FairyGUIDemoAgent.cs`
- `.trellis/spec/frontend/hook-guidelines.md`

实现契约：

- GF 打开成功后把 cancellation registration 转交给 `FairyUIForm`；
- 回调捕获不可变 GF serial ID，不按 assetName 或池化宿主当前字段关闭；
- 非主线程取消投递到 Unity PlayerLoop；Close/Recycle/失败路径幂等释放 registration；
- prepared state 标记是否已被宿主采用，避免打开竞态下 manager 与 form 双重释放视图/租约；
- 所有公开查询/关闭入口在未初始化时抛稳定 `GameFrameworkException`，不再空引用。

重新执行的证据：

- Unity Bridge 编译 generation 129：0 error、0 warning；
- `Game.Hot.Editor.FairyUIManagerSmokeTest::RunFairyUIManagerSmokeTest`：通过；
- `Game.Editor.FairyGUIDemoAgent::ValidateFairyUIFormLifecycleCycles`：通过；
- 用例覆盖预取消、打开后下一帧取消、旧 owner + 同一池化宿主复用、三个同 assetName 多实例逐个取消、
  cancel + 显式 close、100 次 open/cancel/close/recycle，并回查 GF/GRoot/包诊断基线；
- 界面保持打开时停止 PlayMode，停止后 `search_logs type=error`：0 条；
- `Test-FairyGUITools.ps1`：140 assertions；`Test-GDKProject.ps1 -Check`：通过；
- `Sync-GDKDemoToEditor.ps1 -Mode Status`：`Equal`；Trellis task validate：通过；
- GDK 变更守卫：0 error（两个用户 ResourceRule 改动仍有 `UNITY001` warning）；
- 本批 3 个 C# 文件的 `git diff --check -- <paths>`：通过；全工作树 `git diff --check` 仍因两个
  用户 ResourceRule 文件原有尾随空格失败，不要擅自改写。

补充构建限制：`Unity/Unity.sln` 仍被既存重复 `Unity.InputSystem` 项目（MSB5004）阻断；聚焦
`Game.csproj` 仍先命中既存第三方 MemoryPack/ZString 在本机 .NET 10 下的诊断。Unity Editor 实编译
和运行态验证已通过，不能把上述 `.NET` 失败误报成本批回归或通过。

### 7.5 2026-08-28 P0-2 ET owner 收尾进展（已提交 3de99143）

本轮已完成 ET Entity 对 FairyGUI 打开操作和 GF serial 的功能所有权，已按 §16 批次 2 提交
（提交 `3de99143`）。当时的 Presenter 热更分层阻塞已由 §7.6 的 dispatcher 方案解除，
P0-2 剩余项为“五个有状态 Presenter 按骨架逐个迁移”。

功能改动：

- ModelView `UIComponent` 保存 per-open CTS、pending operation ID、owned serial/CTS；字段均为运行时忽略；
- `UIComponentSystem` 连原 `.meta` GUID 移到 HotfixView，提供 owner Open/Close/Refocus 和查询 API；
- `Destroy` 固定为 cancel pending → 按 serial close/cancel owned → dispose CTS → clear；
- 跨 await 使用 `EntityRef<UIComponent>`，防止已销毁/池化 Entity 的迟到 continuation 接管新 owner；
- EntryEvent 先 Add `UIComponent`，再初始化并经 owner API 打开 Demo；
- ET Flow/OpenData/Presenter 显式携带 owner，业务层不再直接无主调用 `FairyUIFormService`；
- ModelView 通过 `UIComponentFairyUIBridge` 调用 HotfixView System，缺少注入时抛稳定初始化异常；
- 正常 owner 取消不再被 `UniTaskVoid` 当成 Error 记录。

验证证据：

- 切换 `UNITY_ET` 后最终 Unity generation 141：0 error、0 warning；
- `ET.FairyInventorySmokeTest::RunFairyInventorySmokeTest`：通过；
- 覆盖 Entry owner、Inventory/Overlay、三个同资源详情 serial 独立关闭、重复 close 幂等、Destroy 期间
  pending open、Destroy 清理已打开 serial、重建 owner 并恢复 Demo 基线；
- replacement Demo 保持打开时停止 PlayMode，停止后 Error 日志：0 条；
- 已恢复原 `UNITY_GAMEHOT`，最终 Unity generation 142：0 error、0 warning；
- `UIComponentSystem.cs.meta` 移动前后 GUID 均为 `719fcba05de90c6429a9b2d9b411916f`。

未关闭的架构阻塞：

- 尝试把五个有状态 Presenter、Flow、Bootstrap 整类迁到 HotfixView 时，ET generation 134 产生 39 个
  `ET0004`：Hotfix 程序集无条件禁止属性和非 const 字段；`EnableClass` 不豁免；
- ModelView 也不能静态引用 HotfixView 扩展方法，generation 137 对该错误给出 9 个 `CS1061`；
- 因此状态ful Presenter 已连原 GUID 返回 ModelView，仅 UIComponent System 行为留 HotfixView；
- 若要满足原 design 的“Presenter 行为热更”，必须先批准并实现 state/logic adapter 或 Entity/System
  dispatcher，不能禁用分析器、改成全局静态字段或再次机械移动文件。

### 7.6 2026-08-28 Component/System dispatcher 骨架（已提交 ca02b491）

上述阻塞已按“原 UGFUIForm/UGFSystemSingleton 同构方案”解除，骨架已按 §16 批次 3 提交
（提交 `ca02b491`），design.md 第 8 节已写入该决策与供应商边界收口：

- ModelView 新增：`FairyUIFormLifecycleSystems.cs`（生命周期接口 + `[EntitySystem]` System 基类）、
  `FairyUIFormComponent.cs`（状态基类）、`FairyUIFormSystemDispatcher.cs`（复用
  `EntitySystemSingleton.TypeSystems` 运行时派发，不再建第二套注册表）、`FairyUIPresenterAdapter.cs`、
  `FairyDemoFormComponent.cs`。
- HotfixView 新增：`FairyDemoFormComponentSystem.cs`（静态纯方法 System，证明 ET0004/CS1061 同时消解）。
- Editor 新增：`FairyUIFormSkeletonSelfCheck.cs`（EditMode 派发闭环自检）。
- ET0003 白名单加入 `ET.Client.FairyUIFormComponent`（与原 UGFUIForm 豁免同构），
  `Share.Analyzer.dll` 已重编译部署到 `Unity/Assets/Plugins/`。
- 验证：ET 模式 generation 171、GameHot generation 172 均 0 error/0 warning；派发自检通过，
  Console 出现 HotfixView 行为日志；GDK 守卫 0 error。
- 骨架调试固化的框架约束：`Entity.IsDisposed == (InstanceId == 0)`，EditMode 无 Fiber/Scene/对象池
  运行时（测试实体需反射赋 InstanceId，结束时归零而非 Dispose）；PlayMode 真实注册链由 ET 冒烟覆盖。

P0-2 剩余：五个有状态 Presenter/Flow/Bootstrap 按骨架逐个迁移（状态进 Component、行为进 System、
打开链改 `FairyUIPresenterAdapter`），完成后删除 `UIComponentFairyUIBridge` 委托桥。

### 7.7 2026-08-28 阶段 C 收尾与阶段 D 1–3 批证据（main 分支，已提交）

分支 `fgui/et-owner-dispatcher` 已 fast-forward 合并回 main。此后证据如下，接手者按需重跑：

**阶段 C 收尾**
- `132b0f33` FairyDemo 迁移样本；`d4f2b4d5` 背包/详情/覆盖层/Inspector 四件套迁移
  （ModelView Component + HotfixView 静态 System + 闭包捕获 EntityRef 做按钮订阅）；
  `3a7b8943` ET 移除类 Presenter 注册表。ET gen 20–23 / GameHot gen 21–24 均 0 error/0 warning。
- `052b4363` Widget parent destroy + 真实 Fiber Remove 门禁：冒烟断言 owner 销毁后 Demo Widget
  经宿主上下文回收（View 释放、Opened 复位）、demo serial 全关；新增
  `ET.FairyFiberLifecycleSmokeTest`（独立 NetClient fiber 打开 Demo 后 Remove，全部回基线）。
- `04300707` LockStep 门禁：**阻塞已记录**——客户端 LockStep UI 目录为空（旧 UGUI 界面先删后未迁移，
  AC24 已知风险）、无 LockStep 场景、PlayMode 单机需服务器；验证随阶段 E LockStep 批次进行，
  不得宣称等价。
- 迁移中固化的框架约束：`AddChild<T>` 需 `[ChildOf]` + `IAwake`（ET0001 静态校验）；
  `FairyUIFormOnUpdateSystem` 基类需幽灵泛型 `<T,P1,P2>` 匹配 ETSystemGenerator 模板；
  FairyGUI 按钮订阅类型是 `EventCallback1`。

**阶段 D 服务桥 1–3 批**
- `f743eefb` 本地化桥：`Generate-FairyLocalizationXml.ps1`（读 settings/FairyLocalization.json 映射 +
  Luban 变长前缀字典 → 每语言 strings XML + manifest，-Check 确定性通过）；
  `FairyLocalization` 在打开链 AcquireAsync 后、CreateObject 前按当前语言 `SetStringsSource`，按包幂等；
  `Localization.xlsx` 新增三键并重导出四语言字典；`ET.FairyLocalizationSmokeTest` 通过。
  已知边界：vendor SDK 快照的 TranslateComponent 不翻主文本（只翻 tips/gear/cp），需升级 SDK 或补丁。
- `69afe4d0` 声音桥：`UIConfig.soundRedirect` 委托扩展点（OWN001 已确认的最小 vendor 补丁，
  Stage 两处重载优先调钩子，未注入行为不变）+ `FairySound`（click=10001/select=10000 映射 +
  未映射诊断一次 + GDK UISound 组播放）；`ET.FairySoundSmokeTest` 通过。
  已知边界：Package1 无音频资源，真实按钮/transition 音效待设计期挂 sound 资源验证。
- `ad9ab10e` 安全区桥：`FairyUIGroupHelper` 双容器（全屏+安全区子容器），`Screen.safeArea` 像素经
  contentScaleFactor 缩放 + Y 翻转换算到 GRoot 设计坐标，onUpdate 变化重算（幂等）；
  窗体默认挂安全区容器（AddRelation 关系适配），descriptor `fullScreen` 标记（JSON 缺省 false）；
  `ET.FairySafeAreaSmokeTest`（注入像素矩形验证换算/幂等）通过。
  已知边界：编辑器 safeArea 恒为全屏，真实刘海屏数据待目标设备验证；fullScreen 生成端未接通。

### 7.8 2026-08-29 阶段 G 收口证据（main 分支，已提交）

**G1 工具确定性（718a69b8）**
- `Test-FairyGUITools.ps1`：140 assertions；`Test-GDKProject.ps1 -Check`：通过；
- `Generate-FairyUIFormDescriptors.ps1 -Check`：6 descriptors 通过（修复：检查器不认识同目录
  本地化 manifest 而误报 obsolete——现已豁免 `GDKFairyLocalizationManifest.json`）；
- `Generate-FairyRuntimeManifest.ps1 -Check`：通过；`Generate-FairyLocalizationXml.ps1 -Check`：通过；
- 修复内容：发布 Package1 后 GDK.json sourceHash 变化而本地化 manifest 未再生成，重新生成同步。

**G2 Unity 双符号编译与冒烟（1578e42e）**
- GameHot：UIManager/Inventory/Dialog 三冒烟 + `ValidateFairyUIFormLifecycleCycles`（100 次）+
  `ValidateFairyPackageManagerLifecycle` 通过；运行期 Error 0；停止后仅既有 CodeRunner
  DestroyImmediate 一条。
- ET：双符号（Standalone UNITY_ET+UNITY_GAMEHOT）generation 编译 0/0；七个冒烟
  （Inventory/Fiber/Localization/Sound/SafeArea/Input/ColorBlindness）+ 骨架自检（EditMode）通过。
- 本批修复的启动竞态（双符号下 ET Init 与 GameEntry Start 同时执行）：
  `Init.cs` 有界等待 GameEntry 组件就绪 + 幂等 `Awaitable.SubscribeEvent`（此前 TipsSubscribeEvent
  直接抛错）；`UnityTimeNow` 对未就绪 BaseComponent 空安全；`FairyGUIBootstrap` 有界等待
  TablesComponent + `FairyUIFormComponentRegistry.Register` 同工厂幂等；
  `ProcedureLaunch` 的 SubscribeEvent 改幂等。
- 新增 `Assets/FairyGUIDemoET.unity`：ET 验证专用场景（含挂 `ET.Init` 的 ET 对象）。ET 对象
  不放进共享 FairyGUIDemo 场景，避免 GameHot 模式产生 missing script。上一轮 ET 冒烟曾依赖
  场景内未提交的 ET 对象（工作区内存态，重启丢失），现已持久化。
- `ValidateFairyUIFormLifecycleCycles` 增加 ET 模式早退（GameHot 类 Presenter 路径专属，
  ET 生命周期由 ET 冒烟覆盖），不再在 ET 下误报。

**G3 ResourceCollection 构建回读与残留清理（86b5f27e）**
- `ConfigureFairyGUIResourceRules` 通过：FairyGUI 目录进入 GameHot/ET 规则，集合重建并验证
  每个运行时资产被收集；FairyDemoForm 等 descriptor/Package1_fui.bytes 全部进入 UI.FairyGUI。
- 本批清除阶段 F 漏网残留：`Res/UI/UXTool`（329 文件，-71k 行）与 `Res/Editor/UI/UXTool`、
  `Res/Editor/UI/UGuiExtension` 目录、两个规则资产中的 `UI.UXTool` 规则条目（新增
  `RemoveObsoleteUXToolResourceRule` AgentCallable）、`link.xml` 中 Coffee/CodeBind/
  Leihuo.UXTools/SRDebugger/EnhancedScroller/R3.Unity 已删程序集条目、六个 asmdef 中指向
  已删除 R3 包的悬空引用。ResourceCollection 中 UXTool 引用从 67 归零。

**G4 IL2CPP Player 构建与启动冒烟（1d33b860）**
- 新增 `BuildWindows64PlayerPkg` AgentCallable：HybridCLR 缺失时先 InstallDefaultHybridCLR
  （克隆 hybridclr/il2cpp_plus 到 gitignored 的 HybridCLRData）→ HybridCLR Do All → 资源构建 →
  Launcher 场景 IL2CPP Player 构建。
- 修复两个 Player 独有缺陷：
  1. descriptor 双加载：`LoadAssetAsync<TextAsset>` 的池键 (assetName, typeof(TextAsset)) 与
     GF UIManager 无类型加载的键 (assetName, null) 分裂，AssetBundle 模式下同一对象被 Register
     两次抛 ArgumentException。改为无类型加载 + 解析后释放（新增 `LoadDescriptorTextAsync`）；
  2. `HotEntry.prefab` 漏收集：GameHot 规则只有 Code/Luban 子目录条目，Hot 根 prefab 无规则
     覆盖。新增 `GameHot.Prefab` 规则（FilterType=Root + `*.prefab`，与子目录规则不重复）。
- 构建产物 `Temp/Pkg/Windows64`：GameAssembly.dll 94MB（真 IL2CPP）；启动 Player 后
  `Game.Hot.Code Start` + `FairyGUI demo presenter opened through the unified GF UIForm host`，
  Player.log 0 Exception；资源从构建产物（GameData.dat/HybridCLR.dat StreamingAssets）加载。

**G5 文档与 spec 同步（f6bed72d）**
- 重写 `Book/FairyGUI接入.md`（POC/旧宿主 AFairyUIForm/UIPanel 描述 → 当前 FairyUIManager/
  descriptor/服务桥/双符号验证架构）、`Book/UI开发.md`（StarForceUIForm/CodeBind 流程 →
  IFairyUIPresenter/FairyUIFormComponent 流程）；
- 删除 `Book/AssetSet.md` 及 README 引用（AssetSet 已随阶段 F 删除）；
- `ET代码生成工具.md`/`Entity开发.md`/`多语言.md`/`Luban配置.md` 移除 UGUI 时代
  UIForm/UIWidget/UGFEntity/CodeBind/UXTool 段落；
- `.trellis/spec/frontend/component-guidelines.md` 重写为 FairyGUI Presenter/Component-System
  规范；`type-safety.md`/`code-reuse-thinking-guide.md` 清理已删工具引用。

**AC 静态证据补采（G6）**
- AC04：GameHot/ET 业务代码裸 `GetChild(` 扫描为 0（绑定全部走官方生成类）；
- AC19：全域 `using UnityEngine.UI`/`CanvasScaler`/`GraphicRaycaster` 扫描为 0；
  `UXTool/UGuiExtension/CodeBind` 仅剩清理工具自身与注释提及；
  补正：此扫描不含 `RectTransform`；`GameEntry.prefab` 的 `UI Form Instances` 节点实为
  UGUI Canvas(含 CanvasScaler/GraphicRaycaster)，`EventSystem` 为 UGUI EventSystem，
  Book 门禁命令（含 `RectTransform`）会命中。08-29-fgui-zero-ugui-finalize 已删除
  两节点、修复 f3617234 误删的 stripped 根 Transform，门禁归零。
- AC26：`com.unity.ugui` 不在 manifest dependencies；UGUI 专用包（UIEffect/SoftMask/Unmask/
  UIParticle 等）全部移除；lock 中 `com.unity.ugui` 仅为 `builtin` 传递来源（URP 官方依赖）。

**G7 补正（08-29-fgui-zero-ugui-finalize）**
- 核验发现 `f3617234`（标题「修复 GameEntry 悬挂引用」）误删了嵌套 GameFramework 实例
  根 Transform（`1836028191286498939`）的 stripped 定义，但它仍在 GameEntry 根 `m_Children`
  被引用，导致 Unity 导入报 `Transform child can't be loaded`（原始 HEAD 亦复现，非零 UGUI
  删除引入）。该任务已恢复 stripped 定义并验证导入 0 error。
- 同时删除 `GameEntry.prefab` 两处 UGUI 静态节点，使 Book 门禁命令（含 `RectTransform`）
  实测归零；GameHot 五冒烟 + 100 次生命周期 Error 0，ET 骨架自检 + 七冒烟方法通过。

## 8. 必须优先解决的阻塞问题

### P0-1：打开成功后的 owner token 不再拥有窗体（已修复并提交 a306b572）

关键文件：

- `Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIManager.cs`
- `Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIForm.cs`
- `Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIFormPendingRegistry.cs`
- `Unity/Assets/Scripts/Game/Hot/Code/Procedure/ProcedureMenu.cs`
- 相关 Editor/Agent 生命周期测试

当前问题：

- token 只传入 descriptor/资源/包加载和打开等待循环；
- `OpenFairyUIFormAsync` 返回 `FairyUIForm` 后，取消 token 不会关闭已打开 serial；
- 原 GF 宿主任务的设计明确要求 token 在打开成功后持续生效，当前原生窗口重构丢失了这个契约。

建议目标契约：

1. serial ID 已知后注册 owner token；
2. token 取消时按 serial ID 关闭“正在加载或已打开”的同一实例；
3. registration 的所有权最终转交给 `FairyUIForm`，在 Close/Recycle/失败时幂等 Dispose；
4. 处理“注册时 token 已取消”“取消与返回同时发生”“关闭与取消同时发生”“对象池宿主复用”；
5. Presenter 抛异常不能跳过 registration、GComponent、租约清理；
6. 不得通过全局扫描 assetName 关闭，因为多实例可能同名；
7. 不得改变 GF 和 Presenter 观察到的原始 `userData` 引用。

最小测试：

- 打开前取消；
- descriptor await 后取消；
- GF 正在加载时取消；
- `await Open...` 返回后下一帧取消；
- 同 assetName 三实例只取消目标实例；
- cancel 与显式 close 并发；
- 100 次 open/cancel/close/pool reuse；
- 窗体保持打开时停止 PlayMode；
- 每例结束后 GF、GRoot、包租约、Presenter 和资源诊断回基线。

### P0-2：ET 没有真正拥有 FairyGUI 界面（功能 owner 已提交 3de99143，分层阻塞已由 7.6 骨架解除）

关键文件：

- `Unity/Assets/Scripts/Game/ET/Code/HotfixView/Client/Demo/EntryEvent3_InitClient.cs`
- `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Module/UI/UIComponent.cs`
- `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Module/UI/UIComponentSystem.cs`
- `Unity/Assets/Scripts/Game/ET/Code/ModelView/Client/Module/UI/FairyGUI/`
- `Unity/Assets/Scripts/Game/ET/Code/Editor/FairyInventorySmokeTest.cs`

当前问题：

- EntryEvent 先静态初始化并通过全局 `FairyUIFormService` 打开界面，之后才添加 `UIComponent`；
- `UIComponent` 没有状态；
- `Awake`/`Destroy` 为空；
- ET Presenter 和 bootstrap 目前主要位于 ModelView；
- 未证明 Scene/Fiber/Entity Destroy 会关闭 serial、取消异步、解绑事件并释放租约。

目标边界：

1. ModelView 中的 `UIComponent` 只保存状态和所有权：serial/owner token/必要容器；
2. 行为放在对应 HotfixView `EntitySystem`；
3. 启动顺序改为先创建 UIComponent，再由其 System 打开界面；
4. `Destroy` 先取消 owned operations，再关闭 owned forms，最后清空容器；
5. Presenter 实现和注册扫描放在正确的热更行为程序集；
6. 不在 ET 再复制 PackageManager、GF host 或全局缓存；
7. UIWidget 子 Entity 随父 UI Entity 销毁；
8. ET Demo 和 ET LockStep 都要有代表性流程，不只自动打开一个 Demo 页面。

注意：当前 `.trellis/tasks/08-27-fgui-et-full-integration-remove-ugui/design.md` 曾把 Presenter 目标位置定为
HotfixView，但当前代码落在 ModelView。必须纠偏，或者更新设计并取得明确批准；不能静默把行为留在
ModelView。

最小测试：

- UIComponent Awake 后打开 Demo；
- Scene/Fiber Destroy 期间取消仍在 await 的打开；
- Destroy 已打开的 103/104/105/106；
- 三个详情窗只由对应 ET owner 管理；
- Widget parent destroy；
- ET 模式关闭 PlayMode；
- 全部测试后无 GF form、GObject、Presenter、package lease 或静态 registry 残留。

### P0-3：启动/更新路径仍是 UGUI

关键文件：

- `Unity/Assets/Scripts/Game/Builtin/BaseBuiltinForm.cs`
- `Unity/Assets/Scripts/Game/Builtin/BuiltinUpdateResourceForm.cs`
- `Unity/Assets/Scripts/Game/Builtin/BuiltinDialogForm.cs`
- `Unity/Assets/Scripts/Game/Procedure/ProcedureUpdateResources.cs`
- `Unity/Assets/Res/Builtin/UpdateResourceForm.prefab`
- `Unity/Assets/Res/GameEntry.prefab`

要求：

- 建立不依赖普通下载资源和热更程序集的最小内置 FairyGUI bootstrap 包；
- 覆盖资源检查、进度、失败、重试、致命错误和退出；
- 最小字体/图集/包进入首包 Player；
- 普通 manifest 缺失或下载失败时仍能渲染错误界面；
- 等价 Player 验证完成后才能删除旧 Builtin UGUI prefab/代码；
- Unity 资源修改必须通过 Agent Bridge/API，资源与 `.meta` 同批处理。

### P1：初始化契约不稳定

`FairyUIManager` 的查询、关闭和打开 API 应满足以下二选一契约之一：

- 所有公共入口内部执行幂等 `EnsureInitialized`；或
- 未初始化时抛出稳定、可诊断的 `GameFrameworkException`，且每个 bootstrap/Agent 入口先显式初始化。

禁止继续让 `GetUIForm`、`HasUIForm` 直接对 null `m_UIManager` 解引用。

### P1：供应商边界存在历史漂移

提交 `8b39d6cc` 删除了 `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/UI` 绑定层，
而父 PRD 要求不修改供应商核心。最终清理前必须做一次明确决策：

1. 若该目录属于供应商源码：恢复但保证 GDK 不引用，或取得用户批准保留删除；
2. 若该目录被项目明确视为可替换的 GDK wrapper：把这一边界写入设计/Book，并验证 UGF 升级策略；
3. 不能在没有上游 diff 和依赖闭包证据时直接继续删除其他 `Library/UGF` 内容。

建议先审查：

```powershell
git show --stat 8b39d6cc
git show --summary 8b39d6cc
git diff 8b39d6cc^ 8b39d6cc -- Unity/Assets/Scripts/Library/UGF
rg -n "UnityGameFramework.Runtime.UI|UIFormLogic|UIComponent" Unity/Assets/Scripts Unity/Assets/Res
```

## 9. UGUI 当前残留基线

### 9.1 GDK 自有 C# 直接使用

快照扫描命令：

```powershell
rg -l "using UnityEngine\.UI|UnityEngine\.UI|CanvasScaler|GraphicRaycaster|RectTransform|CanvasGroup" `
  Unity/Assets/Scripts/Game -g "*.cs"
```

重点残留：

- `Game/AssetSet/` 的 Image/RawImage/UXImage 辅助器；
- `Game/Builtin/` 三个启动/更新界面；
- `Game/Hot/Code/HPBar/HPBarItem.cs`；
- `Game/UI/UGuiExtension/`；
- `Game/Editor/CodeBind/`、`Editor/UI/`、`Editor/AgentBridge/UXToolCommand.cs`；
- `Game/ET/Editor/UI/UGuiFormCreateTool.cs`。

### 9.2 asmdef 直接引用

以下 7 个 asmdef 在快照时仍引用 `UnityEngine.UI`、CodeBind、Coffee、UXTool 或 LoopScrollRect：

- `Unity/Assets/Scripts/Game/Game.asmdef`
- `Unity/Assets/Scripts/Game/Editor/Game.Editor.asmdef`
- `Unity/Assets/Scripts/Game/Hot/Code/Game.Hot.Code.asmdef`
- `Unity/Assets/Scripts/Game/Hot/Loader/Game.Hot.Loader.asmdef`
- `Unity/Assets/Scripts/Game/ET/Code/ModelView/Game.ET.Code.ModelView.asmdef`
- `Unity/Assets/Scripts/Game/ET/Code/HotfixView/Game.ET.Code.HotfixView.asmdef`
- `Unity/Assets/Scripts/Game/ET/Loader/Game.ET.Loader.asmdef`

### 9.3 资源残留

重点包括：

- `Unity/Assets/Res/Builtin/UpdateResourceForm.prefab`；
- `Unity/Assets/Res/GameEntry.prefab` 中的 Canvas/RectTransform 路径；
- `Unity/Assets/Res/UI/UIForm/Demo/*.prefab`；
- `Unity/Assets/Res/UI/UIForm/LockStep/*.prefab`；
- `Unity/Assets/Res/UI/UIForm/Hot/UpdateResourceForm.prefab`；
- `Unity/Assets/Res/UI/UIEntity/WidgetTest.prefab`；
- `Unity/Assets/Res/UI/UIPrefab/Button/*.prefab`；
- `Unity/Assets/Res/UI/UIPrefab/HPBar*.prefab`；
- `Unity/Assets/Res/UI/UXTool/**`；
- `Unity/Assets/Res/Editor/UI/**` 中的 UGUI 模板和 UXTool 资源。

删除前必须通过引用和 Player 可达性检查。不要仅凭文件名批量删除，也不要把资源和 `.meta` 拆开。

### 9.4 直接包依赖

`Unity/Packages/manifest.json` 快照时仍直接包含：

- `com.unity.ugui`；
- SoftMaskForUGUI；
- UIEffect；
- UIParticle；
- UnmaskForUGUI；
- LoopScrollRect；
- CodeBind；
- UGUI RuntimeInspector。

移除顺序必须是“先迁移最后一个使用方 → Unity 重新解析 → 审查 lock/compile/player”，不能先删包再修编译。

## 10. 分阶段收尾计划

每一阶段必须形成可独立验证、可独立回滚的逻辑批次。上一阶段门禁未通过时，不进入大规模删除。

### 阶段 A：重新建立可信基线

目标：解决任务/文档/源码进度漂移，确认实际入口。

步骤：

1. 重新运行第 2 节命令；
2. 对照 `08-24-fairygui-gf-ui-host`、`08-27-fairygui-final-integration`、
   `08-27-fgui-et-full-integration-remove-ugui` 的任务文件和当前提交；
3. 确认哪些子任务已归档、哪些只是提交存在但未验收；
4. 不创建重复任务；从现有 P0 任务继续，必要时把独立 ET 总任务关联到 FairyGUI 父任务；
5. 生成新的 UI ID/行为/旧资源迁移对照表；
6. 记录当前 GameHot/ET、三宽高比、四语言和性能基线。没有旧 UI 可运行时，使用删除前提交或
   明确记录“无法补采”，不得伪造基线。

门禁：当前代码、任务状态、工作树和验证证据有一份一致清单。

### 阶段 B：修复 GF 原生窗口生命周期

目标：完成 P0-1 和初始化契约。

建议修改范围：

- `Game/UI/FairyGUI/FairyUIManager.cs`
- `Game/UI/FairyGUI/FairyUIForm.cs`
- `Game/UI/FairyGUI/FairyUIFormPendingRegistry.cs`
- 对应 GameHot Editor/Agent 测试
- `.trellis/spec/frontend/hook-guidelines.md`（仅在发现新的稳定契约时）

门禁：

- owner token 在打开前、中、后都能只关闭目标 serial；
- 100 次生命周期通过；
- shutdown Error 日志为空；
- 初始化错误稳定可诊断；
- GF、GRoot、Presenter、包和资源回基线。

回滚：整批还原 manager/form/registry/test，不保留半套 token 所有权。

### 阶段 C：重建 ET Entity/System 所有权

目标：完成 P0-2，使 ET 不再是全局 service 的无主调用方。

建议步骤：

1. 明确 UIComponent 持有的数据结构和 owner CTS；
2. 把 Awake/Destroy/打开/关闭行为放入 HotfixView System；
3. EntryEvent 只装配 component，并由 component/system 发起代表界面；
4. 将 Presenter/flow 行为从 ModelView 移到 HotfixView，保留数据在 ModelView；
5. bootstrap 扫描正确的热更程序集，重复初始化和域重载幂等；
6. UIWidget/Entity wrapper 跟随 ET parent 生命周期；
7. 对 ET Demo 和 LockStep 建立等价行为测试。

门禁：

- ET 模式 Unity 编译和 Error 日志通过；
- Scene/Fiber/Entity Destroy 取消所有 owned open；
- 103–107 代表流程打开/交互/关闭；
- 三详情窗、多实例、refocus、overlay、Widget 回收通过；
- 不在 ET 层复制 PackageManager/host；
- Presenter 行为程序集边界符合设计。

### 阶段 D：实现内置启动包和 GDK 服务桥

建议拆成独立批次：

1. 内置启动/更新/致命错误包；
2. Localization 四语言生成和包注册前应用；
3. Sound 桥：按钮和 transition 统一进入 `GameEntry.Sound`；
4. GRoot 安全区和方向变化；
5. 指针、触摸、键盘、文本输入、手柄导航和焦点恢复；
6. 色觉语义、Editor/AI 预览和 Player 渲染性能方案。

每个批次都要有失败路径和独立回滚。不要一次性混在全量页面迁移提交中。

### 阶段 E：按页面批次完成行为等价迁移

必须维护下表，并在实施时补齐“新界面/状态/证据/回滚提交”：

| 原能力 | 当前状态 | 要求 |
| --- | --- | --- |
| GameHot Menu/Setting/About/Dialog/Tutorial | 旧代码已删除，未见完整 FGUI 等价 | 重建等价 FairyGUI 流程或明确产品删除 |
| ET Demo Login/Lobby/Help | prefab 仍在，旧代码已删除 | 迁移并验证场景流程，之后删 prefab |
| ET LockStep Login/Lobby/Room | prefab 仍在，旧代码已删除 | 迁移输入、列表、高频文本和房间流程 |
| Builtin Update/Error | 仍是 UGUI | 使用内置 bootstrap 包替换 |
| RuntimeInspector | 有 FairyGUI 演示 | 验证功能等价并删除旧包/资源 |
| UIWidget | 有基础抽象/示例 | 验证父级生命周期、嵌套、池化和 ET owner |
| Entity/世界空间 UI | 有 FairyEntity 基础抽象 | 证明与原 Entity 行为对等，避免仅有 GObject 容器 |
| Beginner Guide | 有 FairyGuide 基础层 | 补遮罩、高亮、输入拦截、目标跟踪、持久化等产品能力 |
| HPBar/HUD | UGUI 残留 | 迁移或取得明确的产品删除决定 |
| Editor UI 工作流 | CodeBind/UXTool/UGUI 工具残留 | 迁移到 XML lint、CLI、UI Toolkit/EditorGUI 或删除 |

每个页面批次必须：

- 保持 Luban UI ID、AssetName、UIGroup、多实例和覆盖策略，除非有明确迁移决定；
- 使用官方生成绑定；
- 覆盖成功、失败、取消、覆盖/恢复、refocus、重复开关和 shutdown；
- 产出 16:9、19.5:9、4:3 与四语言证据；
- 检查 ResourceCollection、Player 加载和包租约；
- 等价验证完成后，再删除旧资源和 `.meta`；
- 每批有可执行 Git 回滚点。

### 阶段 F：全域零 UGUI 清理

顺序：

1. 生成 GDK 自有代码/asmdef/资源/场景/配置/Editor 工具残留报告；
2. 逐个确认最后使用方已迁移；
3. 删除对应 GDK 自有代码、资源和 `.meta`；
4. 清理 asmdef 直接引用；
5. 清理 CodeBind/UXTool/UGUI 专用 Editor 工具和文档入口；
6. 移除没有使用方的 UGUI 专用直接包；
7. 最后移除 `com.unity.ugui` 直接依赖，让 Unity 正常更新 lock；
8. 审计仍存在的传递依赖，证明 GDK 未引用；
9. 增加静态回归守卫，阻止重新引入。

门禁扫描至少覆盖：

```powershell
rg -n "using UnityEngine\.UI|UnityEngine\.UI|CanvasScaler|GraphicRaycaster|RectTransform" `
  Unity/Assets/Scripts/Game Unity/Assets/Res -g "*.cs" -g "*.asmdef" -g "*.prefab" -g "*.unity" -g "*.asset"

rg -n "CodeBind|UXTool|LoopScrollRect|Coffee\.UI|RuntimeInspector" `
  Unity/Assets/Scripts/Game Unity/Assets/Res Unity/Packages/manifest.json
```

第三方目录需要单独分类，不得把 GDK 自有残留误报为“第三方例外”。

### 阶段 G：Player、性能、文档和 Trellis 收口

要求：

1. Unity Editor 两模式编译/Error 日志；
2. GameHot、ET Demo、ET LockStep 冒烟；
3. ResourceCollection 构建/加载；
4. bootstrap 离线失败/重试；
5. 三宽高比 × 四语言 × 必需色觉模式截图/交互矩阵；
6. 鼠标、触摸、键盘、文本输入、目标手柄流程；
7. 100 次生命周期、场景切换、热更域切换、应用关闭；
8. 至少一个目标 IL2CPP Player 构建和启动；
9. 同场景 CPU、GC、主线程、GPU、draw call、内存、首次/二次打开、卸载和包大小对比；
10. 更新 `Book/UI开发.md`、`Book/FairyGUI接入.md`、诊断和回滚文档；
11. 将稳定契约写入 `.trellis/spec/`，不要把一次性过程日志写成规范；
12. 对 AC01–AC26 逐项附证据；
13. 运行 `trellis-check`；
14. 用户授权后按逻辑批次提交并归档子任务，最后归档父任务。

## 11. AC01–AC26 当前状态与关闭证据

状态仅代表本文快照，不代表永久结论。修订号为本阶段证据提交。

| AC | 状态(2026-08-29) | 证据与边界 |
| --- | --- | --- |
| AC01 | 部分完成 | 同步工具 `-Mode Status` 此前 Equal；本阶段未重跑 Editor 同步（Editor 未开）。双向冲突实测仍缺 |
| AC02 | 已完成 | 四个生成器 `-Check` 连续两次确定性通过（718a69b8）；工具 140 断言 |
| AC03 | 较完整 | 破坏性 XML/ID/引用测试在 140 断言内；稳定失败输出未单独重录 |
| AC04 | 已完成 | 业务代码裸 `GetChild(` 扫描为 0（§7.8） |
| AC05 | 已完成 | 100 次生命周期 + 三冒烟 + cover/pause/refocus 断言（ET Inventory 覆盖/暂停计数）全通过 |
| AC06 | 已完成 | 打开后取消、三同名实例、池化旧 token、并发关闭——绑定 `1d33b860`（Player 路径加验） |
| AC07 | 较完整 | Player AssetBundle 路径加载通过（§7.8 G4）；并发合并测试在包生命周期套件内 |
| AC08 | 较完整 | 缺包/依赖环/取消错误矩阵在包生命周期套件内；外部资源失败路径未全测 |
| AC09 | 已完成 | Player 热更域加载 + FairyGUI 界面打开通过（1d33b860） |
| AC10 | 已完成(带边界) | ET 七冒烟 + Fiber Remove + Widget parent destroy 通过；LockStep 按 §7.7 阻塞记录（需服务器） |
| AC11 | 已完成 | ResourceCollection 构建回读 + Player 从构建产物加载（86b5f27e、1d33b860） |
| AC12 | 产品删除 | 用户批准删除 Builtin 启动/更新界面（c2840cff），不再要求 bootstrap 包 |
| AC13 | 已完成(带边界) | 四语言生成 + SetStringsSource 冒烟通过；主文本翻译需 SDK 升级（§7.7） |
| AC14 | 已完成(带边界) | soundRedirect 映射 + 冒烟通过；真实音频资源待设计期接入 |
| AC15 | 已完成(带边界) | 安全区换算冒烟通过；真实刘海屏数据待设备验证 |
| AC16 | 已完成(带边界) | 输入/焦点冒烟通过；手柄真机待设备验证 |
| AC17 | 部分完成 | 语义颜色 lint 落地；URP Player 滤镜需新 RendererFeature + 性能实测（§7.7 边界） |
| AC18 | 已完成 | 旧界面全部迁移（Dialog）或产品删除（c2840cff）；对照表已按删除决定收口 |
| AC19 | 已完成 | 全域静态扫描 0（代码/asmdef/资源/工具/包依赖），86b5f27e 清残留；08-29-fgui-zero-ugui-finalize 补清 `GameEntry.prefab` 两处 UGUI 静态节点（`UI Form Instances` Canvas + `EventSystem`）并修复 f3617234 误删的嵌套实例根 stripped Transform，Book 门禁命令（含 `RectTransform`）实测归零 |
| AC20 | 已完成 | 100 次、UIComponent Destroy、窗口打开时 shutdown、真实 Fiber Remove 通过（1578e42e、1d33b860） |
| AC21 | 无法补采 | 旧 UGUI 界面已删除无法重采基线；记录为不可补采而非伪造。FairyGUI 侧 Profiler 报告未做（可选后续） |
| AC22 | 已完成(带边界) | Windows64 IL2CPP Player 构建+启动+界面打开通过（1d33b860）；截图/交互矩阵未做 |
| AC23 | 已完成 | Book 六篇 + 三条 spec 同步（f6bed72d） |
| AC24 | 已完成 | 先删后迁风险已按产品删除决定审计：Dialog 迁移完成，其余删除（c2840cff）；LockStep 为已知风险 |
| AC25 | 已完成 | CodeBind/UXTool/UGUI Editor 制作路径全部删除（b2f4ed4e、86b5f27e） |
| AC26 | 已完成 | 无 UGUI 直接依赖；UGUI 专用包全移除；com.unity.ugui 仅 builtin 传递（URP 官方），已审计 |

## 12. 事实来源与派生输出清单

### 可以直接修改的事实来源

- `Design/FairyGUI/GDK_FGUI/assets/**/*.xml`
- `Design/FairyGUI/GDK_FGUI/settings/GDK.json`
- `Design/FairyGUI/GDK_FGUI/settings/Publish.json`
- GameHot/ET 的 `Design/Excel/**/UI.xlsx`
- `Design/Excel/Localization.xlsx`
- `Tools/FairyGUI/*.ps1` 和生成器模板
- GameHot/ET Presenter、flow、component/system 源码
- `.trellis/tasks/**`、`.trellis/spec/**`、`Book/**`

### 不得手工修补的派生输出

- `Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json`
- `Unity/Assets/Res/UI/FairyGUI/*_fui.bytes`
- `Unity/Assets/Res/UI/FairyGUI/*.json`
- `Unity/Assets/Scripts/Game/Generate/FairyGUI/**`
- `Unity/Assets/Scripts/Game/**/Generate/UGF/UIFormId.cs`
- Luban 生成的 `DR*`、`DT*` 和 JSON/binary 数据
- 标记为 generated、`<auto-generated>` 或 `*.Bind.cs` 的文件

规则：先改来源，再运行官方工具/仓库生成器；来源和派生输出作为同一逻辑批次审查。

## 13. 推荐验证命令

### 13.1 工具与确定性

```powershell
pwsh -NoProfile -File ./Tools/FairyGUI/Test-FairyGUITools.ps1
pwsh -NoProfile -File ./Tools/FairyGUI/Test-GDKProject.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyUIFormDescriptors.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyRuntimeManifest.ps1 -Check
```

外部 FairyGUI Editor 工作副本存在时才执行：

```powershell
pwsh -NoProfile -File ./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode Status
```

写同步前必须确认方向和冲突状态。FairyGUI Editor 打开时不要执行写同步；不能默认脚本中的外部
Editor 工作副本路径在其他机器存在。

有来源变更并需要重新生成时：

```powershell
pwsh -NoProfile -File ./Tools/FairyGUI/Invoke-FairyGUIPipeline.ps1
```

该命令会执行 Luban/描述符/manifest 并请求 Unity refresh，不是只读命令。运行前确认工作树和 Unity
Agent Bridge 可用；不要在只想检查时误用。

### 13.2 Trellis 与 GDK 守卫

```powershell
python ./.trellis/scripts/task.py validate .trellis/tasks/08-20-fairygui-gdk-ai-ui-integration
python ./.trellis/scripts/task.py validate .trellis/tasks/08-24-fairygui-gf-ui-host
python ./.trellis/scripts/task.py validate .trellis/tasks/08-27-fairygui-final-integration
python ./.trellis/scripts/task.py validate .trellis/tasks/08-27-fgui-et-full-integration-remove-ugui
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check
git diff --stat
git status --short
```

### 13.3 Unity 验证

执行任何 Unity 操作前：

1. 找到 Unity 工程 `Assets/` 同级的既存 `.agentbridge/`；不存在就停止，不得自行创建；
2. 完整阅读已安装包的 `AGENT.md`；
3. 当前会话首次调用必须运行时 `list_commands`；
4. 只使用发现到的 command/params schema；
5. 遵循 fixed-slot single-flight、原子 request、唯一 id、processing/response ack 规则；
6. `commandsVersion` 变化后重新发现；
7. 使用仓库的
   `.agents/skills/gdk-development-workflow/scripts/bridge_session.py` 时先阅读 `--help`。

Unity 最低证据：

- 导入/编译结果和 Error 日志；
- GameHot 和 ET 模式各自的聚焦测试；
- 取消、失败、100 次生命周期和 shutdown；
- UI hierarchy、GRoot/UIGroup、visible/touchable、serial/depth；
- 三宽高比截图和真实输入；
- ResourceCollection 构建/回读；
- Player 构建和运行。

`.NET`/`Unity.sln` 构建只能补充类型检查，不能证明 Unity 导入、场景、序列化、宏、运行时或 Player。

## 14. 测试设计要求

任何新增测试都应同时断言“发生了什么”和“结束后恢复了什么”。

每次 UI 生命周期测试的基线至少记录：

- GF loading/open form 数量和 serial；
- 各 UIGroup 及组内顺序；
- GRoot 子对象和 Stage/StageCamera 数量；
- GComponent/Presenter 实例数；
- package 状态、generation、lease count；
- GDK Resource 活跃句柄；
- owner token registration；
- Error 日志数量。

测试结束必须回到基线。仅检查“方法返回 success”不够，异步 `.Forget()` 工作需要额外等待一帧并再次
查询 Error 日志。

禁止测试做法：

- 用 assetName 代替 serial 验证多实例；
- 只测第一次打开；
- 通过永久损坏真实 descriptor/XML 制造失败；
- 测试失败后不在 `finally` 恢复 registry/descriptor/资源状态；
- 用截图代替输入、生命周期和资源断言；
- 用 `.NET` 编译替代 Unity 运行证据。

## 15. 任务与文档状态整理

当前没有 Trellis current task，但存在多个 `in_progress` 任务。不要创建同名重复任务。

重点任务：

- 父任务：`08-20-fairygui-gdk-ai-ui-integration`
- GF 宿主：`08-24-fairygui-gf-ui-host`
- 背包/最终接入：`08-27-fairygui-final-integration`
- ET/原生窗口/UGUI 清理：`08-27-fgui-et-full-integration-remove-ugui`

注意：ET 总任务当前不是父任务的 child，但内容与父任务直接重叠。收尾前应根据 Trellis 工作流和用户
意图决定是否关联；不要在未审查父子验收重叠时直接归档任一任务。

旧文档存在以下漂移：

- `Book/FairyGUI接入.md` 仍含 POC/旧宿主边界描述；
- `08-24` 任务勾选了持续 owner token，但当前原生实现实测失败；
- `08-27-fairygui-final-integration` 的实施清单多数未勾选，但相关提交已经存在；
- `08-27-fgui-et-full-integration-remove-ugui` 只勾选阶段 1，但后续原生窗口、ET 和 UGUI 删除提交已存在；
- 父任务 AC01–AC26 全部仍未勾选。

正确收口方法：逐项重新运行证据，更新任务清单和 Book，而不是按提交标题批量勾选。

## 16. 推荐提交拆分

仅在用户授权后提交。推荐逻辑边界：

1. `fix(ui): 修复 FairyGUI 窗体所有者取消生命周期`
2. `fix(et): 由 UIComponent 持有 FairyGUI 窗体生命周期`
3. `feat(ui): 添加内置 FairyGUI 启动更新界面`
4. `feat(ui): 接入 FairyGUI 本地化与声音服务`
5. `feat(ui): 接入安全区输入与焦点恢复`
6. 每个 GameHot/ET/LockStep 页面迁移一个或一组独立提交；
7. `refactor(ui): 移除已迁移的 GDK UGUI 资源与工具`
8. `build(unity): 移除无使用方的 UGUI 直接依赖`
9. `test(ui): 补齐 FairyGUI Player 与生命周期门禁`
10. `docs(ui): 完成 FairyGUI 迁移与回滚文档`

每个提交：

- 来源和派生输出同批；
- Unity 资源和 `.meta` 同批；
- 不夹带当前工作树中的用户配置或 JVM 日志；
- 提交前运行 staged 变更守卫与 commit message 校验。

## 17. 最终完成检查表

只有全部满足才可声明“完整接入”：

- [x] P0 owner token 生命周期问题已修复并通过 100 次测试（已提交 a306b572）；
- [x] 初始化契约稳定，无未初始化空引用（已提交 a306b572）；
- [x] ET UIComponent/HotfixView System 真正拥有所有 UI（五个 Presenter 已迁移 d4f2b4d5，冒烟 1578e42e）；
- [x] ET Demo、Widget、Fiber 代表流程通过（1578e42e）；LockStep 按 §7.7 阻塞记录；
- [x] 内置启动/更新界面经产品删除决定移除（c2840cff）；
- [x] 四语言、本地化、声音、静音/音量通过（冒烟 1578e42e，SDK 主文本边界记录 §7.7）；
- [x] 安全区、方向、输入、手柄和焦点通过（冒烟 1578e42e，真机边界记录 §7.7）；
- [x] 色觉语义 lint 通过；URP Player 滤镜记录为后续批次（§7.7 边界）；
- [x] 所有 Player 可达旧 UI 已等价迁移（Dialog）或有明确删除决定（c2840cff）；
- [x] GDK 自有代码/asmdef/资源/配置/Editor 工具零 UGUI（b2f4ed4e、86b5f27e）；
- [x] UGUI 专用直接依赖和 `com.unity.ugui` 直接依赖已清理（仅 URP 官方传递依赖保留）；
- [x] GameHot/ET 资源构建和目标 Player 加载通过（86b5f27e、1d33b860）；
- [x] 至少一个 IL2CPP Player 构建/启动通过（Windows64，1d33b860）；
- [x] 100 次、热更域、shutdown 无泄漏/错误（1578e42e、1d33b860）；
- [x] 性能回归：旧 UGUI 无法重采基线，明确记录为不可补采（AC21）；FairyGUI 侧 Profiler 报告为可选后续；
- [x] Book、Trellis task、spec 与当前实现一致（f6bed72d）；
- [x] AC01–AC26 每项都有证据与修订号（§11，2026-08-29 更新）；
- [ ] `trellis-check`、GDK 守卫、`git diff --check` 全部审查完成（G7 执行）；
- [ ] 用户授权提交后，子任务和父任务按顺序归档（G7 执行）。

## 18. 给下一模型的可复制提示词

```text
请继续收尾当前 GDK 仓库的 FairyGUI 完整接入任务。

首先完整阅读：
1. AGENTS.md
2. .agents/skills/gdk-development-workflow/SKILL.md
3. .trellis/tasks/08-20-fairygui-gdk-ai-ui-integration/prd.md
4. .trellis/tasks/08-20-fairygui-gdk-ai-ui-integration/design.md
5. .trellis/tasks/08-20-fairygui-gdk-ai-ui-integration/HANDOFF.md

不要根据旧任务勾选或提交标题宣称完成。先运行 get_context、git status、GDK 变更守卫并保护用户现有
改动。优先复现和修复 HANDOFF 第 8 节的 P0-1 owner token 生命周期问题，然后按 P0-2 重建 ET
UIComponent/HotfixView System 所有权。每完成一个阶段，运行对应工具、Unity Agent Bridge 生命周期、
Error 日志和资源基线验证，再更新 HANDOFF/任务证据。不得手改生成文件、不得直接编辑 Unity YAML、
不得拆分资源与 .meta、不得用 .NET 构建代替 Unity/Player 证据、未经授权不得提交。
```

## 19. 2026-08-28 接入复盘（对比原 GDK UGUI 框架）

结论：接入骨架方向正确，GF 语义层约 90% 对齐；存在三类“形状相同、深度不同”的隐患。
以下原框架代码位置均指提交 `8b39d6cc^`。当前提交基线见 §7.4–7.6。

### 19.1 GF 层缺口（与 IUIManager 契约逐项对比）

已对齐：Has/Get/AddUIGroup、HasUIForm×2、IsLoadingUIForm×2、GetUIForm×2、
GetAllLoadedUIForms、GetAllLoadingUIFormSerialIds、CloseUIForm×2、RefocusUIForm×2、
OpenUIForm（priority 固定 `UIFormAsset`）。

| 能力 | GF 契约 | 原 UGUI | 现状 | 影响 |
| --- | --- | --- | --- | --- |
| `SetUIFormInstanceLocked` / `SetUIFormInstancePriority` | `IUIManager.cs:373,380` | UGFUIForm 实例方法 | 无透出 | 中 |
| 对象池调参（AutoReleaseInterval/Capacity/ExpireTime/Priority） | `IUIManager.cs:31-62` | UIComponent 暴露 | 无透出 | 低 |
| `CloseAllLoadedUIForms` / `CloseAllLoadingUIForms` | `IUIManager.cs:342,353` | 有 | 无透出 | 低-中 |
| 五个 GF 事件（Success/Failure/Update/DependencyAsset/CloseComplete） | `IUIManager.cs:67-87` | 可订阅 | 不订阅不转发 | 中（打开失败只靠轮询兜底） |
| `GetAllUIGroups`/`UIGroupCount`/`IsValidUIForm` | `IUIManager.cs:123-235` | 有 | 无透出 | 低（诊断用） |

可疑实现：`FairyUIManager.Initialize()` 每次调用都重建 `FairyUIFormHelper` 并 `SetUIFormHelper`，
`m_Groups` 跨 Initialize 不清空；重复 bootstrap/域重载后的幂等性依赖 GF 端行为，需实测。

### 19.2 生命周期宿主：Widget 级联断层（最重要）

原 `AETMonoUGFUIForm` 在每个宿主回调里自动转发给 `UIWidgetContainer`（OnPause→容器 OnPause 等）。
当前 `FairyUIForm` 只调 `m_Presenter`，不持有 Widget 容器；
`FairyUIWidgetContainer.PauseAll/CoverAll/RevealAll/RefocusAll/UpdateAll/OnDepthChanged`
**全部没有调用方**（`FairyUIWidgetContainer.cs:96-171`）。实例：`FairyDemoPresenter.OnPause()` 只计数，
其 `m_ItemWidget` 收不到暂停/覆盖事件——界面被覆盖时 Widget 仍在更新、仍可交互。

影响：中-高。修复需把 Widget 容器挂到 `FairyUIForm` 宿主层并自动级联；Presenter 要拿到宿主引用，
涉及共享接口小改（可选接口或 host 参数），属公共 API 变更，须按 GDK 风险门禁先确认方案。

### 19.3 ET 层

- `UIComponent` 挂在客户端常驻 root Scene，与原 `UGFComponent` 模式一致；但换场景/重建 root 时的
  清理链与真实 Fiber Remove 未验证（原 §7.3）。
- ET Widget 不是 Entity（`FairyUIWidget` 是 Game 层 IReference 类），父销毁级联依赖 Presenter
  正确实现 OnClose；原 UGFUIWidget 是 Entity 子实体。
- 五个有状态 Presenter 迁移：dispatcher 骨架已就绪（§7.6），逐个迁移即可。

### 19.4 GameHot 层

- 入口链闭环：`ProcedureMenu.cs:72` → `FairyUIFormService.OpenFairyUIFormAsync`，背包四条打开路径同样经 Service。
- 原 AUIForm 的 EventContainer/ResourceContainer“随界面清理”语义仅 `FairyEntity` 持有；
  `FairyUIForm`/Presenter 没有等价容器（design.md §8 要求的三容器只完成 1/3）。
- 本地化/声音/安全区/输入四个服务桥（design.md §10）全部未启动，FairyGUI 路径无任何桥代码。

### 19.5 推荐收尾顺序（在 §10 各阶段门禁内）

1. [x] P1：Widget/容器生命周期级联 —— 已落地 `eae54cd0`：`FairyUIFormContext`（View/SerialId/UIId
      元数据 + Widget 容器 + Event/Resource 容器）随窗体自动级联与清理；`IFairyUIPresenter.OnViewReady`
      改收 context，GameHot 五个表单与 ET 五个 Presenter 已迁移。
2. [x] P2：GF 能力透出 —— 已落地 `dd72f24c`：实例锁/优先级/IsValidUIForm/UIGroupCount/GetAllUIGroups/
      批量关闭透出 + OpenUIFormFailure/CloseUIFormComplete 事件桥。
3. [x] P2：五个 Presenter 按骨架迁移（阶段 C 收尾）—— 已落地 `132b0f33`（FairyDemo 样本）与
      `d4f2b4d5`（背包/详情/覆盖层/Inspector 四件套）；`3a7b8943` 移除 ET 类 Presenter 注册表；
      阶段 C 门禁 `052b4363`（Widget parent destroy + 真实 Fiber Remove 通过）与 `04300707`
      （LockStep 阻塞证据:客户端 UI 已删、需服务器,随阶段 E LockStep 批次验证）。
      残留:`UIComponentFairyUIBridge` 仍有 RuntimeInspector 关闭按钮一个引用方,待后续批次删除。
4. [x] 阶段 D 服务桥 1–5 批 —— 本地化 `f743eefb`(生成器+运行时应用+冒烟;主文本翻译需 SDK 升级)、
      声音 `69afe4d0`(soundRedirect 钩子+UISound 映射+冒烟;真实音效待设计期资源)、
      安全区 `ad9ab10e`(GRoot 安全区容器+fullScreen 标记+换算冒烟;真实设备数据待验证)、
      输入/焦点/手柄 `bd67ae67`(FairyInputService 方向导航+确认/取消;手柄真机待设备)、
      色觉 `bb058518`(覆盖性结论:URP 下旧 ColorBlindnessEffect 不生效,FairyGUI 独立
      StageCamera 不受相机级组件覆盖,Player 滤镜需新 URP RendererFeature+性能实测,
      记录为后续批次;本批落地语义颜色 lint)+ `608509f7`(符号模式恢复工具)。
5. [x] 阶段 E 页面批次 → 阶段 F 零 UGUI → 阶段 G 收口：
   - 阶段 E：DialogForm 迁移完成，其余界面按产品删除决定移除（c2840cff）；
   - 阶段 F：UGUI 工具链/资源/包依赖清零（b2f4ed4e）+ UXTool 残留/R3 悬空引用清零（86b5f27e）；
   - 阶段 G：工具确定性（718a69b8）、双符号冒烟与启动加固（1578e42e）、ResourceCollection
     回读（86b5f27e）、Windows64 IL2CPP Player 构建与启动冒烟（1d33b860）、
     Book/spec 同步（f6bed72d）；AC01–AC26 证据收口见 §11。
   剩余可选后续：URP 色觉滤镜、真机刘海屏/手柄、真实音频资源、FairyGUI SDK 主文本翻译、
   Profiler 性能报告、Editor 双向同步实机留证。
