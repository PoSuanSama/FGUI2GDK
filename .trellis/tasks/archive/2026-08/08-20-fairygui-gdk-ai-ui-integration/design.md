# FairyGUI 完整接入 GDK 与 AI UI 闭环设计

## 1. 设计目标

目标不是给现有 UGUI UIForm 再包一层 FairyGUI，而是把“视图后端”替换成 FairyGUI，同时继续让
GDK/GF 负责界面身份、分组、生命周期、对象池、异步、资源和业务所有权。

设计遵循五个不变量：

1. FairyGUI XML 是视图结构的事实来源，Luban 是 UI 身份与策略的事实来源。
2. GF `UIManager` 继续是运行时 UI 实例管理器，业务不能绕过它直接维护页面栈。
3. GameHot 与 ET 只在业务表现层/实体形态上不同，底层包/宿主/服务不重复。
4. GDK Resource、Localization、Sound、Setting 和 Event 等服务继续拥有资源与状态。
5. GDK 自有 Player 与 Editor UI 路径均不得继续使用 UGUI；未被 GDK 引用的第三方传递依赖不作为
   UI 后端，也不为追求锁文件“零符号”而 fork Unity 或渲染管线。

## 2. 目标架构

```text
Design/Excel：UI + 本地化              Design/FairyGUI XML
               |                               |
               +---------- 校验/生成 -----------+
                               |
                 UI 描述符 + 包清单 + 类型绑定
                               |
                     GDK FairyGUI 接入核心
                               |
          +--------------------+--------------------+
          |                    |                    |
  FairyPackageManager   FairyUIFormHost      FairyGUI 服务桥
          |             + UIGroup 适配器      本地化/声音/
          |                    |              安全区域/输入/色觉
          +--------------------+
                               |
                     FairyGUI GRoot/GComponent
                               |
                    +----------+----------+
                    |                     |
             GameHot 表现层          ET Entity/System
```

## 3. 所有权与模块边界

### 3.1 接入核心

放在 `Unity/Assets/Scripts/Game` 的非热更共享运行时边界，负责：

- 描述符/清单数据模型；
- `FairyPackageManager` 与包租约；
- `FairyUIFormHelper`、`FairyUIGroupHelper`、轻量宿主和 GRoot 分组；
- 本地化、声音、安全区、输入和色觉服务桥；
- 与具体生成类无关的绑定/表现层接口。

该层可以引用 FairyGUI、GF、UniTask 和现有 GDK 服务，不得引用 GameHot 或 ET 具体业务类型。
不修改 `Unity/Assets/Scripts/Library/UGF` 供应商代码。

### 3.2 生成物

生成物按使用方分别进入 GameHot 与 ET 可热更新边界。核心层只认识稳定的非泛型接口或基础绑定，
不能直接引用某个包生成的 `UI_MainView`。

XML 架构变化通常需要热更绑定/表现层与新资源一起发布。若某个生成类型必须进入非热更新核心层，
就会迫使基础 Player 更新，因此只允许 bootstrap 包采用这种特殊策略。

### 3.3 业务层

- GameHot：类型化表现层/控制器，生命周期由宿主转发。
- ET：保留 ModelView/HotfixView、Entity/System 和 UGF 系统分发，View 改为 FairyGUI 绑定。
- 两者通过上下文访问 GDK 能力，不自行加载/卸载包。

## 4. 事实来源与生成流水线

### 4.1 单一事实来源

`Design/FairyGUI/GDK_FGUI/` 是唯一版本化工程。`D:/Unity/Project/GDK_FGUI/` 只允许作为编辑器工作副本。

同步协议应记录最近一次同步的源哈希，支持：

- `repo -> editor`：仅当 Editor 未改动或显式确认导入已完成时覆盖；
- `editor -> repo`：通过结构化 XML 校验后导入；
- 双边变化：直接报冲突，列出差异文件，不做自动合并或静默覆盖。

无需构建常驻监听器、数据库或双向实时同步。一次显式命令和哈希冲突门禁即可满足数据安全。

### 4.2 校验

XML lint 使用结构化 XML 解析，至少验证：

