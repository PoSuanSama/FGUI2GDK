# UI 开发

FairyGUI 作为 GDK Player UI 的唯一视图后端接入 GF 的完整说明见 [FairyGUI 接入](FairyGUI接入.md)。

GDK 的 UI 配置统一由 GF `IUIManager`（经 `FairyUIManager`）管理。业务层可选择 GameHot 的 MonoBehaviour 工作流，或 ET 的 Entity/System 工作流；两者共享 UI 表、资源路径、UIGroup、descriptor 校验和 GF 生命周期。

## 共同配置链路

```text
Design/FairyGUI/GDK_FGUI/assets/**/*.xml
  → FairyGUI publish（官方 C# 绑定 + Package1_fui.bytes）

Design/Excel/GameHot|ET/Datas/Game/UI.xlsx
  → Luban 导出
  → dtuiform.json + UIFormId.cs / UGFUIFormId.cs

Luban UI 身份/策略 + settings/GDK.json 映射 + manifest
  → Generate-FairyUIFormDescriptors.ps1
  → Assets/Res/UI/FairyGUI/<CSName>.json
```

打开链（GameHot 与 ET 共用）：

```text
FairyUIManager.OpenFairyUIFormAsync(uiId, userData, ownerToken)
  → DRUIForm + descriptor 双重校验
  → 包租约 → 本地化 → CreateObject → Presenter → GF OpenUIForm
```

### UI 表字段

| 字段 | 含义 | 要点 |
| --- | --- | --- |
| `Id` | UI 类型编号 | 全表唯一，运行时打开接口使用 |
| `CSName` | 生成的常量名 | 必须是合法且唯一的 C# 标识符 |
| `Desc` | 描述 | 同时写入生成代码注释 |
| `AssetName` | 资源名 | 对应 `Assets/Res/UI/FairyGUI/<AssetName>.json` descriptor 路径 |
| `UIGroupName` | GF UI 分组 | 必须已在启动入口注册（Default/Pop/Message/Guide/RuntimeInspector） |
| `AllowMultiInstance` | 是否允许多实例 | 弹窗类通常开启，主界面通常关闭 |
| `PauseCoveredUIForm` | 被覆盖时是否暂停 | 决定 Pause/Resume 生命周期 |

GameHot 表位于 `Design/Excel/GameHot/Datas/Game/UI.xlsx`，ET 表位于 `Design/Excel/ET/Datas/Game/UI.xlsx`。

## GameHot UI

### 1. 设计 FairyGUI 组件

在 `Design/FairyGUI/GDK_FGUI/assets/Package1/` 下创建组件 XML（编辑器同步流程见 [FairyGUI 接入](FairyGUI接入.md)），发布后获得强类型绑定（`Game.FairyGUI.Package1` 命名空间）。

### 2. 添加 UI 表记录

在 GameHot 的 `UI.xlsx` 增加记录：

| Id | CSName | AssetName | UIGroupName | AllowMultiInstance | PauseCoveredUIForm |
| --- | --- | --- | --- | --- | --- |
| 103 | `TestForm` | `FairyGUI/TestForm` | `Default` | false | true |

执行 Luban 导出，常量生成到 `Unity/Assets/Scripts/Game/Hot/Code/Generate/UGF/UIFormId.cs`。运行 `Generate-FairyUIFormDescriptors.ps1` 生成 descriptor。

### 3. 创建 Presenter

```csharp
using FairyGUI;
using Game.FairyGUI.Package1;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    [FairyUIPresenter(UIFormId.TestForm)]
    public sealed class TestForm : IFairyUIPresenter
    {
        private UITestForm m_View;

        public void OnViewReady(FairyUIFormContext context)
        {
            // context.View 是官方绑定的强类型 GComponent。
            m_View = context.View as UITestForm;
            m_View.testButton.onClick.Add(OnTestButtonClick);
        }

        public void OnOpen(object userData)
        {
        }

        public void OnClose(bool isShutdown, object userData)
        {
            m_View.testButton.onClick.Remove(OnTestButtonClick);
            m_View = null;
        }

        private void OnTestButtonClick(EventContext context)
        {
            FairyUIFormService.CloseOwnedForm(m_View);
        }

        // OnPause / OnResume / OnCover / OnReveal / OnRefocus / OnUpdate 按需实现。
    }
}
```

`HotEntry.InitializeFairyGUI()` 会反射扫描 `[FairyUIPresenter]` 标记构建注册表；`OnViewReady` 收到的 `context` 携带 Widget/Event/Resource 容器（随窗体级联清理），`context.Form` 在 GF OnInit 之后才回填。

### 4. 打开界面

```csharp
FairyUIForm form = await FairyUIFormService.OpenFairyUIFormAsync(
    UIFormId.TestForm, userData, ownerToken);
```

owner token 在打开成功后持续拥有窗体，取消按 serial ID 关闭。对话框使用 `DialogParams` + `FairyDialogFlow.Open`（契约与旧 UGUI DialogParams 等价）。

## ET UI

ET 全部界面走 Component/System 打开链：状态在 ModelView Component，行为在 HotfixView 静态 System，经 `EntitySystemSingleton.TypeSystems` 派发。`UIComponent` 是 UI 的所有者（per-open CTS、owned serial/CTS），`Destroy` 固定执行 cancel pending → close owned → dispose CTS。

### 1. ModelView Component

```csharp
using ET.Client;
using UnityEngine;

namespace ET.Client
{
    [ComponentOf(typeof(UIComponent))]
    public class TestFormComponent : FairyUIFormComponent,
        IAwake, IUGFUIFormOnOpen, IUGFUIFormOnClose
    {
        // 只放状态字段。
        public int ClickCount;
    }
}
```

