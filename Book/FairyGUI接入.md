# FairyGUI 接入

FairyGUI 是 GDK Player UI 的唯一视图后端。GF `IUIManager` 继续拥有 UI ID、UIGroup、serial、多实例、深度、对象池与完整生命周期；业务层可选择 GameHot MonoBehaviour 工作流或 ET Entity/System 工作流。GDK 自有代码、asmdef、资源、配置和工具不再直接使用 UGUI（零 UGUI 静态门禁为 0）。

FairyGUI XML 工程是 AI UI 流程的唯一版本化事实来源。`D:\Unity\Project\GDK_FGUI` 只是本机 FairyGUI Editor 工作副本，不能作为构建输入或长期保留未导入改动。

## 目录归属

| 内容 | 路径 |
| --- | --- |
| FairyGUI XML 事实来源 | `Design/FairyGUI/GDK_FGUI/` |
| 稳定业务契约 | `Design/FairyGUI/GDK_FGUI/settings/GDK.json` |
| 确定性 manifest | `Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json` |
| 本地化映射 | `Design/FairyGUI/GDK_FGUI/settings/FairyLocalization.json` |
| FGUI Agent Bridge 源码 | `External/fgui-agent-bridge/`（vendored Wilson520403 0.8.1，MIT） |
| 免安装 CLI 启动器 | `External/fgui-agent-bridge/fgui-agent.py` |
| Editor 侧插件副本 | `External/fgui-agent-bridge/plugin/`（安装到 FairyGUI 工程 `plugins/agent-bridge`） |
| 编辑器工程同步脚本 | `Tools/FairyGUI/Sync-GDKDemoToEditor.ps1` |
| XML/manifest 检查 | `Tools/FairyGUI/Test-GDKProject.ps1` |
| 工具回归测试 | `Tools/FairyGUI/Test-FairyGUITools.ps1` |
| 描述符生成 | `Tools/FairyGUI/Generate-FairyUIFormDescriptors.ps1` |
| 运行时 manifest 生成 | `Tools/FairyGUI/Generate-FairyRuntimeManifest.ps1` |
| 本地化 XML 生成 | `Tools/FairyGUI/Generate-FairyLocalizationXml.ps1` |
| 发布脚本 | `Tools/FairyGUI/Publish-GDKDemo.ps1` |
| 发布产物 | `Unity/Assets/Res/UI/FairyGUI/` |
| GDK 运行时管理层 | `Unity/Assets/Scripts/Game/UI/FairyGUI/`（FairyUIManager 等） |
| GameHot Presenter | `Unity/Assets/Scripts/Game/Hot/Code/UI/` |
| ET Component/System | `Unity/Assets/Scripts/Game/ET/Code/ModelView` + `HotfixView/Client/Module/UI/` |
| 独立演示场景 | `Unity/Assets/FairyGUIDemo.unity`（GameHot）、`Unity/Assets/FairyGUIDemoET.unity`（ET 双符号验证） |

SDK 固定为 FairyGUI Unity SDK `5.2.0`，上游提交为 `7f8555dd163bd17315f77b64907e07e735cf0ed0`，许可证为 MIT。导入内容位于 `Unity/Assets/Scripts/Library/FairyGUI/`；运行时 asmdef 增加了 `Unity.InputSystem` 引用和 `FAIRYGUI_INPUT_SYSTEM` 版本宏定义，以匹配本工程的新输入系统配置。供应商最小补丁：`UIConfig.soundRedirect` 委托（OWN001），仅新增钩子，未注入时行为不变。

## 运行时架构

### 打开链

```text
GameHot Procedure / ET flow
  -> FairyUIManager.OpenFairyUIFormAsync(uiId, userData, ownerToken)
  -> DRUIForm( Luban UI 表) + FairyUIFormDescriptor 双重校验
  -> FairyPackageManager.AcquireAsync(包租约)
  -> FairyLocalization.ApplyAsync(SetStringsSource)
  -> UIPackage.CreateObject(强类型 GComponent + 绑定类型校验)
  -> Presenter 创建(ET: Component/System 工厂;GameHot: 类 Presenter 注册表)
  -> GF IUIManager.OpenUIForm(descriptor assetName 作为窗体资产 token)
  -> FairyUIForm / FairyUIFormHelper / FairyUIGroupHelper
  -> GF UIGroup、serial、深度、对象池和生命周期
```

要点：

