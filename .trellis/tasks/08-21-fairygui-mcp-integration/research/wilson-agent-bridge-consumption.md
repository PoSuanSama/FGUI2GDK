# Wilson FGUI Agent Bridge 消费调研

## 固定证据

- 上游：`https://github.com/Wilson520403/fgui-agent-bridge`
- 已检查提交：`ba9fa12c1bbe82dbca19785048ae49cf7b5e73ed`
- Python 包/CLI 版本：0.8.1
- Editor 插件：`com.fgui.agent-bridge` 0.8.1，MIT
- 协议：1.0
- 当前配对 Editor 工程：`D:/Unity/Project/GDK_FGUI`

上述提交和本机部署用于兼容性验证，不作为 GDK 安装器。GDK 不硬编码调研 clone/venv 路径，
也不复制上游源码。

## CLI 合同

全局参数必须位于子命令前：

```text
fgui-agent [--project <fairy-or-dir>] [--editor <exe>] [--timeout <seconds>] <command>
```

本任务使用的命令：

```text
status
ping
project
packages
publish --scope packages --package <name> --publish-timeout <seconds>
```

CLI stdout 为格式化 JSON；命令错误以非零码和 stderr/traceback 表达。`status` 是本地只读心跳，
返回 `online`、`versionMatch`、`status.bridgeVersion`、`status.protocolVersion`、
`status.capabilities` 和工程信息。

`BridgeClient.ensure_ready()` 校验心跳新鲜度、协议主版本和 required capabilities；调用具体 action 前
还会检查 action capability。插件 publish 实现为每包构造 FairyGUI `PublishHandler`、等待 `Run()`，
要求 `handler.isSuccess`，并返回 `success`、scope、packages、输出路径快照与耗时。

## 已部署状态

`D:/Unity/Project/GDK_FGUI/plugins/agent-bridge/package.json` 报告 0.8.1。2026-08-21 的实机心跳为
online，bridgeVersion 0.8.1，protocolVersion 1.0，capabilities 含 `ping`、`get_project`、
`list_packages`、`get_publish_settings` 和 `publish`。

## 约束与结论

- 临时验证路径 `D:/Temp/fgui-agent-bridge-publish-ba9fa12/...` 不能进入 GDK 默认配置。
- GDK 通过 `-AgentExecutable`、`FGUI_AGENT_EXE` 或 PATH 消费 CLI。
- Wilson 0.8.1 没有截图 command/capability，不将截图列入本任务验收。
- GDK 不管理 `plugins/agent-bridge`、`.agent`、MCP 注册、上游更新或 Python 依赖。
- 发布仍需 GDK 自己验证目标 bytes 的允许根、存在性、大小、mtime 和 SHA-256；不能只信 CLI 退出码。
