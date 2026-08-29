# Implement — GameHot DialogForm 迁移

## 有序实施清单

### 阶段 1:设计端 Dialog 组件(agent-bridge 驱动)
1. [ ] `create_component` 建 `Dialog` 组件(如 640x360,exported)。
2. [ ] `insert_object`/`create_button` 插入:标题 text、消息 text、Confirm/Cancel/Other 三按钮。
3. [ ] `set_property` 设置名称(titleText/messageText/confirmButton/...)、尺寸、文本。
4. [ ] 三模式:单一组件 + 按钮 visible 由 Presenter 按 Mode 控制(简化,不建三份布局)。
5. [ ] `save_all` 保存;`publish` 发布 Package1(产出 bytes/绑定/描述符)。

### 阶段 2:Luban UI 行
6. [ ] `Design/Excel/GameHot/Datas/Game/UI.xlsx` 恢复 DialogForm 行
      (Id=1, CSName=DialogForm, AssetName=FairyGUI/Dialog, Default, multi=true, pause=true)。
7. [ ] `Tool.exe --AppType=ExcelExporter` 导出;descriptor 生成器产出 `Dialog.json`。

### 阶段 3:GameHot 运行时
8. [ ] `FairyDialogForm : IFairyUIPresenter` + `[FairyUIPresenter(UIFormId.DialogForm)]`,
      OnViewReady 绑定 `UIDialog`,OnOpen 读 `DialogParams` 按 Mode 控制按钮可见与文本,
      回调触发后经 FairyInventoryFlow 同款路径关闭窗体(或直接 context 关闭)。
9. [ ] `DialogParams` 类迁回 GameHot(与旧契约等价)。
10. [ ] `FairyUIDemoAgent`/GameHot 冒烟:`FairyDialogSmokeTest`(三模式/回调/关闭/基线)。

### 阶段 4:删除旧 UGUI
11. [ ] 删除 `Game/Hot/Code/UI/DialogForm.cs`(旧 UGUI,如仍存在)、
      `Res/UI/UIForm/Hot/DialogForm.prefab`(+meta);`rg` 确认无引用方。

## 验证命令
- FGUI 队列:`D:/Unity/Project/GDK_FGUI/.agent/requests/*.json`(id/action/params)
- Unity Bridge:`recompile` → `get_compile_result`;`search_logs type=error`
- GameHot 冒烟:`invoke_agent_method Game.Hot.Editor.FairyDialogSmokeTest::...`
- `python .agents/skills/gdk-development-workflow/scripts/validate_changes.py`
- `pwsh -NoProfile -File ./Tools/FairyGUI/Test-FairyGUITools.ps1`

## 风险文件 / 回滚点
- 发布会重写 `Package1_fui.bytes` 与全部生成绑定;发布前 `git status` 干净,
  发布后逐项审查 diff(绑定新增 UIDialog、其余绑定哈希变化属正常重生成)。
- 旧 DialogForm 删除必须等冒烟通过;来源与派生输出同批提交。
