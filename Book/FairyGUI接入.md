# FairyGUI 接入

目标架构以 FairyGUI 作为 GDK 唯一 Player UI 后端，继续保留 GDK/UGF/ET 的 UI ID、分组、层级、生命周期和配套服务。当前仓库中的运行演示仍是验证阶段 POC；在后续运行时宿主子任务完成前，它还不能代表“零 UGUI”最终状态。

FairyGUI XML 工程是 AI UI 流程的唯一版本化事实来源。`D:\Unity\Project\GDK_FGUI` 只是本机 FairyGUI Editor 工作副本，不能作为构建输入或长期保留未导入改动。

## 目录归属

| 内容 | 路径 |
| --- | --- |
| FairyGUI XML 事实来源 | `Design/FairyGUI/GDK_FGUI/` |
| 稳定业务契约 | `Design/FairyGUI/GDK_FGUI/settings/GDK.json` |
| 确定性 manifest | `Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json` |
| 外部 Editor Bridge | `D:\Unity\Project\GDK_FGUI\plugins\agent-bridge`（由 Wilson 仓库部署） |
| 编辑器工程同步脚本 | `Tools/FairyGUI/Sync-GDKDemoToEditor.ps1` |
| XML/manifest 检查 | `Tools/FairyGUI/Test-GDKProject.ps1` |
| 工具回归测试 | `Tools/FairyGUI/Test-FairyGUITools.ps1` |
| 发布脚本 | `Tools/FairyGUI/Publish-GDKDemo.ps1` |
| 发布产物 | `Unity/Assets/Res/UI/FairyGUI/` |
| GDK 适配层 | `Unity/Assets/Scripts/Game/UI/FairyGUI/AFairyUIForm.cs` |
| FairyGUI 包管理 | `Unity/Assets/Scripts/Game/UI/FairyGUI/FairyPackageManager.cs` |
| GameHot Demo | `Unity/Assets/Scripts/Game/Hot/Code/UI/FairyDemoForm.cs` |
| UIForm 宿主 | `Unity/Assets/Res/UI/UIForm/Hot/FairyDemoForm.prefab` |
| 独立演示场景 | `Unity/Assets/FairyGUIDemo.unity` |

SDK 固定为 FairyGUI Unity SDK `5.2.0`，上游提交为 `7f8555dd163bd17315f77b64907e07e735cf0ed0`，许可证为 MIT。导入内容位于 `Unity/Assets/Scripts/Library/FairyGUI/`；运行时 asmdef 增加了 `Unity.InputSystem` 引用和 `FAIRYGUI_INPUT_SYSTEM` 版本宏定义，以匹配本工程的新输入系统配置。

## AI 编辑与 XML 契约

AI 直接修改仓库 `assets/*/*.xml`。布局、颜色、文本和非业务节点可以自由调整；包、入口组件和业务成员由 `settings/GDK.json` 固定：

| 类型 | 名称 |
| --- | --- |
| Package | `Package1` |
| Component | `MainView` |
| Button | `refreshButton` |
| Text | `statusText` |
| Text | `checkCountText` |

发布前执行：

```powershell
./Tools/FairyGUI/Test-GDKProject.ps1
./Tools/FairyGUI/Test-GDKProject.ps1 -Check
```

第一次命令完成结构检查并更新清单；第二次只读检查清单是否过期。检查覆盖重复 ID/成员名、缺失 `src/pkg`、包依赖环、无效控制器/齿轮、关系目标和稳定业务契约。改动稳定契约必须同时更新业务使用方，并在后续运行时子任务中完成 Unity 编译和运行验证。

`settings/Publish.json` 使用 FairyGUI 官方 C# generator，不维护 GDK 私有绑定编译器。当前配置为：

| 设置 | 值 |
| --- | --- |
| 生成代码 | 开启 |
| 按成员名获取 | 开启 |
| 忽略无名称节点 | 开启 |
| 类名前缀 | `UI_` |
| 成员前缀 | `m_` |
| 命名空间前缀 | `Game.Hot.FairyGUI` |
| 仓库代码路径 | `../../../Unity/Assets/Scripts/Game/Hot/Code/Generate/FairyGUI` |

