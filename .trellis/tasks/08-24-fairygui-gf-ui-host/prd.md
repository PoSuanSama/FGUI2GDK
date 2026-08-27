# FairyGUI GF UIForm 与 UIGroup 统一宿主

## 目标

以无 UGUI 组件的统一 GF `UIForm` 宿主和单一 FairyGUI `GRoot`/UIGroup 容器替换当前每窗体
`UIPanel` POC，同时保留 GDK 的 UI ID、资源身份、分组、深度、多实例、对象池、生命周期和异步打开语义。

## 当前差距

- `AFairyUIForm` 继承 `AExUIForm -> AUIForm`，运行时会添加 `Canvas`、`GraphicRaycaster` 和
  `RectTransform`，不满足 FairyGUI-only 宿主要求。
- 每个窗体创建独立 `UIPanel` 和变换隔离节点，没有把 GF UIGroup 映射为单一 `GRoot` 下的容器。
- GF 打开成功事件和现有 `OpenUIFormAsync` 在 FairyGUI 包、GComponent 和业务绑定就绪前完成；加载失败
  只留下一个空白的已打开 GF 窗体。
- `FairyDemoForm` 仍使用 `GetChild("...")`，官方 C# 绑定输出尚未进入仓库。
- `Assets/Res/UI/FairyGUI` 尚未进入 GameHot/ET 资源规则，当前证据只覆盖 Editor Assets 直读。

## 需求

### R1. 宿主与供应商边界

- 不修改 `Unity/Assets/Scripts/Library/UGF` 中的供应商核心。
- FairyGUI 窗体由具体 `UnityGameFramework.Runtime.UIForm` 承载，但宿主 GameObject 不包含任何 UGUI
  组件，也不为每个窗体创建 `UIPanel` 或 StageCamera。宿主只保留 GF 对象池身份并从 Hierarchy 隐藏；
  实际 FairyGUI 根组件必须直接成为框架 `UI Group - <name>` 的子节点。
- 每个 FairyGUI UIForm 使用唯一、可构建的轻量描述符资源名；描述符记录 schema、UI ID、包、组件、
  绑定/表现层键和预加载策略，不以 UGUI 预制体作为事实来源。

### R2. 打开时序与失败语义

- `OpenFairyUIFormAsync` 先加载并校验描述符，再获取包及依赖租约，最后进入 GF 同步打开边界。
- 自定义 UIForm helper 在返回 `IUIForm` 前同步完成 GComponent、绑定和表现层准备；任何失败必须让 GF
  产生打开失败，而不是产生空白成功窗体。
- 原始 `userData` 对 GF 事件和业务表现层保持可观察一致，不用包装对象改变调用方比较语义。
- 取消、打开期间关闭、GF 打开失败和过时代次都释放预加载租约、描述符和已创建视图。

### R3. UIGroup、深度与生命周期

- 全局只使用一个 FairyGUI Stage/GRoot；每个 GF UIGroup 的现有 GameObject 通过
  `Container(GameObject)` 进入 FairyGUI 显示树，不创建 `Fairy UI Group - <name>` 代理节点。
- UIGroup 深度决定分组容器顺序，组内深度决定窗体在容器内的顺序；多实例不得共享 GComponent。
- `Open/Pause/Resume/Cover/Reveal/Refocus/Close/Recycle` 与 FairyGUI 可见性、交互、焦点和业务生命周期
  对称映射；隐藏的 GF 宿主不得留下可见或可点击的 FairyGUI 对象。
- 最后一个视图关闭后释放其包租约；对象池回收不携带旧描述符、视图、绑定、表现层或所有者令牌。

### R4. 迁移兼容

- 现有 UGUI 页面迁移完成前，自定义 helper 可以保留一个显式、可删除的 GameObject prefab 兼容分支；
  FairyGUI 路径不得依赖该分支或创建 UGUI 组件。
- `AFairyUIForm`、`FairyDemoForm.prefab` 和每窗体 `UIPanel` POC 只保留到统一宿主演示通过；通过后在同一
  变更中删除或停止引用，不形成长期双 FairyGUI 宿主。
- 本子任务只迁移 FairyGUI 演示，不迁移 ETUI、UIWidget、生产页面、启动引导或 GDK 配套设施。

### R5. 资源构建

- GameHot 与 ET 资源规则显式包含 FairyGUI 描述符、manifest、包和外部图集/字体/音频资源。
- 资源路径必须来自生成描述符/manifest，不通过字符串前缀猜测依赖。
- Editor 资源模式和至少一个实际 ResourceCollection 构建/加载路径都能打开演示页面。

### R6. 验证与可观测性

- 诊断能够关联 UI ID、GF serial ID、描述符、包代次、租约数、UIGroup 和首个失败原因。
- 覆盖正常打开、并发、多实例、取消、打开中关闭、失败、pause/resume、cover/reveal、refocus、回收和
  shutdown。
- Unity 编译、Error 日志、GameView 交互/截图和资源基线必须通过 Agent Bridge 收集；`.NET` 构建只作
  补充类型检查。

## 验收标准

- [ ] AC01：FairyGUI UIForm 宿主不含 `Canvas`、`GraphicRaycaster`、`RectTransform` UI、`UIPanel`
  或每窗体 StageCamera；`MainView` 直接位于 `UI Group - Default`，且不存在额外
  `Fairy UI Group - Default`。迁移期 UIGroup 可继续由 `UGuiGroupHelper` 提供组级 Canvas 兼容，
  但不得把这些组件复制到 FairyGUI 窗体宿主。
- [ ] AC02：同一 GRoot 显示树复用 GF UIGroup GameObject 建立唯一低层容器，组深度、组内深度、
  多实例和重新聚焦顺序与 GF 运行时一致。
- [ ] AC03：`OpenFairyUIFormAsync` 仅在描述符、包、GComponent、绑定和表现层可交互后成功，原始
  `userData` 未被替换。
- [ ] AC04：缺描述符、缺包、错误组件/绑定、GF 打开失败和取消均返回稳定失败，不留下 GF 窗体、
  GObject、包租约或 Resource 句柄。
- [ ] AC05：pause/resume、cover/reveal、Visible、close/recycle 同时正确控制 FairyGUI 可见性、
  touchable、焦点和业务生命周期。
- [ ] AC06：演示业务使用官方生成绑定，不包含 `GetChild("...")` 或反射注册；仅重新排序 XML 节点后
  绑定仍工作。
- [ ] AC07：GameHot 与 ET 资源规则包含 FairyGUI 运行时资源，Editor Assets 模式和实际资源构建路径
  都能加载演示描述符与包。
- [ ] AC08：连续 100 次打开/关闭、取消、覆盖/恢复和对象池复用后，GF 实例、GObject、包诊断和资源
  引用回到基线。
- [ ] AC09：Unity Agent Bridge 编译为 0 error，Error 日志为空；16:9、19.5:9 和 4:3 GameView 截图及
  按钮交互通过。
- [ ] AC10：统一宿主演示通过后，旧 `AFairyUIForm` 每窗体 UIPanel 路径不再被运行时代码或 UI 配置引用。

## 非目标

- 不迁移 ETUI/UIWidget 或现有生产页面。
- 不在本任务实现本地化、声音、安全区域、手柄焦点、色觉模式、启动引导或全域 UGUI 清理。
- 不实现 LRU、单项流式资源、长期双后端开关或新的 GF `IUIForm` 供应商类型。
