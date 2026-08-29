# FairyGUI GF UIForm 与 UIGroup 统一宿主设计

## 1. 边界与前置条件

行为缺口位于 GDK 的 GF UI 适配层，而不是 FairyGUI SDK 或 GF 供应商核心。实施前必须满足：

1. 外部 FairyGUI Editor 修改已通过 `FromEditor` 收敛，`Sync -Mode Status` 为 `Equal`。
2. 仓库 manifest `-Check` 通过，真实发布产物和官方 C# 绑定与源 XML 同步。
3. Unity 工程存在已启用的 `.agentbridge/`，可执行编译、资源序列化和运行验证。

预期修改位于 `Tools/FairyGUI`、`Design/FairyGUI/GDK_FGUI`、`Unity/Assets/Scripts/Game/UI/FairyGUI`、
GameHot 演示、GameEntry helper 配置和 GameHot/ET 资源规则。明确不修改 UGF/FairyGUI 供应商源码。

## 2. 运行时数据流

```text
UI.xlsx: UI ID + CSName + AssetName + GF policy
  + GDK.json: CSName -> FairyGUI package/component/binding/presenter mapping
  -> generated FairyUIForm descriptor TextAsset
  -> OpenFairyUIFormAsync(uiId, originalUserData, ownerToken)
  -> resolve DRUIForm and load/validate descriptor
  -> acquire package lease and all explicit external assets
  -> validate component, binding and presenter; create prepared form payload
  -> GF UIManager.OpenUIForm(originalUserData)
  -> GDKUIFormHelper creates a hidden lightweight GameObject pool host from prepared payload
  -> FairyUIFormLogic adopts already prepared state and inserts the view directly below GF UIGroup
  -> GF OnInit/OnOpen and success event
  -> release preload ownership; form owns the prepared state
```

GF 在调用 `IUIFormHelper.CreateUIForm` 前已经把实例登记到内部集合，helper 抛出异常后不会回收该实例。
因此 schema、身份、包、外部资源、组件、绑定和 presenter 等可预期失败必须在进入 GF 前完成；helper
只能执行不可失败的装配和所有权移交。GF 事件仍接收原始 `userData`，内部 prepared payload 不替换它。

## 3. 组件职责

### 3.1 `FairyUIFormDescriptor`

版本化只读数据，至少包含 schema、UI ID、CSName、AssetName、包 ID/名称、组件 ID/名称、绑定键、
表现层键和预加载策略。Luban `UI.xlsx` 是 UI ID、CSName、AssetName、UIGroup、多实例和覆盖暂停策略的
唯一事实来源；`GDK.json` 只按稳定 CSName 声明 FairyGUI 特有映射，不重复 UI ID 或 GF 策略。生成器
必须联结两者并拒绝缺失、重复、身份漂移、basename 冲突和过期描述符。

### 3.2 `FairyUIFormService`

提供以 `uiId` 为唯一公共身份的 `OpenFairyUIFormAsync`，从 `DRUIForm` 读取 AssetName、UIGroup、
AllowMultiInstance 和 PauseCoveredUIForm，负责预加载描述符、完整异步包租约、绑定/presenter 预检、
取消/过时代次、调用 GF 打开和失败清理。它不接受调用方覆盖 GF 策略，也不维护页面栈；查询、关闭、
多实例和组规则继续由 `GameEntry.UI` 决定。

### 3.3 `GDKUIFormHelper`

作为迁移期唯一全局 helper：

- FairyGUI prepared payload 走 FairyGUI 路径，创建普通 Transform GameObject、`UIForm` 和
  `FairyUIFormLogic`，只执行不可失败的状态采用与所有权移交。
- 现有 GameObject prefab 输入暂时保留与 `DefaultUIFormHelper` 等价的兼容路径。
- 所有 UGUI 页面迁移后删除 prefab 分支；不使用布尔后端开关或双写页面。

不得依赖 `IUIFormHelper.CreateUIForm` 抛异常作为失败清理机制；GF 此时已经登记实例。意外异常仍记录为
不变量破坏，但正常错误必须在 service 调用 GF 前返回，并由 service 事务式释放 prepared payload。