- 包/组件/资源 ID 唯一且引用存在；
- 包依赖无环；
- 业务可访问节点有唯一稳定名称；
- 绑定入口组件存在且类型匹配；
- controller/gear/transition 引用目标存在；
- 本地化 key 存在于 Luban 来源；
- 描述符中 UI ID、包、组件和表现层映射唯一；
- XML 哈希与生成输出中的源哈希一致。

### 4.3 输出契约

流水线生成三类输出：

1. FairyGUI 发布包：`*_fui.bytes`、图集、字体、音频等资源。
2. 每个 UIForm 的轻量描述符：唯一 `uiFormAssetName`，包含 UI ID、包 ID/名称、组件
   ID/名称、依赖包、绑定键、表现层键、启动引导标记和预加载策略。
3. GameHot/ET 类型绑定及注册代码：按名称解析成员并生成 `BindAll()`，业务代码不使用裸字符串。

描述符建议是生成的轻量 Unity 资产或字节数据，而不是每个界面的 UGUI 预制体。GF 仍能用唯一资源名
管理实例，自定义辅助器则用描述符创建统一宿主。

## 5. GF 运行时接入

### 5.1 为什么宿主仍使用具体 UIForm 类型

`UnityGameFramework.Runtime.UIComponent` 多处把 `IUIForm` 强制转换成具体
`UnityGameFramework.Runtime.UIForm`。因此完全自定义一个非 `UIForm` 的实现会要求修改供应商核心并
扩大兼容风险。

目标使用一个没有 Canvas、Image、Button、RectTransform 或 GraphicRaycaster 的轻量 GameObject：

- 挂载现有 GF `UIForm`；
- 挂载新的 `FairyUIFormLogic : UIFormLogic`；
- 由 `FairyUIFormHelper` 从描述符创建并绑定；
- 被 GF 对象池回收，FairyGUI `GComponent` 则按明确的视图生命周期创建/释放。

### 5.2 UIGroup 与深度

只使用一个 FairyGUI Stage/GRoot。每个 GF UIGroup 对应 GRoot 下的一个无装饰 `GComponent` 容器，
顺序由 GF 分组深度决定；界面是分组容器的子项，顺序由 GF 组内深度决定。

不为每个界面创建 `UIPanel` 或额外 StageCamera。这样可见性、可交互性、排序、模态和
对象数量都由同一树控制。

### 5.3 生命周期映射

| GF 生命周期 | FairyGUI/业务操作 |
| --- | --- |
| `OnInit` | 描述符已解析且包租约已持有；创建 GComponent、绑定和表现层/实体 |
| `OnOpen` | 添加到分组、设置数据、显示并启用交互；分发业务打开事件 |
| `OnPause` | 保持实例，禁用交互；按 GF 语义隐藏或冻结更新 |
| `OnResume` | 恢复可见性/交互/更新，并分发恢复事件 |
| `OnCover` | 分发覆盖事件；不自行改变 GF 未要求的状态 |
| `OnReveal` | 分发显露事件并恢复焦点候选 |
| `OnRefocus` | 更新 userData，恢复最后有效焦点并分发重新聚焦事件 |
| `OnClose` | 取消所有者令牌、解绑事件、移除分组、释放视图/包租约 |
| `OnRecycle` | 清空所有引用和版本号，确保对象池宿主不携带旧表现层 |

宿主的 `visible` 必须同时控制 `GComponent.visible` 与 `touchable`，不能只隐藏 GameObject。

### 5.4 打开时序

```text
OpenFairyUIFormAsync(uiId, userData, ownerToken)
  -> 解析生成的描述符
  -> FairyPackageManager.AcquireAsync（包及其依赖）
  -> 检查取消状态和版本
  -> GF UIManager.OpenUIForm（唯一描述符资源名）
  -> FairyUIFormHelper 创建对象池宿主
  -> FairyUIFormLogic.OnInit 创建 GComponent、binding 和 presenter
  -> GF OnOpen 分发业务生命周期
  -> 视图就绪后完成成功事件/任务
```

