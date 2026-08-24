# FairyGUI 自定义命令行发布插件

## 目标

让 AI 或开发脚本无需模拟鼠标点击，即可通过 FairyGUI 6.1.4 官方公开的插件脚本入口发布
GDK 的 `Package1`，并获得可机器判断的成功、失败、日志和产物证据。

## 背景

- FairyGUI 官方文档从 6.0 起公开 `-script`、`-scriptArg` 和插件导出函数调用方式。
- 官方示例允许插件构造 `CS.FairyEditor.PublishHandler` 并等待 `Run()`。
- 当前 `Tools/FairyGUI/Publish-GDKDemo.ps1` 使用默认 `-batchmode -b Package1`，社区版会报告
  `only avaliable in professional FairyGUI-Editor`。
- 当前事实来源是 `Design/FairyGUI/GDK_FGUI/`，发布产物进入
  `Unity/Assets/Res/UI/FairyGUI/`。

## 需求

- 在 FairyGUI 项目的 `plugins/` 下提供符合官方格式的 JavaScript/Puerts 插件。
- 插件导出唯一命名的命令行函数，读取请求文件，按包名找到包并调用
  `PublishHandler.Run()`。
- 插件支持覆盖发布输出目录，默认发布主干，并始终调用完成回调，避免编辑器悬挂。
- 插件将成功状态、错误信息和发布上下文写入结果文件，不能只依赖进程退出码。
- PowerShell 包装脚本使用参数列表启动进程，正确处理空格和中文路径。
- 包装脚本提供超时、进程终止、日志路径、结果文件和描述文件校验。
- 默认保持现有 `Package1`、项目路径和 Unity 输出路径，允许调用方覆盖编辑器、项目、包、
  输出、日志和超时时间。
- 同步更新 `Book/FairyGUI接入.md`，说明插件位置、调用命令、成功证据和故障诊断。
- 使用本机 FairyGUI 6.1.4 对临时输出目录执行一次真实发布验证。

## 验收标准

- [ ] `package.json` 和 `main.js` 能被 FairyGUI 项目插件系统发现。
- [x] `Publish-GDKDemo.ps1` 不再依赖 GUI 点击，使用 `-script` 调用插件函数。
- [ ] 成功时临时输出目录生成非空的 `Package1_fui.bytes`，脚本退出码为 0。
- [ ] 包不存在、插件未加载、发布失败或超时时，脚本以非 0 退出并给出日志位置和可操作错误。
- [x] 请求与结果协议不会因路径含空格或中文而破坏参数边界。
- [x] PowerShell、JSON 和 JavaScript 通过静态语法检查。
- [x] GDK 变更守卫、`git diff --check` 和聚焦差异审查完成。

验收说明：FairyGUI 6.1.4 社区版会在项目插件加载前由 `CheckProLicense` 拒绝批处理入口，
因此插件发现、真实成功发布和依赖该入口的完整失败矩阵仍未在持有专业版许可的环境验证。
全仓 `git diff --check` 已执行，其失败仅来自任务范围外的 `AGENTS.md` 和 `CLAUDE.md` EOF 空行；
四个任务文件的聚焦检查通过。

## 不在范围内

- 不增加文件监听、定时轮询或屏幕点击自动化。
- 不修改 FairyGUI XML 组件内容、Unity 场景、预制体、C# 运行时代码或生成产物事实来源。
- 不扩展到多包并行发布、分支发布、CI 服务部署或自动提交。

## 延后处理的风险

官方文档证明插件脚本流程存在，但未明确社区版 6.1.4 是否允许该入口。
