# FairyGUI 多界面发布与最终接入收口

## Goal

在现有 FairyGUI → GDK 接入链路上实现一组可交互的背包界面样例，完成从 FairyGUI Editor 创建与发布、控制器与列表、Luban 配置、描述符和官方绑定生成、UGF UI 生命周期，到多详情窗实例、点击置顶、覆盖栈、失败清理、文档和自动化证据的最终收口。后续业务界面应能沿同一条受约束流程接入，不再依赖一次性手工步骤或旧的 UIPanel/专用预制体方案。

## Background and Confirmed Facts

- 仓库事实来源是 `Design/FairyGUI/GDK_FGUI`，本机 Editor 工作副本是 `D:\Unity\Project\GDK_FGUI`。
- FairyGUI Editor 的 Agent Bridge 当前在线，版本 `0.8.1`、协议 `1.0`，已打开 `Package1/MainView`。
- 本机尚无 `uv`、PATH 中的 `fgui-agent` 或 `FGUI_AGENT_EXE`；用户已授权在独立工具目录安装官方 Wilson `fgui-agent-bridge` CLI 工具链。
- `Package1` 当前导出 `MainView`，运行时通过共享 `FairyUIFormService`、单一 GRoot/UIGroup 宿主、包租约和 Presenter 打开，不再使用每窗体 UIPanel。
- 两份 `UI.xlsx` 当前都缺少 `FairyDemoForm(103)`，但 Luban 派生输出和运行时描述符仍包含 103；这是本任务必须先修复的事实源漂移。
- UI 域全局搜索确认 `104`、`105`、`106` 未被占用。
- `Book/FairyGUI接入.md` 仍引用已删除的 `AFairyUIForm`、`FairyDemoForm.prefab` 和 UIPanel 运行链路，已与当前实现不一致。

## Requirements

### R1. 外部工具链与所有权边界

- 在 GDK 仓库外安装官方 `uv` 和 Wilson `fgui-agent-bridge`，执行 `uv sync --frozen`，记录实际提交和版本。
- 不复制或修改 Bridge 的 Python/MCP 源码，不安装或升级 FairyGUI 工程内插件，不修改 Codex MCP 配置，不直接操作 `.agent` 队列。
- GDK 发布脚本只通过显式 CLI 路径消费外部工具，并继续执行现有状态、工程、包和产物门禁。

### R2. FairyGUI 背包界面事实来源

- 在当前 `Package1` 中创建三个 UGF UIForm 组件和一个可复用列表项组件：
  - `InventoryView`：1280×720 背包主界面，包含分类控制器、分类按钮、物品列表、当前状态、测试覆盖层入口和关闭入口。
  - `InventoryItem`：背包列表项，展示物品名、数量和品质，不单独注册为 UGF UIForm。
  - `ItemDetailWindow`：1280×720 透明根下的浮动详情窗，展示物品信息、实例/serial 状态和关闭入口，允许同时存在多个实例。
  - `InventoryOverlayView`：覆盖背包流程的测试浮层，用于验证 pause/cover 与 resume/reveal。
- `InventoryView` 必须包含名为 `category` 的 FairyGUI Controller，页面至少为 `all`、`equipment`、`consumable`、`quest`；切换页面会更新选中状态和可见物品集合。
- 点击任意 `InventoryItem` 会打开一个新的 `ItemDetailWindow` UIForm，不复用或替换已存在的详情窗。
- 点击任意详情窗的窗口主体会调用 GF `RefocusUIForm`，使该实例移动到同组最上层并触发其 `OnRefocus`；其他详情窗继续存在且状态不丢失。
- 所有业务成员使用稳定名称并进入官方 C# 绑定；不通过运行时 `GetChild("...")` 或反射查找成员。
- 先在已打开的 FairyGUI Editor 中创建、保存和发布，再在关闭 Editor 后通过 `FromEditor` 将 XML 事实来源导回仓库。

### R3. Luban 与描述符身份

- 先在 GameHot 和 ET 两份 `UI.xlsx` 恢复 `FairyDemoForm(103)`，再增加：
  - `104 / FairyInventoryForm / Hot/FairyInventoryForm / Default / AllowMultiInstance=false / PauseCoveredUIForm=false`；
  - `105 / FairyItemDetailForm / Hot/FairyItemDetailForm / Pop / true / false`；
  - `106 / FairyInventoryOverlayForm / Hot/FairyInventoryOverlayForm / Pop / false / true`。
- `UI.xlsx` 继续作为 UI ID、CSName、AssetName、UIGroup 和 GF 策略的唯一事实来源；`GDK.json` 只维护稳定的 `CSName → package/component/binding/presenter` 映射。
- 通过现有 Luban、manifest、官方绑定和 descriptor 生成流程更新派生输出；不得直接修补生成的 ID、绑定或 descriptor。

### R4. GameHot Presenter 与共享宿主

