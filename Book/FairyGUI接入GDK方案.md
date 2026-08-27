# GDK 接入 FairyGUI 方案：保留 GF UI 框架，替换 UGUI 渲染层

## 1. 目标与核心原则

**目标**：在保留 GDK（UnityGameFramework/UGF）成熟 UI 框架能力的前提下，用 FairyGUI 替换
UGUI 作为唯一的渲染、视觉与输入层。

**核心原则**：

1. **框架层保留，渲染层替换**：`UIComponent`/`UIManager`/`UIGroup`/`UIForm`/`UIFormLogic`
   全部保留，只替换“渲染”与“视觉组件”。
2. **两套运行时独立，逻辑层映射**：FairyGUI（`Stage`/`GRoot`）与 GF（`UI`/`UIGroup`）各自
   保留独立根节点，通过 `Container(GameObject)` 加世界矩阵同步做逻辑映射；不做“反向一体化”，
   不合并两套系统的 GameObject。
3. **不修改 vendor core**：不改 FairyGUI，不改 `Library/UGF` 框架核心，只通过 helper 扩展点
   与适配层接入。

## 2. 架构边界

### 2.1 保留（GF 框架层，不改）

| 层 | 组件 | 职责 |
| --- | --- | --- |
| UI 管理 | `UIComponent`、`UIManager` | UI 注册、实例管理、对象池、资源加载 |
| UI 分组 | `UIGroup`、`UIGroupHelper` | 深度分组、显示顺序、暂停覆盖 |
| UI 实例 | `UIForm`、`UIFormLogic` | 生命周期状态机、可见性、深度 |
| 资源 | `ResourceComponent`、`ResourceCollection`、`UIFormId` | 资源加载与 ID 定义 |
| 扩展点 | `UIFormHelper`、`UIGroupHelper` | 自定义创建/释放逻辑 |

### 2.2 替换（UGUI 渲染层 → FairyGUI）

| UGUI | FairyGUI | 说明 |
| --- | --- | --- |
| `Canvas` + `CanvasScaler` | `Stage` + `UIContentScaler` + `GRoot.contentScaleFactor` | 根与缩放 |
| `GraphicRaycaster` + `EventSystem` | `StageEngine` + `Stage.HitTest` | 输入 |
| `Image`/`Text`/`Button`/`RectTransform` 布局 | `GComponent`/`GButton`/`GTextField`/逻辑尺寸 | 视觉组件 |
| UI Camera | `StageCamera` | 渲染相机 |

### 2.3 独立（两套系统各自的根，不合并）

- FairyGUI：`Stage`（`DontDestroyOnLoad` 跨场景）→ `GRoot` → 逻辑 `Container`。
- GF：`UI` 节点 → `UI Form Instances` → `UIGroup`。

## 3. 改造范围（分层次）

### L0 接入层（已有，需从 Demo 泛化为通用层）

- `FairyUIFormService` / `FairyUIFormLogic` / `FairyUIFormHost` / `FairyUIFormPreparedState`
- `FairyUIRootService` / `FairyUIGroupContainer`
- `GDKUIGroupHelper` / `GDKUIFormHelper`
- `FairyPackageManager` / `FairyPackageCatalog`
- `FairyUIPresenterRegistry` / `IFairyUIPresenter`
- `FairyUIFormDescriptor` 及其生成器

### L1 渲染层去 UGUI

- FairyGUI 表单宿主不挂 UGUI 组件（`Canvas`/`RectTransform`/`GraphicRaycaster`）——已做到。
- `StageCamera` 作为 FairyGUI 唯一渲染相机。
- 输入统一走 FairyGUI `HitTest`。

### L2 业务 UI 迁移

- GameHot UGUI 表单 → FairyGUI 组件 + presenter：
  - `MenuForm` → 菜单组件
  - `SettingForm` → 设置组件
  - `AboutForm` → 关于组件
  - `DialogForm` → 对话框/弹窗组件
  - `TutorialForm` → 教程组件

### L3 资源与生成闭环

- FairyGUI `.fairy` 包作为 UI 事实源。
- 生成：Package Binder（如 `UIMainView`）、UIForm 描述符（如 `FairyDemoForm.json`）。
- Luban `UI.xlsx` → `UIFormId`。
- 资源规则：FairyGUI 目录纳入 `ResourceCollection`。

### L4 输入与相机统一

- 停用并移除 UGUI `EventSystem` + `GraphicRaycaster`（确认无 UGUI 残留后）。
- 明确 `StageCamera` 与 GF `UI Camera` 的归属关系。

### L5 工具与文档

- FairyGUI 发布/同步/manifest 工具链。
- `Book/` 与 `AGENTS.md` 文档更新。

