# FairyGUI 完整接入 GDK 与 AI UI 闭环实施计划

## 规划门禁

- [x] 用户确认完全不使用 UGUI 覆盖 GDK 自有 Player 与 Editor UI 工具链。
- [x] 已移除待确认问题，完成最终 PRD 收敛整理。
- [x] 用户在最终规划摘要之后另行明确批准开始实现。
- [ ] 不启动父任务本身；父任务用于计划/集成管理，依次创建和启动实际交付子任务。
- [ ] 每个子任务开始前使用 `trellis-before-dev` 读取当前代码与适用规范，并重新检查已有改动的工作树。

## 工作流顺序

### 0. 基线与清单

- [ ] 冻结当前 UI ID、UI.xlsx 行、UIGroup 策略、GameHot/ET 调用点和 Player 可达 UI 清单。
- [ ] 记录当前 GameHot、ET Demo、ET LockStep、内置界面、RuntimeInspector、HUD/HPBar、世界空间叠加 UI、
  UIWidget 的截图与交互行为。
- [ ] 在相同设备/分辨率下记录 CPU、GC、帧时间、GPU、绘制调用、过度绘制、常驻内存、首次/二次
  打开、资源卸载和 Player 包大小基线。
- [ ] 建立 GDK 全域 UGUI 使用清单，区分自有 Player、自有 Editor、UGUI 专用直接依赖和无法避免的
  Unity/第三方传递依赖；前三类全部进入迁移/删除范围。
- [ ] 记录当前相关资源、预制体和 `.meta`，后续迁移禁止拆分 Unity 资源与 `.meta`。

门禁：基线证据必须可按 Git 修订版本、场景、分辨率和模式复现，否则不得开始删除旧 UI。

### 1. 发布入口（现有子任务）

- [ ] 完成 `08-20-fairygui-cli-publish` 的插件式命令行发布。
- [ ] 验证成功、包不存在、插件未加载、超时和许可限制路径。
- [ ] 真实发布只写唯一临时目录；在本子任务完成前不覆盖版本化 Unity 产物。

门禁：发布结果必须有结构化结果、日志、非空描述符和明确退出码。

### 2. 单一事实来源、检查与生成

- [ ] 将 `Design/FairyGUI/GDK_FGUI` 固化为唯一事实来源。
- [ ] 修改同步工具：保存同步哈希，支持显式仓库到 Editor、Editor 到仓库，并在双边变化时失败。
- [ ] 用 XML 解析器实现包/组件/资源/成员/引用/控制器/齿轮/过渡检查。
- [ ] 校验稳定 ID、唯一业务成员名、包依赖环和本地化键。
- [ ] 配置官方绑定生成，启用按名称成员解析；只添加 GDK 需要的薄层描述符/注册表/过期哈希。
- [ ] 分别生成 GameHot 与 ET 热更新绑定/注册表，禁止业务字符串绑定和反射注册。
- [ ] 连续执行两次全流程并断言第二次无语义差异。

门禁：破坏一个测试 XML 引用、制造重复成员、改变稳定 ID 和制造过期输出均必须让门禁失败。

回滚：只还原同步/检查/生成器输入与脚本；不手改生成文件。

### 3. 启动引导与包管理器

- [ ] 定义带版本的包清单、依赖和每个界面的描述符数据契约。
- [ ] 创建最小启动引导包，并配置到初始 Player 资源。
- [ ] 实现 `FairyPackageManager` 状态机、合并加载、代次、取消和租约。
- [ ] 通过 GDK Resource 加载描述符、图集、字体、音频；FairyGUI 回调使用
  `DestroyMethod.None`。
- [ ] 实现依赖拓扑、循环检测、逆序关闭和 `UIPackage.RemovePackage` 后资源释放。
- [ ] 暴露只读诊断快照，用于测试状态、租约、资源句柄和最后错误。
- [ ] 为缺资源、并发获取、取消、重复释放、迟到的过期完成结果和关闭添加聚焦测试。

门禁：100 次获取/释放与取消/关闭后，条目、UIPackage、GObject 和 Resource 句柄回到基线。

回滚：启动引导包与普通包分离；包管理器未稳定前不迁移生产页面。

### 4. GF UIForm/UIGroup 宿主

