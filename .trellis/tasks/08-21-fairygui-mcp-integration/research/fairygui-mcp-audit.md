# FairyGUI-MCP 审计记录（历史方案，已否决）

> 2026-08-21 决策：不再接入或维护 `kaohum/FairyGUI-MCP`。本文件仅保留为历史调研证据；
> 当前方案消费已部署的 `Wilson520403/fgui-agent-bridge`，不修改其插件、Python 或 MCP 源码。

## 审计对象

- 上游仓库：`https://github.com/kaohum/FairyGUI-MCP`
- 审计提交：`7b54465c69155c6621029b3be797adc0b8c91653`
- 本地只读副本：`D:/Temp/FairyGUI-MCP-audit-3d991035866441b18ef62f6366ccc0ed`
- 许可证：MIT（`LICENSE:1-18`）
- 运行时：Python `>=3.10`；`fastmcp>=0.1.0`、`pydantic>=2.0.0`；Windows 截图额外依赖
  `pywin32` 与 `Pillow`（`pyproject.toml:5-24`）
- 上游没有锁定依赖文件，也没有自动化测试目录；README 将验证描述为手工调用。

## 能力与边界

Python stdio MCP 服务通过 `plugins/MCPBridge/bridge` 下的 JSON 文件队列与 FairyGUI Lua
插件通信。上游提供包/组件读取与解析、编辑器打开/预览/控制器/截图、保存/关闭和发布入口。
它不是 XML 生成器：AI 仍需编辑 GDK 仓库中的 XML/配置，再经过 GDK 检查、同步和生成工具形成
可发布输入。上游的 `fg_validate_component` 只能作为辅助诊断，不能替代
`Tools/FairyGUI/Test-GDKProject.ps1` 的 GDK 契约检查。

## 关键风险证据

### 1. 通信文件不是原子发布

- Python 直接向最终命令文件写入：`src/mcp_fairygui/bridge/command_queue.py:63-68`。
- Python 读取结果遇到 `JSONDecodeError` 会继续轮询：`command_queue.py:94-111`。
- Lua 直接向最终结果文件写入：`plugin/MCPBridge/src/command_handler.lua:353-366`。
- Lua 在解析失败、读取失败或命令未执行时仍会删除命令文件：
  `command_handler.lua:138-223`，尤其是 `217-220`。

结果是半写入 JSON 可能被读取；解析失败的命令会丢失而不是进入可诊断的失败状态。

### 2. 没有单飞约束

`CommandHandler.poll` 在一次定时器回调中遍历所有命令文件并执行：
`plugin/MCPBridge/src/command_handler.lua:130-223`。Python 端也没有跨请求锁。
多个 MCP 调用可能在 FairyGUI 主线程操作、保存、预览或发布时互相交错。

### 3. 文件变更工具缺少根目录约束

`fg_move_resource` 将 `package_name`、`new_path` 和名称直接拼入包路径并移动文件：
`src/mcp_fairygui/tools/file_tools.py:246-328`，关键路径计算在 `269-303`。
`fg_delete_resource` 同样直接拼接目录并删除文件：`file_tools.py:330-404`，关键路径在
`348-391`。在 `resolve()` 后没有统一的根目录包含性检查，也没有跨文件原子回滚。

### 4. 发布返回值不是完成证明

- Lua 的 `PublishHandler:Run()` 是异步触发，调用后立即返回：
  `plugin/MCPBridge/src/command_handler.lua:2194-2217`。
- 单包处理器在触发后立即返回 `published=true`：`command_handler.lua:2343-2354`。
- 单包失败会回退到工具栏点击，而工具栏路径可能发布全部包：
  `command_handler.lua:2357-2365`。
- Python 仅把该字段格式化为“发布已触发”，没有检查预期字节/代码产物：
  `src/mcp_fairygui/tools/editor_tools.py:538-567`。

这会把“命令已排队”误报为“目标包发布成功”，并有误发布全部包的风险。

### 5. 全局副作用与高风险探查命令

`main.lua` 全局替换 `fprint` 并持续设置 `Application.runInBackground=true`：
`plugin/MCPBridge/main.lua:10-18`、`64-87`。插件还暴露 `reload_all_plugins`、API 探查和
发布探查等内部能力；这些能力不属于 GDK 的最小闭环，且可能触发已销毁插件回调或跨插件状态变化。

## 采用决策

1. 采用该上游提交作为基线，保留 MIT 版权与上游来源，不绕过 FairyGUI Community/Professional
   许可检查，不实现自定义二进制发布器。
2. 将上游 Python 服务和 Lua 插件作为 `Tools/FairyGUI` / GDK FairyGUI 项目中的固定第三方
   输入；所有补丁以可审查的最小差异保存，并记录上游提交。
3. 仅在接入层修复原子命令/结果发布、单飞、超时/陈旧文件清理和发布产物确认；发布仍调用
   FairyGUI 官方 `PublishHandler`，禁止失败后全量工具栏回退。
4. 默认不注册 `fg_move_resource`、`fg_delete_resource`、全插件重载、内部 API 探查和触发后不等待结果的
   日志操作。若未来开放文件变更，必须先通过根目录包含性检查、引用检查、
   备份/回滚和独立审查。
5. MCP 只指向 `D:/Unity/Project/GDK_FGUI` Editor 工作副本；仓库
   `Design/FairyGUI/GDK_FGUI` 仍是唯一事实来源。同步、检查、清单和绑定生成仍由
   GDK 工具负责，MCP 不得绕过它们。

## 验证重点

- 半写入命令/结果、损坏 JSON、重复请求、并发请求、超时和陈旧文件均可重复测试。
- 预览、截图、控制器切换和读取工具必须返回机器可判定结果；已知编辑器分辨率/截图限制需记录。
- 发布前后记录输出目录快照；仅当目标包的预期字节数据、描述符/资源和启用的绑定输出在超时内
  出现、大小非零且哈希/修改时间发生预期变化时才报告成功；否则按失败处理。
- MCP 成功连接后仍必须运行 GDK `Status`、XML 检查、清单 `-Check` 和现有聚焦测试。
- 上游 GUI/Community 许可限制导致无法自动发布时，报告为未验证或手工发布，而不是伪造成功。
