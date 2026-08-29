# FairyGUI 完整接入 GDK 与 AI UI 闭环

## 目标

以 FairyGUI 作为 GDK 游戏与 UI 制作流程的唯一 UI 后端，完全停止在 GDK 自有 Player 和 Editor
代码、资源、配置及工具中使用 UGUI，同时保留 GDK 基于 UnityGameFramework（GF）、GameHot、ET
和 Luban 建立的 UI 管理语义，并建立一条以仓库内 FairyGUI XML 为事实来源、可生成、可发布、
可编译、可运行、可截图和可交互验证的 AI UI 闭环。

完成后，AI 可以直接修改可审查的 XML 和配置，通过确定性工具链得到类型安全绑定与运行时资源；
业务仍使用 GDK 的 UI ID、UIGroup、生命周期、资源、声音、本地化、设置、事件、实体、热更新和
ET Entity/System 设施，不退化为绕过框架的独立 `GRoot` 页面集合。

## 背景

- GDK 当前使用 `Design/Excel/GameHot/Datas/Game/UI.xlsx`、GF `UIManager`、GameHot UIForm 和
  ETUI 两条业务路径；UI ID、分组、多实例及覆盖暂停规则已经形成稳定契约。
- 当前 FairyGUI POC 仍继承 UGUI `AUIForm`，会创建 `Canvas`、`RectTransform` 和
  `GraphicRaycaster`；每个界面又创建独立 `UIPanel`，且 GF 已报告打开成功后才异步加载
  FairyGUI 资源，因此还不具备生命周期和资源正确性。
- 当前 POC 对 FairyGUI 外部纹理、字体和声音资源直接报错，没有包引用计数、依赖清单和卸载协议；
  业务绑定仍通过字符串子节点名完成。
- `Design/FairyGUI/GDK_FGUI/` 与 `D:/Unity/Project/GDK_FGUI/` 两份项目内容已经发生漂移；现有
  同步脚本只会从仓库覆盖外部副本，无法检测或拉回外部编辑结果。
- GameHot 和 ET 资源规则尚未包含 `Assets/Res/UI/FairyGUI`。内置资源更新界面在普通可下载资源
  就绪前运行，不能依赖正常的远端 FairyGUI 包。
- `com.unity.ugui` 是多个现有包和渲染管线的传递依赖，因而“运行时不使用 UGUI”不能用
  `packages-lock.json` 中完全不存在该包来判定。
- 用户已确认“完全不使用 UGUI”覆盖 GDK 自有 Player UI 和 Editor UI 工具链。UGUI 专用工具与包
  必须迁移或删除；只有 Unity/第三方包内部无法消除且未被 GDK UI 路径引用的传递依赖可以保留。
- GDK 当前正式支持 `ChineseSimplified`、`ChineseTraditional`、`English`、`Korean` 四种语言，
  设置界面采用保存设置并重启后生效的语义。

## 需求

### R1. 兼容边界

- 所有 Player 可达的游戏 UI 视图必须迁移到 FairyGUI，包括 GameHot、ET Demo、ET LockStep、
  内置更新/错误界面、运行时调试界面、UIWidget、HUD/HPBar 和世界空间 UI。
- GDK 自有 Editor UI 制作与检查流程必须迁移到 FairyGUI XML/发布/生成/验证闭环，或在不存在等价
  需求时删除；不得继续用 UGUI 预制体、RectTransform、Canvas、GraphicRaycaster、CodeBind 或
  UXTool UGUI 组件作为 UI 事实来源或工具输入。
- Unity Editor 工具本身若需要窗口，可使用 UI Toolkit/EditorGUI；批处理优先使用结构化 CLI。它们只
  操作 FairyGUI/Luban 事实来源，不得产生第二套 Player UI 描述或重新引入 UGUI。
- 保留 GF `UIManager` 的 UI ID、资源身份、UIGroup、深度、多实例、对象池以及
  `Init/Open/Update/Pause/Resume/Cover/Reveal/Refocus/Close/Recycle` 行为。
- 保留现有 `OpenUIForm`/异步打开的可观察语义；异步成功只能在 FairyGUI 包、组件、类型绑定和
  业务 presenter 全部可用后报告。
- 不修改 `Unity/Assets/Scripts/Library/UGF` 中的 GF 供应商核心；通过 GDK 辅助器、宿主、服务和
  适配层接入。
