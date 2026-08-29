# Entity 开发

GDK 使用 GF EntityComponent 负责资源加载、实例池和显示生命周期。GameHot 直接编写 EntityLogic；ETEntity 则让 ET 实体拥有一个 GF Entity 视图，并在 ET System 中接收 GF 生命周期。

## 选择实现方式

| 方式 | 逻辑位置 | 特点 |
| --- | --- | --- |
| GameHot Entity | `Game/Hot/Code/Entity/` | EntityData + EntityLogic，符合 GF 工作流 |
| ETEntity | `Game/ET/Code/ModelView` + `HotfixView` | ET 所有权、System 和 GF 视图解耦 |

## 共同配置链路

```text
Entity.xlsx
  → ExcelExporter
  → Assets/Res/Editor/Luban/dtentity.json
  → EntityId.cs 或 UGFEntityId.cs
  → GF 根据 AssetName、EntityGroupName、Priority 显示预制体
```

### Entity 表字段

| 字段 | 含义 | 要点 |
| --- | --- | --- |
| `Id` | Entity 类型编号 | 全表唯一，不是运行时实例编号 |
| `CSName` | 生成常量名 | 合法且唯一的 C# 标识符 |
| `Desc` | 描述 | 写入生成代码注释 |
| `AssetName` | 资源名 | 相对 `Assets/Res/Entity/`，不含扩展名 |
| `EntityGroupName` | GF Entity 分组 | 必须已在 GameEntry 中配置 |
| `Priority` | 实例优先级 | 影响对象池与释放策略 |

GameHot 表位于 `Design/Excel/GameHot/Datas/Game/Entity.xlsx`，ET 表位于 `Design/Excel/ET/Datas/Game/Entity.xlsx`。

## GameHot Entity

### 1. 创建资源与配置

把预制体放入 `Unity/Assets/Res/Entity/`。例如 `NewEntity.prefab` 对应表中的 `AssetName=NewEntity`。

新增表记录后执行 `Game/Tool/ExcelExporter`。常量生成到：

```text
Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/EntityId.cs
```

### 2. 定义运行数据

项目 Demo 使用 `EntityData` 区分“类型编号”和“实例编号”。类型编号来自 `EntityId`，实例编号由 EntityComponent 生成或由业务指定。

```csharp
using UnityEngine;

namespace Game.Hot
{
    public sealed class NewEntityData : EntityData
    {
        public NewEntityData(int entityId, int typeId)
            : base(entityId, typeId)
        {
        }

        public float Speed { get; init; }
    }
}
```

复杂实体可参考 `AsteroidData`：构造时从 `HotEntry.Tables` 读取 Luban 记录，把配置转换为本次实例需要的只读状态。

### 3. 定义 EntityLogic

```csharp
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public sealed class NewEntity : Entity
    {
        private NewEntityData m_Data;

        protected override void OnShow(object userData)
        {
            base.OnShow(userData);
            m_Data = userData as NewEntityData;
            if (m_Data == null)
            {
                Log.Error("NewEntityData is invalid.");
            }
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            CachedTransform.Translate(Vector3.forward * m_Data.Speed * elapseSeconds);
        }

        protected override void OnHide(bool isShutdown, object userData)
        {
            m_Data = null;
            base.OnHide(isShutdown, userData);
        }
    }
}
```

`Game.Hot.Entity` 已在 `OnShow` 中处理 `EntityData`、位置、旋转和名称。只需要基础 GF 扩展而不需要 Demo 数据约定时，可从 `AHotEntity` 构建自己的基类。

### 4. 显示与隐藏

```csharp
var data = new NewEntityData(
    GameEntry.Entity.GenerateSerialId(),
    EntityId.NewEntity)
{
    Position = Vector3.zero,
    Speed = 3f,
};

GameEntry.Entity.ShowEntity<NewEntity>(EntityId.NewEntity, data);
```