## 4. 迁移路径（分阶段，每阶段可独立验证）

### 阶段 1：接入层固化（当前已完成大半）

- 把 Demo 专用逻辑泛化：presenter 注册、descriptor 生成、包管理。
- 验收：非 Demo 表单也能走通打开/关闭/回收。

### 阶段 2：单表单试点去 UGUI（当前已做）

- `FairyDemoForm` 完全不依赖 UGUI 组件。
- 验收：层级断言、视锥、输入、100 次回收、shutdown Error=0。

### 阶段 3：业务 UI 逐表单迁移

- 按优先级迁移（先弹窗/对话框，再菜单/设置/关于/教程）。
- 每个表单：FairyGUI 组件 + presenter + descriptor + 资源规则。
- 验收：迁移的表单走 FairyGUI，未迁移的仍走 UGUI（并存）。

### 阶段 4：移除 UGUI 遗留

- 停用并删除未使用的 UGUI 表单、资源、`EventSystem`、`GraphicRaycaster`。
- 处理 `UI Form Instances` 的 Canvas 依赖（若 UIGroup 不再需要 `RectTransform`）。
- 验收：无 UGUI 组件残留，纯 FairyGUI 渲染。

### 阶段 5：工具与文档固化

- 完善工具链、生成器、文档。
- 验收：新 UI 从设计到运行全链路可复现。

## 5. 回滚点

### R1 阶段 1-2：接入层/试点

- 边界：FairyGUI 接入层独立，不影响现有 UGUI。
- 回滚：删除接入层 + 恢复 `GameEntry` 的 helper 配置为默认 UGUI helper。

### R2 阶段 3：业务迁移

- 边界：逐表单迁移，UGUI 与 FairyGUI 并存。
- 回滚：每个表单可单独回退（保留 UGUI 源，停用不删除）。

### R3 阶段 4：移除 UGUI（不可逆点）

- 边界：删除 UGUI 表单、资源、输入系统。
- 回滚：通过 git 历史回滚，不在运行时回退。
- 触发条件：所有 UGUI 表单迁移完成 + 验收通过 + 备份。

## 6. 完美接入的最终形态

### 6.1 最终层级

```text
GameEntry
└── Builtin
    └── UI                               (GF 根, UIComponent)
        ├── UI Form Instances            (GF UIGroup 容器)
        │   ├── UI Group - Default
        │   └── UI Group - Pop
        └── Stage                        (FairyGUI 根, 归入 UI 下)
            └── GRoot
                └── (逻辑树: Container(UIGroup) → 各 FairyGUI 表单)
```

### 6.2 生命周期映射（GF UIFormLogic ↔ IFairyUIPresenter）

| GF 生命周期 | FairyGUI presenter | 说明 |
| --- | --- | --- |
| `OnInit` | `OnViewReady` | 首次初始化 |
| `OnOpen` | `OnOpen` | 每次打开 |
| `OnPause` | `OnPause` | 被覆盖 |
| `OnResume` | `OnResume` | 恢复 |
| `OnCover` | `OnCover` | 遮挡 |
| `OnReveal` | `OnReveal` | 遮挡恢复 |
| `OnRefocus` | `OnRefocus` | 重新激活 |
| `OnUpdate` | `OnUpdate` | 每帧 |
| `OnClose` | `OnClose` | 关闭 |
| `OnRecycle` | — | 对象池回收 |
| `OnDepthChanged` | — | 深度排序 |

### 6.3 输入与渲染统一

- 输入：FairyGUI `Stage.HitTest` 作为唯一输入。
- 渲染：`StageCamera` 作为唯一 FairyGUI 渲染相机。
- 对象池：GF 对象池（`UIForm` 实例）保留，FairyGUI 的 `GComponent` 由 prepared state 管理。

### 6.4 资源与生成闭环

```text
Design/FairyGUI 包 → 发布 → Assets/Res/UI/FairyGUI → manifest
    → 生成 Binder/Descriptor → Luban UIFormId → ResourceCollection
```

## 7. 关键风险与对策

| 风险 | 对策 |
| --- | --- |
| 双树不一致 | 世界矩阵同步 + 层级断言（已实现） |
| 缩放叠加 | Stage 归入普通节点（非 Canvas），GRoot 保持 Stage 子节点 |
| 生命周期/取消竞态 | 幂等取消/释放 + 100 次回收回归（已实现） |
| shutdown 顺序 | `isDisposed` 防护（已实现） |
| 包引用计数泄漏 | 引用计数 + 反向释放 + 诊断回读（已实现） |
| 输入冲突 | 统一 FairyGUI 输入，移除 UGUI 射线 |