- 新界面复用现有共享宿主、包租约、异步打开、原始 `userData` 和 Presenter 生命周期，不恢复专用 UI 预制体，不修改 UGF/FairyGUI 供应商核心。
- 每个 Presenter 使用对应的官方生成绑定；背包夹具数据通过原始 `userData` 引用传递物品数据、实例令牌和 close/refocus/open-overlay 动作，不为框架新增关闭句柄公共 API。
- `HotEntry` 以可审查的确定性映射注册同一 Package1 binder 和四个 Presenter；未知 CSName 继续快速失败。

### R5. 生命周期与失败验证

- Controller 验证覆盖四个分类页面、按钮与 selectedPage 同步、列表过滤和重复切换后状态稳定。
- 多实例验证至少由三个不同物品详情窗组成，覆盖不同 GComponent/Presenter/serial/item/token、点击非顶层窗口后置顶、重复 refocus、不按打开顺序关闭，以及最终资源基线恢复。
- 覆盖栈验证覆盖背包和详情窗清理后的 104/106 打开、pause/cover、关闭覆盖层、resume/reveal/refocus、最终关闭和包租约回收。
- 失败验证覆盖缺 descriptor、缺包、缺组件、错误绑定、缺 Presenter、打开取消和并发一成一败；临时 descriptor/注册表替身必须在 `finally` 中恢复，不能把故意损坏的资源提交或发布。
- 现有 `FairyDemoForm` 的打开、交互、100 次生命周期和 shutdown 回归继续通过。

### R6. 文档与可复现流程

- 更新 `Book/FairyGUI接入.md`，移除旧 POC 路径，说明共享宿主、外部 CLI 安装边界、Editor 创建/保存/发布、关闭后 `FromEditor`、Excel/Luban、描述符和 Unity 验证的真实顺序。
- 文档不得写死本机临时 clone/venv 路径；使用显式参数或环境变量示例。

### R7. 变更纪律

- 保留当前工作树中的用户文件；八个 JVM 崩溃日志不属于任务范围，也不得提交。
- FairyGUI Editor 打开期间不得执行仓库到 Editor 的写同步；Unity 资源修改和验证遵循 Unity Agent Bridge 契约。

## Acceptance Criteria

- [ ] AC01：官方 CLI 工具链在仓库外安装并通过版本、`status`、`ping`、工程和包检查，项目内插件与 `.agent` 未被修改。
- [ ] AC02：Editor 中存在并保存 InventoryView、InventoryItem、ItemDetailWindow、InventoryOverlayView；`category` Controller 的四个页面、稳定成员和官方生成绑定与契约一致。
- [ ] AC03：Package1 精确单包发布成功，`Package1_fui.bytes`、manifest、绑定和 descriptor 通过产物及确定性检查。
- [ ] AC04：两份 UI 源表同时包含 103–106，Luban 输出与 UI ID、路径、分组、多实例和覆盖策略完全一致。
- [ ] AC05：104 的分类控制器真实驱动列表过滤；点击物品可同时打开至少三个 105 实例，点击任一非顶层详情窗会使其成为最上层且其他实例保持不变。
- [ ] AC06：105 的 serial、GComponent、Presenter、物品数据和实例令牌彼此隔离，可重复 refocus 并任意顺序关闭；104/106 产生真实 pause/cover 与 resume/reveal/refocus 迁移。
- [ ] AC07：所有新 Presenter 使用官方绑定，原始 userData 引用不被包装或替换，关闭后没有 GObject、Presenter、GF UIForm 或包租约残留。
- [ ] AC08：缺失/漂移/取消/并发失败均产生可诊断失败，临时测试修改全部恢复且 Git 中没有故意损坏的资源。
- [ ] AC09：Unity Editor 编译为 0 error；背包分类、物品点击、详情置顶、重复打开关闭、shutdown，以及 16:9、19.5:9、4:3 视觉检查通过。
- [ ] AC10：GameHot 与 ET 资源集合、工具测试、生成一致性、GDK 守卫和文档检查通过；现有 UGUI/ETUI 路径未被意外改变。

## Out of Scope

- 不迁移现有 Setting/About/Dialog、ETUI/UIWidget 或其他生产 UGUI 页面。
- 不新增 FairyGUI 包、图片、字体、声音、动画或第三方 UI 资产；背包物品使用文本、色块和内建图形占位。
- 不升级 FairyGUI SDK、UGF、Unity 包、Bridge 插件或修改供应商源码。
- 不把外部 Bridge 源码、虚拟环境、MCP 配置或机器绝对路径提交到 GDK。
- 不重构已验证的共享宿主公共 API；只有新回归暴露缺陷时才做最小修复并重新评审风险。

## Key Decisions

- 用户已授权安装官方外部 CLI 工具链；安装仅用于驱动当前已安装的 `0.8.1` Editor 插件。
- 收口使用背包主界面、可复用物品项、多实例详情窗和测试覆盖层，而不是抽象计数面板或迁移现有生产 UGUI 页面。
- 103–106 同时存在于 GameHot/ET UI 源表，以保持两种 GDK 模式的 UI 身份一致；运行演示和 Presenter 仍只实现 GameHot。
- 详情窗采用真正的多实例 UGF UIForm；点击窗口通过原始 userData 中的动作调用 `GameEntry.UI.RefocusUIForm`，从而同时验证 GF 排序、FairyGUI 深度映射和 Presenter `OnRefocus`。