- [ ] 实现无 UGUI 组件的统一宿主预制体/创建路径，保留具体 GF `UIForm`。
- [ ] 实现 `FairyUIFormLogic`、`FairyUIFormHelper` 和 `FairyUIGroupHelper`。
- [ ] 初始化单一 GRoot，为每个 GF UIGroup 创建 FairyGUI 分组容器。
- [ ] 映射分组深度、界面深度、可见性、可交互性、模态、暂停/覆盖/恢复和重新聚焦。
- [ ] 实现视图就绪的 `OpenFairyUIFormAsync`：先获取包，再进入 GF 同步打开边界。
- [ ] 处理打开中关闭、GF 打开失败、重复打开、多实例、对象池复用和关闭。
- [ ] 用最小代表界面验证完整 GF 生命周期，替换当前每个界面一个 UIPanel 的 POC。

门禁：GF 成功事件/任务触发时绑定与表现层已可交互；隐藏宿主不会留下可见/可点击 GObject。

回滚：保留旧 POC 仅到新垂直切片通过，不建立长期后端开关。

### 5. GameHot 表现层

- [ ] 定义核心层/表现层最小接口和 `FairyUIFormContext`。
- [ ] 将 Event/Entity/Resource/UIWidget 的所有者作用域清理能力从 MonoBehaviour 子树抽出到上下文。
- [ ] 生成 GameHot 表现层注册表，验证 HybridCLR 热更新程序集引用方向。
- [ ] 把 FairyGUI 演示改为生成绑定 + 表现层，不使用 `GetChild("...")`。
- [ ] 迁移一个包含按钮、列表、弹窗、资源加载和 UI 声音的代表性 GameHot 页面。
- [ ] 验证热更新代码加载、域/所有者切换、事件解绑和对象池宿主复用。

门禁：GameHot 代表流程通过生命周期、交互、热更新和泄漏测试后才进入 ET 适配。

### 6. ETUI 与 UIWidget

- [ ] 将 `UGFUIForm<T>` 的 View 契约改为 FairyGUI 绑定，同时保留 UGF 生命周期接口。
- [ ] 保留 `UGFSystemSingleton` 分发，核对 ModelView/HotfixView 程序集引用与生成位置。
- [ ] 将 ET UIWidget 改为绑定 GComponent 的子 Entity，定义父级/所有者销毁规则。
- [ ] 迁移 ET Demo Login/Lobby/Help 垂直切片。
- [ ] 迁移 ET LockStep Login/Lobby/Room 垂直切片，覆盖输入框、列表和高频文本更新。
- [ ] 验证 Entity 销毁、异步等待取消、界面关闭、场景切换和应用关闭。

门禁：ET 代表流程不创建 UGUI 控件/预制体/UIPanel，且实体/系统顺序与旧流程对照一致。

### 7. GDK 配套设施

- [ ] 从 `Design/Excel/Localization.xlsx` 生成 FairyGUI 翻译数据，并在包注册前应用。
- [ ] 验证四种语言的静态文本、按钮、列表、齿轮、属性和长文本布局；保持重启生效语义。
- [ ] 将 FairyGUI 按钮/过渡声音接入 GDK UISound、SoundGroup、静音和音量。
- [ ] 实现 GRoot 安全区域/全屏容器与安全区域坐标转换、方向变化更新。
- [ ] 接入指针/触摸/键盘/文本输入/手柄焦点适配器和关闭后焦点恢复。
- [ ] 复用现有色觉矩阵做 Editor/AI 预览，加入语义颜色检查。
- [ ] 验证现有相机效果是否覆盖 FairyGUI；若否，实现最小最终合成阶段并对比性能。

门禁：配套设施矩阵的功能、截图、日志和 Profiler 证据全部完成，不以单张截图替代交互验证。

### 8. 现有 Player UI 迁移

按以下批次逐一创建子任务；每批先迁移 XML/绑定/表现层，再验证，最后删除对应 UGUI 资源与代码：

- [ ] 启动引导更新/错误/对话框。
- [ ] GameHot Menu、Setting、About、Dialog、Tutorial 等表单。
- [ ] ET Demo 表单。
- [ ] ET LockStep 表单。
- [ ] UIWidget/UIPrefab 复用组件。
- [ ] RuntimeInspector 迁移为 FairyGUI 视图，并移除其 UGUI 包/资源路径。
- [ ] HPBar、HUD 和世界空间叠加 UI。
- [ ] 其他扫描发现的 Player 可达 UGUI UI。

每批必须：

