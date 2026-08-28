# GameHot DialogForm 迁移到 FairyGUI

## Goal

把旧 UGUI `DialogForm`(UI ID 1)迁移为 FairyGUI 组件 + GameHot 类 Presenter,
恢复退出确认等产品闭环,为后续 Menu/Setting 等页面批次提供通用对话框基础设施。

## Requirements

- R1 设计端 Package1 新增 `Dialog` 组件:标题、消息文本、三模式按钮区
  (确认/取消/中立),元素命名稳定可绑定(getMemberByName)。
- R2 UI ID 保持 1,`DialogForm`/Default 组/multi=true/pause=true,与旧 Luban 行一致。
- R3 运行时 `DialogParams` 契约等价:Mode(1/2/3)、Title/Message、Confirm/Cancel/Other
  文本与回调、PauseGame、UserData;回调触发后关闭窗体。
- R4 发布流水线:fairy 工程经 agent-bridge `publish` 产出 `Package1_fui.bytes`、
  `UIDialog` 绑定、descriptor;来源与派生输出同批提交。
- R5 GameHot `FairyDialogForm : IFairyUIPresenter` 挂 `[FairyUIPresenter(UIFormId.DialogForm)]`。

## Acceptance Criteria

- [ ] Unity 双模式编译 0 错误、0 警告。
- [ ] 三模式冒烟:仅确认/确认+取消/确认+取消+中立,各按钮回调触发且窗体关闭。
- [ ] 重复开关、暂停/恢复(PauseGame)回基线。
- [ ] 关闭后 GF/GRoot/包租约回基线,Error 日志 0。
- [ ] 旧 UGUI `DialogForm.cs`/`DialogForm.prefab` 删除且无引用方。