- 没有绕过 GF 维护第二套页面栈；一切走 `GameFramework.UI` 语义层。
- FairyGUI 视图由单一 GRoot 承载，每个 UIGroup 映射为 GRoot 下的组容器（含安全区子容器）。
- `FairyUIManager` 把 Stage 挂到 GameEntry 下 Builtin(GameFramework 实例根)的静态 `UI` 节点下，与旧 UGUI 的 UIComponent 位置一致；节点静态存在于 `GameFramework.prefab`，运行时不动态创建。
- descriptor JSON 由 Luban UI 行 + GDK.json 映射生成（`Generate-FairyUIFormDescriptors.ps1`），打开时校验 descriptor 身份与 GF 策略未漂移。
- owner token 在打开成功后仍持续拥有窗体；取消按 serial ID 关闭，Close/Recycle/失败路径幂等释放。
- 包租约支持依赖拓扑、并发合并与逆序释放；最后租约释放后才 `UIPackage.RemovePackage`。

### 服务桥（FairyGUI 不直接拥有 GDK 服务）

| 桥 | 实现 | 行为 |
| --- | --- | --- |
| 本地化 | `FairyLocalization` | 打开链 AcquireAsync 后、CreateObject 前按当前语言 `SetStringsSource`，按包幂等。已知边界：vendor SDK 快照的 TranslateComponent 不翻主文本，需升级 SDK 或补丁 |
| 声音 | `FairySound` | `UIConfig.soundRedirect` 钩子把 click=10001/select=10000 重定向到 GDK UISound 组；未映射资源只诊断一次 |
| 安全区 | `FairyUIGroupHelper` | `Screen.safeArea` 像素经 contentScaleFactor 缩放 + Y 翻转换算到 GRoot 设计坐标；变化重算幂等；descriptor `fullScreen` 标记决定是否挂安全区容器 |
| 输入/焦点/手柄 | `FairyInputService` | Input System 轮询方向导航 + 确认/取消映射到顶部窗体导航根；焦点恢复可测试 |
| 色觉 | `FairyColorBlindness` | 语义颜色 lint；URP 下旧 ColorBlindnessEffect(OnPostRender) 不生效，Player 滤镜需新 URP RendererFeature（已记录为后续批次） |

### GameHot 入口

`HotEntry.InitializeFairyGUI()` 构建 Presenter 注册表（反射扫描 `[FairyUIPresenter]` 标记）、初始化 `FairyUIManager`、安装声音/输入桥、注册五个 UIGroup。业务经 `FairyUIFormService.OpenFairyUIFormAsync` 打开界面。

### ET 入口

`FairyGUIBootstrap.InitializeAsync()` 注册 UI ID -> Component 工厂（`FairyUIFormComponentRegistry`），全部界面走 Component/System 打开链。`UIComponent` 是 owner（per-open CTS、pending operation、owned serial/CTS），`Destroy` 固定执行 cancel pending -> close owned -> dispose CTS。行为全部在 HotfixView 静态 System 中，经 `EntitySystemSingleton.TypeSystems` 派发（与 UGFUIForm/UGFSystemSingleton 同构）。

### 双符号验证

ET 冒烟需要 GameHot 流程初始化 GameEntry 组件 + ET 流程跑 UI。`FairyGUIDemoAgent.SwitchToDualSymbols` 给 Standalone 写入 `UNITY_ET + UNITY_GAMEHOT`（仓库菜单互斥无法产生双符号状态），`RestoreDefaultSymbols` 恢复客户端平台 `UNITY_GAMEHOT`/Server `UNITY_ET` 默认布局。ET 验证场景是 `FairyGUIDemoET.unity`（含 ET 对象挂 `ET.Init` 组件）；不要把 ET 对象放进共享的 FairyGUIDemo 场景。

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

发布 `Package1` 后，官方生成器会在代码路径下创建 `Package1/`，生成类使用 `Game.Hot.FairyGUI.Package1` 命名空间和 `GetChild("成员名")` 绑定。

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

任何写同步都要求 FairyGUI Editor 已关闭，避免覆盖未保存内容。预演可在 Editor 打开时执行且不会修改 XML、设置或状态文件：

```powershell
./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode ToEditor -WhatIf
```

同步状态保存在 Editor 工程的 `.gdk-sync-state.json`。哈希计算会统一换行，并把仓库相对发布路径与 Editor 绝对路径映射为逻辑占位符。`plugins/` 与 `.agent/` 不属于同步集合，脚本不会创建、覆盖或删除任何 Editor 插件或 Bridge 运行时数据。

