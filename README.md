# GameDevelopmentKit

> **🔗 本仓库基于 [GameDevelopmentKit（GDK）](https://github.com/XuToWei/GameDevelopmentKit) 二次开发**，在其之上完成 FairyGUI 完整接入与零 UGUI 改造（本仓库：`PoSuanSama/FGUI2GDK`）。

GameDevelopmentKit（GDK）是一套 [Unity] 游戏开发框架。服务端基于 [ET 8.1]，客户端以 [UnityGameFramework]（GF）为底座，可选择纯 GF（GameHot）或 ET 开发模式；**UI 视图层为 [FairyGUI]**，GDK 自有代码、资源与工具链已实现零 UGUI。

## 核心能力

| 领域 | 能力 |
| --- | --- |
| 成熟稳定 | 经商业项目验证，覆盖客户端、服务端、热更新、数据、网络、UI 与构建等完整开发链路 |
| 双端架构 | [Unity] 客户端与 [ET 8.1] 服务端共享协议、配置和基础设施；客户端支持 [纯 GF（GameHot）][模式选择] 与 ET 模式 |
| UI | [FairyGUI] 作为唯一 Player UI 视图后端，GF `IUIManager` 继续拥有 UI ID、分组、层级、多实例、对象池与完整生命周期；GameHot 走 Presenter 工作流，ET 走 Component/System 打开链 |
| UI 工具链 | [FGUI Agent Bridge]（`External/fgui-agent-bridge/`）随仓库内置：Editor 插件、免安装 CLI 与 AI Skill 开箱即用，配合 [FairyGUI 接入](Book/FairyGUI接入.md) 完成 XML 编辑、发布与 AI UI 闭环 |
| 热更新 | [HybridCLR] 管理热更程序集、AOT 元数据与构建流程 |
| ET 与 GF 集成 | ETUI、ETEntity 接入 ET 生命周期，[UniTask] 统一异步模型 |
| 数据与协议 | [Luban] 导出配置，[Proto2CS] 生成 ET/MemoryPack 与 GF/Protobuf 协议代码 |
| 网络 | [UnityWebSocket] 提供 WebSocket 通道 |
| 编辑器工具 | [代码生成]、[Toolbar]、[一键构建] 与 [Unity Agent Bridge] 驱动的 AI 工作流 |

## 运行模式

| 模式 | 编译符号 | 适用场景 |
| --- | --- | --- |
| 纯 GF（GameHot） | `UNITY_GAMEHOT`（必选） | 使用 GF 客户端并加载 GameHot 业务程序集 |
| ET | `UNITY_ET` | ET 实体系统、客户端与服务端共享业务模型 |
| HybridCLR | 叠加 `UNITY_HOTFIX` | 将当前业务模块改为 DLL 资源加载 |

`UNITY_ET` 与 `UNITY_GAMEHOT` 互斥，`UNITY_HOTFIX` 可叠加。编辑器切换模式时会同步更新 Luban 工程、资源收集规则、`link.xml` 和 HybridCLR 程序集列表。

## 快速开始

1. 安装 [.NET 8 SDK](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) 和 [Unity 6000.3.18f1](https://unity.com/releases/editor/whats-new/6000.3.18f1)。

2. 在仓库根目录编译工具项目：

   ```powershell
   dotnet build Kit.sln
   ```

   也可通过 [Rider](https://www.jetbrains.com/rider/download/) 编译 `Kit.sln`，或在 Unity 中选择 `Game > Build Tool Editor`。

3. 用 Unity 打开 `Unity/`，加载 `Assets/Launcher.unity`，点击 Play。

模式切换和独立服务端启动方式见 [完整快速开始](Book/快速开始.md)。

## 文档导航

| 主题 | 文档 |
| --- | --- |
| 索引与架构 | [Book 文档索引](Book/README.md)、[项目结构与模式选择](Book/Project结构.md) |
| UI 开发 | [UI 开发](Book/UI开发.md)、[FairyGUI 接入](Book/FairyGUI接入.md) |
| 业务开发 | [Entity 开发](Book/Entity开发.md) |
| 配置与协议 | [Luban 配置](Book/Luban配置.md)、[Proto 生成](Book/Proto生成工具.md)、[多语言](Book/多语言.md) |
| 热更新与构建 | [HybridCLR 热更新](Book/HybridCLR热更.md)、[一键打包](Book/一键打包.md) |
| 开发工作流 | [Trellis AI 开发工作流](Book/Trellis工作流.md) |

## 主要依赖

| 分类 | 依赖 |
| --- | --- |
| 核心框架 | [UnityGameFramework]、[UGFExtensions]、[ET 8.1] |
| UI | [FairyGUI]（Unity SDK 5.2.0，MIT）、[FGUI Agent Bridge]（0.8.1 内置，MIT） |
| 热更新与配置 | [HybridCLR]、[Luban]、[Luban Extension] |
| 异步、序列化与网络 | [UniTask]、[MemoryPack Extension]、[Protobuf Unity]、[UnityWebSocket] |
| 编辑器工具 | [SocoTools]、[FolderTag]、[Unity Agent Bridge] |

> 注：`com.unity.ugui` 仅作为 URP 的官方传递依赖保留，GDK 自有代码、asmdef、资源、配置与工具链不直接使用 UGUI。

[Unity]: https://unity.com/
[UnityGameFramework]: https://github.com/EllanJiang/UnityGameFramework
[UGFExtensions]: https://github.com/FingerCaster/UGFExtensions
[ET 8.1]: https://github.com/egametang/ET/commit/b7bdaa0dcd5c682d968ec8922eb7a6dc4637011c
[HybridCLR]: https://github.com/focus-creative-games/hybridclr
[Luban]: https://github.com/focus-creative-games/luban
[Luban Extension]: https://github.com/XuToWei/Luban-Extension
[UniTask]: https://github.com/Cysharp/UniTask
[MemoryPack Extension]: https://github.com/XuToWei/MemoryPack-Extension
[Protobuf Unity]: https://github.com/XuToWei/Protobuf-Unity
[UnityWebSocket]: https://github.com/psygames/UnityWebSocket
[FairyGUI]: https://www.fairygui.com/
[FGUI Agent Bridge]: https://github.com/Wilson520403/fgui-agent-bridge
[SocoTools]: https://github.com/crossous/SocoTools
[FolderTag]: https://github.com/liyingsong99/FolderTag
[Unity Agent Bridge]: https://github.com/XuToWei/UnityAgentBridge
[模式选择]: Book/Project结构.md
[ETUI]: Book/UI开发.md
[ETEntity]: Book/Entity开发.md
[Proto2CS]: Book/Proto生成工具.md
[代码生成]: Book/ET代码生成工具.md
[Toolbar]: Book/自定义Toolbar.md
[一键构建]: Book/一键打包.md

## 商业依赖、交流与许可

- 商业插件：[Odin Inspector](https://assetstore.unity.com/packages/tools/utilities/odin-inspector-and-serializer-89041)，需自行购买并遵守授权条款。
- QQ 群：`949482664`
- 项目代码采用 [MIT License](LICENSE)；第三方资源和插件遵循各自许可。
