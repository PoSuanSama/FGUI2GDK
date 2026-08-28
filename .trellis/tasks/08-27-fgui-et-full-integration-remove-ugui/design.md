# Design — FGUI ET 完整接入并移除 UGUI 栈

## 1. 目标架构（方向 A）

保留 `GameFramework.UI` 语义层不动，用一套 FairyGUI 原生实现替换 UGUI 绑定层。

新增（放 `Game` 程序集，两模式共享、非热更）：

- `FairyUIForm : IUIForm`：`Handle` = `GComponent`；`OnInit/OnOpen/OnClose/OnPause/OnResume/OnCover/OnReveal/OnRefocus/OnUpdate/OnDepthChanged` 转给 Presenter，并把 `visible/touchable/sortingOrder` 落到 GComponent。
- `FairyUIGroupHelper : IUIGroupHelper`：`SetDepth` 映射到 FairyGUI 分组深度。
- `FairyUIFormHelper : IUIFormHelper`：`InstantiateUIForm` = 加载 UIPackage + `CreateObject`；`CreateUIForm` = 建 `FairyUIForm`；`ReleaseUIForm` = 释放 GComponent + 包。
- `FairyUIManager`：封装 `GameFramework.UI.UIManager` + 资源/包加载，作为新入口（替代 `GameEntry.UI`）。

删除：`FairyUIFormLogic / FairyUIFormHost / FairyUIGroupContainer / GDKUIFormHelper`（UGUI 胶水），`FairyUIFormService` 改为调用 `FairyUIManager`。

## 2. 程序集归属（核心难点）

### 已确认约束
- `Game`：defineConstraints 空 → GF/ET 两模式都编译。
- `Game.Hot.Code`：要求 `UNITY_GAMEHOT` → 仅 GameHot 编译。
- `Game.ET.Code.{ModelView,HotfixView,Hotfix}`：要求 `UNITY_ET` → 仅 ET 编译。

### 方案：基础设施共享、界面逻辑热更

- FairyGUI **基础设施**（`FairyUIManager`、`FairyUIForm`、Helper、描述符解析、包管理、`IFairyUIPresenter`、`FairyUIPresenterAttribute`、注册表、生成的 `Package1` 绑定类）→ 全部下沉到 `Game`（或一个独立的 FairyGUI 绑定程序集），两模式共享。
- 业务界面逻辑 → 保持热更：GameHot 模式在 `Game.Hot.Code`（`IFairyUIPresenter` 类 + Attribute 反射注册，无 ET0004 约束）。
  ET 模式采用 **Component/System 双态**（见第 8 节决策）：状态在 `Game.ET.Code.ModelView` 的
  `FairyUIFormComponent` 子类，行为在 `Game.ET.Code.HotfixView` 的 static Entity System，
  经 ModelView 的 `FairyUIFormSystemDispatcher` 运行时派发，不用反射注册表。

### 热更边界（已定）
- 选 A：界面逻辑热更。GameHot 与 ET 各持一份界面逻辑。
- ET 侧**完整复刻背包演示**：4 个界面（Demo/背包/详情/遮罩）在 ET 侧按 Component（ModelView）/System（HotfixView）各写一份，交互对齐（多实例/置顶/覆盖/拖拽）。

## 3. ET 接入方式

- ET 的 `UIComponent`（Entity）通过共享的 `FairyUIManager` 打开界面；打开/关闭语义走 `GameFramework.UI`，不再走 `UGFUIForm`。
- ET 侧界面逻辑按第 8 节拆为 Component/System：状态放 ModelView，行为放 HotfixView，由 `FairyUIFormSystemDispatcher` 派发；ET 启动流程不再反射扫描 Presenter Attribute。
- 删除 ET 的 `UGFUIForm` 体系（`AETMonoUGFUIForm`、`UGFUIForm`、`UIForm*Component`、`MonoUIForm*`、Demo/LockStep 界面）。

## 4. 遗留问题解法