GF 的辅助器回调是同步边界，因此包在调用 GF 打开前必须准备完成。首版采用包级预加载，
不在辅助器中塞入不可等待的异步单项加载。

关闭、所有者销毁或版本变化发生在任一 `await` 后时，租约必须释放，且不得继续调用 GF 打开。
若 GF 打开失败，失败处理同样释放租约并保留原始错误。

## 6. FairyPackageManager 包管理器

### 6.1 状态模型

每个包只有以下稳定状态：`Unloaded -> Loading -> Ready -> Releasing -> Unloaded`，失败回到
`Unloaded`。同一包的并发 Acquire 共用一个加载操作；每个成功调用得到独立租约。

包条目记录：

- 稳定包 ID/名称和描述符资源；
- 资源清单与包依赖 ID；
- 进行中的操作、代次/版本和取消状态；
- 活跃租约数量；
- GDK Resource 句柄；
- 已注册的 `UIPackage` 实例；
- 最后错误与诊断上下文。

### 6.2 所有权与卸载

FairyGUI 加载器回调只返回由 GDK Resource 持有的对象，并设置 `DestroyMethod.None`。卸载顺序固定为：

1. 阻止新视图并确认活跃视图/租约为零；
2. 销毁 GComponent/绑定；
3. `UIPackage.RemovePackage`；
4. 释放图集/字体/音频/描述符等 GDK 句柄；
5. 清空条目并推进代次。

首版只在最后一个租约释放或关闭时卸载。空闲超时/LRU 仅在测量证明反复加载成为问题时添加。

### 6.3 失败规则

- 清单缺失、资源类型不匹配和依赖环在加载前失败；
- 底层失败只在包管理器记录一次完整上下文，上层保留原因，不重复刷屏；
- 取消不是错误，但必须走与失败相同的清理断言；
- 关闭时等待或取消进行中的加载，按依赖逆序释放；
- 通过代次丢弃迟到的过期完成结果，不能复活已关闭的包。

## 7. 类型安全绑定与表现层契约

优先采用 FairyGUI 官方生成类能力，配置 `getMemberByName=true`，再增加 GDK 薄层生成器输出
描述符、注册表与过期哈希；不重新实现 FairyGUI XML 到 UI 类的完整编译器。

建议契约：

```text
IFairyViewBinding
  Component
  BindAll()
  UnbindAll()

IFairyUIFormPresenter
  OnInit(context, binding, userData)
  OnOpen/OnUpdate/OnPause/OnResume/OnCover/OnReveal/OnRefocus/OnClose/OnRecycle
```

这里不为单个产品建立多层接口/工厂。一个生成的注册表直接按稳定键创建绑定和
表现层；只有核心层与热更新边界需要的接口才保留。

`FairyUIFormContext` 提供：

- 当前 UI ID、序列 ID、分组和所有者令牌；
- 关闭/重新聚焦；
- GDK 声音、本地化、设置、事件、资源、实体；
- Fairy UIWidget 创建/关闭；
- 受所有者生命周期约束的订阅和加载辅助。

## 8. GameHot 适配器

现有 `StarForceUIForm`/MonoBehaviour 表单改为普通类型表现层。生成注册表位于 GameHot 热更新
程序集，核心层通过非热更新接口调用，不直接引用具体表现层。

迁移后业务代码只操作生成绑定，例如 `binding.RefreshButton`，事件在打开/关闭时对称绑定。
EventContainer、EntityContainer、ResourceContainer 的“随界面清理”语义进入上下文所有者，而不是
继续依赖从 GameObject 子树扫描 UGUI widget。

## 9. ET 适配器

保留以下 ET 契约：

- `UIComponent` 创建/查找/关闭 UI Entity；
- `IUGFUIFormOn*` 生命周期与 `UGFSystemSingleton` 分发；
- ModelView 持有状态，HotfixView System 持有行为；
- 所有者销毁时取消异步并释放视图/包租约。

`UGFUIForm<T>` 的 `T` 从 `AETMonoUGFUIForm` 改为生成的 FairyGUI 绑定类型；ET UIWidget 变为绑定
`GComponent` 的子 Entity。不要在 ET 层再创建 UIPanel、GameObject 控件或单独的包缓存。

