---
name: gdk-development-workflow
description: 约束 GameDevelopmentKit（GDK）中的实现、调试、审查、验证和 Git 交付。当 Codex 修改或评估 GDK 的 Unity/C#/.NET 代码、资源、场景、预制体、Luban 或 Proto 输入与生成输出、包、构建配置、测试、文档、提交或拉取请求，以及需要使用 Unity Agent Bridge 时，使用此 Skill。将本工作流用于代码、资源、目录、资源管理、质量、安全和提交规范；不作工程判断的简单只读说明可跳过。
---

# GDK 开发工作流

## 目标

以项目原有的目录归属完成最小且完整的 GDK 变更，同时保证 Unity 序列化安全、生成过程可复现、验证力度与风险相称，并留下可审计的 Git 证据。

## 执行工作流

### 1. 建立基线

1. 阅读所有适用的 `AGENTS.md`。
2. 确定仓库根目录，并在编辑前检查 `git status --short`。
3. 保留用户无关变更，绝不清理、重置或改写这些变更。
4. 选择实现模式前，阅读相邻实现、程序集定义、相关 `Book/` 文档和生成器输入。
5. 说明预期范围与验收证据；仅在下文风险门禁适用时暂停并询问。

工作树已有改动时，尽早运行守卫脚本：

```powershell
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
```

### 2. 对变更分类

只加载当前任务所需的参考规范：

| 变更类型 | 必须阅读的参考规范 |
| --- | --- |
| 模块、程序集、目录、生成代码 | `references/repository-layout.md` |
| C# 或运行时行为 | `references/code-standards.md` |
| Unity 资源、`.meta`、场景、预制体、导入器、资源路径 | `references/asset-management.md` |
| Unity Editor 查询、修改、编译、日志、测试、截图 | `references/unity-agent-bridge.md` |
| 依赖、安全、性能、迁移、可观测性 | `references/engineering-quality.md` |
| 测试选择与完成证据 | `references/validation.md` |
| 分支、提交、PR 或交接 | `references/git-delivery.md` |

任务涉及多行时，将其视为一个工作流，并加载所有匹配的参考规范。

### 3. 应用风险门禁

对明确、局部且可逆的工作自主继续。执行以下操作前，暂停并征得用户确认：

- 修改公共 API、序列化结构、协议、存档数据或配置格式；
- 引入或升级依赖、Unity 包、分析器或构建服务；
- 删除或批量移动资源、修改大量带 GUID 的文件，或批量改写场景/预制体；
- 在显著不同的用户体验、架构、兼容性或迁移方案之间作选择；
- 运行会清除用户可能需要的输出的破坏性生成或构建步骤；
- 扩大到请求范围以外的模块，或有意破坏向后兼容性。

门禁获批后，记录选定方案、迁移路径和回滚策略。

### 4. 修改事实来源

1. 将代码放入实际归属模块，遵守 ET/GameHot、客户端/服务端、热更新、Editor/运行时和程序集边界。
2. 先修改生成器输入，再更新输出。绝不手动编辑已标记为生成的文件或 `*.Bind.cs`。
3. 使用现有辅助工具、分析器、代码生成器、资源系统和生命周期模式。
4. 将源码、生成输出、资源、`.meta`、配置和测试纳入同一个逻辑变更。
5. 避免无关格式化、包变动、资源重新导入或项目设置变更。

### 5. 通过 Agent Bridge 管控 Unity 操作

执行任何 Unity 查询或修改前，遵循 `references/unity-agent-bridge.md`，并完整阅读已安装包的 `AGENT.md`。如果工程的 `Assets/` 同级没有既存的 `.agentbridge/`，停止 Unity 操作，并报告 Unity 尚未安装或启动 AgentBridge。绝不创建该桥接目录。

当已发现的 Bridge/Unity API 可以表达某项操作时，不要直接编辑 Unity YAML。若不存在受支持的命令，说明降级方案，并在操作后验证 GUID、fileID、引用、导入、编译和日志。

### 6. 按风险验证

阅读 `references/validation.md`，运行确定性守卫脚本，再执行足以覆盖改动面的最小构建、测试或 Bridge 检查。.NET 构建不能证明 Unity 编译、资源导入、场景完整性或运行时行为。

```powershell
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
```

解决每个错误。审查每个警告，并修复它或说明其合理性。

### 7. 审查并交接

1. 审查 `git diff --check`、`git diff --stat` 和实际补丁。
2. 检查用户数据丢失、失效引用、生命周期泄漏、仅修改生成文件、秘密信息、大型二进制文件和缺失 `.meta` 等问题。
3. 报告行为变更、影响范围、实际执行的验证、剩余风险和未验证项。
4. 检查被跳过、不可用、仅靠推断或被较弱检查替代时，不得宣称其已通过。
5. 提交前使用 `scripts/validate_commit_message.py` 校验提交信息，并遵循 `references/git-delivery.md`。

## 使用脚本

- `scripts/validate_changes.py`：检查变更路径中的 Unity 元数据、生成源码来源、包锁一致性、秘密信息、大型文件、禁止提交的输出和仓库大小写冲突。
- `scripts/validate_commit_message.py`：强制执行 GDK 的 Conventional Commit 标题和正文规范。
- `scripts/bridge_session.py`：在不硬编码 Unity 命令结构的前提下，执行 Agent Bridge 的固定槽位、原子、单通道会话。启动前，主代理仍须阅读已安装的 `AGENT.md`。

使用 `--help` 查看各脚本用法。修改脚本后，运行对应的 `--self-test`。