Unity 项目类型会选择 FairyGUI 自带 `GenCode_CSharp`，所以 `codeType` 保持编辑器默认空值。发布 `Package1` 后，官方生成器会在代码路径下创建 `Package1/`，生成类使用 `Game.Hot.FairyGUI.Package1` 命名空间和 `GetChild("成员名")` 绑定。

## 仓库与 Editor 同步

先查看状态；`Status` 永远不写文件：

```powershell
./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode Status
```

常见状态及处理：

| 状态 | 含义 | 操作 |
| --- | --- | --- |
| `Equal` | 两端内容相同 | 可继续编辑或发布 |
| `RepositoryChanged` | 只有仓库变化 | `-Mode ToEditor` |
| `EditorChanged` | 只有 Editor 变化 | `-Mode FromEditor` |
| `UninitializedDifferent` | 首次建立状态且两端不同 | 明确方向并加 `-Initialize` |
| `Conflict` | 两端都在上次同步后变化 | 手工合并，脚本不会覆盖 |

仓库修改完成后同步到 Editor：

```powershell
./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode ToEditor
```

在 FairyGUI Editor 中保存的修改需要导回仓库：

```powershell
./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode FromEditor
```

首次已有差异时必须明确保留哪一侧。例如当前外部工程包含较新的手工布局，应先保存并关闭 FairyGUI Editor，再执行：

```powershell
./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode FromEditor -Initialize
```

任何写同步都要求 FairyGUI Editor 已关闭，避免覆盖未保存内容。预演可在 Editor 打开时执行且不会修改 XML、设置或状态文件：

```powershell
./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode ToEditor -WhatIf
```

同步状态保存在 Editor 工程的 `.gdk-sync-state.json`。哈希计算会统一换行，并把仓库相对发布路径与 Editor 绝对路径映射为逻辑占位符。`plugins/` 与 `.agent/` 不属于同步集合，脚本不会创建、覆盖或删除任何 Editor 插件或 Bridge 运行时数据。

执行写同步前，脚本会先把待导入一侧放入临时沙箱，应用仓库稳定契约与相对发布路径，再运行完整的 XML、资源引用和业务成员检查。预检失败不会修改仓库工程、Editor 工程或同步状态；应先根据错误修复源 XML，再重新检查 `Status`。单文件写入使用原子替换，但磁盘或权限故障仍可能造成多文件只完成一部分，此时必须重新运行 `Status` 并检查两侧内容，不能把同步视为跨文件事务。

## 命令行发布

