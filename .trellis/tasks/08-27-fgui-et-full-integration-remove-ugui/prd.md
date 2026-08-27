# FGUI ET 完整接入并移除 UGUI 栈

## Goal

让 FairyGUI 成为 GDK 唯一、完整的 UI 系统：在 ET 侧完整接入；移除 UGUI 界面栈（方向 A：保留窗口管理语义、去掉 UGUI 绑定）；顺带解决与之耦合的接入遗留问题。

## Background

### 分层现状（方向 A 的成本依据）

GDK 的 UI 分两层，且 `GameFramework.UI` 已是纯语义层：

- `GameFramework.UI`：`IUIManager/IUIGroup/IUIForm/IUIGroupHelper/IUIFormHelper` + `UIManager`。`IUIForm.Handle` 是 `object`，`IUIFormHelper` 三个方法全是抽象 object 进出。grep 不到任何 Canvas/MonoBehaviour/UnityEngine 依赖。
- `UnityGameFramework.Runtime.UI`：`UIComponent/UIForm/UIFormLogic/DefaultUIFormHelper/DefaultUIGroupHelper`，把语义绑定到 MonoBehaviour + Canvas。

结论：方向 A 是"给 FairyGUI 写新的 `IUIForm`/`IUIGroupHelper`/`IUIFormHelper` 实现 + 一个新入口"，不是重写窗口管理语义。

### FairyGUI 当前寄生状态

- `FairyUIFormLogic : UIFormLogic`、`FairyUIFormService` 走 `GameEntry.UI.OpenUIForm`、`GDKUIFormHelper` 建 UIForm + 挂逻辑、`FairyUIFormHost/PreparedState/UIGroupContainer` 是"把 GComponent 塞进 MonoBehaviour+Canvas"的胶水。

### ET 侧 UI 现状

- ET 的 `AETMonoUGFUIForm : AUIForm`（`AUIForm` 是 GF UIFormLogic 封装），用 `UIComponent : Entity` + `UGFUIForm` 组件打开界面，生命周期经 `UGFSystemSingleton` 分发到 ET EntitySystem。同样依赖 UGUI 绑定层。

### ET 跑不通 FGUI 的根因

- Presenter/绑定/入口都在 `Game.Hot.Code`（`defineConstraints: UNITY_GAMEHOT`）；`SwitchToET` 移除 `UNITY_GAMEHOT`，该程序集不编译，`FairyUIPresenterRegistry` 无人注入。

## Requirements

- **R1 抽离窗口管理**：保留 `GameFramework.UI` 语义层；为 FairyGUI 新增 `IUIForm`/`IUIGroupHelper`/`IUIFormHelper` 实现（GComponent 作为窗口内容）与新入口，不再经 MonoBehaviour+Canvas。
- **R2 移除 UGUI 界面栈**：删除 Game.Hot（MenuForm/SettingForm/AboutForm/DialogForm/TutorialForm + StarForceUIForm）与 ET（UGFUIForm 体系）的 UGUI 界面代码、prefab，以及 `UnityGameFramework.Runtime.UI` 绑定层。
- **R3 ET 侧完整接入 FGUI 并复刻背包演示**：ET 的 `UIComponent` 能打开 FairyGUI 界面；在 ET 模板下完整复刻 4 个界面（FairyDemoForm / FairyInventoryForm / FairyItemDetailForm / FairyInventoryOverlayForm）及其交互（多实例、置顶、覆盖、拖拽）。程序集归属与热更边界重新设计。
- **R4 遗留问题**：解决 CSName 编译期约束（问题 2）与描述符生成/Luban 导出编排（问题 5）；问题 3/4/6 随 R1/R2/R3 自然消除。

## Acceptance Criteria

- [ ] GameHot 模式下 FairyGUI 背包/多实例/置顶/覆盖/拖拽冒烟测试通过，且不再依赖 `GameEntry.UI` / `UIFormLogic`。
- [ ] ET 模式下完整复刻背包演示（4 个界面 + 多实例/置顶/覆盖/拖拽），冒烟测试通过。
- [ ] UGUI 界面（MenuForm/SettingForm/AboutForm/DialogForm/TutorialForm + ET UGFUIForm 系列）及其 prefab、绑定层代码已删除，`GameEntry.UI` 相关 UGUI 入口不再存在。
- [ ] Presenter 通过编译期可校验的方式（非纯字符串）与 `CSName` 关联。
- [ ] 描述符生成与 Luban 导出有统一编排，不再需要手工顺序/冷启动环。

## Out of Scope（本次不处理，待定）

- GF Entity、HPBar、UXTool、RuntimeInspector、UIEntity/Widget：本次不移除，后续单独用 FairyGUI 实现。
- 遗留问题 1（编辑器工程 ↔ Git 双拷贝同步 + 关编辑器门禁）：本次 defer，单独排期。

## Open Questions

- 无（已收敛）。