## 10. GDK 服务桥

### 10.1 本地化

`Design/Excel/Localization.xlsx` 继续是唯一文本来源。生成阶段按 FairyGUI 包/组件/成员
生成翻译 XML/字节数据，在 `UIPackage.AddPackage` 和任何 GComponent 创建前通过 FairyGUI
翻译 API 应用。

覆盖静态文本、按钮标题、列表项、齿轮和组件属性。MVP 沿用现有设置保存后重启语义，避免为实时
切换引入所有已打开视图的重建协议；以后若产品要求实时切换，再增加原子重建与焦点/状态恢复。

### 10.2 声音

FairyGUI 默认通过 `Stage.PlayOneShotSound` 播放按钮和过渡音频，会绕过 GDK 声音组。
集成层应禁用/替换该直接路径：资源 URL 映射到 `UISound`/GDK asset，播放请求转发
`GameEntry.Sound`，音量和静音读取现有 Setting。音频对象仍由 GDK Resource 持有。

### 10.3 安全区域与分辨率

全局分辨率/内容缩放器只初始化一次。每个 UIGroup 提供安全区域/全屏两个明确容器；
安全区域容器根据 `Screen.safeArea` 转换到 GRoot 坐标并更新位置/尺寸，页面通过 FairyGUI 关系适配。
方向或安全区域变化时统一重算，不在每个界面复制逻辑。

### 10.4 输入与焦点

指针、触摸、键盘与输入法采用 FairyGUI SDK 已有支持。GDK 适配器负责 Input System 操作到
焦点移动/确认/取消的映射、默认焦点、弹窗焦点圈、关闭后焦点恢复和无效节点跳过。

### 10.5 色觉无障碍

色觉能力分两层：

- 生产门禁：复用现有红/绿/蓝矩阵做 Editor/AI GameView 截图，并 lint 只靠颜色表达状态的问题；
- Player：先验证现有相机/后处理 `ColorBlindnessEffect` 是否覆盖 FairyGUI 最终输出。若不覆盖，
  使用经过测量的最终合成阶段；不默认给整个 `GComponent` 添加 `ColorFilter`，因为它会触发
  绘制模式/RenderTexture 成本。

无论技术路径如何，颜色标记与图标/文字冗余提示是主要无障碍手段，滤镜是模拟/辅助能力。

## 11. 启动引导与资源构建

启动引导包只包含更新进度、错误、重试、退出和最小字体/图集，并随初始 Player 内置。
它在常规资源更新前由非热更新加载器初始化，不依赖 GameHot/ET 热更新程序集或可下载包清单。

普通包进入 GameHot 与 ET ResourceRule；清单明确列出描述符、图集、字体、音频和依赖，
禁止通过字符串前缀猜资源。构建后校验 manifest 中每个逻辑 package 的产物都存在。

## 12. 迁移策略

采用“基础设施先行、垂直切片验证、再批量迁移”：

1. 固化单一事实来源、检查、发布、描述符和绑定。
2. 完成包管理器、GF 宿主/分组和代表性 GameHot POC。
3. 完成 ET form/widget 垂直切片。
4. 接入本地化/声音/安全区域/输入/色觉能力。
5. 迁移启动引导。
6. 按 GameHot、ET Demo、ET LockStep、RuntimeInspector、HUD/世界空间 UI 分批迁移。
7. 迁移或删除 GDK 自有 Editor UGUI 制作、检查、预览和 CodeBind 工具。
8. 删除 GDK 自有 Player/Editor UGUI 资源和代码，加入传递依赖边界守卫。

每批包含 UI ID 对照、行为测试、截图、资源清单和回滚提交。基础设施稳定前不同时迁移所有页面。

## 13. AI 闭环

```text
AI 修改仓库 XML/配置
  -> XML、引用和本地化检查
  -> FairyGUI CLI 发布
  -> 生成描述符、清单和绑定
  -> 过期与确定性检查
  -> 通过运行时发现的 Agent Bridge 命令执行 Unity 导入和编译
  -> 运行代表场景
  -> 截图矩阵及语义/坐标交互
  -> 日志及生命周期/资源断言
  -> 以 Git 修订版本为键归档证据清单
```

