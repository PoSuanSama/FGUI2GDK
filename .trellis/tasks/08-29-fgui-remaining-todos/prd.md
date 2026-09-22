# FairyGUI 接入 GDK 运行时硬化与 UGUI 能力对齐

## Goal

将当前 FairyGUI 接入从“功能可运行、主要链路有冒烟”推进到可持续维护的运行时基础设施：保留 GF 的 UI 管理语义，同时补齐 FairyGUI 自有的资源、异步、热更、输入、层级和销毁边界，使失败、取消、场景切换和重复打开都具有确定行为。

用户价值：UI 打开失败不会留下半初始化对象；窗体关闭后不会继续更新或持有资源；多语言、ET Component、UI 层级和热更重载不会依赖隐含的全局状态；Player 和真机验证有可复现证据。

## Background and confirmed facts

- FairyGUI 是当前 GDK Player 的唯一视图后端；GF IUIManager 继续负责 UI ID、UIGroup、serial、对象池和生命周期。[Book/FairyGUI接入.md](../../../Book/FairyGUI接入.md)
- 打开链会在调用 GF OpenUIForm 之前创建 descriptor、package lease、GComponent、Presenter 和 Context。[FairyUIManager.cs:308](../../../Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIManager.cs:308)
- GF InternalOpenUIForm 在 OnInit/OnOpen 异常时只发送失败事件，不负责 FairyGUI 预构造对象的回滚。[UIManager.cs:941](../../../Unity/Assets/Scripts/Library/UGF/GameFramework/UI/UIManager.cs:941)
- FairyLocalization 按 package 缓存语言，但 UIPackage.SetStringsSource 是全局状态。[FairyLocalization.cs:27](../../../Unity/Assets/Scripts/Game/UI/FairyGUI/FairyLocalization.cs:27)
- ET 打开链创建 per-open Component，但失败路径没有调用 PresenterFactory.Dispose，传入的 dispose 委托也是空实现。[UIComponentSystem.cs:84](../../../Unity/Assets/Scripts/Game/ET/Code/HotfixView/Client/Module/UI/UIComponentSystem.cs:84)
- FairyUIGroupHelper 将安全区和全屏窗体放入两个父容器，却使用同一深度列表操作两个父节点的 child index。[FairyUIGroupHelper.cs:19](../../../Unity/Assets/Scripts/Game/UI/FairyGUI/FairyUIGroupHelper.cs:19)
- Context、InputService、PackageManager、UIPackage 和 Presenter registry 都存在需要显式关闭或失效处理的静态/长生命周期状态。
- 已有收尾任务记录了版本更新降级、ET Player 证据、Runner/Router 生命周期、服务桥和测试证据缺口；本任务将其中与 FairyGUI 运行时硬化直接相关的内容纳入主验收，其余保留为明确的后续项。

## Requirements

### R1. 打开事务必须可回滚（P0）

为一次 UI 打开建立明确的事务所有权。无论 descriptor 解析、资源加载、package 注册、绑定、Presenter 创建、OnViewReady、GF OnInit、GF OnOpen、取消或失败事件在哪一步失败，都必须释放本次创建的 GComponent、Presenter/ET Component、Context 容器和 package lease，并清理 pending registry 与 GF loading 状态。

### R2. 所有者生命周期必须覆盖 Presenter 异步任务（P0）

窗体 Context 提供可取消的 lifetime token 和有效性检查。Presenter、ET System、Widget、Entity 的异步任务必须能够在关闭、取消、owner 销毁和场景切换时停止；关闭后不得重新创建 Events/Resources 或访问已释放视图。

### R3. 本地化必须与 FairyGUI 全局翻译状态一致（P1）

明确全局翻译表的所有权。并发打开、不同 package、语言切换和失败重试不能使用过期缓存；同一运行时会话中的语言应用必须串行且可观测。若 SDK 限制无法实现 package 隔离，则契约改为“一个运行时只允许一个 active language”。

### R4. UIGroup 深度和安全区必须确定（P1）

安全区窗体、全屏窗体、跨 UIGroup 深度和重复添加/移除必须保持正确的视觉顺序；Screen.safeArea 异常值不能产生负尺寸；旋转、分辨率变化和首次挂载都必须稳定。

### R5. 资源与 descriptor 必须具备完整性边界（P1）

