# FairyGUI/GDK 现状与风险清单

快照日期：2026-08-20

本文记录父任务 PRD 和设计所依据的仓库证据。它不是实施契约；最终需求以 `../prd.md` 为准。

## 1. 现有 GDK UI 契约

当前 UI 技术栈中有价值的部分并不是 UGUI 渲染本身。GDK 已经提供：

- 由 Luban 驱动的 UI ID、资源名、UIGroup 名称、多实例策略和被覆盖时暂停策略；
- GF `UIManager` 的实例标识、分组、优先级、对象池和异步事件；
- `UIFormLogic` 生命周期：初始化、打开、更新、暂停/恢复、覆盖/显露、重新聚焦、关闭、回收；
- GameHot/HybridCLR 与 ET Entity/System 所有权模型；
- 由界面持有并负责清理的 EventContainer、EntityContainer、ResourceContainer 和 UIWidget；
- GDK Localization、Sound、Setting 和 Resource 服务。

`Unity/Assets/Scripts/Game/UI/Common/AExUIForm.cs:36` 通过扫描 GameObject 树初始化 UGUI 控件，
而第 87-140 行转发关闭、暂停、恢复、覆盖、显露、重新聚焦、更新和深度变化。第 223-415 行展示的
事件、实体和资源所有权设施必须脱离 UGUI 继续保留。

## 2. 当前 FairyGUI POC 的结论

### 2.1 仍然依赖 UGUI

`Unity/Assets/Scripts/Game/UI/FairyGUI/AFairyUIForm.cs:9` 继承 `AExUIForm`，后者又继承
`AUIForm`。`AUIForm.cs:36-44` 会添加 Canvas、拉伸 RectTransform 并添加 GraphicRaycaster。

结论：尽管可见内容使用 FairyGUI，这个 POC 仍不是仅使用 FairyGUI 的运行时后端。

### 2.2 每个界面各自使用一个 UIPanel

`AFairyUIForm.cs:86-92` 为每个界面创建新的 GameObject 和 UIPanel。这使 FairyGUI 视图成为独立面板，
而不是单一 GRoot/UIGroup 树的子节点。第 134-139 行把 GF 深度复制到面板排序顺序，但没有从结构上表达
分组层级、模态和触摸语义。

### 2.3 打开完成时视图尚未就绪

`AFairyUIForm.cs:24-30` 先调用基类 `OnOpen`，然后才以 `LoadFairyViewAsync(...).Forget()` 启动异步加载。
包加载、UIPanel 创建和业务绑定直到第 54-103 行才完成。

结论：观察方可能已经收到 GF 打开成功，但视图仍不存在；关闭/取消也只是依赖整数版本守卫，而不是
由所有者作用域管理的资源操作。

### 2.4 不支持外部资源

`AFairyUIForm.cs:119-131` 设置 `DestroyMethod.None`，对每个外部资源请求记录错误并返回 null。
包含描述文件之外的图集、字体或音频的真实包无法正确加载。

### 2.5 缺少包所有权协议

POC 在 `AFairyUIForm.cs:69` 调用 `UIPackage.AddPackage`，但没有引用计数、依赖清单、
`UIPackage.RemovePackage`、GDK 资源句柄列表或关闭顺序。关闭面板只会在第 153-163 行销毁面板 GameObject。

### 2.6 业务绑定使用字符串

`Unity/Assets/Scripts/Game/Hot/Code/UI/FairyDemoForm.cs:21-23` 通过字符串解析 `refreshButton`、
`statusText` 和 `checkCountText`。重命名或类型变化要到运行时才会失败。当前发布配置禁用了代码生成，
且 `getMemberByName=false`。

### 2.7 只有 GameHot 演示

POC 从 `Unity/Assets/Scripts/Game/Hot/Code/Procedure/ProcedureMenu.cs` 引用；不存在对应的 ET FairyGUI
界面/UIWidget 适配器。因此，它没有证明与 ET ModelView/HotfixView 生命周期兼容。

## 3. 为什么需要轻量 GF UIForm 宿主

GF 核心暴露了辅助接口，但 UnityGameFramework 包装层假定使用其具体运行时类型：

- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/UI/UIComponent.cs:352`
- `UIComponent.cs:362`
- `UIComponent.cs:376`
- `UIComponent.cs:399`
- `UIComponent.cs:413`
- `UIComponent.cs:435`
- `Unity/Assets/Scripts/Library/UGF/UnityGameFramework/Runtime/UI/OpenUIFormSuccessEventArgs.cs:79`

这些位置都会把 `IUIForm` 转换为 `UnityGameFramework.Runtime.UIForm`。用全新的具体实现替换它会要求
修改第三方框架核心。保留现有 UIForm，用轻量 GameObject 承载，并新增一个不依赖 UGUI 的 UIFormLogic，
可以用小得多的影响范围保持兼容。

## 4. 事实来源漂移

当前存在两个 FairyGUI 工程：

- 仓库源：`Design/FairyGUI/GDK_FGUI`
- 外部 Editor 副本：`D:/Unity/Project/GDK_FGUI`

2026-08-20 采集的哈希：

| 文件 | 仓库 SHA-256 | 外部副本 SHA-256 | 状态 |
| --- | --- | --- | --- |
| `assets/Package1/package.xml` | `60BEDEED...5784F76` | `ED879202...B0F5F63B` | 不同 |
| `assets/Package1/MainView.xml` | `DECC3416...E1B0BB` | `B302F9D2...724F2F` | 不同 |
| `assets/Package1/RefreshButton.xml` | 先前已检查 | 先前已检查 | 相同 |

已发布描述文件：

- `Unity/Assets/Res/UI/FairyGUI/Package1_fui.bytes`
- 最后修改时间 `2026-08-20 20:55:07`
- SHA-256 `2B773D40...96D753`

`Tools/FairyGUI/Sync-GDKDemoToEditor.ps1:61-83` 只会用 `-Force` 将仓库文件复制到 Editor 副本。
它不支持从 Editor 导回仓库，也没有上次同步哈希或冲突检查。第 89-91 行还明确禁用了代码生成。

风险：开发者可以在 FairyGUI Editor 中作出有效修改，却在下次同步时被静默覆盖。

## 5. 资源构建缺口

GameHot 资源规则证据：

- `Unity/Assets/Res/Editor/Config/ResourceRuleEditor_GameHot.asset:38-92`
- 包含 UIForm、UIPrefab、UISound、UISprite、UXTool 和 RuntimeInspector；
- 不包含 `Assets/Res/UI/FairyGUI`。

ET 资源规则证据：

- `Unity/Assets/Res/Editor/Config/ResourceRuleEditor_ET.asset:38-102`
- 包含 Demo/LockStep UIForm、UIPrefab、UISound、UISprite、UXTool 和 RuntimeInspector；
- 不包含 `Assets/Res/UI/FairyGUI`。

当前演示可以在面向 Assets 的 Editor 路径中运行，但没有证明资源收集、资源构建或 Player 会包含其
描述文件以及依赖的图集、字体和音频资源。

## 6. 启动引导约束

`Unity/Assets/Scripts/Game/Builtin/BuiltinUpdateResourceForm.cs:6-17` 是使用 Text 和 Slider 的 UGUI 界面。
`Unity/Assets/Res/Builtin/UpdateResourceForm.prefab` 被启动 GameEntry 预制体引用，并在普通可下载资源
完成更新前使用。

由此得到以下约束：

- 替代界面不能只存在于普通可下载的 FairyGUI 资源包中；
- 启动引导 UI 代码不能依赖尚未加载的 GameHot/ET 热更新代码；
- 启动引导所需的包、字体和图集必须包含在首包 Player 中，而且失败界面不能再递归依赖普通 UI 系统。

## 7. 迁移清单

快照检索在 `Unity/Assets/Scripts/Game` 下找到 36 个直接耦合 UGUI 的 Player 侧文件或 asmdef：

- 5 个 AssetSet 图片辅助文件；
- 3 个内置界面类；
- 3 个检查/创建游戏 UI 的 Editor 工具；
- 2 个 ET asmdef；
- 5 个 GameHot UI 类；
- 8 个生成的 Mono/CodeBind 绑定文件；
- 1 个 HPBar 类；
- 1 个 GameHot asmdef；
- 8 个共享 UGUI/公共/扩展文件。

已知运行时资源范围包括：

- 12 个 UIForm 预制体；
- 1 个 UIEntity/UIWidget 预制体；
- 1 个 RuntimeInspector 预制体；
- 内置 `UpdateResourceForm.prefab`；
- HPBar 预制体；
- 可复用的 UGUI 按钮/UIPrefab 和 UXTool 资源。

迁移任务必须重新生成这份清单。它证明只转换 Menu/Setting 界面并不能完成 Player 后端替换。

## 8. UGUI 包依赖图

`Unity/Packages/packages-lock.json` 表明，至少有以下包依赖 `com.unity.ugui`：

- `com.coffee.softmask-for-ugui`（`packages-lock.json:30-35`）；
- `com.coffee.ui-effect`（`packages-lock.json:39-44`）；
- `com.coffee.ui-particle`（`packages-lock.json:48-53`）；
- `com.unity.render-pipelines.core`（`packages-lock.json:194-201`）；
- `me.qiankanglai.loopscrollrect`（`packages-lock.json:295-300`）。

用户选择完整移除 GDK 自有 UGUI。可执行的验收规则是：

> GDK 自有的 Player 或 Editor 代码、资源、asmdef、配置或工具均不得使用 UGUI。仅当没有任何 GDK UI
> 路径引用或实例化某个 Unity/第三方包时，该包才可以保留内部传递依赖。

最后一个 GDK 使用方完成迁移后，应移除仅服务于 UGUI 的直接依赖包。要求 `com.unity.ugui` 本身从锁文件
消失仍可能迫使项目分叉无关的渲染管线，因此当它只作为未使用的传递依赖保留时，不要求将其移除。

## 9. 本地化结论

- `Design/Excel/Localization.xlsx` 是事实来源。
- 存在 `ChineseSimplified`、`ChineseTraditional`、`English` 和 `Korean` 四种语言的运行时资源。
- `Unity/Assets/Scripts/Game/Procedure/ProcedureLaunch.cs:77-89` 限制并选择这四种语言。
- `Unity/Assets/Scripts/Game/Hot/Code/UI/SettingForm.cs:131-139` 持久化所选语言并请求重启。
- FairyGUI `UIPackage` 在 `Unity/Assets/Scripts/Library/FairyGUI/Runtime/UI/UIPackage.cs:624`
  调用 `TranslationHelper.LoadFromXML`。

FairyGUI 翻译会在组件创建前修改包原始数据。因此，翻译源必须在注册包和构造组件之前应用。
运行时切换语言需要重建并重新绑定已打开视图，当前 GDK 对等兼容不要求支持这一能力。

## 10. 声音结论

FairyGUI 直接通过 Stage 播放音频：

- `Runtime/Core/Stage.cs:679-692` 调用 Unity AudioSource 的 `PlayOneShot`；
- `Runtime/UI/GButton.cs:631-632` 为按钮声音调用 Stage；
- `Runtime/UI/GComponent.cs:1666-1670` 和 `Transition.cs:1376-1383` 对组件/过渡声音执行同类调用。

缺少桥接时，FairyGUI 声音会绕过 GDK UISound ID、声音组、静音/音量设置和资源所有权。只集成包音频加载
并不足够；还必须接入播放分发，或者禁止使用 FairyGUI 原生声音字段并以生成的 GDK 事件替代。

## 11. 安全区域与输入结论

- 现有 UXTool 安全区域辅助逻辑面向 RectTransform，无法作用于 GComponent 树。
- FairyGUI 提供指针、触摸、键盘、文本输入和基础列表导航支持。
- 完整的手柄导航、默认焦点、模态焦点约束以及关闭/重新聚焦恢复属于 GDK 产品行为，需要显式适配器和测试。

应优先使用单一的 GRoot 级安全区域服务，而不是每个界面挂脚本。必须在 16:9、19.5:9/刘海屏和 4:3
下测试，不能根据单个 Editor 分辨率推断正确性。

## 12. 色觉无障碍结论

- Editor 预览使用 `Unity/Assets/Scripts/Library/UXTool/Editor/Tools/UXTools/Logic/Colorblind/ColorBlind.cs`。
- 运行时矩阵应用位于
  `Unity/Assets/Scripts/Library/UXTool/Runtime/UXGUI/Components/ColorBlindnessEffect.cs`。
- FairyGUI 提供 `ColorFilter`，但对整个 GComponent 应用过滤器可能进入绘制模式，并通过 RT 分配和渲染。

风险最低的目标是保留现有预览矩阵用于 AI 截图验证，增加语义颜色规则，并通过纵向渲染测试确认现有
相机效果是否覆盖 FairyGUI 最终输出。只有确认没有覆盖时，才增加经过性能测量的最终合成实现。

## 13. 类型安全结论

FairyGUI 官方生成类已经解决了大部分绑定工作。接入层应配置并使用它们，而不是再构建第二个 UI 编译器。
`getMemberByName=true` 可降低 XML 兄弟节点重排的影响，同时由 lint 强制稳定且唯一的名称和 ID。

剩余的 GDK 专用生成内容仅包括：

- UI ID 到界面描述符；
- package/component 到生成绑定工厂；
- presenter/entity 工厂注册；
- source hash/过期输出检查；
- 包资源/依赖清单。

生成的具体绑定应位于 GameHot/ET 热更新程序集中，使架构更新可以与新的 FairyGUI 资源一同发布。
把全部绑定放入非热更新共享程序集虽然更简单，但每次 XML 架构变化都将要求更新基础 Player。

## 14. 风险登记表

| 严重程度 | 风险 | 失败模式 | 必需的缓解措施/证据 |
| --- | --- | --- | --- |
| 严重 | 视图就绪前报告打开成功 | 调用方访问空绑定；关闭后迟到的视图仍出现 | GF 打开前预加载包；生成/取消测试 |
| 严重 | 启动引导依赖可下载包 | 更新/错误 UI 空白且启动无法恢复 | 内置最小包和离线失败测试 |
| 严重 | 两份 FairyGUI 事实来源 | 有效的 Editor XML 被静默覆盖 | 只保留一个仓库事实来源；同步哈希和冲突失败 |
| 严重 | 资源所有权错误 | 图集/字体/音频泄漏或重复销毁 | 租约模型；`DestroyMethod.None`；先 RemovePackage 再卸载测试 |
| 高 | GF 第三方代码转换为具体类型 | 自定义 IUIForm 在包装层崩溃 | 保留现有具体 UIForm 宿主；不分叉第三方代码 |
| 高 | 每个界面使用独立 UIPanel | 暂停/分组/深度/触摸行为偏离 GF | 单一 GRoot 配合分组 GComponent |
| 高 | 生成绑定位于非热更新程序集 | XML 资源更新要求更新基础应用 | 按运行模式输出热更新绑定 |
| 高 | ET 生命周期分歧 | 已销毁 Entity 遗留存活的 GComponent/包 | 所有者令牌和 ET 销毁/关闭测试 |
| 高 | 资源规则遗漏 FGUI | Editor 中正常，Player 中缺失 | 更新 GameHot/ET 规则并在构建 Player 中加载 |
| 高 | FairyGUI 直接播放音频 | 绕过设置和资源所有权 | GDK Sound 桥接及静音/音量测试 |
| 高 | 大范围移除 UGUI 破坏 Editor/渲染 | 无关包和工具损坏 | 移除全部 GDK 自有使用；审计但不分叉无法避免的第三方传递依赖 |
| 中 | 根节点 `ColorFilter` 成本 | 增加 RT、GPU 时间和内存 | 相机/最终渲染阶段验证和性能分析门禁 |
| 中 | 安全区域坐标不匹配 | 刘海遮挡或控件不可访问 | GRoot 坐标服务和分辨率矩阵 |
| 中 | 迁移后仍残留字符串绑定 | XML 重命名后只在运行时失败 | 生成绑定、lint 和静态检索 |
| 中 | 过早引入单项异步加载 | 复杂辅助逻辑产生竞态/取消问题 | 先预加载整个包；只有证据支持时再增加单项流式加载 |
| 中 | 永久保留双后端 | 维护成本翻倍且行为不一致 | 纵向切片迁移；仅短期保留，过门禁后删除 |

## 15. “完美兼容”的建议定义

完美兼容应指行为和工作流对等，而不是实现细节完全相同：

- GF/GDK 调用方保留 UI 标识、生命周期和服务；
- GameHot 与 ET 保留各自的所有权/热更新模型；
- 每个 Player 可见 UI 均由 FairyGUI 支撑；
- GDK 自有 Editor UI 创建/检查不再使用 UGUI 预制体、组件或 CodeBind 路径；
- AI 能够确定性地修改、生成、运行和验证 UI；
- 资源、本地化、声音、无障碍、输入和启动引导行为均有测试；
- UGUI 只可保留在无法避免的 Unity/第三方传递依赖图内；GDK 自有 Editor 工具不例外。

这个定义既可执行，也与用户目标一致。若要求包括第三方源码在内的整个仓库完全没有 UGUI 符号，
则必须分叉无关包或渲染管线，仍不属于本任务范围。
