# FairyGUI 零 UGUI 归零与文档口径收口 — 设计

## 1. 变更边界

唯一事实改动是 `Unity/Assets/Res/GameEntry.prefab` 内两个 UGUI 静态节点删除,以及 `Book/FairyGUI接入.md` 与 HANDOFF §11 的 AC19 口径文字。不改任何 C# 逻辑、asmdef、包依赖或已验证的共享宿主契约。

明确不修改:

- `FairyUIManager`/`FairyUIForm`/`FairyUIGroupHelper`/`FairyPackageManager` 等运行时管理层;
- FairyGUI/UGF 供应商源码;
- `com.unity.ugui` 包依赖(已确认仅 URP 传递)。

## 2. 删除前引用验证(先于任何删除)

必须全部通过才允许删除:

1. **静态引用扫描**:`rg -n "UI Form Instances|EventSystem" Unity/Assets/Scripts` 确认无 `Find`/按名路径访问;`rg -n "UnityEngine.EventSystems|GetComponent<EventSystem>" Unity/Assets/Scripts/Game` 确认 Game 目录无 UGUI EventSystem 引用(ET 的 `EventSystem` 是另一套类,命中属正常,需按命名空间区分)。
2. **GF UIComponent 初始化核对**:在 `Library/UGF` 内确认 `UIComponent` 不动态 `CreateChild`/`Find` 这两个节点;其 UGUI 渲染绑定已在 `8b39d6cc` 删除,若残留查找需定位替代挂载点。
3. **输入链核对**:确认 FairyGUI 输入走 `Stage.HitTest` + `FairyInputService`,`EventSystem` 节点的 `InputSystemUIInputModule` 未被任何 FairyGUI/ET 逻辑消费;Input System 的全局组件不被该节点承载。
4. **场景可达性**:确认 `FairyGUIDemo.unity`/`FairyGUIDemoET.unity` 等场景作为 `GameEntry.prefab` 实例,删除 prefab 子节点后实例跟随更新,不产生 missing reference(通过 Unity 导入后 Error/警告日志验证)。

## 3. 删除策略(Unity Agent Bridge)

- 先读已安装 Unity Agent Bridge 的 `AGENT.md`,首次运行时 `list_commands`,遵循 fixed-slot single-flight、原子 request、唯一 id、`processing.json`→`response.json` ack。
- 用桥提供的资源编辑能力删除 `GameEntry.prefab` 中 `UI Form Instances`(fileID `6965649249549914950`)与 `EventSystem`(fileID `4479103245402218293`)两个 GameObject,连同其子物体(当前均无子物体)。
- `.prefab` 与 `.prefab.meta` 同批处理;删除后让 Unity 重新导入,刷新 meta GUID(如无 GUID 变化则不动 meta)。
- 不手工编辑 YAML;不批量按文件名删其他资源。

## 4. 门禁与回归

- **零 UGUI 门禁**:重跑 `Book/FairyGUI接入.md` 原文 `rg` 命令,`Unity/Assets/Scripts/Game` + `Unity/Assets/Res` 命中为 0。
- **包依赖**:确认 `packages-lock.json` 中 `com.unity.ugui` 仍 `depth:2`/`source:builtin`(URP 传递),未因本任务变化。
- **Unity 冒烟**:GameHot 三个冒烟 + `ValidateFairyUIFormLifecycleCycles`(100 次)+ `ValidateFairyPackageManagerLifecycle`;ET 七个冒烟 + 骨架自检;停止 PlayMode 后 `search_logs type=error` 为 0。
- **Player**:`BuildWindows64PlayerPkg` 构建 + 启动 + 界面打开;若外部前置阻塞则记录首个错误,不冒充通过。

## 5. 回滚点

- 删除是 `GameEntry.prefab` 的原子改动,整文件 `git checkout` 即回滚;不保留半删状态。
- 若引用验证发现隐藏依赖,删除不执行,转为「记录例外」决策并暂停。

## 6. 风险

| 风险 | 对策 |
| --- | --- |
| GF `UIComponent` 隐式查找节点 | R1-2 先核对 `Library/UGF`;命中则定位替代挂载点,不盲删 |
| `EventSystem` 承载 Input System 全局组件 | R1-3 核对输入链;若依赖则仅删 `UI Form Instances`,`EventSystem` 转例外 |
| prefab 删除后场景 missing reference | R1-4 导入后查 Error/警告;桥内验证 |
| 手改 YAML 破坏 prefab | 全部经 Unity Agent Bridge,禁止直改;`git diff` 审查 |