- “无 UGUI”以 GDK 自有代码、asmdef、场景、预制体、资源、配置和 Editor 工具零 UGUI 使用为验收
  口径。允许的唯一例外是 Unity/第三方包内部不可避免的传递依赖，且这些依赖不得被 GDK UI 路径
  直接引用、实例化或作为资产事实来源。

### R2. 唯一事实来源与确定性生产

- `Design/FairyGUI/GDK_FGUI/` 是唯一可提交、可修改的 FairyGUI 项目事实来源。
- 外部编辑器工作副本不得形成第二事实来源；同步工具必须在覆盖前检测差异，提供明确的导入、
  导出或冲突失败行为，禁止静默丢失 XML 修改。
- FairyGUI XML、包 ID、组件 ID、成员名和跨资源引用必须经过机器校验；重复名、缺失引用、非法
  ID、未命名的业务节点和破坏性重命名应在发布前失败。
- 发布、descriptor/manifest 生成和类型绑定生成必须可重复执行；同一输入应产生语义一致的输出，
  生成文件不得成为手工编辑的事实来源。
- 现有子任务 `08-20-fairygui-cli-publish` 负责无 GUI 点击的 FairyGUI 发布入口，并作为本任务的
  发布阶段依赖。

### R3. 类型安全与热更新边界

- 每个业务可访问组件必须有生成的类型安全绑定；业务代码禁止用裸字符串和反射查找节点或注册
  具体生成类型。
- 生成器使用稳定成员名/ID，并采用按名称解析能力，避免仅因 XML 中兄弟节点排序变化而断裂。
- GameHot 与 ET 生成绑定必须进入各自可热更新的程序集边界；XML 架构变化不能被错误地固化到
  仅随基础包更新的非热更程序集。
- 生成输出禁止手工修改，并具备过期输出检测：XML 与生成物不一致时编译或质量门禁失败。

### R4. FairyGUI 包与资源生命周期

- 提供 GDK 所有的包管理服务，统一通过 GDK Resource 系统加载 FairyGUI 描述符、图集、
  字体、音频和依赖包。
- 包加载必须合并重复请求、支持取消/版本失效、引用计数租约、依赖顺序和循环依赖检测。
- GDK 所有资源采用明确的所有权与卸载顺序：先销毁组件并 `UIPackage.RemovePackage`，再释放
  GDK Resource 资产，避免 FairyGUI 与 GDK 双重销毁。
- 资源规则和确定性清单必须覆盖 GameHot、ET、Editor 资源模式和 Player 资源构建；
  缺包、缺图集、缺字体、取消、重复关闭和应用关闭均有可操作错误且不泄漏。
- 默认按包预加载；仅在真实性能证据证明需要时再引入更细粒度的异步单项加载。

### R5. GF、GameHot 与 ET 业务适配

- 以轻量 GameObject 宿主承载 GF 现有具体 `UIForm`，宿主不添加任何 UGUI 组件；FairyGUI 视图
  直接进入单一 `GRoot` 下对应 UIGroup 容器。
- GameHot 界面改为类型化表现层/控制器，接收 FairyGUI 绑定和 GDK 上下文，并保留
  关闭、声音、事件、资源、实体、UIWidget 等能力。
- ETUI 保留 Entity/System 所有权、UGF 生命周期接口与 `UGFSystemSingleton` 分发；View 改为生成的
  FairyGUI 绑定，UIWidget 改为绑定 `GComponent` 的 ET Entity。
- 不在 GameHot 和 ET 各自复制包管理、宿主、生命周期或服务桥；两条业务路径共享同一个 GDK
  FairyGUI 接入核心层。

### R6. GDK 配套设施

- 本地化继续以 `Design/Excel/Localization.xlsx` 为唯一文本来源；生成 FairyGUI translation 数据，
  在包注册和组件创建前应用，覆盖四种现有语言。MVP 保持“切换语言后重启生效”的现有行为。
- FairyGUI 原生按钮/transition 声音不得绕过 `GameEntry.Sound`；所有 UI 声音遵循 `UISound` 配置、
  静音、音量和 GDK 资源所有权。
- 提供 FairyGUI 原生安全区布局服务，覆盖全面屏、刘海屏、4:3 和方向变化；不复用依赖
  `RectTransform` 的 UXTool 安全区域实现。