Agent Bridge 命令和参数在每个 Unity 会话中通过运行时 `list_commands` 发现，计划不硬编码不存在的
命令。自动化入口优先是少量无参、可重复、失败即抛出预期/实际值的 AgentCallable 或 Unity 测试。

## 14. 性能与可观测性

迁移前后使用相同设备、分辨率、场景和操作脚本，记录：

- 包首次/二次获取与界面打开时间；
- 主线程峰值、每帧 GC 分配量和 99 分位帧时间；
- UI 绘制调用、过度绘制、RT/绘制模式和 GPU 时间；
- 包/图集/字体常驻内存与最后一个租约释放后的回收；
- Player 包大小和启动引导增量。

日志只在包/宿主负责边界记录一次：稳定 UI ID、包 ID、状态转换、代次、租约
数量和原始失败原因。测试可读取只读诊断快照，业务不依赖内部条目。

## 15. 未采用的方案

- **继续扩展当前 `AFairyUIForm`**：仍继承 UGUI、每个界面一个 UIPanel，无法修复视图就绪和分组
  语义，拒绝。
- **绕过 GF 直接管理 GRoot 页面栈**：会丢失 GDK UI ID/分组/对象池/ET 集成，拒绝。
- **实现新的非 GF `UIForm` 类型**：UGF `UIComponent` 有具体类型转换，需修改供应商核心，拒绝。
- **每个界面保留一个 UGUI 预制体**：不能达到仅使用 FairyGUI 的 Player 视图，且让 XML/预制体形成双事实来源，
  拒绝；只保留生成描述符和统一宿主。
- **首版实现单项级流式资源系统**：复杂度和同步边界风险高，包预加载加大小预算已足够，延后。
- **把整个 GRoot 永久加 FairyGUI `ColorFilter`**：可能触发 RT/绘制模式，未经测量不采用。

## 16. 推进与回滚

- 基础设施和每个迁移批次使用独立子任务与提交，子任务在代表性运行验证后才归档。
- 旧 UGUI 页面在对应 FairyGUI 页面通过 Player 门禁前不删除；通过后同一批次删除，避免永久双实现。
- Editor 工具按“仍有产品价值才迁移”的原则处理：资源检查、色觉预览、交互验证等能力接入新的
  FairyGUI/AI 工具链；需要窗口时使用 UI Toolkit/EditorGUI，批处理优先结构化 CLI。只服务 UGUI
  prefab 制作或 CodeBind 的能力直接删除，不再重建一套等价外壳。
- 回滚以 Git 提交为单位，不设计长期运行时后端开关。短期测试开关仅存在于迁移分支并在
  最终清理，防止维护两套生产路径。
- 包/描述符格式变更需版本字段；新旧资源混用时立即失败，不能用错误组件继续运行。
- 启动引导失败时保留最小日志和退出路径，不能依赖尚未初始化的普通 UI 错误弹窗。

## 17. 父子任务关系

父任务负责总体需求、边界、排序、跨子任务验收和最终集成评审。建议在本规划获批后逐个创建并启动：

1. `fairygui-cli-publish`（已存在并已关联）：无点击发布入口。
2. 单一事实来源、XML 检查、描述符/清单与绑定生成。
3. bootstrap 与 `FairyPackageManager`。
4. GF UIForm/UIGroup 宿主接入。
5. GameHot 类型化表现层接入。
6. ETUI 与 FairyGUI UIWidget 接入。
7. 本地化、声音、安全区域、输入和色觉无障碍。
8. 所有 Player UI 迁移。
9. Editor UXTool/CodeBind/UI 检查流程迁移与清理。
10. AI/AgentBridge 编译、运行、截图和交互验证。
11. GDK 全域 UGUI 清理、专用依赖清理、Player 构建与性能门禁。

有顺序依赖的子任务必须在各自 `implement.md` 中明确，不依赖父子树隐式表达。
