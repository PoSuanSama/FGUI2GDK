# Bug Analysis: FairyGUI UIGroup 双树与释放顺序

### 1. Root Cause Category

- **Category**: B - Cross-Layer Contract，辅以 D - Test Coverage Gap 和 E - Implicit Assumption。
- **Specific Cause**: `Container(GameObject)` 只把外部节点接入 FairyGUI 逻辑显示树；对
  `UserGameObject` 不会改 Unity Transform 父级。GF UIGroup 又继承 ScreenSpace Canvas 的变换，若不显式
  对齐 GRoot 世界矩阵，直接子节点会位于 StageCamera 视锥外。异步包完成回调可同步释放 state，且 Editor
  shutdown 会先 dispose Stage、再执行 GF `OnPause/OnClose`，原实现都假定底层对象仍存活。

### 2. Why Fixes Failed

1. 只让 UIGroup Container 进入 GRoot：逻辑父级正确，但没有解决外部 Transform 的位置和缩放。
2. 只验证方法返回成功：遗漏 `.Forget()` 后续异常，未发现 CTS 二次 `Cancel()`。
3. 只跑主动 close：遗漏“窗体保持打开时停止 PlayMode”的反向销毁顺序。

### 3. Prevention Mechanisms

| Priority | Mechanism | Specific Action | Status |
| --- | --- | --- | --- |
| P0 | Runtime | 同步 UIGroup/GRoot 世界矩阵并对 disposed 显示对象做幂等清理 | DONE |
| P0 | Test | 断言逻辑/物理父级、视锥、100 次取消回收和打开状态 shutdown | DONE |
| P1 | Documentation | 在 frontend lifecycle spec 固化双树与同步 continuation 契约 | DONE |
| P1 | Review | Agent 方法后单独查询 Error，不把返回成功当作后台任务无异常 | DONE |

### 4. Systematic Expansion

- **Similar Issues**: 其他复用外部 GameObject 的 FairyGUI `Container`、同步 completion source 的资源租约。
- **Design Improvement**: 让 `FairyUIRootService` 成为唯一双树映射点，包 state 的 cancel/dispose 只走幂等私有入口。
- **Process Improvement**: UI 验证必须包含主动关闭和 Editor shutdown 两种顺序。

### 5. Knowledge Capture

- [x] 更新 `.trellis/spec/frontend/hook-guidelines.md`。
- [x] 更新本任务实施清单与 Bridge 验证证据。
- [x] 仓库无 `src/templates/markdown/spec/` 镜像目录，无需同步模板。
- [ ] Git 提交由用户明确授权后执行。
