# FairyGUI 自定义命令行发布插件设计

## 范围边界

行为缺口位于 FairyGUI 外部发布入口：现有脚本调用受限的默认批处理发布，而官方插件系统已经提供
脚本分发和 `PublishHandler`。本任务在 FairyGUI 项目扩展点内增加发布适配，不修改编辑器或 Unity
运行时。

预计修改：

- `Design/FairyGUI/GDK_FGUI/plugins/GDKCliPublish/package.json`：声明项目插件入口。
- `Design/FairyGUI/GDK_FGUI/plugins/GDKCliPublish/main.js`：实现官方脚本发布函数和结果协议。
- `Tools/FairyGUI/Sync-GDKDemoToEditor.ps1`：将项目插件同步到实际打开的编辑器镜像工程。
- `Tools/FairyGUI/Publish-GDKDemo.ps1`：生成请求、启动编辑器、等待并校验结果。
- `Book/FairyGUI接入.md`：更新可复现的使用与诊断流程。

不修改 XML、Publish.json、Unity 资源和 FairyGUI 安装目录。

仓库 FairyGUI 工程与 `D:\Unity\Project\GDK_FGUI` 编辑器镜像是两个目录。命令行发布可以直接使用
仓库工程；要让编辑器“插件”页签显示插件，必须把 `plugins/` 同步到实际打开的镜像工程。同步动作不
删除镜像中的其他文件；若 XML 或发布设置存在差异，不能为了显示插件而强制覆盖它们。

## 数据流

```text
PowerShell 参数
  -> 临时 request.json
  -> FairyGUI -p <project> -script gdkPublishPackage -scriptArg <request.json>
  -> 项目插件 main.js
  -> App.project.GetPackageByName
  -> PublishHandler.Run
  -> 临时 result.json + Editor 日志
  -> PowerShell 验证结果和 Package1_fui.bytes
```

## 请求契约

请求 JSON 包含：

- `packageName`：必填，当前默认 `Package1`。
- `outputPath`：必填，规范化后的绝对目录。
- `resultPath`：必填，插件写回结果的绝对文件路径。

协议文件位于系统临时目录的唯一子目录。PowerShell 使用 UTF-8 无 BOM 写入，参数中只传协议文件
路径，避免 JSON 转义和命令行拆分问题。

## 结果契约

结果 JSON 至少包含：

- `success`：`PublishHandler.isSuccess` 与异步完成状态共同决定。
- `packageName`、`exportPath`：诊断上下文。
- `message`：失败原因或完成摘要。

插件无论成功或失败都先尝试写结果，再调用 FairyGUI 提供的完成回调。PowerShell 将缺失结果文件、
`success=false`、非零退出、超时和缺失描述文件分别报告。

## 进程处理

PowerShell 使用 `System.Diagnostics.ProcessStartInfo.ArgumentList`，不拼接 Shell 命令。进程隐藏启动，
轮询只负责超时和已知许可错误的早期诊断。超时后终止进程树。日志保留；唯一协议目录在结束时清理。

## 兼容性与回滚

- 插件使用 6.1.4 本地 `editor.d.ts` 已声明的 API，不增加依赖或编译步骤。
- 使用普通 `main.js`，避免要求 Node/npm/TypeScript 工具链。
- 删除插件目录并还原 PowerShell 脚本即可回滚；项目 XML 和 Unity 产物格式不变。
- 社区版若拒绝脚本入口，错误由包装脚本明确暴露，不采用其他入口规避。