`ShowEntity<T>` 根据表记录解析资源、分组和优先级。需要统一回收多个实例时，使用 `EntityContainer` 管理所有权和取消令牌。

```csharp
GameEntry.Entity.TryHideEntity(serialId);
```

隐藏会触发 `OnHide`，实例是否立即销毁由 GF EntityGroup 的对象池配置决定。

## ETEntity

UGUI 时代的 `UGFEntity<T>` 宿主层与 `UGFEntityCodeCreator` 已随零 UGUI 清理移除。ET 侧世界空间实体当前使用 `FairyEntity` 基础抽象：

```csharp
namespace ET.Client
{
    [ComponentOf(typeof(GFEntityComponent))]
    public class FairyEntityComponent : Entity, IAwake<Game.FairyEntity>, IDestroy
    {
        public Game.FairyEntity Entity { get; set; }
    }
}
```

`Game.FairyEntity` 是 Game 层基础类（挂在 FairyGUI 组件容器下，随宿主上下文清理）；它只有基础抽象与示例，不是与原 UGUI Entity 行为完全对等的证明。需要世界空间 UI/实体行为的业务，应在 FairyEntity 基础上补齐等价实现并随批次验证。

## 生命周期对应关系

| ETEntity 回调 | 触发时机 | 常见用途 |
| --- | --- | --- |
| `IAwake` | ET 实体创建 | 初始化业务状态 |
| `OnShow` | GF Entity 显示完成 | 读取 View、播放出生表现 |
| `OnUpdate` | GF Entity 更新 | 视图插值、动画同步 |
| `OnHide` | GF Entity 隐藏 | 停止表现、解订阅 |
| `OnRecycle` | GF 实例回收 | 清理 Mono 缓存 |
| `OnAttached` / `OnDetached` | 子实体挂接变化 | 武器、挂点、组合体 |
| `OnAttachTo` / `OnDetachFrom` | 自身父级变化 | 跟随与解绑 |
| `IDestroy` | ET 实体销毁 | 清理 ET 侧状态 |

## Entity 与 UIEntity

普通世界对象使用 `Entity.xlsx` 和 `UGFEntityId`。UI 内可复用的实体资源使用 `UIEntity.xlsx` 和 `UGFUIEntityId`，加载接口为 `ShowUIEntityAsync`。

两张表的资源根目录和 GF 分组可以不同。UIWidget 内部加载的视图属于 UIEntity，不要把普通 EntityId 传给 UIEntity 接口。

## 常见问题

### 显示时报 EntityGroup 不存在

检查表中的 `EntityGroupName` 是否已配置在 `Unity/Assets/Res/GameEntry.prefab`。编辑器预加载流程会扫描表并输出缺失分组。

### ETEntity 的 `View` 为 null

确认 `FairyEntityComponent` 已 Add 且 `Game.FairyEntity` 视图在宿主上下文（FairyUIFormContext 或 FairyEntity 容器）中未被回收。

### 修改生成的 EntityId 后又被覆盖

ID 文件由 ExcelExporter 生成。应修改 `Entity.xlsx` 的 `Id`、`CSName` 或描述，再重新导出。

### 实例隐藏后仍收到 ET 逻辑

GF 隐藏与 ET Dispose 是两个层次。ETEntity 应由 ET 所有者移除或 Dispose；只调用 GF 的 HideEntity 不会自动销毁业务实体。

## 关键代码

| 作用 | 文件 |
| --- | --- |
| GF Entity 扩展 | `Game/Entity/EntityExtension.cs` |
| GameHot 示例基类 | `Game/Hot/Code/Entity/EntityLogic/Entity.cs` |
| FairyGUI 实体基础 | `Game/UI/FairyGUI/FairyEntity.cs` |
| ET 实体 Component | `Game/ET/Code/ModelView/Client/Module/GFEntity/FairyEntityComponent.cs` |
| Entity 常量生成器 | `Share/Tool/ExcelExporter/Generate/GenerateUGFEntityId.cs` |
