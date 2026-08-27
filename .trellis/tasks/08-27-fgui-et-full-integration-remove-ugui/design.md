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
- 具体 **Presenter**（业务界面逻辑）→ 保持热更：GameHot 模式在 `Game.Hot.Code`，ET 模式在 `Game.ET.Code.HotfixView`（均实现 `IFairyUIPresenter` + 打 Attribute）。注册表由各模式启动时反射扫描本模式程序集并注入。

### 热更边界（已定）
- 选 A：界面逻辑热更。GameHot 与 ET 各持一份 Presenter（实现同一个 `IFairyUIPresenter`）。
- ET 侧**完整复刻背包演示**：4 个界面（Demo/背包/详情/遮罩）在 `Game.ET.Code.HotfixView` 各写一份，交互对齐（多实例/置顶/覆盖/拖拽）。

## 3. ET 接入方式

- ET 的 `UIComponent`（Entity）通过共享的 `FairyUIManager` 打开界面；打开/关闭语义走 `GameFramework.UI`，不再走 `UGFUIForm`。
- ET 侧 Presenter 放 `Game.ET.Code.HotfixView`，复用 `IFairyUIPresenter` + Attribute，由 ET 启动流程反射扫描 `Game.ET.Code.HotfixView` 注册；完整复刻 4 个背包演示界面。
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