- 鼠标、触摸、键盘、文本输入和手柄导航必须有明确输入映射、焦点恢复和自动化验证。
- 色盲能力至少包含与现有 UXTool 矩阵一致的预览、AI 截图检查和语义颜色规则；Player 运行时模式
  需以不会破坏 FairyGUI 渲染或造成不可接受 RT/绘制模式成本的方式实现，并经过 Profiler
  证据确认。
- 设置、本地化、声音、事件、实体和资源仍由 GDK 服务持有，FairyGUI 只承担
  视图与交互表达。

### R7. 启动、迁移与回滚

- 提供随初始 Player 内置的最小启动引导 FairyGUI 包，用于资源检查、更新进度、致命错误和重试；
  它不能依赖尚未下载的普通资源包。
- 迁移按可独立验证的批次进行，并维护 UI ID/行为对照表；同一 Player 界面不得长期保留 UGUI 与
  FairyGUI 双写实现。
- 每批迁移必须可回滚到上一可运行提交；删除旧预制体、CodeBind、UGUI 适配和资源规则只能在
  等价 FairyGUI 流程通过 Player 验证后进行。
- 最终提供静态边界检查，阻止 GDK 自有 Player 或 Editor 路径重新引入 `UnityEngine.UI` 或 UGUI
  资源；第三方传递依赖单独审计，不作为 GDK 使用 allowlist。
- 清理 GDK 自有 Editor UI 工具和 UGUI 专用依赖：有明确 FairyGUI 等价价值的能力迁移，没有继续
  价值的能力删除；工具迁移不得引入第二套 UI 编辑器或 XML 事实来源。
- `com.coffee.softmask-for-ugui`、`com.coffee.ui-effect`、`com.coffee.ui-particle`、
  `me.qiankanglai.loopscrollrect`、UGUI RuntimeInspector 等专用依赖在无剩余使用方后必须从直接依赖
  与资源中移除。`com.unity.ugui` 必须从 GDK 直接依赖移除，仅可因 Unity/第三方传递依赖继续存在。

### R8. AI 闭环与质量门禁

- AI 流程必须覆盖：修改 XML/配置、检查、发布、清单/绑定生成、Unity 导入/编译、运行目标
  场景、截图、按坐标或语义节点交互、日志检查和结果归档。
- 截图测试至少覆盖 16:9、19.5:9 刘海屏和 4:3，四种语言及三种色觉模拟；长文本不得溢出，
  交互控件不得重叠或失焦。
- 记录迁移前基线并在同一场景复测 CPU、GC、主线程停顿、GPU/过度绘制、常驻内存、首次打开、
  二次打开和资源卸载。任一关键指标回归超过 10% 必须有分析和显式接受，不能静默通过。
- 完成 Unity Editor 编译、聚焦自动化测试、重复开关/取消/关闭测试和至少一个目标平台
  IL2CPP Player 构建；`.NET` 构建不得替代 Unity 证据。
- 更新 `Book/UI开发.md`、FairyGUI 接入文档、AI 操作说明、故障诊断和迁移/回滚文档。

## 验收标准

- [ ] AC01：仓库内 FairyGUI 项目是唯一事实来源；外部副本存在未导入修改时，同步命令明确失败且
  不覆盖文件。
- [ ] AC02：对同一 Git 输入连续执行两次检查、发布、清单和绑定生成，第二次不产生
  非确定性差异。
- [ ] AC03：XML 检查能阻止重复业务成员名、缺失组件/资源引用、非法或变化的稳定 ID，以及应生成
  绑定却未命名的节点。
- [ ] AC04：生成绑定可编译，业务 UI 代码中没有 FairyGUI `GetChild("...")`、反射注册或手写生成
  文件；节点仅重新排序时绑定仍有效。
- [ ] AC05：GF UIGroup、多实例、深度、对象池和全部生命周期在 FairyGUI 宿主上通过自动化测试；
  pause/cover 会同步控制视图可见性与可交互性。
- [ ] AC06：打开成功事件与 `OpenUIFormAsync` 只在 GComponent、绑定和表现层已就绪后完成；
  加载期间关闭或所有者销毁不会产生迟到界面。
- [ ] AC07：并发打开同一包只触发一次底层加载；引用计数随界面/控件打开关闭精确变化，最后一个
  租约释放后按策略卸载。
- [ ] AC08：缺包、依赖环、外部图集/字体/音频缺失、加载失败、取消和关闭都能返回稳定错误，
  不残留 GObject、宿主、资源句柄或事件订阅。
