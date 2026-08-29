# FairyGUI 零 UGUI 归零与文档口径收口

## Goal

把「FairyGUI 是 GDK Player UI 唯一视图后端、GDK 自有资源零 UGUI」这条验收口径真正闭合：清除 `GameEntry.prefab` 里残留的两个 UGUI 静态节点，使 `Book/FairyGUI接入.md` 的零 UGUI 静态门禁归零，并同步修正文档与 HANDOFF 中 AC19 的「全域扫描 0」表述，使其与实现一致。

## Background and Confirmed Facts

收尾核验发现一个可复现的硬偏差：零 UGUI 静态门禁并非 0。运行 `Book/FairyGUI接入.md` 的原文门禁命令，`Unity/Assets/Res/GameEntry.prefab` 返回 2 处命中，对应两个 UGUI 静态残留节点：

| 节点名 | 组件 | GameObject fileID |
| --- | --- | --- |
| `UI Form Instances` | UGUI `RectTransform` + `CanvasRenderer` + `Canvas` + `CanvasScaler`(脚本 guid `0cd44c1031e13a943bb63640046fad76`,含 `m_UiScaleMode`/`m_ReferencePixelsPerUnit`) | `6965649249549914950` |
| `EventSystem` | UGUI `EventSystem`(guid `76c392e42b5098c458856cdf6ecaaaa1`) + `InputSystemUIInputModule`(guid `01614664b831546d2ae94a42149d80ac`) | `4479103245402218293` |

代码层面已确认零引用：

- `FairyUIManager.AttachStageToBuiltinUI`(`FairyUIManager.cs:102-134`)只把 FairyGUI `Stage` 挂到 `Builtin/UI` 静态节点下,不引用 `UI Form Instances`。
- `FairyUIGroupHelper` 完全 FairyGUI 化:`Container`/`GComponent`/`GRoot`,深度排序用 `GComponent.sortingOrder`(`FairyUIGroupHelper.cs:138`),与 UGUI Canvas 无关。
- 全仓库 `Find("UI Form Instances")` / `Find("EventSystem")` 无命中;Game 目录下 `UGUIFormHelper`/`UGUIGroupHelper`/`UGUIForm` 已删净(0 命中)。
- ET 代码里的 `EventSystem` 是 ET 自己的事件类(`EventSystem.Instance.Publish/Invoke`),与 Unity `UnityEngine.EventSystems.EventSystem` 无关。
- 注意:与早前分析不同,这不是「GF UIForm 深度排序依赖 UGUI Canvas」的结构性依赖,而是 GameFramework.prefab 中未被当前 FairyGUI 路径使用、也未删除的死节点残留。

已知不确定项(实施时验证,不预判结论):

1. GameFramework 的 `UIComponent` 初始化逻辑是否会在运行时查找 `UI Form Instances`(其 UGUI 渲染绑定层已在 `8b39d6cc` 删除,但需确认)。
2. `EventSystem` 节点的 `InputSystemUIInputModule` 是否被 FairyGUI 输入链(`Stage.HitTest` + `FairyInputService`)或其他非 UGUI 逻辑隐式依赖。

## Requirements

### R1. 删除前引用验证

- 用 `rg`/`Grep` 全仓库(含 `Library/UGF` 与 ET 侧)确认没有任何 `Find("UI Form Instances")`、`Find("EventSystem")`、`GetComponent<EventSystem>` 或按节点名路径访问这两个节点的代码。
- 确认 GameFramework `UIComponent` 的 `Awake/Initialize` 不动态创建或查找这两个节点;若查找,必须先定位替代挂载点并取得决策,不得盲目删除。
- 确认 FairyGUI 输入链不依赖 `EventSystem` 节点,Input System 的全局 `InputSystem` 组件不被该节点承载。

### R2. 经 Unity Agent Bridge 删除节点

