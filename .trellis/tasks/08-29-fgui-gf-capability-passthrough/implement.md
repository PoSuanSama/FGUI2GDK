# FairyGUI GF 能力透出复核与补齐 — 实施计划

## 0. Start Gate

- [ ] `git status --short` + GDK 变更守卫基线。
- [ ] 读 `Library/UGF/GameFramework/UI/IUIManager.cs` 确认四属性作用域与事件参数契约。

## 1. 复核已透出能力(R1)

- [ ] 核对实例锁/优先级、批量关闭、诊断、Failure/CloseComplete 事件桥的透传与空安全。

## 2. 透出对象池调参(R2)

- [ ] `FairyUIManager` 增加四属性薄转发(含可能的 UIGroup 重载)。
- [ ] 读写走 `GetRequiredUIManager()`,未初始化异常契约一致。

## 3. 桥接三个 GF 事件(R3)

- [ ] 增加 `OpenUIFormSuccess`/`OpenUIFormUpdate`/`OpenUIFormDependencyAsset` 静态事件。
- [ ] `Initialize` 幂等订阅 + `Dispose` 对称退订。
- [ ] 转发方法 `?.Invoke(sender, args)`。

## 4. 验证(R4)

- [ ] 冒烟:四属性回读一致;Success/Update 事件转发;重复 Initialize 订阅不翻倍。
- [ ] GameHot/ET 冒烟回归,Error 0。

## 5. 文档与 Finish

- [ ] 更新 HANDOFF §19.1 缺口清单为「已补齐」+ 修订号。
- [ ] `validate_changes.py` + `git diff --check`。
- [ ] 经用户授权后提交;归档任务。