- 问题 3（AssetName 重载）：删 UGUI 界面后，`AssetName` 只服务 FGUI，移除 `normalAny` 的 prefab 分支，回退为单一语义。
- 问题 4（双份 UI.xlsx）：ET 接入后统一两份表（FGUI 行与字段一致）。
- 问题 6（宿主胶水）：R1 完成即消除。
- 问题 2（CSName 字符串）：`FairyUIPresenterAttribute` 参数改为强类型（如 `UIFormId` 常量或类型 `typeof`），运行时用 `GetTypeId` 或常量匹配，消除拼写字符串。
- 问题 5（编排）：新增一个脚本把 `luban export → 描述符生成 → 资源刷新` 串成单命令，并移除冷启动环。

## 5. 兼容与迁移

- 分阶段迁移，每阶段可编译、可回滚：
  1. 新增 `FairyUIForm/Helper/UIManager`（不删旧宿主），让 FairyGUI 走新入口。
  2. 切换 `FairyUIFormService` 到新入口，验证 GameHot。
  3. 下沉基础设施 + Presenter 抽象到 Game，ET 侧接入，验证 ET。
  4. 删除 UGUI 界面 + 绑定层 + 旧宿主胶水。

## 6. 关键风险 / 待验证点

阶段 1 已确认：
- F1：`IUIManager` 单例不能二次初始化。`SetObjectPoolManager` 无条件重建 "UI Instance Pool"，需 `HasObjectPool` 预检幂等（已修复）。
- F2：UGUI 组名冲突。`GameEntry.prefab` 的 `UIComponent` 在 PlayMode 已注册 "Default"/"Pop"（GDKUIGroupHelper），与 `FairyUIManager` 需要的同名 `FairyUIGroupHelper` 组冲突，且 `IUIManager` 无 remove-group API。
- 过渡策略（已定）：阶段 2 切换 `FairyUIFormService` 到 `FairyUIManager` 的同时，移除 `UIComponent` 的 UGUI 组注册，让 `FairyUIManager.AddUIGroup` 接管组；并同步删除 UGUI 界面（它们本就依赖这些组）。

## 7. 关键风险 / 待验证点

- `GameFramework.UI.UIManager` 是否被其他系统（Entity/Scene）间接依赖，删除 `UnityGameFramework.Runtime.UI` 前需确认依赖闭包。
- [阶段1已确认] `UIManager` 是 `GameFramework` 模块，经 `GameFrameworkEntry.GetModule<IUIManager>()` 获取；契约：`uiFormAssetName -> IResourceManager.LoadAsset(异步) -> InstantiateUIForm(同步) -> CreateUIForm`。异步桥接：FairyUIFormService 先 AcquireAsync 包、再 UIPackage.CreateObject 得 GComponent，走一个已实例化路径交回 UIManager，避免在同步 InstantiateUIForm 内做异步。
- ET 的 `UIComponent` 目前是 Entity 范式，直接调用共享 `FairyUIManager` 会引入"Entity 世界外的服务"，需确认 ET 侧可接受（或做轻量 Entity 包装）。

## 8. 2026-08-28 实编译纠偏与架构决策（已定：Entity/System dispatcher 同构方案）

### 8.1 阻塞回顾

- 原“把有状态 Presenter 整类放 HotfixView”方案与 ET 分析器冲突：`ET0004` 无条件禁止 Hotfix 程序集
  的属性和非 const 字段（`Share/Analyzer/Analyzer/HotfixProjectFieldDeclarationAnalyzer.cs`），
  `EnableClass` 不能豁免；ModelView 又不能反向引用 HotfixView（generation 137 的 9 个 CS1061）。
- **决策**：采用与原 `UGFUIForm`/`UGFSystemSingleton`（提交 8b39d6cc 删除前，`ET/Loader/UGF/UIForm/`）
  同构的 Entity/System dispatcher 方案。禁止关闭分析器或把实例状态改成全局静态字段。

