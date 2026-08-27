# FairyGUI 多界面发布与最终接入收口

## Goal

在现有 FairyGUI → GDK 接入链路上增加一组可重复验证的界面样例，并完成从 FairyGUI Editor 发布、GDK 资源加载、UGF UI 生命周期、多实例与覆盖栈行为，到自动化验证和文档的最终收口。最终结果应让后续业务界面能够沿同一条受约束流程接入，而不再依赖一次性手工步骤或旧的 UIPanel/预制体方案。

## Requirements

- 使用当前已打开的 `GDK_FGUI` FairyGUI 工程作为界面事实来源；界面必须先在 FairyGUI Editor 中创建并保存，再通过项目既有发布链路进入 GDK，不能直接把发布产物当作手工编辑源。
- 在 `Package1` 中新增 3 个面向回归验证的界面：
  - 多实例界面：用于同时打开同一种 UI 的多个实例，并显示可区分的实例标识与关闭入口。
  - 栈底界面：用于观察打开、暂停、恢复、关闭等生命周期状态。
  - 覆盖界面：用于覆盖栈底界面、触发暂停/恢复，并提供关闭返回入口。
- 为新增界面补齐 GDK 的 UI 配置、FairyGUI 映射、生成描述符、绑定及 Presenter 接入；UI ID 暂按 `104`、`105`、`106` 规划，实施前必须以两个 Excel 源表为准确认未占用。
- `UI.xlsx` / Luban 配置继续作为 UI ID、资源名、分组、是否允许多实例、是否暂停被覆盖界面的唯一事实来源；`GDK.json` 只维护稳定的 `CSName → package/component/binding/presenter` 映射。
- 新界面应复用现有 `FairyDemoForm` 的共享宿主、包租约、异步打开及 Presenter 生命周期机制，不恢复每界面 UIPanel 或专用 UI 预制体。
- 多实例验证至少覆盖：同一 UI 同时存在两个实例、实例拥有不同的 FairyGUI 组件与 Presenter/实例标识、任意顺序关闭互不干扰。
- 覆盖栈验证至少覆盖：打开栈底、打开覆盖层、栈底暂停、关闭覆盖层、栈底恢复、最终关闭及资源租约回收。
- 失败路径验证不得通过发布故意损坏的 FairyGUI 资源完成；应使用测试期临时描述符或工厂替身覆盖缺包、缺组件、缺绑定、缺 Presenter、打开中取消，以及并发请求一成一败等路径。
- 更新 `Book/FairyGUI接入.md`，删除或改正旧的 `AFairyUIForm`、每界面 UIPanel 和专用预制体说明，形成可执行的编辑、发布、配置、生成、验证步骤。
- 保留当前工作树中已有的用户改动和其他 Trellis 任务成果，不回退、不重做、不覆盖无关文件。
- FairyGUI Editor 打开期间不得执行仓库到编辑器的写同步；若通过官方 Agent Bridge 驱动编辑器，保存与发布完成后，需关闭编辑器并按既有 `FromEditor` 流程回收事实源。
- 不复制、改写或绕过 FairyGUI Agent Bridge 的请求队列，也不在 GDK 仓库内私自维护桥接器、Python 环境或插件版本。

## Acceptance Criteria

- [ ] FairyGUI Editor 中存在并已保存 3 个新增界面，组件命名稳定，发布检查通过。
- [ ] 发布产物与运行时清单进入项目约定目录，仓库事实源、编辑器工作副本和发布输出之间无未解释漂移。
- [ ] 两套 UI 配置源及其 Luban 输出包含新增界面，ID、AssetName、UIGroup、AllowMultiInstance、PauseCoveredUIForm 与预期一致。
- [ ] 描述符生成成功，新增映射无重复、无陈旧项、无手工修改生成文件。
- [ ] Unity 编译通过；新增界面可以经 UGF 打开、刷新、关闭，并保持完整生命周期。
- [ ] 自动化或 AgentCallable 验证证明多实例隔离、覆盖栈暂停/恢复、包租约平衡和并发失败隔离成立。
- [ ] 缺包、缺组件、缺绑定、缺 Presenter、打开取消等失败路径可诊断，且不会遗留 UI、Presenter 或资源租约。
- [ ] 现有 FairyDemoForm 回归通过，旧 UGUI/ETUI 路径没有被意外改变。
- [ ] `Book/FairyGUI接入.md` 与实际实现一致，后续开发者可仅按文档完成同类界面接入。
- [ ] GDK 变更守卫无错误；针对变更面完成脚本测试、生成一致性检查、.NET 构建与 Unity 侧验证，结果和限制均被记录。

## Notes

- 父任务：`08-20-fairygui-gdk-ai-ui-integration`。
- 当前仓库发布脚本依赖外部 `fgui-agent` CLI；本机当前既没有 PATH 命令，也没有设置 `FGUI_AGENT_EXE`。
- 已确认 FairyGUI Editor 内的 Agent Bridge 在线，但项目规范禁止直接操作 `.agent` 队列，必须经官方 CLI/MCP 使用。
- 历史决策继续有效：UI 运行属性归 Luban，稳定 FairyGUI 映射归 `GDK.json`；真实多实例端到端验证是本次收口的核心缺口。
- 待用户决策：是否允许在实施阶段安装官方 Wilson `fgui-agent-bridge` CLI 工具链（包括 `uv` 和官方仓库）；若已有工具链，也可提供明确的 CLI 路径。
