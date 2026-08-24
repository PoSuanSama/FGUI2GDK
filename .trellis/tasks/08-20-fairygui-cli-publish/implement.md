# 实施计划

1. 添加 `GDKCliPublish` 插件清单和 `main.js`。
2. 将 `Sync-GDKDemoToEditor.ps1` 扩展为同步项目 `plugins/` 到编辑器镜像。
3. 将 `Publish-GDKDemo.ps1` 改为请求/结果协议与官方 `-script` 调用。
4. 更新 FairyGUI 接入文档，区分默认专业版批处理与项目插件脚本发布。
5. 运行 JSON 解析、JavaScript 语法和 PowerShell AST 语法检查。
6. 使用 `D:\Unity\FairyGUI\main\FairyGUI-Editor.exe` 发布到唯一临时目录。
7. 验证成功产物；若许可门禁拒绝，记录准确日志和未通过项。
8. 运行 GDK 变更守卫、`git diff --check`、聚焦差异/统计，并检查未触碰用户无关变更。

## 验证命令

```powershell
Get-Content -Raw Design/FairyGUI/GDK_FGUI/plugins/GDKCliPublish/package.json | ConvertFrom-Json
node --check Design/FairyGUI/GDK_FGUI/plugins/GDKCliPublish/main.js
[System.Management.Automation.Language.Parser]::ParseFile(...)
./Tools/FairyGUI/Publish-GDKDemo.ps1 -EditorPath D:/Unity/FairyGUI/main/FairyGUI-Editor.exe -OutputPath <temp>
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check
git diff --stat
```

## 回滚点

- 插件加载失败：删除新增插件目录，现有 FairyGUI 项目仍可手工发布。
- 包装脚本协议失败：还原单个 PowerShell 文件，不影响 XML 或发布设置。
- 发布运行产生的验证输出只写临时目录，不覆盖版本化 Unity 资源。

## 评审结果

- `package.json` 解析、`main.js` Node 语法检查和 PowerShell AST 解析通过。
- Node 模拟测试覆盖发布成功、包不存在、`Run()` 拒绝和 `isSuccess=false`，每条路径均只回调一次。
- FairyGUI 6.1.4 社区版真实运行在约 0.96 秒内识别专业版许可门禁并保留日志；未误伤既有进程。
- GDK 变更守卫为 0 错误、386 个任务范围外警告；四个任务文件的聚焦空白字符/差异检查通过。
- 全仓 `git diff --check` 仍因任务范围外的 `AGENTS.md`、`CLAUDE.md` 文件末尾空行失败。
- 真实成功发布与非空 `Package1_fui.bytes` 仍需在持有 FairyGUI 专业版许可的环境验证。
