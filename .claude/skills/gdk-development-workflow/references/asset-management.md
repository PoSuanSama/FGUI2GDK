# Unity 资源与资源管理规范

## 保持资源身份稳定

Unity GUID 是持久引用。将每个资源及其 `.meta` 视为一个整体。

- 如果已发现的 Unity Agent Bridge/AssetDatabase 命令支持相关操作，通过该命令创建、移动、重命名和删除已导入资源。
- 绝不通过重新生成 `.meta` 来修复引用丢失。恢复原始元数据，或执行有计划的迁移。
- `Unity/Assets` 下每个新增文件和文件夹都必须包含对应的 `.meta`。
- 移动脚本、预制体、场景、材质、控制器或 ScriptableObject 后，检查 GUID 和 fileID 引用。
- 避免在 Windows 上仅修改名称大小写。必要时通过 Unity 执行一次明确的中间名称重命名。

运行 `scripts/validate_changes.py`，检测配对缺失、变更 GUID 冲突、大小写冲突、序列化资源和大型文件。

## 使用正确的来源与目标目录

| 资源类型 | 来源或目标目录 |
| --- | --- |
| 运行时资源 | `Unity/Assets/Res/` 下现有的业务领域文件夹 |
| 热更新 UI 窗体 | `Unity/Assets/Res/UI/UIForm/Hot/` |
| 实体 | `Unity/Assets/Res/Entity/` 或现有的已配置子目录 |
| 仅供 Editor 使用的资源/配置 | `Unity/Assets/Res/Editor/` |
| Luban 二进制/JSON 输出 | 现有 `Unity/Assets/Res/Luban/` 流程 |
| Luban 源数据 | `Design/Excel/` |
| Proto 源文件 | `Design/Proto/` |
| 本地化源数据 | `Design/Excel/Localization.xlsx` |

不要为了符合个人习惯创建新的根目录。遵循现有资源规则、表中 `AssetName`、分组和加载器约定。

## 保持配置与资源同步

对于 UI、Entity、UIEntity、Sound、Scene、本地化或其他由表驱动的资源：

1. 新增或更新源资源。
2. 更新对应的 Excel/Luban 数据行，使用唯一且稳定的 ID，并填写正确的相对 `AssetName`。
3. 运行项目导出工具。
4. 检查生成的 ID/数据和资源集合变更。
5. 通过所属的 GameHot 或 ET 路径验证加载。

绝不通过编辑生成的 ID 文件来让缺失的数据行通过编译。

## 优先使用 Unity 序列化 API

对预制体、场景、ScriptableObject、导入器、组件、层级和序列化属性执行操作时，使用 Agent Bridge/Unity API。修改前读取目标，修改后再次读取确认。

只有运行时 `list_commands` 证明不存在能表达该任务的受支持操作后，才能将直接编辑 YAML 作为最后手段。执行前：

- 说明 Unity API 路径为何无法满足需求；
- 通过 Git diff 记录预期对象结构，不要创建重复的资源副本作为备份；
- 保持 YAML 头、类 ID、GUID、fileID、组件列表、父子链接、缩进不变；
- 强制导入，并通过 Bridge 验证层级/属性、编译结果和 Error 日志。

## 控制导入与平台成本

- 复用现有的纹理、音频、模型预设和相邻资源的导入设置。
- 有意识地设置压缩、最大尺寸、MipMap、读写、网格优化、动画导入和平台覆盖项。
- 没有运行时需求时，不要启用 Read/Write、未压缩纹理/音频或内嵌材质。
- 避免大范围重新导入。检查产生的 `.meta` 差异，确认没有意外的平台配置变动。
- 保持地址/资源分组与 `ResourceCollection` 一致；文件存在于 `Assets` 下并不能证明它会被打入成品。

## 管理大型资源和二进制资源

- 添加二进制文件前，检查大小和许可证。
- 未获批准时，不要引入 Git LFS 或修改仓库级二进制文件策略。
- 如果成品只需要优化后的派生资源，按照项目现有惯例将源资源放在运行时目录之外。
- 只有完成依赖/引用发现和运行时归属方审查后，才能删除资源。
- 绝不以 Unity 资源形式提交秘密信息、签名密钥、服务凭据、私有证书或用户数据。

## 验证视觉资源和运行时资源

修改 UI、场景或预制体后，在目标分辨率和宽高比下验证，并检查 Error 日志。外观重要时应保留截图。验证运行时加载时，检查配置的 ID/路径、分组、关闭/隐藏生命周期和资源释放行为；仅导入成功并不足够。