### 8.2 同构方案

| 原 UGFUIForm（8b39d6cc^） | FGUI 对应 |
| --- | --- |
| `UGFUIForm : Entity` 在 Loader 保存窗口状态 | `FairyUIFormComponent : Entity` 在 ModelView 保存 `FairyForm/View/UserData/IsShutdown` |
| `IUGFUIFormOn*` marker + `IUGFUIFormOn*System : ISystemType` | `IFairyUIFormOn*` + `IFairyUIFormOn*System`（ModelView） |
| `UGFUIFormOn*System<T>` 抽象基类（`[EntitySystem]`） | `FairyUIFormOn*System<T>` 同构，方法名与 `[EntitySystem]` 方法名一致 |
| `UGFSystemSingleton` 运行时查表派发 | `FairyUIFormSystemDispatcher` 复用 `EntitySystemSingleton.TypeSystems` 查表（不再建第二套注册表） |
| `AETMonoUGFUIForm` 把 userData 交给 Entity 后派发 | `FairyUIPresenterAdapter : IFairyUIPresenter` 先写 Component 状态再派发 |
| HotfixView static System 纯方法 | 同款（不触发 ET0004） |

要点：

1. 状态只能在 ModelView（ET0004）；行为只在 HotfixView；非热更层不静态引用 HotfixView（运行时查表）。
2. 派发复用 `EntitySystemSingleton` 现有注册：`[EntitySystem]` 方法由 `ETSystemGenerator` 生成具体
   System 类，`EntitySystemSingleton.Awake` 统一注册进 TypeSystems；本方案不复制原 `UGFSystemSingleton`
   自建 TypeSystems 的部分。
3. GameHot 模式不受影响：继续用 `Game.Hot.Code` 的 `IFairyUIPresenter` 类（该程序集无 ET0004 约束）。

### 8.3 骨架（本批落地，未迁移业务）

- ModelView 新增：`FairyUIFormLifecycleSystems.cs`、`FairyUIFormComponent.cs`、
  `FairyUIFormSystemDispatcher.cs`、`FairyUIPresenterAdapter.cs`、`FairyDemoFormComponent.cs`。
- HotfixView 新增：`FairyDemoFormComponentSystem.cs`（最小示例，证明 ET0004/CS1061 同时消解）。
- Editor 新增：`FairyUIFormSkeletonSelfCheck.cs`（AgentCallable 骨架自检，EditMode 派发闭环回归）。
- ET0003 分析器白名单加入 `ET.Client.FairyUIFormComponent`（与原 UGFUIForm 豁免同构），
  `Share/Analyzer/Analyzer/EntityClassDeclarationAnalyzer.cs` 已改并重编译 DLL 部署到
  `Unity/Assets/Plugins/Share.Analyzer.dll`。
- 现有五个有状态 Presenter/Flow/Bootstrap 仍在 ModelView（P0-2 功能 owner 已通过）；后续批次按此骨架
  逐个迁移，迁移完成后删除 `UIComponentFairyUIBridge` 委托桥。

### 8.4 骨架验证证据（2026-08-28）

- 切换 UNITY_ET 后 Unity generation 171：0 error、0 warning；GameHot 模式 generation 172：0 error、0 warning。
- `ET.FairyUIFormSkeletonSelfCheck::RunFairyUIFormSkeletonSelfCheck` 通过：HotfixView 生成的
  `FairyUIFormOnOpenSystem/OnCloseSystem` 被 EntitySystemSingleton 注册进 TypeSystems，
  ModelView dispatcher 按 marker/接口查表派发成功，非实现接口派发安全无副作用。
- Console 证据：`FairyDemoForm OnOpen: view is not ready.`（HotfixView 行为真实执行，异常隔离正常）。
- 自检期间发现的框架约束（已写入自检注释）：`Entity.IsDisposed == InstanceId == 0`，EditMode 下无
  Fiber/Scene/对象池运行时，测试实体需反射赋 InstanceId 并在结束时归零而非 Dispose；PlayMode 真实
  注册链由 ET 冒烟覆盖。
