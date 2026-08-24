# FairyGUI 事实来源与生成契约

## 1. 适用范围与触发条件

修改 `Design/FairyGUI/GDK_FGUI` 下的 FairyGUI 事实来源、`Tools/FairyGUI` 下的同步/检查工具，
或生成的 `GDKFairyManifest.json` 时，必须遵守本契约。

仓库中的 FairyGUI 工程是唯一版本化事实来源。`D:\Unity\Project\GDK_FGUI` 之类的外部工程只是
Editor 工作副本，必须先通过同步协议收敛，才能发布或评审。

## 2. 调用签名

```powershell
./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 `
  [-Mode Status|ToEditor|FromEditor] [-SourceProjectPath <directory>] `
  [-EditorProjectPath <directory>] [-OutputPath <directory>] `
  [-CodeOutputPath <directory>] [-Initialize] [-WhatIf]

./Tools/FairyGUI/Test-GDKProject.ps1 `
  [-ProjectPath <directory>] [-ManifestPath <file>] [-Check]
```

`Test-FairyGUITools.ps1` 是不依赖第三方测试框架的聚焦回归入口。

## 3. 契约

- `settings/GDK.json` 架构版本 1 持有稳定的项目/包/入口/成员 ID 和类型；它不重复布局、文本、
  颜色或其他视觉 XML。
- `.gdk-sync-state.json` 只存在于 Editor 工作副本，包含 `schemaVersion`、`projectId` 和
  `lastCommonHash`。
- 同步哈希输入包含 `.fairy`、`assets/**` 和 `settings/Publish.json`；计算时规范化文本换行，
  并把机器相关的发布/代码路径映射为逻辑占位符。
- 仓库 `Publish.json` 保持相对输出路径；Editor 副本使用绝对输出路径。
- FairyGUI 官方 C# 生成保持启用，并设置 `getMemberByName=true`；不得构建第二个绑定编译器。
- 清单路径使用 `/`，集合稳定排序，哈希基于规范化的 LF 文本和 SHA-256，输出采用不带 BOM 的
  UTF-8，并以一个 LF 结尾。

## 4. 验证与错误矩阵

| 条件 | 必需行为 |
| --- | --- |
| `Status` | 返回分类和哈希；绝不写入 |
| 没有状态文件且两端工程不同 | 要求显式方向并加 `-Initialize` |
| 只有仓库变化 | 允许 `ToEditor`；拒绝 `FromEditor` |
| 只有 Editor 变化 | 允许 `FromEditor`；拒绝 `ToEditor` |
| 两端都变化且内容不同 | 返回 `Conflict`；不写任何内容 |
| FairyGUI Editor 已打开 | 拒绝实际同步；允许 `-WhatIf` |
| 辅助配置/发布配置无效 | 写入前在预检阶段失败 |
| 路径解析到工程根之外 | 读/写/删除前拒绝 |
| XML/引用/控制器/关系/契约无效 | 检查失败并给出可操作消息 |
| 清单字节不同，包括 BOM/CRLF | `-Check` 失败且不重写文件 |

预检和冲突拒绝必须保证零写入。每个目标文件都使用原子替换，但意外磁盘或权限故障可能让多文件同步
只应用一部分；此时重新运行 `Status`、检查两份副本并再次同步。不得宣称存在多文件事务。

## 5. 正常、基础与失败用例

- 正常：`FromEditor -Initialize` 导入有意选定的 Editor 布局，重新应用仓库发布不变量，记录共同哈希，
  检查通过，且 `Status` 返回 `Equal`。
- 基础：仓库路径是相对路径，Editor 路径是绝对路径，但路径规范化产生相同的工程哈希。
- 失败：共同状态建立后，两份 XML 都发生变化；脚本拒绝任一方向，且不修改 XML、发布设置、插件或状态。

## 6. 必需测试

- 运行 `Test-FairyGUITools.ps1`；断言相同/未初始化/单边变化/冲突/错误方向、`-WhatIf`、预检零写入、
  路径逃逸、插件与 `.agent` sentinel 零写入和发布路径转换。
- 断言检查工具会拒绝缺失引用、只有 `pkg` 没有 `src`、重复 ID/名称、无效控制器/关系、包依赖环、
  组件路径逃逸和稳定契约漂移。
- 生成两次清单并断言字节完全相等；断言 `-Check` 会拒绝内容、BOM 和 CRLF 漂移。
- 运行 PowerShell AST 解析、JSON/XML 解析、聚焦空白字符检查和 GDK 变更守卫。

## 7. 错误与正确做法

错误：复制看起来更新的工程、在比较前规范化已经生成的清单，或把机器绝对路径写入仓库
`Publish.json`。

正确：检查 `Status`，选择显式且受允许的方向，写入前验证，逐字节比较清单，并把机器路径转换限制在
同步边界。
