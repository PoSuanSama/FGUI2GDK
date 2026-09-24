# 实施计划

## 阶段 0：基线和诊断

- [ ] 代码修改前重新阅读当前任务文件和适用的 GDK/Trellis 规范。
- [x] 增加 operation、serial、package 诊断信息，不改变现有行为。
- [x] 使用现有 GameHot、ET 冒烟流程和 package diagnostics 采集基线，并记录 git 状态。
- [x] 任何 Unity 查询或修改前，确认当前 Unity Agent Bridge 是否可用。

## 阶段 1：P0 事务和所有权硬化

- [x] 将 FairyUIManager 的逐帧打开轮询改为具有关联关系的完成/失败处理。
- [x] 为同步对象池打开和异步 GF 加载分别明确 pending state 的采纳和回滚，并保证幂等。
- [x] 确保 GF 在采纳之后失败时，释放 view、Presenter、Context、package lease 和 pending registry 状态。
- [x] 让 ET PresenterFactory 释放未采纳的 Component，并让 Adapter 的关闭清理具备异常安全性。
- [x] 为 Context 增加 lifetime token 和 cleared guard，并将现有 Presenter 异步调用迁移到 owner-scoped cancellation。
- [x] 为 OnViewReady、OnOpen、资源失败、取消和 owner 销毁增加基础回归测试。
- [x] 将 GameHot OnViewReady、OnOpen、绑定准备失败和关闭后 Context 失效提升为逐类 100 次压力回归。
- [x] 将 ET pending open owner 取消与成功打开后的 owner Dispose/Fiber Remove 提升为逐类 100 次压力矩阵，并逐次核对 Entity、GF、GRoot、Context、Widget 和 package 基线。
- [x] 增加 OnViewReady 完成、GF serial 尚未分配时 owner 销毁的精确时序测试，并断言业务 OnClose 恰好一次。

风险点：本阶段跨越 GF 回调、FairyGUI 对象和 ET Entity。不能修改 vendor GF core，应在 FairyUIManager/Adapter 边界完成适配。

## 阶段 2：本地化和组层级正确性

- [x] 将按 package 的语言缓存改为串行的进程级 active language 状态。
- [x] 明确应用失败重试和语言切换后的窗口重建策略。
- [x] 修复 FairyUIGroupHelper 对安全区/全屏父容器的排序，并限制非法安全区尺寸。
- [x] 增加混合层级与多组 Editor 分辨率测试。
- [ ] 增加真机旋转和重复添加/移除测试。

风险点：层级顺序会影响模态窗口行为；必须保持 GF 组深度语义，并记录最终的父容器层级契约。

## 阶段 3：完整性和 Shutdown

- [x] 校验 descriptor 的 package/component/dependency 身份与 catalog、生成契约一致。
- [x] 通过 ResourceComponent 校验允许的资源根目录，并在包 descriptor、外部 TextAsset 和本地化 XML 应用前验证 manifest SHA-256。
- [x] 为 PlayerLoop、事件桥、声音钩子、package registry、Stage 和组辅助器增加明确的 FairyGUI Shutdown/重载处理。
- [x] 让 FairyInputService 的每次初始化使用独立 PlayerLoop registration；Shutdown 后同帧重初始化时旧 registration 只退场一次，不会复用新会话状态重复轮询。
- [x] 让 HotEntry 和 ET Runner 从已有生命周期钩子调用 Shutdown。
- [x] 增加 FairyUIManager shutdown 通知后的 ET Bootstrap 静态注册复位、重复初始化和新 owner 打开/关闭回归。
- [x] 增加 manifest hash 匹配/不匹配契约测试。
- [ ] 通过销毁实际 ET Runner 对象验证生产 OnDestroy 集成。
- [ ] 增加场景重载、domain/hot reload 和重复 PlayMode 的全局资源清理矩阵。

风险点：Shutdown 不能释放仍被 GF 窗体持有的 package。必须先关闭窗体，再通过 lease 释放 package。

## 阶段 4：生成化注册和延期产品缺口

- [x] 根据当前 HybridCLR/link.xml 约束评估生成式 Presenter/Package 注册；GameHot Presenter 工厂表和共享 Game 程序集的 Package Binder 分发表均为静态生成。
- [x] GameHot Presenter 静态工厂表由 Unity Editor 生成入口创建并提供只读校验，禁止手工编辑生成文件。
- [x] 从 Publish.json 和源 manifest 生成共享 Package Binder 分发表，并在 GameHot/ET 引导中共用；生成流水线先刷新 manifest。
- [ ] 执行 ET IL2CPP Player 验证、设备安全区/输入矩阵、URP 色觉方案决策和性能基线采集。
- [ ] 在修改 Procedure 前，先完成版本/更新 UX 的产品决策。

## 验证命令和证据

验证基线 `6ed43a50` 及其后续小轮次的运行时证据记录在同目录的 `evidence.md`。其中已收录
2026-09-24 Unity Agent Bridge 的 GameHot 失败回滚、包 descriptor 资源失败 100 次回归、生成绑定
类型错配 100 次回归、混合层级/安全区、100 次生命周期、hash 契约和 Shutdown/reinitialize 结果；
GameHot OnViewReady、OnOpen、绑定准备失败与关闭后 Context 失效的逐类 100 次回归；
四组 Unity Editor Game View 分辨率矩阵，以及 ET owner Destroy/Fiber Remove 生命周期回归；
ET serial 分配前 owner Destroy 的精确时序、取消顺序和完整资源基线回归；
ET pending owner 取消与成功打开后 owner Dispose/Fiber Remove 的逐类 100 次压力矩阵；
FairyInputService Shutdown 后同帧重初始化、重复 Initialize 幂等和 PlayerLoop 单次轮询回归；
本轮 ET Bootstrap shutdown 通知后的静态注册复位、重复初始化与新 owner 生命周期回归；
未覆盖项仍按证据边界保持未勾选。

最低确定性检查：

    python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
    git diff --check
    git diff --stat
    pwsh -NoProfile -File ./Tools/FairyGUI/Test-FairyGUITools.ps1
    pwsh -NoProfile -File ./Tools/FairyGUI/Test-GDKProject.ps1 -Check
    pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyUIFormDescriptors.ps1 -Check
    pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyRuntimeManifest.ps1 -Check
    pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyLocalizationXml.ps1 -Check
    pwsh -NoProfile -File ./Tools/FairyGUI/Generate-FairyPackageBinderRegistry.ps1 -Check

Unity 专项证据必须来自运行时发现的 Agent Bridge 命令：编译结果、Error 日志扫描、GameHot/ET 聚焦冒烟、生命周期循环、本地化/安全区/输入/声音检查，以及排期中的 Player 构建和启动。不能用 .NET 编译替代这些证据。

## 审查门禁

- [ ] 没有把生成输出作为事实来源直接编辑。
- [ ] 没有恢复 UGUI 运行时依赖。
- [ ] 每个 async await 都有 owner/cancellation 决策。
- [ ] 每个资源/容器都有唯一所有者和释放路径。
- [ ] 每个警告都已修复，或记录了合理原因。
- [x] 行为或契约变化后同步更新相关 spec 文档。
- [x] 提交前运行全范围 trellis-check，并分别记录已验证和未验证证据。