### 2. HotfixView System

```csharp
namespace ET.Client
{
    [EntitySystemOf(typeof(TestFormComponent))]
    public static partial class TestFormComponentSystem
    {
        [UGFUIFormSystem]
        private static void UGFUIFormOnOpen(this TestFormComponent self)
        {
        }

        [UGFUIFormSystem]
        private static void UGFUIFormOnClose(this TestFormComponent self, bool isShutdown)
        {
        }
    }
}
```

生命周期接口与旧 UGFUIForm 同构（OnOpen/OnClose/OnPause/OnResume/OnCover/OnReveal/OnRefocus/OnUpdate/OnRecycle），`FairyUIFormLifecycleSystems.cs` 提供 System 基类。

### 3. 登记工厂

在 `FairyGUIBootstrap.InitializeAsync()` 中登记 UI ID → Component 工厂：

```csharp
FairyUIFormComponentRegistry.Register(
    UGFUIFormId.TestForm,
    static owner => owner.AddChild<TestFormComponent>());
```

### 4. 打开与关闭

```csharp
UIComponent owner = root.GetComponent<UIComponent>();
FairyUIForm form = await owner.OpenFairyUIFormAsync(UGFUIFormId.TestForm, openData);
owner.CloseFairyUIForm(form.SerialId);   // 按 serial 幂等
```

不要绕过所有者直接调 `FairyUIManager`；`Destroy` 时未完成的 pending open 会被取消。

## 生命周期对应关系

| GF 回调 | 触发时机 | 常见用途 |
| --- | --- | --- |
| `OnInit` | 首次创建（GF 侧） | 经 context 回填元数据 |
| `OnOpen` | 每次打开 | 绑定事件、读取参数、刷新数据 |
| `OnUpdate` | 界面更新 | 少量持续刷新逻辑 |
| `OnPause` / `OnResume` | 被覆盖 / 恢复 | 暂停输入或动画 |
| `OnCover` / `OnReveal` | 遮挡状态变化 | 可见性相关处理 |
| `OnRefocus` | 多实例重新聚焦 | 更新焦点状态 |
| `OnClose` | 关闭 | 解订阅、停止任务 |
| `OnRecycle` | GF 实例回收 | 清理视图缓存 |

Widget/Event/Resource 容器挂在 `FairyUIFormContext` 上，随窗体自动级联与清理，Presenter 不需要手工管理。

## UIWidget

可复用的小型组件在 FairyGUI 中直接使用组件嵌套（`GComponent` + 内部逻辑），或使用 `FairyUIWidget` 抽象（挂载到宿主 context 的 Widget 容器，父级销毁时随宿主统一回收）。ET 侧 Widget 随父 UI Entity 的 `OnClose` 级联清理。

## 常见问题

### 打开后提示 UIGroup 不存在

检查 `UIGroupName` 是否已在启动入口注册：GameHot 在 `HotEntry.InitializeFairyGUI()`，ET 在 `FairyGUIBootstrap.InitializeAsync()`。两者都注册 Default/Pop/Message/Guide/RuntimeInspector 五组。

### 修改 Excel 或 XML 后 descriptor 校验失败

descriptor 由 Luban UI 行 + GDK.json 映射生成，任何一侧漂移都会在打开时被 `ValidateDescriptor` 拒绝。重跑 `Generate-FairyUIFormDescriptors.ps1` 并检查 `-Check`。

### Presenter 收不到回调

确认 Presenter 标记了 `[FairyUIPresenter(UIFormId.X)]`（GameHot）或 Component 已登记到 `FairyUIFormComponentRegistry`（ET），且官方绑定类型与 descriptor `bindingType` 一致。

### 允许多实例但第二次打开失败

同时检查表中的 `AllowMultiInstance` 与所有权。GF 必须允许多实例；ET 侧同一 owner 的多个实例按 serial ID 独立管理。

## 关键代码

| 作用 | 文件 |
| --- | --- |
| FairyGUI 原生窗口管理 | `Game/UI/FairyGUI/FairyUIManager.cs` |
| GF 窗体宿主 | `Game/UI/FairyGUI/FairyUIForm.cs` |
| Presenter 接口与上下文 | `Game/UI/FairyGUI/IFairyUIPresenter.cs`、`FairyUIFormContext.cs` |
| 包租约管理 | `Game/UI/FairyGUI/FairyPackageManager.cs` |
| 服务桥 | `FairyLocalization.cs`、`FairySound.cs`、`FairyUIGroupHelper.cs`、`FairyInputService.cs`、`FairyColorBlindness.cs` |
| GameHot Presenter 示例 | `Game/Hot/Code/UI/FairyDemoForm.cs`、`FairyDialogForm.cs` |
| GameHot 打开服务 | `Game/Hot/Code/UI/FairyUIFormService.cs` |
| ET 生命周期骨架 | `Game/ET/Code/ModelView/Client/Module/UI/FairyGUI/FairyUIFormLifecycleSystems.cs` |
| ET 工厂登记表 | `Game/ET/Code/ModelView/Client/Module/UI/FairyGUI/FairyUIFormComponentRegistry.cs` |
| ET bootstrap | `Game/ET/Code/ModelView/Client/Module/UI/FairyGUI/FairyGUIBootstrap.cs` |
| UI 常量生成器 | `Share/Tool/ExcelExporter/Generate/GenerateUGFUIFormId.cs` |
