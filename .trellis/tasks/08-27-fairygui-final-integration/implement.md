# FairyGUI 多界面发布与最终接入收口实施计划

## 0. Start Gate

- [ ] 用户确认本 PRD、设计和实施摘要。
- [ ] 运行 `task.py start`，确认任务状态为 `in_progress`。
- [ ] 记录 `git status --short`，保留并排除现有 JVM 崩溃日志。
- [ ] 运行 GDK 变更守卫并记录基线。

## 1. Install and Verify the External CLI

- [ ] 在仓库外安装官方 `uv`，记录版本。
- [ ] 克隆 `https://github.com/Wilson520403/fgui-agent-bridge.git` 到独立工具目录并记录提交。
- [ ] 执行 `uv sync --frozen`，定位虚拟环境中的 `fgui-agent`。
- [ ] 只读验证 CLI `status -> ping -> project -> packages`，确认服务端/插件均为兼容的 `0.8.1`/协议 1.x；不运行插件同步或 MCP 配置。

## 2. Repair the UI Source Baseline

- [ ] 回读 GameHot/ET 两份 `UI.xlsx`，确认 104–106 仍未占用。
- [ ] 通过项目 Excel/Agent Bridge 能力在两份源表恢复 103 并新增 104–106；不直接编辑 Luban 输出。
- [ ] 运行现有 Luban 检查与导出，确认两套生成 ID/数据一致且 103–106 完整。
- [ ] 第二次导出无非确定性差异。

## 3. Create the FairyGUI Inventory Components

- [ ] 通过官方 CLI/Editor API 在 `Package1` 创建 `InventoryView`、`InventoryItem`、`ItemDetailWindow`、`InventoryOverlayView`。
- [ ] 在 `InventoryView` 增加 `category` Controller 及 `all/equipment/consumable/quest` 页面，配置分类按钮状态和稳定命名的 GList/文本/按钮成员。
- [ ] 配置 `InventoryItem` 列表项，以及具有透明根、浮动窗口框和稳定命中区域的 `ItemDetailWindow`。
- [ ] 回读文档树、Controller 页面、对象属性和成员类型；保存全部文档。
- [ ] 更新 Editor 工作副本中的 `GDK.json` 映射，执行项目结构检查。

## 4. Publish and Recover the Repository Fact Source

- [ ] 用显式 `fgui-agent` 路径运行 `Publish-GDKDemo.ps1 -PackageName Package1`。
- [ ] 检查发布 JSON、bytes 大小/mtime/hash、官方绑定和组件 manifest。
- [ ] 关闭 FairyGUI Editor 后执行 `Sync-GDKDemoToEditor.ps1 -Mode FromEditor`，确认 `Status=Equal`。
- [ ] 运行 `Test-GDKProject.ps1` 更新仓库 manifest，再用 `-Check` 验证。
- [ ] 生成运行时 manifest 和四个 descriptor；重复生成并确认字节无差异。

## 5. Implement GameHot Presenters

- [ ] 增加背包、详情窗、覆盖层数据对象和 Presenter，使用官方生成绑定与 `InventoryItem` item renderer。
- [ ] 扩展 `HotEntry` 的 Package1 binder/Presenter 显式映射，保留未知映射快速失败。
- [ ] 实现 Controller 分类过滤、物品点击打开 105、多详情窗 token/serial/位置、close/refocus、覆盖层和生命周期计数，不修改共享宿主公共 API。
- [ ] 点击详情窗主体时调用其原始 userData 中的 refocus 动作，断言 `GameEntry.UI.RefocusUIForm` 后该实例位于 Pop 组最上层。
- [ ] 检查按钮解绑、owner 取消、close/recycle 和 shutdown 清理对称。

## 6. Add Final Integration Verification

- [ ] 新增独立 `FairyGUIFinalIntegrationAgent`，实现 Controller/列表、三个详情窗点击置顶、覆盖栈和失败清理 AgentCallable。
- [ ] 临时 descriptor/registry 变体全部使用备份与 `finally` 恢复，测试后运行 `git diff` 确认无夹具残留。
- [ ] 扩展工具测试，覆盖 103 源漂移、104–106 映射、重复/过期 descriptor 和确定性生成。

## 7. Unity and Runtime Validation

- [ ] 完整阅读已安装 Unity Agent Bridge `AGENT.md`，首次执行运行时 `list_commands`。
- [ ] 通过发现到的命令等待导入/编译完成并查询 Error 日志。
- [ ] 调用发布结构、Controller/列表、三个详情窗多实例/点击置顶、覆盖栈、失败清理和现有 103 回归 AgentCallable。
- [ ] 在 16:9、19.5:9、4:3 下验证背包、多个详情窗和覆盖层，保留截图并执行分类、物品、窗口置顶和关闭交互。
- [ ] 运行 100 次 open/close/cancel/cover/reveal/refocus/pool reuse，并在窗体打开时停止 PlayMode 验证 shutdown。
- [ ] 分别刷新并回读 GameHot/ET 资源集合，确认四个 descriptor、manifest、bytes 和包外资源归属 `UI.FairyGUI`。

## 8. Documentation and Quality Gate

- [ ] 更新 `Book/FairyGUI接入.md`，移除 `AFairyUIForm`、UIPanel、专用 prefab 和 POC 状态说明。
- [ ] 运行：

```powershell
pwsh -NoProfile -File ./Tools/FairyGUI/Test-FairyGUITools.ps1
pwsh -NoProfile -File ./Tools/FairyGUI/Test-GDKProject.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode Status
python ./.trellis/scripts/task.py validate .trellis/tasks/08-27-fairygui-final-integration
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check
git diff --stat
```

- [ ] 运行可执行的定向 .NET 构建；若 restore/外部前置阻塞，记录首个错误，不用其替代 Unity 证据。
- [ ] 使用 `trellis-check` 执行全量 PRD/设计/实现一致性检查。

## 9. Finish

- [ ] 将新发现的稳定生命周期/生成契约写入 `.trellis/spec/`，避免重复记录已有规则。
- [ ] 审查生成输入/输出、Unity `.meta`、秘密信息、机器路径、二进制和无关文件。
- [ ] 经用户授权后按 GDK Conventional Commit 提交；不提交 JVM 日志、Bridge 源码、虚拟环境或 `.agent`。
- [ ] 记录验证结果和未验证项，完成并归档当前 Trellis，再评估父任务是否可收口。

## Rollback Points

- 外部 CLI 安装与仓库完全分离，可单独删除。
- Editor 组件在 `FromEditor` 前可用 undo/delete 回退，不影响仓库事实源。
- Excel/GDK 映射与派生输出作为一个逻辑批次回滚并重新生成。
- Presenter/AgentCallable 不改变共享宿主公共 API，可按新增文件和注册映射整体回滚。
