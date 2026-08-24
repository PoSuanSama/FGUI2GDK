# FairyGUI CLI 发布契约

## 1. 所有权边界

本契约覆盖 `Tools/FairyGUI/Publish-GDKDemo.ps1` 和 `Sync-GDKDemoToEditor.ps1`。GDK 消费外部
[Wilson FGUI Agent Bridge](https://github.com/Wilson520403/fgui-agent-bridge) 的 `fgui-agent` CLI；
GDK 不复制、安装、升级或修改其 Editor 插件、Python/MCP 源码、`.agent` 队列或 Codex MCP 配置。

`D:\Unity\Project\GDK_FGUI` 是 Editor 工作副本，`Design/FairyGUI/GDK_FGUI` 是仓库事实来源。
同步哈希清单只允许 `.fairy`、`settings/Publish.json` 和 `assets/`；仓库契约
`settings/GDK.json` 只允许单向补给 Editor。`plugins/`、`.agent/` 以及未知目录不得参与 hash、
复制、删除或初始化。

## 2. 调用签名

```powershell
./Tools/FairyGUI/Publish-GDKDemo.ps1 `
  [-AgentExecutable <path>] [-SourceProjectPath <dir>] [-EditorProjectPath <dir>] `
  [-PackageName <name>] [-OutputPath <dir>] [-TimeoutSeconds <1..3600>]
```

CLI 发现顺序必须为：显式 `-AgentExecutable`、`FGUI_AGENT_EXE`、PATH 中的 `fgui-agent`。
所有进程参数通过 `ProcessStartInfo.ArgumentList` 传递；禁止拼接 shell 命令或写死临时 clone/venv 路径。

## 3. 发布门禁与命令

发布前依次执行：

1. `Test-GDKProject.ps1 -Check`；
2. `Sync-GDKDemoToEditor.ps1 -Mode Status`，要求 `state=Equal`；
3. `fgui-agent --project <editor> status`，要求 online、versionMatch、协议 `1.x`、`publish` capability；
4. `ping`、`project`、`packages`，确认工程根和目标包；
5. `publish --scope packages --package <PackageName> --publish-timeout <seconds>`。

禁止使用 `active`、`all` 或隐式全量发布作为回退。Wilson CLI 的 stdout 必须是 JSON，退出码必须为 0，
且 publish JSON 的 `success` 必须为 `true`。

## 4. 产物证明

目标产物为 `<OutputPath>/<PackageName>_fui.bytes`。脚本必须验证路径仍在输出根内、存在且非空，并输出：

- 绝对路径；
- 字节数；
- UTC mtime；
- 发布前/后的 SHA-256；
- 状态、包检查和 publish 的 CLI JSON。

重复发布允许确定性 hash 不变；CLI 成功不能替代产物检查。

## 5. 错误矩阵

| 条件 | 必需行为 |
| --- | --- |
| CLI 缺失 | 启动前失败，提示三种发现方式 |
| lint/manifest 或同步非 Equal | 不调用 publish |
| status 离线、过期、版本/协议不匹配 | 不调用 publish |
| 工程或包不匹配 | 不调用 publish，不回退其他 scope |
| CLI 非零、超时或 JSON 无效 | 失败并保留阶段与可操作错误 |
| bytes 缺失、为空或越界 | 即使 CLI success 也失败 |

## 6. 测试要求

- PowerShell AST 解析和 `Test-FairyGUITools.ps1` focused tests；
- fake CLI 覆盖成功、状态失败、包缺失、非零、非 JSON、精确参数和产物失败；
- 同步测试在 `plugins/agent-bridge`、未知插件和 `.agent` 放置 sentinel，断言所有模式前后不变；
- 实机验证顺序为 `status -> ping -> project -> packages -> Package1 publish -> artifact verify`。
  Editor 离线时必须记录未验证，不得用 fake 结果宣称实机发布通过。
