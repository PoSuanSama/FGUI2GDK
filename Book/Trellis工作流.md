# Trellis AI 开发工作流

GDK 使用 [Trellis](https://github.com/mindfold-ai/Trellis) 管理 AI 开发任务、项目规范和跨会话工作日志。当前仓库初始化版本记录在 `.trellis/.version`，Codex 是已启用的平台。

## 环境与安装

- Node.js 18.17 或更高版本
- Python 3.9 或更高版本
- 全局 CLI：`npm install -g @mindfoldhq/trellis@0.6.15`

仓库已经初始化，无需再次执行 `trellis init`。检查状态：

```powershell
trellis --version
trellis platforms
python ./.trellis/scripts/get_context.py
```

## 目录职责

| 路径 | 职责 |
| --- | --- |
| `.trellis/workflow.md` | 任务规划、实现、验证和收尾阶段 |
| `.trellis/spec/` | GDK 服务端、Unity 客户端与跨层规范 |
| `.trellis/tasks/` | PRD、设计、实现上下文和归档任务 |
| `.trellis/workspace/` | 开发者工作日志与会话索引 |
| `.agents/skills/trellis-*` | Codex 可调用的 Trellis Skill |
| `.codex/` | Codex hooks、子代理和项目配置 |

`.agents/skills/gdk-development-workflow/` 仍是 GDK 工程变更的强制工作流。Trellis 负责任务和上下文编排，GDK Skill 负责 Unity、生成文件、验证和 Git 交付门禁；两者同时适用时都必须遵守。

## Codex 首次启用

Codex hooks 已在项目中生成。Codex 0.129 及以上版本首次进入仓库后，在 CLI/TUI 执行 `/hooks`，审核并允许本项目的 Trellis hooks。未批准前仍可手动加载 `trellis-start`，但不会自动注入每轮工作流状态和子代理上下文。

当前项目关闭 Trellis 自动 Git 提交：`.trellis/config.yaml` 中 `session_auto_commit: false`。任务归档和工作日志只写文件，不会自行暂存或提交；Git 交付继续遵守 GDK 规则。

## 日常流程

1. 新会话加载 `trellis-start`，确认开发者、Git 状态、活动任务和相关 Spec。
2. 需要工程修改时，按 Trellis 工作流先确认是否创建任务，再维护 PRD/设计/实现上下文。
3. 实现前读取对应的 `backend`、`frontend` 和 `guides` Spec，同时加载 GDK Skill。
4. 变更后执行 Trellis 检查、GDK 变更守卫以及受影响模块的构建、Unity/Bridge 或资源验证。
5. 使用 `trellis-finish-work` 收尾；是否提交由开发者明确决定。

常用任务查询：

```powershell
python ./.trellis/scripts/task.py list --mine
python ./.trellis/scripts/task.py list-archive
python ./.trellis/scripts/get_context.py --mode packages
```

## 更新

先升级全局 CLI，再从干净、已审查的工作树执行项目更新，并检查生成差异：

```powershell
trellis upgrade
trellis update
trellis platforms
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
```

Trellis 更新可能改写受管理块和模板文件。不得用 `--force` 覆盖 GDK 的 `AGENTS.md`、自定义 Spec 或 `gdk-development-workflow` Skill；更新后必须确认这些内容仍然存在。

Trellis 使用 AGPL-3.0-only 许可证，GDK 自身及其他第三方依赖仍分别遵循各自许可证。