- [ ] 更新 UI ID/AssetName/描述符对照，不静默改变 ID 或 UIGroup 规则。
- [ ] 覆盖成功、失败、取消、覆盖/恢复和重复打开/关闭。
- [ ] 产出 16:9、19.5:9、4:3 与四语言截图。
- [ ] 检查资源规则、构建产物、`.meta`、引用和 Player 加载。
- [ ] 对比基线行为与性能。
- [ ] 形成可独立回滚的提交边界。

### 9. AI 与 Agent Bridge 闭环

- [ ] 提供单一入口串联 XML 检查、发布、生成、过期/确定性检查。
- [ ] 在 Unity 会话首次操作前读取 AgentBridge `AGENT.md` 并运行时发现命令/架构/版本。
- [ ] 实现或复用少量聚焦 AgentCallable/Unity 测试：导入/编译、打开界面、点击/输入/导航、
  截图、资源快照、日志断言和清理。
- [ ] 生成证据清单：Git 修订版本、FGUI 源哈希、发布产物哈希、模式、分辨率、语言、
  色觉模式、测试结果及截图/日志/性能分析路径。
- [ ] 故障时保留首个可操作错误和日志，不覆盖源 XML 或已知良好产物。

门禁：AI 对演示 XML 做一个布局和文本修改后，可以无 GUI 点击完成从发布到运行验证，并得到机器可判定结果。

### 10. Editor UGUI 工具迁移与清理

- [ ] 盘点 UXTool、CodeBind、UI 检查器、预览、安全区、色觉、模板/Widget Library 和 Editor Agent
  命令的实际使用方与产品价值。
- [ ] 把仍有价值的结构检查、色觉预览、截图、交互、资源预算和本地化检查迁移到 FairyGUI XML lint、
  发布生成器、AI/Agent Bridge、UI Toolkit/EditorGUI 或结构化 CLI 工具。
- [ ] 删除仅服务 UGUI 预制体/RectTransform/Canvas/CodeBind 的菜单、配置、模板、资源和文档入口。
- [ ] 清理 GDK Editor asmdef 对 `UnityEngine.UI`、UGUI 扩展和 UXTool UGUI 运行时的引用。
- [ ] 验证 FairyGUI 项目、发布、结构检查、绑定、预览和质量检查流程无需打开或生成 UGUI 预制体。

门禁：GDK 自有 Editor 代码与工具资源通过零 UGUI 扫描，新 UI 完整流程不触发旧 UXTool/CodeBind。

### 11. GDK 全域 UGUI 清理与最终门禁

- [ ] 生成 GDK 自有运行时/Editor 代码/asmdef/资源/场景/预制体/配置的 UGUI 使用报告。
- [ ] 删除已迁移界面预制体、CodeBind 输出、UGUI 控件、辅助器/扩展和资源规则；Unity 资源与 `.meta`
  同批处理。
- [ ] 移除 GameHot、ET、共享 Game 和 Editor asmdef 中的直接 `UnityEngine.UI` 引用。
- [ ] 移除 SoftMaskForUGUI、UIEffect、UIParticle、LoopScrollRect、UGUI RuntimeInspector 等无剩余使用方的
  直接依赖、资源、link.xml/AOT 配置和文档。
- [ ] 从 `manifest.json` 移除 `com.unity.ugui` 直接依赖并由 Unity 重新解析锁文件；若它仍作为传递依赖
  存在，只记录来源，不分叉 Unity/渲染管线。
- [ ] 仅为 Unity/第三方包内部不可避免且 GDK 未引用的传递依赖建立审计清单，不建立 GDK 使用白名单。
- [ ] 运行全部生命周期/资源/配套设施/截图/交互回归测试。
- [ ] 完成至少一个目标平台 IL2CPP Player 构建与启动/资源更新/进入游戏冒烟。
- [ ] 复测完整性能矩阵；任何 >10% 回归必须优化或取得显式接受。
- [ ] 更新 UI 开发、FairyGUI、GameHot、ETUI、AI 操作、诊断和回滚文档。

最终门禁：PRD AC01-AC26 全部有实际证据，父任务做跨子任务集成评审后才能归档。

## 验证策略

### 静态检查与生成

```powershell
# 具体脚本名称由对应子任务确定；以下是必须覆盖的验证类别。
<sync-command> -WhatIf
<fairygui-xml-lint>
<fairygui-publish-to-temp>
<descriptor-and-binding-generate>
<stale-and-determinism-check>
rg -n "GetChild\(\"|UnityEngine\.UI|Canvas|GraphicRaycaster" Unity/Assets/Scripts/Game Unity/Assets/Res
```

