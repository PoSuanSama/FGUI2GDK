# Implement — 清理 UGUI 残留界面(产品删除)

## 有序实施清单

1. [ ] 删除 GameHot 旧界面 prefab:`Res/UI/UIForm/Hot/UpdateResourceForm.prefab`
      (+meta;旧 UGUI 更新表单,无代码引用)
2. [ ] 删除 ET Demo 界面 prefab:`Res/UI/UIForm/Demo/UIHelp/UILobby/UILogin.prefab`
      (+meta;ET UGFUIFormId 已无对应常量,无代码引用)
3. [ ] 删除 ET LockStep 客户端 UI prefab:`Res/UI/UIForm/LockStep/UILSLogin/UILSLobby/UILSRoom.prefab`
      (+meta;客户端 UI 代码已删,Server 侧 LockStep 代码保留)
4. [ ] 删除 ET `UIType.cs`(+meta;无引用)
5. [ ] 删除 `Res/UI/UIPrefab/Button/*.prefab`(+meta;查引用后删)
6. [ ] UI.xlsx 清理:ET/GameHot 表删无界面的旧行(Menu/Setting/About 已在 UIFormId 无常量)
7. [ ] `rg` 验证无悬挂引用;`recompile` 0 error;`validate_changes.py`

## 保留项
- `Builtin/` 代码 + `Res/Builtin/UpdateResourceForm.prefab`:启动/版本检查/更新流程依赖(P0-3)
- `AssetSet/`、`UGuiExtension/` UGUI helper:Editor 工具引用,阶段 F 统一处理
