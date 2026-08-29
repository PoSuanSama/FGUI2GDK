# FairyGUI 零 UGUI 归零与文档口径收口 — 实施计划

## 0. Start Gate

- [x] 重跑只读基线:`get_context.py`、`git status --short`、`git log --oneline -5`。
- [x] 运行 GDK 变更守卫并记录基线(0 错误)。
- [x] 复现偏差:零 UGUI 门禁命令确认 `GameEntry.prefab` 命中 2 处(UI Form Instances + EventSystem)。

## 1. 删除前引用验证(R1)

- [x] `rg` 全仓库 `UI Form Instances` / `EventSystem`:代码层零引用(仅 `FairyUIManager.cs:120` 注释);`UnityEngine.EventSystems` 在 Scripts 下 0 命中。
- [x] 核对 `Library/UGF/GameFramework/UI` 不查找两节点(纯逻辑层,UGUI 渲染绑定已在 `8b39d6cc` 删除)。
- [x] 核对 FairyGUI 输入链:`FairyInputService` 用 Input System + FairyGUI Stage,不依赖 UGUI EventSystem。
- [x] 确认两节点无子物体、fileID 仅自身定义引用;并确认 `UI Form Instances` 是 GameFramework.prefab 嵌套实例的 added object。

## 2. 删除节点(R2,降级方案)

Bridge 命令集无直接编辑 prefab 资产的命令(`prefab` 仅场景实例操作),且当前场景无 GameEntry 实例。按 GDK 降级方案精确编辑 YAML:

- [x] 阅读 Unity Agent Bridge `AGENT.md`,首次 `list_commands`。
- [x] 删除 `UI Form Instances`(GameObject `6965649249549914950` + 5 组件)与 `EventSystem`(GameObject `4479103245402218293` + 3 组件)共 10 块。
- [x] 清理 3 处引用:GameEntry 根 `m_Children` 的 EventSystem、PrefabInstance `m_InstanceRoot` modification、`m_AddedGameObjects` 条目(替换为 `[]`)。
- [x] `git diff` 审查 + Python 完整性检查(0 悬空 fileID)。

### 2.1 修复 f3617234 引入的 prefab 损坏(新发现)

核验过程中发现一个预先存在的、更根本的缺陷:`f3617234`(标题「修复 GameEntry 悬挂引用」)误删了嵌套 GameFramework 实例根 Transform(`1836028191286498939`)的 stripped 定义,但它仍在 GameEntry 根 `m_Children` 被引用,导致 Unity 导入报 `Transform child can't be loaded`。

- [x] 恢复 `1836028191286498939` 的 stripped Transform 定义(`m_CorrespondingSourceObject` 指向 GameFramework.prefab 根 `433714`)。
- [x] 验证:清空日志后 refresh,`search_logs` 0 error;`get_asset` 确认嵌套实例 Builtin 完整、两 UGUI 节点已删。

## 3. 零 UGUI 门禁归零(R3)

- [x] 重跑门禁命令,`Unity/Assets/Scripts/Game` + `Unity/Assets/Res` 命中为 0(exit 1)。
- [x] `com.unity.ugui` 仍为 URP 传递依赖(本任务未改包依赖,已在前置核验确认)。

## 4. 运行时与 Player 回归(R4)

- [x] GameHot 冒烟 + 100 次生命周期 + 包租约生命周期通过,Error 0。
      (UIManager/Inventory/Dialog 三冒烟 + `ValidateFairyUIFormLifecycleCycles` 100 次 +
      `ValidateFairyPackageManagerLifecycle`,停止后 Error 0;3 条 warning 为既存脚本缺失
      HP Bar 等 UGUI 遗留,与本次删除无关。)
- [x] ET 骨架自检 + 七冒烟通过(方法本身 `ok`)。
      停止后出现 10 条 error,堆栈全在 ET Server 网络层(端口 bind 30002-30004/20001 冲突 +
      NetComponent/ProcessOuterSender 空引用 + ET.Init OnDestroy 关闭时序),无任何
      GameEntry/FairyGUI UI 相关,为 ET 双符号模式既存问题,非本次删除引入。
- [ ] `BuildWindows64PlayerPkg` 构建 + 启动 + 界面打开(或记录首个可操作错误)。(AC05,成本高,另议)

## 5. 文档口径对齐(R5)

- [ ] 更新 `Book/FairyGUI接入.md` 零 UGUI 门禁段落,说明残留已删除、门禁归零。
- [ ] 更新 HANDOFF §11 AC19 为「已完成」并附本任务修订号;补充 f3617234 stripped 损坏的修复记录。

## 6. Finish

- [x] 运行 `validate_changes.py`(0 错误,1 个已满足证据的 UNITY001 提示)+ `git diff --check` 通过。
- [ ] `trellis-check` 一致性检查。
- [ ] 经用户授权后按 GDK Conventional Commit 提交;prefab 与 meta 同批。
- [ ] 归档本任务。
