# FairyGUI 单一事实来源与确定性生成

## 目标

以仓库中的 FairyGUI 工程作为唯一版本化事实来源，让开发者与 AI 能安全地编辑 XML、往返 FairyGUI Editor、检查结构契约并得到可复现的发布输入。

## 背景

- 仓库工程位于 `Design/FairyGUI/GDK_FGUI`，外部 FairyGUI Editor 工作副本位于 `D:/Unity/Project/GDK_FGUI`。
- 两份工程当前存在差异；外部 `MainView.xml` 是较新的手工编辑结果，不能被静默覆盖。
- FairyGUI 6.1.4 社区版可通过 GUI 手工发布，但命令行入口受专业版许可限制。
- FairyGUI 自带 C# 绑定生成器，并支持按成员名生成绑定；GDK 不需要维护第二套 C# UI 编译器。

## 需求

- `Design/FairyGUI/GDK_FGUI` 必须是唯一提交到 Git 的 FairyGUI 项目事实来源。
- 同步工具必须支持 `Status`、`ToEditor` 和 `FromEditor` 三种显式模式，并保存上次共同状态的 SHA-256。
- 首次同步时若两端不同，工具必须要求调用者显式指定方向和 `-Initialize`；不能猜测或覆盖。
- 两端相对上次状态都变化且内容不同，工具必须报冲突并在写入前退出。
- 同步工具必须保留仓库 `Publish.json` 的相对路径，在 Editor 副本中将发布与代码生成路径转换为绝对路径。
- 同步中的删除和覆盖必须限定在已解析的项目根目录内，并支持 PowerShell `ShouldProcess` / `-WhatIf`。
- XML 检查必须使用结构化 XML 解析，验证项目、包、资源、组件引用、成员名、控制器/齿轮、关系和稳定业务契约。
- 稳定契约必须存在仓库配置中，至少固定 Package1、MainView 及 `refreshButton`、`statusText`、`checkCountText`。
- manifest 必须从仓库 XML 生成，使用 `/` 路径、稳定排序、LF 文本规范化和 SHA-256，并保证重复生成字节一致。
- `Publish.json` 必须启用 FairyGUI 官方 C# 生成器，按成员名绑定，输出到 GameHot 的 FairyGUI 生成目录。
- 所有脚本必须使用 PowerShell 7 与 .NET 内置 XML、JSON 和 SHA-256 能力，不新增依赖。
- 文档必须说明 AI 编辑、检查、同步、GUI 手工发布和专业版 CLI 发布的完整路径及许可边界。

## 不在范围内

- Unity 场景、预制体、运行时 UIForm 宿主、包管理器、资源规则和 ET 绑定。
- 自制 FairyGUI 二进制发布器或绕过 FairyGUI 专业版许可。
- 删除 UGUI、迁移现有页面或修改 FairyGUI 第三方运行时。
- 自动提交 FairyGUI 官方生成的 C# 绑定；本阶段只配置其输出与验证入口。

## 验收标准

- [x] `Status` 在两端相同、仅仓库变化、仅 Editor 变化和双端冲突时返回明确、可机器判断的状态且不写文件。
- [x] `ToEditor` / `FromEditor` 对首次差异要求 `-Initialize`，对双端冲突拒绝写入，并能在成功同步后建立共同状态。
- [x] 仓库与 Editor 的发布配置分别保持相对路径和绝对路径，路径差异不会造成内容哈希永久漂移。
- [x] `-WhatIf` 不修改项目文件或同步状态，所有目标均位于已解析根目录内。
- [x] 当前仓库 FairyGUI XML 通过检查；缺失引用、重复 ID/名称、无效控制器/关系和稳定契约漂移均能被聚焦测试拒绝。
- [x] 清单包含项目、包、组件、资源引用、业务成员和规范化内容哈希，连续生成两次无字节差异。
- [x] `Publish.json` 启用 `getMemberByName` 的官方 C# 生成配置，Editor 工作副本得到可用绝对输出路径。
- [x] 工具自测、PowerShell AST、JSON/XML 解析、聚焦空白字符检查和 GDK 变更守卫完成；不可用的许可发布验证被明确标为未验证。
- [x] `Book/FairyGUI接入.md` 可让开发者从仓库 XML 开始，安全进入 Editor，并通过 GUI 手工发布资源与 C# 绑定。

## 说明

该子任务是父任务 `08-20-fairygui-gdk-ai-ui-integration` 的第一条可独立验证生产链，不改变 Player 运行时行为。