发布由已部署的 [FGUI Agent Bridge](https://github.com/Wilson520403/fgui-agent-bridge) 执行。
GDK 只调用外部 `fgui-agent` CLI，不复制或管理其插件、Python、MCP 注册和 `.agent` 队列。
先确认 FairyGUI Editor 正在运行且 Bridge 心跳新鲜，再运行：

```powershell
$env:FGUI_AGENT_EXE = 'C:\tools\fgui-agent.exe'
./Tools/FairyGUI/Publish-GDKDemo.ps1 -PackageName Package1
```

也可以覆盖包、工程、输出、日志和超时参数：

```powershell
./Tools/FairyGUI/Publish-GDKDemo.ps1 `
  -AgentExecutable 'C:\tools\fgui-agent.exe' `
  -EditorProjectPath 'D:\Unity\Project\GDK_FGUI' `
  -PackageName Package1 `
  -OutputPath 'D:\Temp\GDK FairyGUI Output' `
  -TimeoutSeconds 120
```

脚本先执行 XML lint、manifest `-Check` 和 `Sync ... -Mode Status`，然后按顺序调用
`status`、`ping`、`project`、`packages`，最后只执行：

```text
fgui-agent --project D:\Unity\Project\GDK_FGUI publish --scope packages --package Package1
```

成功必须同时满足：

- Bridge 状态为 online、版本匹配、协议为 1.x 且声明 `publish` capability；
- CLI 返回 JSON 且 `success` 为 `true`；
- 输出目录存在非空的 `Package1_fui.bytes`。

最终 JSON 证据包含产物绝对路径、大小、UTC mtime，以及发布前后 SHA-256。CLI 缺失、状态过期、
包名不匹配、非零退出、JSON 无效或产物无效都会失败，不回退到 active/all 发布。

默认产物为：

```text
Unity/Assets/Res/UI/FairyGUI/Package1_fui.bytes
```

Editor 离线或心跳过期时只报告首个可操作错误；重新打开 FairyGUI Editor 后再重试。Wilson 0.8.1
不提供截图 capability，视觉证据由 Editor 预览和后续 Unity Agent Bridge 流程负责。

## 手工发布

先按上一节确认 `Status=Equal`，然后打开：

```text
D:\Unity\Project\GDK_FGUI\GDK_FGUI.fairy
```

`项目/发布设置` 已预配置为：

| 设置 | 值 |
| --- | --- |
| 发布路径 | `D:\GitHubProject\GDK\Unity\Assets\Res\UI\FairyGUI` |
| 扩展名 | `bytes` |
| 包格式 | 使用二进制格式 |
| 描述文件压缩 | 开启 |
| 最大纹理尺寸 | `2048`（编辑器默认值） |
| 超出后自动分页 | 开启 |
| 尺寸选项 | 2 的次方幂 |
| 允许旋转 | 关闭 |
| 裁剪图片边缘空白 | 开启 |
| 发布代码 | 开启 |
| 代码路径 | `D:\GitHubProject\GDK\Unity\Assets\Scripts\Game\Hot\Code\Generate\FairyGUI` |
| 获取成员方式 | 按名称 |

选择 `Package1` 后点击“发布”。成功标志是同时生成非空的 `Package1_fui.bytes` 和官方 C# 绑定目录。不要把 `.objs`、Editor 缓存、临时文件或 `.gdk-sync-state.json` 加入仓库。

发布后若继续在 FairyGUI Editor 修改界面，关闭 Editor 并执行 `FromEditor`，再重新生成清单；否则 Git 中的仓库工程不是最新事实来源。

## GDK 配置链路

Demo 使用 GameHot 模式。先通过 `Game/Define Symbol/Add UNITY_GAMEHOT` 切换模块，再按标准流程执行 Luban 校验与导出。UI 源行位于 `Design/Excel/GameHot/Datas/Game/UI.xlsx`，ID 为 `103`，资源名为 `Hot/FairyDemoForm`。

运行时链路如下：

```text
MainView.xml
  -> FairyGUI publish
  -> Package1_fui.bytes
  -> FairyPackageManager + ResourceComponent
  -> UIPackage + UIPanel lease
  -> UGF UIForm lifecycle
```

### 运行时包管理

`FairyPackageManager` 是 GDK 与 `UIPackage` 之间的唯一运行时边界。窗体打开时通过 `ResourceComponent` 加载
`<Package>_fui.bytes`，同一包的并发获取共享一次注册；每个 `AFairyUIForm` 持有一个租约，关闭或打开失败时释放租约。
最后一个租约释放后才会调用 `UIPackage.RemovePackage`，随后卸载描述符和通过 FairyGUI 外部资源回调加载的图集、字体、声音等资源。

`AFairyUIForm` 创建的 `UIPanel` 逻辑归属于对应 GF UIForm，层级保持在 `UI Group/UIForm` 子树中，并由 UIForm 持有和销毁。
UIForm 与 `UIPanel` 之间的 `FairyGUI Transform Isolation` 节点会抵消 GF Canvas 传递的世界位置、旋转和缩放，避免 FairyGUI 渲染对象离开 Stage Camera 视锥。

外部资源回调只负责把请求交给 GDK `ResourceComponent`，并以 `DestroyMethod.None` 交给 FairyGUI；资源所有权仍由包管理器持有和
释放。新增外部资源必须同步加入 GDK 资源规则并通过运行时加载验证。

## 运行演示

在 Unity 中打开 `Assets/FairyGUIDemo.unity` 并进入播放模式。该场景保留了 `Launcher` 的完整 GDK 启动链，进入 `ProcedureMenu` 后会自动打开 `FairyDemoForm`，无需加入 `Build Settings`。

界面出现后点击“刷新状态”可验证 FairyGUI 输入事件和 UGF 窗体生命周期。Console 会输出 `FairyGUI refresh interaction handled.`，界面计数同步递增。

## POC 边界

当前演示包只使用 FairyGUI 图形和文本；包管理器已支持图集、音频、字体和其他 Unity 资源的异步回调加载。生产页面仍需把这些资源
加入 GDK 资源规则，并验证 AssetBundle 分组、AOT/IL2CPP、重复打开关闭、覆盖/恢复和取消路径。
