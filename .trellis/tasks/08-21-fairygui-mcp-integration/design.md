# FGUI Agent Bridge 消费设计

## 1. 所有权边界

```text
Design/FairyGUI/GDK_FGUI (repo source)
        | lint + manifest + explicit sync
        v
D:/Unity/Project/GDK_FGUI (Editor mirror)
        | external Wilson fgui-agent CLI
        v
FairyGUI Editor plugins/agent-bridge
        | exact package publish
        v
Unity/Assets/Res/UI/FairyGUI/<Package>_fui.bytes
```

GDK 拥有源/镜像同步、前置门禁、CLI 进程边界和发布产物证明。Wilson 仓库拥有 Editor 插件、
协议实现、Python CLI/MCP 与运行时队列。两者不互相复制源码。

## 2. CLI 发现与进程契约

`Publish-GDKDemo.ps1` 按以下顺序解析可执行文件：

1. `-AgentExecutable`
2. 环境变量 `FGUI_AGENT_EXE`
3. `Get-Command fgui-agent`

所有参数通过 `ProcessStartInfo.ArgumentList` 传递。包装器捕获 stdout/stderr、限制总超时，要求退出码
为 0 且 stdout 可解析为单个 JSON 值。不得调用 shell 字符串或写入仓库外固定安装路径。

## 3. 发布时序

1. 解析 repo、Editor、输出目录与目标 bytes，验证输出位于允许根。
2. 运行 `Test-GDKProject.ps1`。
3. 运行 `Generate-GDKManifest.ps1 -Check`。
4. 运行同步 `Status`，要求 `state=Equal`。
5. 调用 Wilson `status`，要求 online、versionMatch、协议主版本 1、`publish` capability。
6. 调用 `ping`、`project`、`packages`，确认工程根和包名。
7. 快照目标 bytes 的存在性、大小、mtime、SHA-256。
8. 调用 `publish --scope packages --package <name> --publish-timeout <seconds>`。
9. 验证目标 bytes 存在、非空、仍在输出根内，记录发布后快照。
10. 输出一个 JSON 对象，包含门禁结果、CLI publish 结果和 artifact 前后证据。

重复发布允许 SHA-256 不变，因为同一输入的确定性输出应保持一致；成功必须由 Wilson 发布结果和
非空目标产物共同证明，mtime 用于区分是否执行过发布。

## 4. 同步边界

同步器只处理 FairyGUI 源工程的显式清单：`.fairy`、`assets/`、`settings/` 等既有事实来源文件。
删除旧插件同步条目后，`plugins/` 与 `.agent/` 对 Status hash 和复制集合均不可见。测试在两个目录放置
随机 sentinel，并对所有同步方向前后比较内容与 SHA-256。

## 5. 错误处理

- 前置门禁失败：停止在第一个失败点，不启动 publish。
- CLI 缺失或非零：包含解析后的可执行路径、命令阶段和 stderr，不泄露完整环境。
- JSON 无效：报告 stdout/stderr 摘要，不能将人类日志误判为成功。
- 工程或包不匹配：停止，不回退 active/all。
- 产物缺失、为空或越界：即使 CLI 返回 success 也失败。
- 超时：只终止本次启动的 CLI 进程树。

## 6. 兼容与回滚

新参数以 `-AgentExecutable`、`-EditorProjectPath` 和 `-OutputPath` 为核心；旧 batch Editor 参数不保留，
因为其实现依赖已退役的自研插件。回滚只需恢复 GDK 包装脚本/文档；不得删除外部
`plugins/agent-bridge` 或 `.agent`。
