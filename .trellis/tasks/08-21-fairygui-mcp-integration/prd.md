# FGUI Agent Bridge 与 AI 编辑闭环

## 目标

让 GDK 使用已部署的 `Wilson520403/fgui-agent-bridge` 完成 FairyGUI Editor 自动化和单包发布，
同时保留仓库 FairyGUI 源工程的同步、lint、manifest 与产物校验门禁。

## 背景与已确认事实

- `Design/FairyGUI/GDK_FGUI` 是仓库事实来源，`D:/Unity/Project/GDK_FGUI` 是 Editor 工作副本。
- 外部工程已安装 `plugins/agent-bridge` 0.8.1，协议为 1.0；GDK 不负责安装或更新插件。
- Wilson CLI 提供 `status`、`ping`、`project`、`packages` 和
  `publish --scope packages --package <name>`，并输出 JSON。
- CLI 的稳定发现顺序应是显式参数、`FGUI_AGENT_EXE`、`PATH`；临时 clone/venv 路径不得写入仓库。
- Wilson 0.8.1 不提供截图能力。视觉证据由 Editor 预览和后续 Unity Agent Bridge 流程承担。
- 旧 `FairyGUI-MCP`、`MCPBridge` 和 `GDKCliPublish` 方案已否决，不再作为 GDK 维护面。

## 需求

### R1. 外部工具边界

- GDK 只消费 `fgui-agent` CLI，不复制、补丁或同步 Wilson 的插件、Python、MCP 或 `.agent` 数据。
- GDK 不创建、修改或验证 Codex MCP 注册；MCP 是用户可选配置。
- 发布脚本不得依赖仓库外临时 clone 的固定绝对路径。

### R2. 发布前门禁

- 发布前运行 FairyGUI XML lint 和 manifest `-Check`。
- `Sync-GDKDemoToEditor.ps1 -Mode Status` 必须返回 `Equal`，否则零发布。
- `fgui-agent status` 必须为 online、版本匹配、协议主版本 1，且声明 `publish` capability。
- 用 `ping`、`project` 和 `packages` 验证连接、目标工程与目标包。

### R3. 精确发布与产物证明

- 只调用 `publish --scope packages --package <PackageName>`，禁止隐式 active/all 发布。
- 调用后验证 `<PackageName>_fui.bytes` 位于指定 GDK 输出根内、存在且非空。
- 输出结构化证据：绝对路径、大小、UTC mtime、发布前后 SHA-256、CLI 发布结果。
- CLI 非零、JSON 无效、包缺失、路径越界或产物无效都必须失败并保留首个可操作错误。

### R4. 同步隔离

- 同步清单不包含 `plugins/` 或 `.agent/`。
- `Status`、`ToEditor`、`FromEditor` 和初始化均不得创建、覆盖或删除 Editor 插件及运行时数据。
- 仓库中的旧 `MCPBridge`、`GDKCliPublish` 和对应工具实现应移除。

### R5. 文档与验证

- `Book/FairyGUI接入.md` 和工具规范描述 Wilson 消费边界、CLI 发现顺序和发布命令。
- focused tests 覆盖 CLI 发现、门禁失败、精确参数、JSON/产物校验与插件 sentinel 不变性。
- 实机验证按 `status -> ping -> project -> packages -> Package1 publish -> artifact verify` 执行。

## 不在范围内

- 修改、安装、升级或同步 `fgui-agent-bridge` Editor 插件。
- 维护 Wilson Python/MCP 源码、`.agent/` 队列或 Codex MCP 注册。
- 为 Wilson 0.8.1 添加截图功能。
- 修改 Unity runtime、场景、预制体、UGUI 或业务 UI 生命周期。
- 发布全部包或隐式选择当前活动包。

## 验收标准

- [x] AC01：发布器按 `-AgentExecutable`、`FGUI_AGENT_EXE`、`PATH` 顺序发现 CLI，缺失时给出明确错误。
- [x] AC02：lint、manifest 或同步状态非 `Equal` 时不会调用 CLI 发布。
- [x] AC03：状态必须 online、versionMatch、协议 1.x 且含 `publish` capability。
- [x] AC04：只执行指定包发布，目标包不存在或工程不匹配时失败。
- [x] AC05：成功结果包含 bytes 的路径、非零大小、UTC mtime 和发布前后 SHA-256。
- [x] AC06：CLI 非零、非 JSON、路径越界、产物缺失或空文件均失败。
- [x] AC07：同步三种模式和初始化均不改变测试工程中的 plugin/.agent sentinel。
- [x] AC08：仓库不再包含 `MCPBridge`、`GDKCliPublish` 或 GDK 自研 Bridge 实现。
- [x] AC09：文档与工具规范明确 GDK/外部 Bridge 的所有权边界和操作顺序。
- [x] AC10：focused tests、Trellis check、GDK 变更守卫和实机 Package1 发布证据通过。
