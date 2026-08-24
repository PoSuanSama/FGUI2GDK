# FGUI Agent Bridge 实施计划

## A. 规划与基线

- [x] 记录用户决策：停止维护 FairyGUI Editor 插件和 `kaohum/FairyGUI-MCP`。
- [x] 确认外部 Wilson 0.8.1 插件、协议 1.0、CLI 参数和 publish capability。
- [x] 建立 GDK 变更守卫基线：0 errors，386 个与本任务无关的既有 warnings。
- [x] 验证 Trellis 任务文档和上下文清单。

## B. GDK 工具改造

- [x] 重写 `Tools/FairyGUI/Publish-GDKDemo.ps1`：CLI 发现、lint/manifest/sync 门禁、Wilson 状态与
      包检查、精确单包发布、bytes 证据 JSON。
- [x] 从 `Sync-GDKDemoToEditor.ps1` 的同步集合中移除插件管理。
- [x] 删除仓库自研 `Tools/FairyGUI/MCPBridge`、镜像 `plugins/MCPBridge` 和 `plugins/GDKCliPublish`。

## C. 测试与文档

- [x] 扩展 `Test-FairyGUITools.ps1`，用 fake CLI 覆盖成功/失败和精确参数。
- [x] 添加 `plugins/agent-bridge`、未知插件与 `.agent` sentinel，证明所有同步模式均零写入。
- [x] 更新 `Book/FairyGUI接入.md` 和 `.trellis/spec/tools/fairygui-cli-publish.md`。
- [x] 更新 PSD 后续任务，将截图职责改为 Editor 预览与 Unity Agent Bridge。

## D. 验证顺序

```powershell
python ./.trellis/scripts/task.py validate 08-21-fairygui-mcp-integration
pwsh -NoProfile -File ./Tools/FairyGUI/Test-FairyGUITools.ps1
pwsh -NoProfile -File ./Tools/FairyGUI/Test-GDKProject.ps1
pwsh -NoProfile -File ./Tools/FairyGUI/Test-GDKProject.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode Status
pwsh -NoProfile -File ./Tools/FairyGUI/Publish-GDKDemo.ps1 -AgentExecutable <fgui-agent>
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check
```

实机发布前依次确认 `status`、`ping`、`project`、`packages`，只发布 `Package1`，随后核对
`Package1_fui.bytes` 的路径、大小、mtime 和 SHA-256。若 Editor 离线，实机步骤明确记为未验证，
不得用 fake CLI 结果替代。

## 回滚点

- 删除旧插件目录前，focused tests 必须先证明同步器不再管理 `plugins/`。
- 不删除或修改外部 `D:/Unity/Project/GDK_FGUI/plugins/agent-bridge`、`plugins-disabled`、`.agent`。
- 不创建提交；工作区无关变更保持原样。

## 实施证据

- `Test-FairyGUITools.ps1` 使用临时仓库/Editor 镜像和 fake `fgui-agent.cmd`，不访问真实外部插件或
  `.agent`。覆盖显式参数、`FGUI_AGENT_EXE`、PATH、精确单包发布、无效 JSON、包缺失、产物缺失、
  非 Equal 同步门禁与三类 sentinel。
- 仓库扫描确认 `Tools/FairyGUI/MCPBridge`、`plugins/MCPBridge`、`plugins/GDKCliPublish` 已无文件。
- 2026-08-21 聚焦验证：PowerShell AST 3/3；Trellis context validation 通过；
  `Test-FairyGUITools.ps1` 101/101 assertions；仓库 `Test-GDKProject.ps1 -Check` 通过；真实镜像
  `Sync -Mode Status` 为 `Equal`，hash 为
  `3f0365a1da792518b33b111ffb3d05a1d632f676ccaa692830d795051e9aee71`。
- GDK 变更守卫为 0 errors、386 个既有工作树 warnings。通过 Wilson CLI 正常启动 Editor 后，
  Bridge/plugin 0.8.1、协议 1.0 和 publish capability 均通过；精确 `Package1` 发布耗时 159 ms。
- `Package1_fui.bytes` 为 6887 bytes，mtime 从 `2026-08-21T09:01:24.6401230Z` 更新为
  `2026-08-21T11:20:57.0311363Z`；发布前后 SHA-256 均为
  `db9ea28f9ed10979524d64bdf8e8df6120afb0d71905e2ff6c6663adca45ac07`。
