# FairyGUI GF 能力透出复核与补齐 — 实施计划

## 0. Start Gate

- [x] `git status --short` + GDK 变更守卫基线(0 错误)。
- [x] 读 `Library/UGF/GameFramework/UI/IUIManager.cs` 确认四属性(`InstanceAutoReleaseInterval`/`InstanceCapacity`/`InstanceExpireTime`/`InstancePriority`)为 UIManager 级,五事件参数类型在 `GameFramework.UI` 命名空间。

## 1. 复核已透出能力(R1)

- [x] 核对实例锁/优先级、批量关闭、诊断、Failure/CloseComplete 事件桥均走 `GetRequiredUIManager()` 薄转发,空安全一致。

## 2. 透出对象池调参(R2)

- [x] `FairyUIManager` 增加四属性薄转发(UIManager 级,无 UIGroup 重载)。
- [x] 读写走 `GetRequiredUIManager()`,未初始化异常契约一致。

## 3. 桥接三个 GF 事件(R3)

- [x] 增加 `OpenUIFormSuccess`/`OpenUIFormUpdate`/`OpenUIFormDependencyAsset` 静态事件。
- [x] `Initialize` 幂等订阅(`m_EventsAttached` 守护,无重复订阅)。
- [x] 转发方法 `?.Invoke(sender, args)`,与既有 Failure/CloseComplete 同构。

## 4. 验证(R4)

- [x] 冒烟 `RunFairyGFPassthroughSmokeTest` 通过:四属性回读一致;Success 事件桥转发
      (`successCount==1`);重复 Initialize 订阅幂等(第二次 Initialize 不重复订阅)。
- [x] GameHot 冒烟回归 Error 0;ET 冒烟为上一任务 P0 已覆盖,本任务仅改 FairyUIManager
      透出层,ET 路径经同构代码复用。

## 5. 文档与 Finish

- [ ] 更新 HANDOFF §19.1 缺口清单为「已补齐」+ 修订号 712845a3。
- [x] `validate_changes.py`(0 错误 0 警告) + 编译 gen 7 0 error。
- [x] 经用户授权后提交(712845a3);归档任务待办。
