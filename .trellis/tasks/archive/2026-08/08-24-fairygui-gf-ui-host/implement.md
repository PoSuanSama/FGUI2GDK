# FairyGUI GF UIForm 与 UIGroup 统一宿主实施计划

## 0. 前置门禁

- [x] 保存并关闭 FairyGUI Editor，执行 `FromEditor`，确认同步状态为 `Equal`。
- [x] 重新生成并检查仓库/Unity manifest，真实发布 Package1 和官方 C# 绑定。
- [x] 恢复 Unity Agent Bridge：工程已有 `.agentbridge/`，首次 `list_commands` 成功。
- [x] 记录当前工作树中的用户改动并保持不变。

## 1. 事实源、描述符与生成

- [x] 恢复 GameHot/ET `UI.xlsx` 的 FairyDemoForm 行，并仅通过 Luban 导出恢复生成 ID/数据。
- [x] 将 `GDK.json` 的 UIForm 映射改为以稳定 `CSName` 为键，不再重复 UI ID、AssetName 或 GF 策略。
- [x] 联结 Luban UI 数据与 FairyGUI manifest/映射生成版本化 descriptor，不手写派生输出。
- [x] 扩展聚焦测试，拒绝缺失/重复/漂移身份、basename 冲突、未知包/组件/绑定和过期/多余描述符。
- [x] 生成两次并断言第二次字节无差异。

## 2. 包管理同步租约

- [x] manifest 显式声明包描述与全部外部资产路径；禁止从包名前缀猜测依赖。
- [x] 只有全部外部资产加载成功后才进入 Ready；任一失败使 acquisition 失败并完整回滚。
- [x] 覆盖 prepared payload 所有权移交、取消、GF 失败、重复释放和依赖逆序释放。

## 3. GRoot 与 UIGroup 容器

- [x] 实现单一 `FairyUIRootService`，按 GF UIGroup 身份创建/复用/释放容器。
- [x] 映射 group depth、depthInUIGroup、多实例顺序、可见性和 touchable。
- [x] 断言没有创建每窗体 UIPanel 或额外 StageCamera。

## 4. Helper 与 UIFormLogic

- [x] 实现迁移期 `GDKUIFormHelper` 的 prepared payload 路径和隔离的旧 prefab 兼容路径。
- [x] 将 schema/包/外部资产/组件/绑定/presenter 等可预期失败前移到 GF open 之前。
- [x] 实现 `FairyUIFormLogic` 的 prepared state 采用、全部 GF 生命周期、`finally` 清理和对象池重置。
- [x] 实现仅接收 `uiId` 的 `OpenFairyUIFormAsync`，从 DRUIForm 应用 GF 策略，保留原始 userData，
      并覆盖打开成功后的持续 owner token 与 GF serial ID 清理。
- [x] 在第一条 FairyGUI 打开路径前一次性初始化 `FairyUIRootService`，重复初始化保持幂等。

## 5. 演示迁移

- [x] 使用官方 Package1/MainView 生成绑定替换所有 `GetChild("...")`。
- [x] 将演示业务接到统一宿主，移除 `AFairyUIForm`、每窗体 UIPanel 和变换隔离路径的运行时引用。
- [x] 统一宿主通过后删除或停止引用 `FairyDemoForm.prefab` POC；资源与 `.meta` 同批处理。

## 6. 资源与 Unity 配置

- [x] 通过 Unity Agent Bridge/API 配置 GameEntry custom helper，不手改 prefab YAML。
- [x] 通过 Unity Agent Bridge/API 为 GameHot/ET 资源规则加入 FairyGUI 描述符和包目录。
- [x] 重新生成/回读 ResourceCollection，验证 manifest、descriptor、bytes 和外部资源归属。

## 7. 验证

```powershell
pwsh -NoProfile -File ./Tools/FairyGUI/Test-FairyGUITools.ps1
pwsh -NoProfile -File ./Tools/FairyGUI/Test-GDKProject.ps1 -Check
pwsh -NoProfile -File ./Tools/FairyGUI/Sync-GDKDemoToEditor.ps1 -Mode Status
python ./.trellis/scripts/task.py validate 08-24-fairygui-gf-ui-host
python .agents/skills/gdk-development-workflow/scripts/validate_changes.py
git diff --check
git diff --stat
```

Unity Agent Bridge 运行时发现后执行：编译/Error 日志、描述符与资源集合回读、100 次生命周期测试、
16:9/19.5:9/4:3 GameView 截图和按钮交互。`.NET` 构建只能作补充类型检查。

- [x] Unity Agent Bridge 运行时验证：GameHot/ET 编译、Error 日志、描述符与资源集合回读、100 次生命周期测试、
      16:9/19.5:9/4:3 GameView 截图和按钮交互。

## 8. UIGroup 层级纠偏

- [x] 用 `GDKUIGroupHelper` 和 `Container(GameObject)` 复用框架 `UI Group - <name>` 节点。
- [x] 隐藏仅承担 GF 对象池身份的轻量宿主，使 `MainView` 成为 UIGroup 的直接可见子节点。
- [x] 通过 Agent Bridge 同时配置 UIForm/UIGroup helper，重新执行编译、层级、交互、截图、100 次生命周期和 shutdown 验证。

## 9. 上游合并后的收尾纠偏

- [x] 合并最新 GDK 后复核任务边界；无未解决冲突，继续保持不修改 UGF/FairyGUI vendor 核心。
- [x] 通过 Unity AssetDatabase 删除旧 `FairyDemoForm.prefab` 及 `.meta`，重建并回读 GameHot/ET
      ResourceCollection，消除描述符与 prefab 双入口。
- [x] 将 prepared-state 交接改为对象池同步上下文 + 新实例 GF serial ID 绑定；同描述符、同/null
      `userData` 的并发请求不再依赖队列顺序，GF 与 presenter 仍观察原始 `userData`。
- [x] 扩展 100 次 PlayMode 探针，每 10 次真实覆盖/恢复并检查 pause/cover、resume/reveal、refocus、
      visibility/touchability 与 userData 引用身份；最终 GF、GRoot、包租约和资源诊断回到基线。

## 10. 2026-08-28 持续 owner token 复核纠偏

- [x] 修复打开返回后 owner token 失效：registration 按 GF serial ID 关闭目标实例，并由
      `FairyUIForm` 在 Close/Recycle/失败路径释放。
- [x] 覆盖已取消 token、打开后下一帧取消、旧 token + 池化宿主复用、三个同 assetName 多实例逐个
      取消、cancel + 显式 close、100 次回基线和窗口保持打开时停止 PlayMode。
- [x] Unity Bridge generation 129 编译 0 error/0 warning；GameHot smoke、生命周期 Agent 和退出后
      Error 日志验证通过。
- [x] 补充 `.trellis/spec/frontend/hook-guidelines.md`，固定“捕获 serial、转交 registration 所有权、
      池化复用前释放”的可执行契约。

## 回滚点

- 描述符生成器、核心宿主、演示迁移和资源配置分别保持可审查边界。
- Helper 未通过失败/对象池门禁前不切换 GameEntry 配置。
- ResourceCollection 实际加载未通过前不删除旧演示 POC。

## 收口证据（2026-08-29 阶段 G）

38/38 已勾选。GF 宿主 owner token/100 次/池化/shutdown 在阶段 G 重验通过；
Player AssetBundle 路径的 descriptor 双加载缺陷已修复（提交 1d33b860）。