- 通过 Unity Agent Bridge(先读已安装包 `AGENT.md`、首次 `list_commands`)删除 `GameEntry.prefab` 中 `UI Form Instances` 与 `EventSystem` 两个 GameObject。
- 只删节点,不重建、不格式化其余 prefab 内容;`.prefab` 与 `.prefab.meta` 同批处理;若 `.meta` 因删除无变化,保持原样。

### R3. 零 UGUI 门禁归零

- 删除后重跑 `Book/FairyGUI接入.md` 原文门禁命令,`Unity/Assets/Scripts/Game` 与 `Unity/Assets/Res` 下命中为 0。
- 单独确认 `com.unity.ugui` 仍仅为 URP 官方传递依赖(`packages-lock.json` `depth:2`/`source:builtin`),本任务不改包依赖。

### R4. 运行时与 Player 回归

- GameHot 冒烟(`FairyUIManagerSmokeTest` 等)与 ET 冒烟通过,Error 日志 0。
- 打开/关闭/回收 100 次生命周期与 shutdown 不因缺节点报错。
- 若可行,执行 `BuildWindows64PlayerPkg` 构建+启动冒烟,确认删除节点后 Player 正常打开界面。

### R5. 文档口径对齐

- 修正 `Book/FairyGUI接入.md` 中「命中必须为 0」的表述,说明门禁已实际归零(删除残留后)。
- 同步 HANDOFF(`.trellis/tasks/archive/2026-08/08-20-fairygui-gdk-ai-ui-integration/HANDOFF.md`)§11 AC19 状态为「已完成」并附本任务修订证据;若发现不可删的隐藏引用,改为如实记录「GF 框架核心层保留某静态宿主节点」的例外并写清边界,而非宣称归零。

## Acceptance Criteria

- [ ] AC01:全仓库引用扫描确认两个节点无任何 `Find`/`GetComponent`/路径访问;GF `UIComponent` 初始化与 FairyGUI 输入链均不依赖它们。
- [ ] AC02:经 Unity Agent Bridge 删除 `UI Form Instances` 与 `EventSystem` 两个节点,`.prefab`/`.meta` 同批,`git diff` 干净且无手工 YAML 破坏。
- [ ] AC03:`Book/FairyGUI接入.md` 零 UGUI 门禁命令对 `Unity/Assets/Scripts/Game` + `Unity/Assets/Res` 命中为 0。
- [ ] AC04:GameHot 与 ET 冒烟回归通过,运行期 Error 0;100 次生命周期与 shutdown 无异常。
- [ ] AC05:Player 构建/启动冒烟通过(或记录首个可操作错误,不冒充通过)。
- [ ] AC06:`Book/FairyGUI接入.md` 与 HANDOFF §11 AC19 口径已与实现一致,AC19 标注本任务修订。
- [ ] AC07:GDK 变更守卫(`validate_changes.py`)与 `git diff --check` 通过。

## Out of Scope

- 不触碰已验证的共享宿主公共 API、FairyGUI/UGF 供应商源码、包租约与 owner token 生命周期。
- 不迁移/删除其他生产 UGUI 页面或资源(已有产品删除决定的不在本任务重复处理)。
- 不处理阶段 3 边界项:URP 色觉滤镜、真机刘海屏/手柄、真实音频资源、SDK 主文本翻译、三宽高比×四语言截图矩阵、Editor 双向同步留证、Profiler 报告——这些另立后续任务。
- 不改 `com.unity.ugui` 的包依赖(已确认仅 URP 传递)。

## Key Decisions

- 范围聚焦「零 UGUI 归零 + 文档口径」这一当前可复现硬偏差,不把依赖外部设备/资源的边界项混入。
- 默认策略是删除两个残留节点(代码已零引用);若 R1 验证发现隐藏引用,则升级为「记录例外」的决策点,并取得用户批准后再定,不静默宣称归零。
- 删除经 Unity Agent Bridge 执行,不直接手改 Unity YAML;所有资源修改与 `.meta` 同批。
