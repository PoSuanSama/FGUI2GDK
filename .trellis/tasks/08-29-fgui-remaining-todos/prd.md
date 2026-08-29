# FairyGUI 收尾遗留项

## Goal

记录 FairyGUI 收尾完成后仍遗留的待办项，供后续按需立项处理。本任务本身不实现这些项，只做清单登记与优先级标注。

## Requirements

### R1. 启动/版本检查/资源更新降级（P0，需产品决策）

`ProcedureCheckVersion` 直接跳过版本检查、强制更新 URL 不可用；`ProcedureUpdateResources` 只有日志、无进度 UI/失败重试/退出，失败后状态机停住。对应原 AC12 的内置 FairyGUI bootstrap 包要求。需产品决策：重建 FairyGUI bootstrap，或正式确认产品删除。

### R2. ET 生产 Player 证据不足（P1）

仅 GameHot Windows64 IL2CPP 证据；ET 是双符号 Editor 冒烟，LockStep 等价性未验证。需 ET IL2CPP Player 构建 + HybridCLR/裁剪/AOT/资源加载验证。

### R3. DestroyImmediate 生命周期问题（P1）

`ET/Loader/Init.cs` 的 `Init.OnDestroy` 里 `DestroyImmediate(runner)` 与 Unity 自动销毁 Runner（本 GameObject 组件）冲突，报 "Destroying object multiple times" + 连锁 `Runner.OnDestroy` NRE。直接移除 DestroyImmediate 或把 World.Dispose 移到 Init.OnDestroy 都导致 fiber 清理不完整（RouterManager fiber 重复）。需重构 Runner 生命周期（不随 Init 的 GameObject 自动销毁）。

### R4. Router 端口 30300 系列冲突（P1）

`f30c2ebc` 的 ReuseAddress 只覆盖 `UdpTransport`；ET Server 的 Router 服务用 HTTP 监听（30300）+ 自己的 UDP（30301-30304），重复进出 PlayMode 时端口冲突。需给 Router 的 transport 也加 ReuseAddress，或确认其 bind 路径。

### R5. 服务桥/无障碍不完整（P2）

SDK 主文本本地化未解决、无真实音频、真机刘海屏/手柄矩阵未做、色觉只有 lint 无 Player URP 滤镜。依赖外部资源/设备/SDK 升级。

### R6. 验证证据不可持续（P2）

运行时检查主要是 AgentCallable 冒烟，无 Unity Test Framework 测试集/CI/跟随修订号截图矩阵；AC21 性能无基线、三宽高比×四语言截图矩阵未完成。

### R7. ET Demo 的 InventoryItem 演示 Widget（可选）

ET 模式 `FairyDemoFormComponentSystem.OnViewReady` 有意创建 `FairyInventoryItemWidget` 演示 Widget 容器用法，GameHot 模式没有。如不需要可移除。

## Acceptance Criteria

- [ ] R1 已决策（重建 bootstrap 或产品删除），或明确记录为待决策。
- [ ] R2 ET IL2CPP Player 构建/启动证据补齐，或记录阻塞原因。
- [ ] R3 Runner 生命周期重构，消除 "multiple times" + Runner.OnDestroy NRE，且 fiber 清理完整。
- [ ] R4 Router 30300 端口冲突消除，重复进出 PlayMode 无 bind error。
- [ ] R5/R6/R7 逐项处理或明确记录为后续批次。

## Out of Scope

- 不重新实现已完成的接入层（零 UGUI、能力透出、输入/声音/文档、fullScreen）。
- 不改已验证的共享宿主公共 API、包租约、owner token 生命周期。

## Key Decisions

- 本任务为清单登记，各项需单独立项实施；R1 必须先产品决策。
- R3 属于 ET 框架生命周期，改动需谨慎，优先小步验证 fiber 清理完整性。