- GDK 变更守卫 0 error（2 个 UNITY001 warning 为用户原有 ResourceRule 文件，非本批）。

### 8.5 供应商边界决策（P1 收口）

提交 8b39d6cc 删除的 `ET/Loader/UGF/UIForm/`、`UGFSystemSingleton` 属 GDK 自有 wrapper
（路径在 `Game/ET/Loader`，非 `Library/UGF/UnityGameFramework` 供应商核心），按“GDK 可替换包装层”
处理：不恢复，边界写入本设计。后续删除其它 `Library/UGF` 内容前仍需先做上游 diff 与依赖闭包证据。

## 9. 阶段 D 服务桥接面调查（2026-08-28）

### 9.1 本地化

- GDK 侧：GF `LocalizationComponent`（`Library/UGF/UnityGameFramework/Runtime/Localization/`，
  `GetString(key)` + `Language` 枚举）；`Game/Hot/Code/Generate/LocalizationKey.cs` 与 ET 同款
  生成 key 常量；`Game/Localization/XmlLocalizationHelper.cs` 加载 Luban 导出文本。
- FairyGUI 侧：`UIPackage.SetStringsSource(XML)` + `TranslationHelper.LoadFromXML`（格式
  `<string name="componentId-elementId">text</string>`）；无其他运行时 i18n 路径。
- 桥接方案：生成阶段把 Luban 四语言文本转成 FairyGUI 字符串 XML（每语言一份），bootstrap
  在 `AddPackage` 前按当前 `GameEntry.Localization.Language` 加载对应 XML；Package1 组件把
  文本节点改为绑定字符串 ID。方案批准后实现。

### 9.2 声音

- FairyGUI 侧：按钮/transition 声音走 `GComponent.__playSound` →
  `Stage.inst.PlayOneShotSound(clip, volumeScale)`，`Stage._audio` 是 Stage GameObject 上的
  AudioSource（`Stage.EnableSound/DisableSound` 可控），`soundVolume` 公开可设。
- GDK 侧：`SoundExtension.PlayUISound(uiSoundId)` 读 Luban `DRUISound` 表经 GF `UISound` 组播放；
  音量/静音在 `Constant.Setting`（`Setting.SoundVolume/UISoundVolume`）驱动 GF 声音组。
- 桥接方案：`FairyUIManager.Initialize` 时把 FairyGUI 声音路径接到 GDK——
  `Stage.soundVolume` 由 `Setting.UISoundVolume` 驱动；FairyGUI 的音频资源 URL 不直接放行，
  设计阶段在描述符里给按钮/transition 的 `sound` 字段映射 `UISound` ID（或 MVP：禁用
  FairyGUI 直接播放并给按钮 transition 绑定 GDK 回调）。方案批准后实现。

### 9.3 安全区 / 输入 / 焦点 / 色觉

- 安全区：`Screen.safeArea` 转 GRoot 坐标更新安全区容器（design.md §10.3 已定形态），
  待设计项目提供三宽高比验证场景。
- 输入：FairyGUI SDK 已支持指针/触摸/键盘；手柄导航与焦点恢复需 GDK InputSystem 映射
  （design.md §10.4），依赖游戏的实际输入需求，暂缓到有明确手柄场景时实现。
- 色觉：先验证现有 `ColorBlindnessEffect` 是否覆盖 FairyGUI 最终输出（design.md §10.5），
  未验证前不得给 GComponent 加 ColorFilter。

### 9.4 实施顺序

1. 本地化桥（生成 + bootstrap 应用 + 四语言验证）。
2. 声音桥（禁用/重定向 FairyGUI 直接播放 → GDK Sound）。
3. 安全区/方向变化。
4. 输入/焦点/手柄（有明确需求时）。
5. 色觉验证与语义颜色。
