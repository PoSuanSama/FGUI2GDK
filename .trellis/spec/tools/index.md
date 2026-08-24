# GDK 工具规范

本节覆盖 `Tools/` 下仓库自有的导出、生成和 Editor 自动化，以及它们的外部工具适配器。

## 质量检查

- [FairyGUI 事实来源与生成](./fairygui-source-generation.md)：保留仓库事实来源、同步状态、检查、
  清单和官方绑定生成契约。
- [FairyGUI CLI 发布](./fairygui-cli-publish.md)：消费外部 FGUI Agent Bridge 时，保留同步门禁、
  精确单包参数、JSON/产物证明和插件所有权边界。
- 将不可信路径作为结构化进程参数传递，以超时限制操作，并同时验证机器可读结果和预期产物。
- 运行聚焦语法/行为检查和 GDK 变更守卫；无法执行的持证工具检查必须报告为未验证。