## FGUI Agent Bridge（随仓库内置，开箱即用）

发布由 [FGUI Agent Bridge](https://github.com/Wilson520403/fgui-agent-bridge) 执行。
Bridge 仓库（Wilson520403/fgui-agent-bridge 0.8.1，MIT）已完整内置在
`External/fgui-agent-bridge/`：Python CLI/MCP 源码、FairyGUI Editor 插件、安装同步脚本
与 AI Skill。不需要外部克隆、`uv` 虚拟环境或全局安装。

### 1. 安装 Editor 侧插件

把内置插件目录复制到 FairyGUI 工程（或直接运行 bridge 自带的同步脚本）：

```powershell
# 方式 A:复制(目标工程路径按实际调整)
Copy-Item -Recurse -Force `
  External/fgui-agent-bridge/plugin `
  D:\Unity\Project\GDK_FGUI\plugins\agent-bridge

# 方式 B:用 bridge 自带的同步脚本(自动做文件对比与更新)
python External/fgui-agent-bridge/scripts/sync_to_project.py --project D:\Unity\Project\GDK_FGUI
```

安装后重新打开 FairyGUI Editor（已验证 6.1.4），插件会在工程下创建 `.agent/` 队列目录并轮询指令。

### 2. 使用免安装 CLI

`External/fgui-agent-bridge/fgui-agent.py` 免安装启动 CLI：把 `src/fairygui_agent`
加入 `sys.path` 直接运行 `cli.main`，CLI 路径只依赖 Python 3.10+ 标准库
（MCP 服务 `mcp_server.py` 才需要 `mcp` 包，按 bridge README 用 `uv sync` 安装）。
可用 `FGUI_AGENT_EXE` 环境变量或 `-AgentExecutable` 参数指向：

```powershell
$env:FGUI_AGENT_EXE = "$(Resolve-Path .)\External\fgui-agent-bridge\fgui-agent.py"
./Tools/FairyGUI/Publish-GDKDemo.ps1 -PackageName Package1
```

直接调用时把本文件作为脚本传给 python（`--project` 是全局参数，须在子命令之前）：

```text
python External/fgui-agent-bridge/fgui-agent.py --project D:\Unity\Project\GDK_FGUI status
```

### 3. AI 工作流 Skill（可选）

bridge 自带 `.agents/skills/fgui-agent-bridge/SKILL.md`（含命令参考与能力清单）。
需要 AI 直接操作 FairyGUI Editor 的会话可把该 Skill 复制到宿主 `.claude/skills/` 或
`.agents/skills/`，让 AI 按技能说明调用 CLI。

## 命令行发布

先确认 FairyGUI Editor 正在运行且 Bridge 心跳新鲜，再运行：

```powershell
$env:FGUI_AGENT_EXE = "$(Resolve-Path .)\External\fgui-agent-bridge\fgui-agent.py"
./Tools/FairyGUI/Publish-GDKDemo.ps1 -PackageName Package1
```

也可以覆盖包、工程、输出、日志和超时参数：

```powershell
./Tools/FairyGUI/Publish-GDKDemo.ps1 `
  -AgentExecutable 'External/fgui-agent-bridge/fgui-agent.py' `
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

Editor 离线或心跳过期时只报告首个可操作错误；重新打开 FairyGUI Editor 后再重试。

## 手工发布

先按上一节确认 `Status=Equal`，然后打开：

```text
D:\Unity\Project\GDK_FGUI\GDK_FGUI.fairy
```

`项目/发布设置` 已预配置为：

| 设置 | 值 |
| --- | --- |
| 发布路径 | `D:\GitHubProject\FGUI2GDK\Unity\Assets\Res\UI\FairyGUI` |
| 扩展名 | `bytes` |
| 包格式 | 使用二进制格式 |
| 描述文件压缩 | 开启 |
| 最大纹理尺寸 | `2048`（编辑器默认值） |
| 超出后自动分页 | 开启 |
| 尺寸选项 | 2 的次方幂 |
| 允许旋转 | 关闭 |
| 裁剪图片边缘空白 | 开启 |
| 发布代码 | 开启 |
| 代码路径 | `D:\GitHubProject\FGUI2GDK\Unity\Assets\Scripts\Game\Hot\Code\Generate\FairyGUI` |
| 获取成员方式 | 按名称 |

选择 `Package1` 后点击“发布”。成功标志是同时生成非空的 `Package1_fui.bytes` 和官方 C# 绑定目录。不要把 `.objs`、Editor 缓存、临时文件或 `.gdk-sync-state.json` 加入仓库。

发布后若继续在 FairyGUI Editor 修改界面，关闭 Editor 并执行 `FromEditor`，再重新生成清单；否则 Git 中的仓库工程不是最新事实来源。发布后必须重跑描述符与本地化检查（源哈希已变化）：

```powershell
./Tools/FairyGUI/Generate-FairyUIFormDescriptors.ps1 -Check
./Tools/FairyGUI/Generate-FairyLocalizationXml.ps1 -Check
```

## 事实来源与派生输出

可以直接修改的事实来源：

- `Design/FairyGUI/GDK_FGUI/assets/**/*.xml`
- `Design/FairyGUI/GDK_FGUI/settings/GDK.json`、`Publish.json`、`FairyLocalization.json`
- GameHot/ET 的 `Design/Excel/**/UI.xlsx`
- `Tools/FairyGUI/*.ps1` 和生成器模板
- GameHot/ET Presenter、flow、component/system 源码

不得手工修补的派生输出：

- `Design/FairyGUI/GDK_FGUI/generated/GDKFairyManifest.json`
- `Unity/Assets/Res/UI/FairyGUI/*_fui.bytes`
- `Unity/Assets/Res/UI/FairyGUI/*.json`（descriptor / manifest / localization manifest / strings XML）
- `Unity/Assets/Scripts/Game/Hot/Code/Generate/FairyGUI/**`
- `Unity/Assets/Scripts/Game/**/Generate/UGF/UIFormId.cs`
- Luban 生成的 `DR*`、`DT*` 和 JSON/binary 数据

规则：先改来源，再运行官方工具/仓库生成器；来源和派生输出作为同一逻辑批次审查。

## 运行演示

GameHot：在 Unity 中打开 `Assets/FairyGUIDemo.unity` 并进入播放模式。该场景保留了 `Launcher` 的完整 GDK 启动链，进入 `ProcedureMenu` 后会自动打开 `FairyDemoForm`。

ET：切 `UNITY_ET`（或 Standalone 双符号）后打开 `Assets/FairyGUIDemoET.unity`。场景含挂 `ET.Init` 的 ET 对象；EntryEvent 装配 `UIComponent` 后经 Component/System 链打开 Demo。

界面出现后点击“刷新状态”可验证 FairyGUI 输入事件和 GF 窗体生命周期。Console 会输出 `ET FairyGUI demo form opened through the Component/System chain.`（ET）或对应 GameHot 日志，界面计数同步递增。

## 验证

工具确定性：

```powershell
pwsh -NoProfile -File ./Tools/FairyGUI/Test-FairyGUITools.ps1
pwsh -NoProfile -File ./Tools/FairyGUI/Test-GDKProject.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyUIFormDescriptors.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyRuntimeManifest.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyLocalizationXml.ps1 -Check
```

Unity 冒烟（经 Unity Agent Bridge 的 AgentCallable）：

- GameHot：`FairyUIManagerSmokeTest`、`FairyInventorySmokeTest`、`FairyDialogSmokeTest`、`ValidateFairyUIFormLifecycleCycles`（100 次）、`ValidateFairyPackageManagerLifecycle`
- ET：`FairyInventorySmokeTest`、`FairyFiberLifecycleSmokeTest`、`FairyLocalizationSmokeTest`、`FairySoundSmokeTest`、`FairySafeAreaSmokeTest`、`FairyInputSmokeTest`、`FairyColorBlindnessSmokeTest`、`FairyUIFormSkeletonSelfCheck`（EditMode）

Player：`Game.Editor.FairyGUIDemoAgent.BuildWindows64PlayerPkg` 执行 HybridCLR 安装（缺失时）→ Do All → 资源构建 → Launcher 场景 IL2CPP Player 构建，输出 `Temp/Pkg/Windows64`。构建后启动 `GameDevelopmentKit.exe` 验证热更域加载与 FairyGUI 界面打开。

零 UGUI 静态门禁：

```powershell
rg -n "using UnityEngine\.UI|UnityEngine\.UI|CanvasScaler|GraphicRaycaster|RectTransform" `
  Unity/Assets/Scripts/Game Unity/Assets/Res -g "*.cs" -g "*.asmdef" -g "*.prefab" -g "*.unity" -g "*.asset"
```

GDK 自有代码命中必须为 0（`com.unity.ugui` 作为 URP 官方传递依赖保留，见 `Unity/Packages/manifest.json`）。
