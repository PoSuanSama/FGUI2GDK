# Git、提交与拉取请求规范

## 有意识地控制变更范围

- 除非用户明确要求，否则不要创建分支、提交、推送或 PR。
- 为 Codex 工作创建分支时，除非用户指定其他名称，否则使用 `codex/<type>-<short-kebab-slug>`。
- 每个提交只包含一个逻辑关注点。当机械生成/重构和行为变更都能独立构建与审查时，将它们拆开。
- 在同一逻辑提交中包含源码与生成输出、资源与 `.meta`、迁移与读写支持。
- 绝不包含无关的用户变更。明确暂存具体路径，并检查暂存区差异。

## 使用 GDK Conventional Commits

提交标题格式：

```text
<type>(<scope>): <简短中文动宾描述>
```

允许的 type：

| Type | 用途 |
| --- | --- |
| `feat` | 新增用户或开发者可见的能力 |
| `fix` | 修复缺陷 |
| `refactor` | 不改变行为的结构调整 |
| `perf` | 有测量证据的性能改进 |
| `test` | 仅测试变更 |
| `docs` | 仅文档变更 |
| `build` | 构建、包、生成器或依赖系统 |
| `ci` | CI 自动化 |
| `chore` | 不改变产品行为的维护工作 |
| `revert` | 还原之前的提交 |

使用稳定的小写 scope，例如 `unity`、`et`、`hot`、`dotnet`、`ui`、`entity`、`luban`、`proto`、`assets`、`bridge`、`analyzer`、`tools` 或 `docs`。归属确有需要时，可以添加含义精确的新 scope。

示例：

```text
fix(et): 修复重连后会话状态未清理
feat(ui): 添加角色详情弹窗
build(bridge): 升级 UnityAgentBridge 至 2.0.2
docs(gdk): 补充资源导入与验证流程
```

规则：

- 标题不超过 72 个字符，末尾不加标点。
- 描述结果，不描述编辑动作；避免 `优化代码`、`更新文件` 等含糊标题。
- 当动机、权衡、迁移或行为不明显时，空一行后添加正文。
- 普通正文行不超过 100 个字符；URL 可超出此限制。
- 破坏性变更需在 `:` 前添加 `!`，并添加包含迁移说明的 `BREAKING CHANGE:` 页脚。
- 问题链接放在页脚，不要放在标题中。

提交前验证：

```powershell
python .agents/skills/gdk-development-workflow/scripts/validate_commit_message.py "fix(et): 修复重连后会话状态未清理"
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py --staged
git diff --cached --check
git diff --cached --stat
```

## 准备便于审查的拉取请求

PR 描述必须说明：

1. 变更原因，以及用户或开发者可见的结果。
2. 发生变更的归属模块、资源、配置、生成输出和迁移。
3. 实际执行的自动化验证及 Unity/Bridge 验证。
4. 兼容性、发布、性能、安全和资源引用风险。
5. 视觉变更提供截图，性能工作提供前后对比证据。
6. 明确列出未验证的平台或流程。

在必要检查完成或已明确记录阻塞原因前，保持 PR 为草稿。不要隐藏生成差异；解释其来源，并优先审查有意义的输入变更。

## 交接前审查

- 检查 `git status --short`、`git diff --check` 和完整补丁。
- 确认没有加入秘密信息、私有数据、本地路径、缓存、构建输出或意外二进制文件。
- 确认每个新增、移动或删除的 Unity 资源都有正确的 `.meta` 变更。
- 确认文档和测试与行为一致。
- 确认提交/PR 的声明没有超出实际收集到的证据。