### 3.4 `FairyUIFormLogic`

持有描述符、窗体包租约、GComponent、生成绑定、表现层和所有者取消源。它只采用 service 已完成的
prepared state；GF 生命周期只分发已就绪对象。owner token 在打开成功后仍持续生效，取消时按 serial ID
关闭对应 GF 窗体。关闭清理放在 `finally` 中，顺序为业务解绑、GComponent 从组移除并 Dispose、租约
释放、取消注册释放、引用清空，presenter 异常不得跳过资源清理。

### 3.5 `FairyUIRootService` 与 `GDKUIGroupHelper`

全局持有单一 GRoot。`GDKUIGroupHelper` 仍由 UGF 创建在既有 `UI Group - <name>` GameObject 上；
`FairyUIRootService` 使用 FairyGUI 官方 `Container(GameObject)` 将该节点接入 GRoot 的低层显示树。
`UserGameObject` 语义保留 UGF Transform 父级，因此不会再创建代理 GameObject，实际视图的 Transform
可直接位于 `UI Group - <name>` 下。服务按 group depth 排序低层容器，组内容器按
`depthInUIGroup` 排序视图。

GF 轻量宿主 GameObject 仍挂在对应 `UIGroup.Helper` 下以保留具体 `UIForm` 和对象池契约，但设置
`HideInHierarchy`，不充当 FairyGUI 视觉父节点。实际 FairyGUI 视图通过低层 Container 接入同一
GRoot 显示树；不修改 UGF/FairyGUI 供应商核心，也不保留第二棵可见 UI 树。

## 4. 包租约同步边界

`FairyPackageManager` 只有在包描述和 manifest 明确列出的所有外部资产成功加载后才能进入 Ready。
异步预加载租约包含完整依赖闭包；service 将其所有权转交 prepared payload，再由窗体采用。任何中间
失败都按依赖逆序释放，迟到完成结果继续由包代次拒绝；外部资产失败必须使 acquisition 失败，不能只记日志。

## 5. 生命周期映射

| GF | FairyGUI/表现层 |
| --- | --- |
| Prepare/OnInit | 创建 GComponent、绑定和表现层；直接加入 GF UIGroup 低层容器但初始不可见 |
| OnOpen | 应用原始 userData，显示、启用交互并分发 Open |
| OnPause | 隐藏并禁用交互；分发 Pause |
| OnResume | 恢复显示/交互；分发 Resume |
| OnCover/OnReveal | 保持 GF 可见语义，只分发并更新焦点候选 |
| OnRefocus | 使用新 userData 分发并恢复有效焦点 |
| OnDepthChanged | 更新 group 容器和组内子项顺序 |
| OnClose | 取消所有者、解绑、移除/Dispose 视图、释放租约 |
| OnRecycle | 清空所有身份、版本、绑定和诊断引用 |

`InternalSetVisible` 同时写 GComponent `visible` 与 `touchable`；轻量宿主 GameObject 的激活状态不是
FairyGUI 可见性的唯一来源。

## 6. 生成与资源

描述符和官方绑定都是派生输出。生成顺序固定为 XML/契约检查、FairyGUI 发布/官方绑定、描述符生成、
过期检查。GameHot 与 ET 资源规则分别加入同一 FairyGUI 资源目录，ResourceCollection 回读必须证明
descriptor、manifest、bytes 及外部资源均被收集。

## 7. 失败与回滚

- 描述符/schema/ID/包/外部资产/组件/绑定/presenter 错误在调用 GF 前失败。
- owner token 取消时关闭 GF 正在加载或已经打开的 serial ID，并释放两侧租约。
- helper 采用 prepared state 后不再执行可预期失败工作；任何意外失败都执行事务式回收并报告不变量破坏。
- 迁移期旧 UGUI helper 分支保持原行为；FairyGUI 演示可单独回滚到 POC 提交，但不保留运行时开关。
- 资源规则、GameEntry helper 引用和 Unity 资源通过 Agent Bridge 修改并与 `.meta` 同批提交。
