# Unity Agent Bridge 2.x 工作流

## 首先阅读已安装契约

每次执行 Unity 查询或修改前：

1. 查找同时包含 `Assets/` 和同级既存 `.agentbridge/` 的 Unity 工程。
2. 查找已安装的 `me.xw.unityagentbridge` 包，通常位于 `Library/PackageCache/` 或内嵌的 `Packages/` 文件夹下。
3. 完整阅读其中的 `AGENT.md`。若该文件与本规范不同，以已安装契约为准。
4. 如果已安装契约或 `.agentbridge/` 任一不存在，停止并报告 Unity 尚未安装或启动 AgentBridge。不要创建目录或槽位文件。

仓库当前在 `Unity/Packages/packages-lock.json` 中通过 Git 哈希锁定该包；仍须阅读已安装副本，因为包解析结果和宿主状态可能变化。

## 保持协议不变量

- 同一时间只执行一个固定槽位交换。
- 每个请求使用全新、非空且不超过 64 个字符的 ID。
- 写入 `request.json.tmp`，刷新到磁盘，再以原子操作重命名为 `request.json`。
- 绝不操作 `processing.json`。
- 完整读取 `response.json` 并校验其 ID，然后等待 Unity 删除 `processing.json`。
- 只有 `processing.json` 消失后才能删除 `response.json`；删除操作是明确的确认响应。
- 请求和响应信封必须小于 1 MiB，`params` 必须是对象。
- 上一个请求/响应得到确认前，绝不发布新请求。

## 先发现，再执行

每个会话的第一个请求必须是：

```json
{"command":"list_commands","params":{}}
```

缓存返回的命令名、`paramsSchema`、`batchAllowed`、`supportsUndoCollapse` 和 `commandsVersion`。后续每次调用都必须依据该响应构造。绝不将命令清单或结构复制到此 Skill 中。

仅在以下情况刷新发现结果：

- 响应返回了不同的 `commandsVersion`；
- 扩展被安装、移除、启用或禁用；
- 响应返回 `UNKNOWN_COMMAND`。

调用可由代理执行的方法时，运行已发现的 `list_agent_methods`，复用其返回的完整方法 ID，并遵守 `timeoutSeconds`。不要虚构方法 ID。

## 使用会话辅助脚本

先定位已安装契约：

```powershell
python .agents/skills/gdk-development-workflow/scripts/bridge_session.py --project Unity --show-contract
```

完整阅读该文件，然后使用明确的确认参数启动持久终端会话：

```powershell
python .agents/skills/gdk-development-workflow/scripts/bridge_session.py --project Unity --ack-contract
```

辅助脚本会定位已安装契约和 Bridge 根目录，强制首次调用执行命令发现，完成原子发布/确认，跟踪 `commandsVersion`，并从每行输入中读取一个 JSON 对象。脚本有意不硬编码命令结构。

先发送 `list_commands` 并读取完整结果，再发送已发现的命令：

```json
{"command":"list_commands","params":{}}
{"command":"<discovered-command>","params":{"<schema-key>":"<value>"},"timeout":30}
{"action":"quit"}
```

运行辅助脚本前，主代理必须亲自阅读 `AGENT.md`。辅助脚本存在并不代表已确认该契约。

## 根据任务选择 Unity 证据

只使用运行时发现结果中存在的命令：

- 修改资源前检查 AssetDatabase 路径/依赖；
- 修改场景/预制体前检查层级、组件和序列化属性；
- 修改 C# 或序列化引用后编译 Unity 并读取编译结果；
- 导入、编译、进入播放模式或运行时操作后搜索 Error 日志；
- 可用时运行相关的 EditMode/PlayMode 测试；
- 视觉工作保留 GameView/SceneView 证据；
- 多步骤的项目专用冒烟测试使用 `list_agent_methods` 和聚焦的 `AgentCallable` 流程。

不要用 `dotnet build Unity/Unity.sln` 替代 Editor 编译。

## 保守恢复

- `INVALID_PARAMS`：重新阅读缓存的结构，修正 params，并使用新 ID。
- `UNKNOWN_COMMAND`：刷新 `list_commands`，再决定是否使用新 ID 重试。
- `COMMAND_DISABLED`：报告该状态；不要绕过宿主策略。
- `INTERRUPTED`：假定副作用未知，检查实际状态，再决定发起新请求是否安全。
- `METHOD_EXECUTION_FAILED` 或 `HANDLER_EXCEPTION`：诊断实现/消息；不要盲目重试。
- `RESPONSE_TOO_LARGE`：缩小查询范围，并使用新 ID 重试。

如果长时间运行的方法超过公布的等待预算，继续轮询原交换。绝不发布第二个请求。