- [ ] AC09：GameHot 代表界面完整通过打开、交互、覆盖/暂停、恢复、关闭、回收和热更新模式
  验证。
- [ ] AC10：ET Demo、ET LockStep 和 ET UIWidget 代表流程保留 Entity/System 生命周期、取消和释放
  语义，并通过对应模式验证。
- [ ] AC11：GameHot 与 ET 资源规则都包含 FairyGUI 包及依赖，Editor 资源模式与目标 Player
  可以实际加载，不仅在 Assets 直读模式工作。
- [ ] AC12：启动引导包随初始 Player 内置；在普通资源不可用时仍可显示更新、失败、重试和退出流程。
- [ ] AC13：四种现有语言均能正确显示 FairyGUI 静态文本、按钮标题、列表项、gear 和组件属性；切换
  后按现有重启语义生效。
- [ ] AC14：UI 静音和音量设置能控制按钮与过渡声音，Profiler/日志证明没有并行的 FairyGUI
  原生音频所有权路径。
- [ ] AC15：16:9、19.5:9 刘海屏和 4:3 下安全区、弹窗、列表、输入框和长文本截图无越界、遮挡或
  不可点击区域。
- [ ] AC16：鼠标、触摸、键盘、文本输入和目标手柄布局完成打开、导航、确认、取消及焦点恢复测试。
- [ ] AC17：正常、红色、绿色和蓝色色觉模式可在 AI/Editor 预览中复现；Player 模式满足设计可辨识性
  且有渲染性能证据。
- [ ] AC18：所有 Player 可达 GameHot、ET、内置界面、RuntimeInspector、HUD/HPBar、世界空间叠加 UI 和
  UIWidget 已迁移并有行为对照验证。
- [ ] AC19：静态检查确认 GDK 自有运行时/Editor 代码、asmdef、场景、预制体、资源和配置中不存在
  `UnityEngine.UI`、Canvas、GraphicRaycaster、RectTransform UI、UGUI CodeBind 或 UXTool UGUI 视图
  使用；例外仅限未被 GDK UI 路径引用的 Unity/第三方传递依赖源码。
- [ ] AC20：重复 100 次打开/关闭、打开中取消、场景切换、热更新域切换和应用关闭检查无新增
  错误、未释放包、持续增长的宿主/GObject 或资源引用。
- [ ] AC21：迁移前后同场景性能报告齐全；超过 10% 的关键指标回归均有原因、优化或显式接受记录。
- [ ] AC22：Unity Editor 编译、聚焦测试、资源构建/加载冒烟及至少一个 IL2CPP Player 构建通过；
  GameView 截图和交互证据可追溯到输入 Git 修订版本。
- [ ] AC23：文档覆盖新建 UI、修改 XML、发布、生成、GameHot/ET 绑定、本地化、声音、安全区、输入、
  色盲、诊断和回滚流程。
- [ ] AC24：旧 UGUI 资源与代码只在等价验证后删除，且每个迁移批次都有可执行的 Git 回滚点。
- [ ] AC25：GDK 自有 Editor UI 制作、检查、预览和绑定工作流已迁移到 FairyGUI/结构化 XML 工具、
  UI Toolkit/EditorGUI 或结构化 CLI，或经使用方扫描证明无价值后删除；仓库文档不再指导创建 UGUI
  预制体 UI，Editor 工具不产生第二套 Player UI 描述。
- [ ] AC26：`com.unity.ugui` 直接依赖以及无剩余使用方的 UGUI 专用直接依赖、资源和 link/AOT 配置
  已移除；若
  `com.unity.ugui` 仍因渲染管线或第三方包传递存在，审计清单证明 GDK 未直接引用或实例化它。

## 不在范围内

- 修改或 fork GF 供应商核心以引入新的 `IUIForm` 具体类型。
- 同时长期维护 UGUI 与 FairyGUI 两套 Player 视图后端。
- 在没有 Profiler 或包大小证据前实现按 FairyGUI 单项的复杂流式加载、LRU 或远程缓存系统。
- 改变当前“语言设置后重启生效”的产品语义；实时重建所有已打开界面可作为后续任务。
- 修改 Unity 渲染管线或分叉第三方包，只为强行让 `com.unity.ugui` 从传递依赖图中消失；只要 GDK
  自有 UI 路径零使用，该包可以作为未使用的传递依赖存在。
- 重写 FairyGUI Editor 或 GF UIManager 供应商代码。
