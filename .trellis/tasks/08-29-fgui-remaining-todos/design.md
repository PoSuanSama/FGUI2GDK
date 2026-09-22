# 技术设计

## 1. 运行时边界

GF IUIManager 继续作为 UI ID、UIGroup、serial、暂停/遮挡语义和 UI 实例对象池的唯一所有者。FairyGUI 负责视觉对象和 package 注册。ET 负责 Component Entity，GameHot 负责 Presenter 实例。任何层都不能直接释放其他层拥有的对象，必须通过明确的 lease 或事务接口完成交接。

运行时流程调整为：

UI 请求 → 创建 OpenTransaction → 身份校验 → 获取 package lease → 准备 package 绑定、视图和 Presenter → 调用 GF OpenUIForm → OnInit 只采纳一次 → OnOpen 完成打开 → 窗体持有 Context 生命周期和 package lease → Close、Cancel、Failure 都进入同一个幂等回滚路径。

## 2. 打开事务和 GF 回调关联

新增内部事务对象或等价的状态持有者，至少包含：

- 稳定的 operation id 和可选的 GF serial id；
- descriptor、package lease、view、Presenter、ET Component 的所有权标记；
- Prepared、Adopted、Opened、Released 状态转换；
- 一个幂等的 Rollback(reason) 方法；
- 用于成功/失败通知的 completion source，替代逐帧轮询；
- 与 OpenUIFormFailure、OpenUIFormSuccess 事件的关联关系。

如果 GF 从对象池同步打开，采纳动作在 pending handoff 作用域内完成。如果 GF 异步加载，事务必须在回调前绑定 serial。如果 GF 在 FairyUIForm 已采纳事务后报告失败，事务必须主动调用窗体清理路径，因为 GF 在 InternalOpenUIForm 内部异常时不会替 FairyGUI 对象执行清理。

现有公开的 OpenFairyUIFormAsync 签名保持兼容。可以增加内部重载或内部关联表。失败信息必须保留原始异常，并包含 ui id、descriptor、package、serial 和 operation id。

## 3. 生命周期和 ET 所有权

FairyUIFormContext 增加 lifetime source/token 和 cleared 标记。Clear 之后，Events、Resources、Widgets 以及后续容器的访问都必须失败。窗体关闭时先取消 lifetime，再执行 Presenter 清理、子对象清理、事件/资源清理、视图释放和 package lease 释放，顺序必须确定。

PresenterFactory 变成明确的可释放交接对象。ET 通过工厂创建 Component；如果采纳没有完成，工厂负责销毁 Component。Adapter 的 OnClose 必须使用 finally 清理和释放 Component，避免 ET System 抛异常后留下子 Entity。

Presenter 的异步操作必须从 Context token 派生。现有 Forget 调用点必须绑定该 token 并观察异常，或者改为统一的 owner-scoped helper。

## 4. 本地化

将 UIPackage.SetStringsSource 视为进程级全局资源。维护一个 active language 和一个串行应用任务。打开 package 前等待当前语言应用完成；应用失败时不能更新成功标记。语言切换必须使标记失效，并明确重新打开或重建窗口的策略。除非 SDK 支持真正隔离，否则不能宣称 package 之间可以独立使用不同语言。

## 5. UIGroup 和安全区布局

在执行阶段结合 FairyGUI child-index API 选择一种兼容实现：

1. 使用一个组根，并在组内明确全屏层和安全区层的偏移；
2. 为不同父容器维护独立的深度列表，并固定父容器之间的层级顺序。

最终方案必须明确 fullScreen 与 safeArea 窗体的顺序，不能把一个父节点的索引传给另一个父节点。安全区换算必须先限制 x/y，再将 width/height 限制为非负值，并且只在根容器尺寸有效后应用。

## 6. 完整性和生成身份

加载时执行以下检查：

- 校验 descriptor schema 和 Luban 策略；
- 校验 descriptor 的 package id/name、component id/name 与 catalog 和生成契约一致；
- 要求 descriptor 和运行时资源路径位于配置的 FairyGUI 根目录下；
- manifest 提供 hash 时，在注册前验证 hash；
- 拒绝未声明的依赖和资源；
- 在诊断中保留原始 hash/version。

Hash 校验必须通过项目资源抽象完成，不能绕过 ResourceComponent 的所有权。如果生产环境的远程内容使用签名，签名验证应作为独立 provider 边界实现，不能通过字符串拼接或 Shell 命令代替。

## 7. Shutdown 和重载

增加合适的内部或公开 Shutdown 路径，依次完成：

1. 停止 InputService 的 PlayerLoop 注册；
2. 如果由 GDK 安装，则解除 GF 事件桥和 sound redirect；
3. 关闭已加载和正在加载的窗体；
4. 释放 package lease 并移除已注册 package；
5. 清空本地化、Presenter 和 Component registry；
6. 仅在宿主真正关闭时释放 Stage 和组辅助器。

初始化必须幂等。Shutdown 后未重新初始化前，运行时 API 必须拒绝使用。HotEntry 和 FairyGUIBootstrap 通过已有销毁/关闭钩子调用该路径。

## 8. 测试和证据

为 catalog、descriptor 和生命周期辅助逻辑增加聚焦测试。Unity 运行时行为使用 Unity Agent Bridge 或现有 AgentCallable 契约验证。必须覆盖：每个打开阶段的注入失败、每个 await 之后取消、重复打开/关闭、owner 销毁、混合组层级、多语言并发、重复初始化、场景重载和 package hash 不匹配。

不能把 .NET 或 Unity solution 编译当作 Unity 导入、Player、视觉、输入或资源行为的证明。需要分别记录 Unity 编译/日志、目标分辨率截图以及 Player/设备结果。

## 兼容性和回滚

不修改 UGF 公共 API 或 Luban UI ID。内部状态改动应保持增量兼容。如果某个运行时硬化切片引入回归，应回滚该切片，同时保留新增测试和诊断。descriptor/manifest schema 变化必须同时修改生成器输入和迁移逻辑，不能手工修补生成输出。