descriptor 的 UI/package/component/dependency 身份必须与 Luban、Fairy manifest 和实际绑定一致。manifest 已生成的 hash 必须被验证，运行时资源路径必须限制在允许根目录。远程热更新启用时必须能拒绝版本、签名或 hash 不匹配的内容。

### R6. 静态全局状态必须可关闭（P1）

为 FairyUIManager、InputService、PackageManager、UIPackage、soundRedirect、localization 和 presenter/component registry 定义初始化、重复初始化、Shutdown、场景切换和热更重载行为。重复进入 PlayMode 不得复用旧 ResourceManager、旧 Stage、旧委托或旧 package。

### R7. 绑定和注册应优先生成化（P2）

保留现有公开入口兼容性，新增 UI 时优先由工具生成 presenter/package 注册表，减少运行时反射、IL2CPP 裁剪和 link.xml 漂移。反射路径必须有明确的缺失类型诊断和裁剪验证。

### R8. 补齐可持续验证和既有收尾缺口（P1/P2）

新增失败/取消/重复打开/关闭重开/owner 销毁/多语言并发/混合深度/资源 hash 的回归覆盖；补充 ET IL2CPP Player、真机安全区/输入、URP 色觉和性能基线证据。版本更新功能是否恢复由产品决策单独确认，不在本轮擅自改变启动产品流程。

## Acceptance criteria

- [ ] 任意打开阶段失败后，连续 100 次打开/关闭循环的 UI、package lease、pending state、ET child entity 和资源计数回到基线。
- [ ] OnViewReady、OnOpen、绑定错误、资源失败、owner 取消、owner Destroy 六类失败路径都有可观测异常且无残留。
- [ ] 窗体关闭后，Context API 立即失效；Presenter 的迟到异步回调不能触碰 FairyGUI/Unity API。
- [ ] 并发打开两个 package 或切换语言时，翻译结果与 active language 一致，无过期缓存。
- [ ] Default/Pop/Message 等组中混合 fullScreen 与 safeArea 窗体时，深度顺序稳定；安全区变化不会出现负尺寸或错位。
- [ ] descriptor、manifest、package bytes、外部资源和生成绑定的身份/hash 校验失败会阻止打开并释放已取得资源。
- [ ] 重复初始化、场景切换、HotEntry/ET bootstrap 重载和重复 PlayMode 后，不存在旧 PlayerLoop action、委托、Stage、package 或 ResourceManager 引用。
- [ ] GameHot 和 ET 的回归冒烟、Unity 编译/日志检查、必要的 Player/设备验证均有记录；无法执行的验证明确写出原因。
- [ ] 现有 GF UI API、UI ID、Luban 字段、资源路径和热更程序集边界保持兼容；不恢复 UGUI 运行时依赖。

## In scope

- Unity/Assets/Scripts/Game/UI/FairyGUI/ 的打开事务、Context、package/localization、GroupHelper、Input/声音桥和 Shutdown。
- ET UIComponent、Fairy Presenter Adapter、Component factory 的失败清理和 owner 生命周期。
- descriptor/manifest 生成输出的运行时校验，以及必要的生成器/测试输入。
- FairyGUI 运行时回归测试、Editor AgentCallable 验证、Unity/Player/设备验证记录和相关 Book/spec 文档。

## Out of scope

- 恢复 UGUI prefab、Canvas、GraphicRaycaster 或旧 UGUI UI 栈。
- 在没有产品决定前重建完整版本检查/资源更新 UX。
- 更换 FairyGUI SDK、资源系统或引入新的第三方依赖。
- 未经性能基线证明的大规模 package 拆分或全局架构重写。
- 与 FairyGUI 无关的 ET Runner、Router 端口问题；它们保留在原收尾任务中单独处理。

## Key decisions

- 保留 GF IUIManager 作为唯一 UI 语义宿主；不建立第二套页面栈。
- 优先修复失败事务、所有权和全局状态，再做性能和产品能力扩展。
- 当前 SDK 的全局 strings source 采用“单 active language + 串行应用”契约；若后续要求同进程多语言并存，必须先升级/封装 SDK 并重新评估范围。
- 所有新运行时状态都必须有明确 owner、取消路径、Shutdown 路径和诊断标识。

## Deferred items

- 版本检查/资源更新 UI 和强制更新策略。
- ET IL2CPP/LockStep 全量生产证据、真机矩阵、URP 色觉滤镜和性能基线，作为 R8 的分阶段验收。