### GDK 守卫

```powershell
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check
git diff --stat
```

### Unity 验证

Unity 查询与修改必须使用当前会话运行时发现的 Agent Bridge 命令，不在计划中硬编码命令名。
实际证据至少包括：

- Editor 导入/编译与错误日志；
- EditMode/PlayMode 聚焦测试；
- GameHot、ET Demo、ET LockStep 模式冒烟；
- 启动引导在普通资源不可用时的行为；
- 资源集合构建/加载；
- GameView 截图/交互矩阵；
- IL2CPP Player 构建/启动；
- Profiler 前后对比。

`Unity/Unity.sln` 或 `.NET` 构建只能作为额外类型检查，不能声称证明 Unity 运行时正确。

## 高风险变更面

- `Design/Excel/GameHot/Datas/Game/UI.xlsx` 与生成 UI ID：配置兼容边界，先改来源再生成。
- `Design/Excel/Localization.xlsx` 与生成本地化：不手改 bytes/生成 key。
- `Unity/Assets/Scripts/Game/UI`：共享运行时核心层，影响 GameHot 与 ET。
- `Unity/Assets/Scripts/Game/Hot/Code`：HybridCLR 热更边界。
- `Unity/Assets/Scripts/Game/ET/Loader/UGF/UIForm`、`UIWidget` 和 ET Code：Entity/System 生命周期边界。
- `Unity/Assets/Res/Editor/Config/ResourceRuleEditor_*.asset`：资源构建规则，需 Unity 回读和 Player 加载。
- `Unity/Assets/Res/Builtin`、GameEntry 启动引导预制体/场景：普通资源就绪前运行，失败会阻断启动。
- 旧 UI 预制体/场景/`.meta`：仅在等价路径验证后删除，禁止手工 YAML 批量改写。

## 回滚规则

- 每个子任务和页面批次使用独立可运行提交，回滚不依赖本地未提交文件。
- 生成器修改回滚生成器输入与全部派生输出，禁止只回滚一侧。
- Unity 资源移动/删除连同 `.meta` 和引用变更一起回滚。
- 启动引导、包管理器、宿主未通过各自门禁时，不允许进入大规模页面迁移。
- 不保留长期双后端功能开关；若短期迁移验证使用开关，最终清理前删除。
- 包/描述符版本不兼容时立即失败，回滚到上一整套资源与代码版本。

## 每个子任务启动前的评审清单

- [ ] 子任务拥有单一、可独立验证的交付物。
- [ ] 明确上游依赖、输入事实来源、生成输出和回滚点。
- [ ] 已读取相邻实现、asmdef、资源规则及适用 Trellis/GDK 规范。
- [ ] 没有把 Editor、Player 运行时、GameHot、ET 或启动引导边界混在一个实现层。
- [ ] 没有新增只有一个实现的接口/工厂、常驻监听器、LRU 或预想式扩展点。
- [ ] 验收包含失败/取消/重复/关闭，而不只包含成功路径。

## 2026-08-21 首个运行时纵向切片

- [x] 新增 `FairyPackageManager`，通过 GDK `ResourceComponent` 加载包描述符和外部资源，合并并发获取并使用引用计数租约卸载。
- [x] `AFairyUIForm` 持有包租约，覆盖关闭、打开失败、过时异步结果、暂停/恢复和覆盖/揭示生命周期。
- [x] FairyGUI `UIPanel` 保持归属 GF UIGroup/UIForm 层级，中间隔离节点抵消 Canvas 变换传递，并由 UIForm 显式持有和销毁。
- [x] `FairyDemoForm` 通过 GF UIForm 打开，`Package1/MainView` 成功创建并完成按钮计数交互。
- [x] Unity Agent Bridge 编译为 0 error / 0 warning；Stage Camera、层、材质、可见性和视锥检查通过；GameView 截图显示完整 FairyGUI 页面。
- [x] FairyGUI 项目 `-Check`、镜像 `Status=Equal`、Trellis context 校验和 GDK 变更守卫通过。
- [ ] 本轮仅完成包管理与 GF/GameHot 演示纵向切片；依赖包拓扑、取消令牌、统一 GRoot UIGroup 容器、ETUI、启动引导、配套设施和全量页面迁移仍按后续阶段实施。
