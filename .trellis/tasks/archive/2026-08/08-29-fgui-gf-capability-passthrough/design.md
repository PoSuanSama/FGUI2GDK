# FairyGUI GF 能力透出复核与补齐 — 设计

## 1. 变更边界

只改 `Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIManager.cs`(透出 + 事件桥),以及 HANDOFF §19.1 缺口清单文字。不触碰 GF `IUIManager` 供应商接口、共享宿主公共 API、owner token/打开链/包租约。

## 2. 对象池调参透出(R2)

`FairyUIManager` 增加四个只读/可写属性,薄转发到 `GetRequiredUIManager()`:

```text
InstanceAutoReleaseInterval -> IUIManager.InstanceAutoReleaseInterval
InstanceCapacity            -> IUIManager.InstanceCapacity
InstanceExpireTime          -> IUIManager.InstanceExpireTime
InstancePriority            -> IUIManager.InstancePriority
```

- 作用域按 GF 契约如实暴露(先读 `IUIManager.cs` 确认是 UIManager 级还是含 UIGroup 重载);若 GF 有按组重载则同样透出重载,不臆造。
- 读写均走 `GetRequiredUIManager()`,复用既有未初始化异常契约。

## 3. 事件桥补齐(R3)

与既有 `OpenUIFormFailure`/`CloseUIFormComplete` 同构(见 `FairyUIManager.cs:31-36,75-76,470-477`):

```text
static event EventHandler<OpenUIFormSuccessEventArgs> OpenUIFormSuccess
static event EventHandler<OpenUIFormUpdateEventArgs> OpenUIFormUpdate
static event EventHandler<OpenUIFormDependencyAssetEventArgs> OpenUIFormDependencyAsset
```

- `Initialize` 时 `m_UIManager.OpenUIFormSuccess += On...` 等对称订阅,`Dispose` 时 `-=`。
- 转发方法只 `?.Invoke(sender, args)`,不改动 args。
- 事件参数类型从 `GameFramework.UI` 命名空间引用,确保与 GF 一致。

## 4. 验证设计(R4)

- 单测/冒烟经 Unity Agent Bridge AgentCallable:回读四属性值;人为触发一次打开,断言 Success/Update 事件被静态事件转发;依赖资产事件若难以稳定触发则用一次真实加载触发或降级为「转发方法单元断言」并记录。
- 计数断言 Initialize 两次后事件订阅数不翻倍(对称退订)。

## 5. 回滚点

- 单文件 `FairyUIManager.cs` 整体还原即回滚;事件桥与透出不拆分半套。
- 若 GF 对象池属性存在按组重载的隐藏语义,先确认再透出,不盲写。

## 6. 风险

| 风险 | 对策 |
| --- | --- |
| 对象池属性作用域理解偏差 | 先读 `IUIManager.cs` 契约与实现,确认 UIManager/UIGroup 级 |
| 事件重复订阅泄漏 | Initialize 幂等订阅 + Dispose 对称退订 + 计数断言 |
| DependencyAsset 事件难触发 | 真实加载触发或降级为转发方法单测并记录 |
