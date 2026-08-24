# FairyGUI 事实来源与生成实施计划

## 变更边界

最小行为差距是当前双副本可被单向强制覆盖，且 XML 到发布产物之间没有可执行契约或确定性索引。行为归属在仓库 FairyGUI 源工程和 `Tools/FairyGUI`，不是 Unity 运行时。

预期修改：

- `Design/FairyGUI/GDK_FGUI/settings/GDK.json`：稳定 GDK 语义契约。
- `Design/FairyGUI/GDK_FGUI/settings/Publish.json`：官方 C# 绑定生成配置。
- `Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json`：确定性派生索引。
- `Tools/FairyGUI/Sync-GDKDemoToEditor.ps1`：有状态、冲突安全的双向同步。
- `Tools/FairyGUI/Test-GDKProject.ps1`：XML 检查、契约验证和清单生成/检查。
- `Tools/FairyGUI/Test-FairyGUITools.ps1`：无第三方框架的聚焦回归测试。
- `Book/FairyGUI接入.md`：开发者与 AI 操作流程。

明确不修改 Unity 场景、预制体、运行时宿主、资源规则、ET、UGUI 清理或第三方代码。

## 实施步骤

1. 为当前 Package1/MainView 建立最小稳定契约，并配置官方 C# 按名称绑定输出。
2. 重写同步工具：路径边界、规范化哈希、状态分类、三种模式、首次初始化、冲突保护和 `ShouldProcess`。
3. 实现结构化 XML 检查和稳定清单的生成/`-Check` 模式。
4. 建立临时目录自测，覆盖同步状态矩阵、路径规范化、检查失败矩阵和清单幂等。
5. 先对真实双副本执行只读 `Status`；确认状态后用 `FromEditor -Initialize` 导入较新的手工 XML 并建立共同状态。
6. 生成清单，连续生成两次并用 `-Check` 验证无过期/字节漂移。
7. 更新文档，说明仓库编辑、同步、GUI 发布、生成绑定和专业版 CLI 边界。
8. 执行质量门禁，审查聚焦 diff；不修改无关工作树文件。

## 验证

```powershell
pwsh -NoProfile -File Tools/FairyGUI/Test-FairyGUITools.ps1
pwsh -NoProfile -File Tools/FairyGUI/Test-GDKProject.ps1 -ProjectPath Design/FairyGUI/GDK_FGUI -Check
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check -- Design/FairyGUI/GDK_FGUI Tools/FairyGUI Book/FairyGUI接入.md .trellis/tasks/08-20-fairygui-source-generation
```

FairyGUI 真实 CLI 发布只在专业版环境验证；社区版许可阻塞必须如实记录。GUI 发布由用户手工执行。

## 验收证据（2026-08-21）

- 在确认 FairyGUI Editor 未运行、审查差异只有 `MainView.step2Title` 文本更新后，执行真实
  `FromEditor`；随后 `Status` 返回 `Equal`，仓库与 Editor 哈希及共同哈希均为
  `3f0365a1da792518b33b111ffb3d05a1d632f676ccaa692830d795051e9aee71`。
- `Test-FairyGUITools.ps1` 通过 78 项断言，覆盖同步状态矩阵、错误方向、首次初始化、冲突零写入、
  `-WhatIf`、发布路径转换、预检失败、结构检查失败和 manifest 字节漂移。
- manifest 连续生成两次的 SHA-256 均为
  `2efef55b5745275412f2bf3579b52805907847ee6b91d2c0cf49da161f8d4f21`；
  `Test-GDKProject.ps1 -Check` 通过，源哈希为
  `6905f07d0d2b85204e1eb0aa4800f54c49c3a4d7fa74d8b54f3c534bdc8313f2`。
- 3 个 PowerShell 脚本通过 AST 解析；3 个 JSON 和 4 个 XML/Fairy 文件通过结构解析；任务文件均为
  UTF-8 无 BOM、LF 且以 LF 结尾，聚焦 `git diff --check` 通过。
- GDK 变更守卫检查 643 个工作区变更路径，结果为 0 个错误、386 个警告；警告均来自本子任务范围外
  已存在的生成文件、第三方 FairyGUI SDK、包锁和 Unity 资源变更，本子任务未扩大或修改这些路径。
- FairyGUI 6.1.4 Community 的专业版 CLI 发布仍不可用，本子任务只验证并记录许可证边界；GUI 手工发布
  流程和官方 C# 绑定输出路径已写入 `Book/FairyGUI接入.md`。

## 回滚点

- 真实双副本同步前：仅工具与配置变更，可直接恢复该子任务文件。
- `FromEditor -Initialize` 前：先保留只读状态证据；冲突或检查失败时不导入。
- 清单是派生输出，必须与契约和 XML 同批回滚，不能单独修补。
