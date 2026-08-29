# FairyGUI 事实来源与生成设计

## 范围边界

本阶段只修改：

- `Design/FairyGUI/GDK_FGUI`
- `Tools/FairyGUI`
- `Book/FairyGUI接入.md`

仓库工程是唯一 Git 事实来源。`D:/Unity/Project/GDK_FGUI` 是机器本地工作副本，只保存 `.gdk-sync-state.json`，不反向成为构建输入。

## 项目契约

`settings/GDK.json` 记录 GDK 需要稳定的语义，不复制布局：项目 ID、包 ID/名称、入口组件 ID/名称/导出状态，以及入口组件的必需业务成员名与类型。

布局、颜色、文本和非业务节点仍只存在于 FairyGUI XML 中。这样 AI 可以自由编辑视觉内容，而稳定 ID 或业务绑定漂移会在发布前失败。

## 同步协议

`Sync-GDKDemoToEditor.ps1` 接收互斥模式 `Status`、`ToEditor` 或 `FromEditor`。Editor 根下的 `.gdk-sync-state.json` 保存协议版本和上次同步的规范化项目哈希。

计算哈希时：

1. 枚举项目受管文件并使用 `/` 相对路径稳定排序。
2. 排除 `.gdk-sync-state.json` 与生成目录。
3. XML 和 JSON 文本统一为 LF。
4. 将 `Publish.json` 的发布路径与代码路径规范化为逻辑占位符。
5. 对路径、分隔符、字节长度和内容逐项写入 SHA-256。

状态判断：

| 状态标识 | 含义 | 允许操作 |
| --- | --- | --- |
| equal | 两端规范化哈希相同 | 任一方向可刷新状态 |
| repository-changed | 仅仓库偏离上次哈希 | `ToEditor` |
| editor-changed | 仅 Editor 偏离上次哈希 | `FromEditor` |
| conflict | 两端都偏离且内容不同 | 拒绝写入 |
| uninitialized-different | 无状态文件且两端不同 | 仅显式方向加 `-Initialize` |

写入采用先校验、后复制、最后原子替换状态文件的顺序。同步是项目镜像操作，删除只针对受管文件，且每个写入/删除经过 `ShouldProcess`。所有解析后的目标路径必须仍位于对应项目根内。冲突、错误方向和预检失败保证零写入；单个文件使用原子替换，但意外磁盘/权限故障不提供多文件事务回滚，失败后必须重新检查 `Status` 并收敛。

仓库 `Publish.json` 使用相对 Unity 输出路径。同步到 Editor 时转换为绝对 Unity 资源和 GameHot 代码目录；从 Editor 导入时转换回仓库相对路径，因此机器路径不会写入 Git。

## 检查与清单

`Test-GDKProject.ps1` 使用 .NET XML API 读取 `assets/*/package.xml` 与组件 XML，验证：

- 项目/包/资源 ID 和成员名唯一；
- 包声明的组件文件存在；
- `src` 和可选 `pkg` 指向已知资源；
- 包依赖图无环；
- 齿轮引用的控制器存在；
- 关系的非空目标指向当前组件成员；
- `settings/GDK.json` 的稳定包、入口和业务成员契约匹配。

同一解析模型生成 `generated/GDKFairyManifest.json`。对象属性顺序固定，包/组件/成员按稳定键排序，文本统一 LF，JSON 使用 UTF-8 无 BOM 和结尾换行。`-Check` 模式只比较预期字节，不改文件，用于 CI 过期输出检查。

## 绑定生成

不新增绑定生成器。`settings/Publish.json` 配置 FairyGUI 6.1.4 官方 C# generator：`allowGenCode=true`、`getMemberByName=true`、`ignoreNoname=true`、`classNamePrefix=UI_`、`memberNamePrefix=m_`、`packageName=Game.Hot.FairyGUI`，并将 `codePath` 指向 `Unity/Assets/Scripts/Game/Hot/Code/Generate/FairyGUI`。

仓库保持相对 `codePath`，Editor 副本由同步工具改成绝对路径。`codeType` 保持 FairyGUI Unity 项目当前默认值，避免猜测未公开枚举。

## 兼容性与回滚

- 社区版继续在 GUI 中发布；专业版 CLI 子任务复用同一项目配置。
- 本阶段不修改已发布字节数据或运行时，回滚只需恢复仓库项目、工具和文档。
- 冲突和预检失败不写任何文件；意外 I/O 故障可能留下部分文件，需通过 `Status` 重新收敛。删除状态文件可回到必须显式 `-Initialize` 的安全状态。
