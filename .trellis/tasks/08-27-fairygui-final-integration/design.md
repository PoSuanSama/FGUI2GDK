# FairyGUI 多界面发布与最终接入收口设计

## 1. Change Boundary

当前最小行为缺口不是共享宿主本身，而是接入链尚未被带 Controller/GList 的真实业务界面、多实例浮动窗、点击置顶和真实覆盖栈共同证明，并且 UI 事实源已出现 103 行缺失、派生输出仍保留的漂移。

变更归属：

- 外部工具：仓库外的官方 `uv` 与 `fgui-agent-bridge` CLI，只作为执行依赖。
- FairyGUI 事实来源：`Design/FairyGUI/GDK_FGUI/assets/Package1`、`settings/GDK.json` 与派生 manifest。
- Luban 事实来源：GameHot/ET 两份 `Design/Excel/**/UI.xlsx`。
- GameHot：新增三个 Presenter/数据对象，扩展 `HotEntry` 映射。
- Editor 验证：新增独立的最终接入 AgentCallable，不继续膨胀已有单界面探针职责。
- 文档：更新 `Book/FairyGUI接入.md`。

明确不修改共享宿主公共契约、UGF/FairyGUI 供应商源码、现有生产 UGUI 页面、Bridge 插件或 MCP 配置。

## 2. External CLI Boundary

官方仓库克隆到 GDK 仓库之外的固定开发工具目录，记录 `git rev-parse HEAD`，使用官方锁文件执行 `uv sync --frozen`。执行时把虚拟环境中的 `fgui-agent` 绝对路径显式传给 `Publish-GDKDemo.ps1`；仓库脚本和文档不保存该机器路径。

安装后只执行只读 `status/ping/project/packages` 验证服务端与 Editor 插件版本匹配。当前插件已在线且为 `0.8.1`，因此不运行 `sync_to_project.py --apply`；若版本不匹配则停止，不在 Editor 打开时升级插件。

## 3. FairyGUI Component Contract

背包夹具均位于现有 `Package1`，使用现有 MainView 的简洁深色验证风格，不引入外部资源。

| Component | Runtime form | Stable generated members | Purpose |
| --- | --- | --- | --- |
| `InventoryView` | `FairyInventoryForm` | Controller `category`; `itemList`, `allButton`, `equipmentButton`, `consumableButton`, `questButton`, `statusText`, `openOverlayButton`, `closeButton` | 背包分类、列表和业务入口 |
| `InventoryItem` | 非 UIForm | `nameText`, `quantityText`, `qualityGraph` | GList 可复用物品项 |
| `ItemDetailWindow` | `FairyItemDetailForm` | `windowFrame`, `itemNameText`, `categoryText`, `quantityText`, `descriptionText`, `instanceText`, `closeButton` | 可多开、可点击置顶的详情浮动窗 |
| `InventoryOverlayView` | `FairyInventoryOverlayForm` | `messageText`, `closeButton` | 覆盖并返回背包 |

`InventoryView` 尺寸为 1280×720；`category` Controller 的页面固定为 `all/equipment/consumable/quest`，分类按钮切换 selectedPage，Presenter 根据页面刷新 GList。`ItemDetailWindow` 使用 1280×720 透明根和可定位的窗口框，透明区域不放置全屏命中对象，使下层详情窗仍可被点击。

组件通过 Editor API 创建和编辑，保存后执行 Package1 精确发布。组件 ID 由 FairyGUI Editor 生成并进入 `package.xml`/manifest，代码不猜测或手写 ID。`GDK.json` 的 UIForm 映射使用组件名、官方绑定全名和 Presenter 全名；`InventoryItem` 作为包内组件由 `InventoryView` 的 item renderer 使用，不生成 descriptor。

## 4. Source and Generated Data Flow

```text
FairyGUI Editor Package1 components
  -> save + exact Package1 publish
  -> Package1_fui.bytes + official C# bindings
  -> close Editor + FromEditor
  -> repository XML/package.xml
  -> Test-GDKProject + runtime manifest

GameHot/ET UI.xlsx (103..106)
  -> existing Luban export
  -> DRUIForm JSON/binary + GameHot/ET generated UIForm IDs

UI.xlsx identity/policy + GDK.json Fairy mapping + Fairy manifest
  -> Generate-FairyUIFormDescriptors.ps1
  -> Assets/Res/UI/FairyGUI/*.json
  -> FairyUIFormService
  -> shared GF UIForm/GRoot/UIGroup host
  -> generated binding + GameHot Presenter
```

生成顺序固定为：恢复/新增 Excel 源行 → Luban 导出 → Editor 创建/保存/发布 → 关闭 Editor 并 `FromEditor` → manifest 检查/生成 → descriptor 生成 → Unity 导入/编译/运行验证。若实际工具要求发布早于 Luban，可交换前两项，但每个生成器运行前必须已有完整输入，且最终进行两次无差异生成检查。

## 5. Runtime Registration and User Data

`Package1Binder.BindAll()` 对四个表单共用。`HotEntry` 的 Presenter factory 使用 CSName 的显式映射创建 `FairyDemoForm`、`FairyInventoryForm`、`FairyItemDetailForm` 和 `FairyInventoryOverlayForm`。

三个新 Presenter 各自只依赖官方生成绑定。测试用数据对象是 GameHot 业务数据，不改变 `FairyUIFormService` 签名，也不包装 GF 看到的原始 userData：

- 背包数据保存确定性的物品集合、打开详情/覆盖层和关闭动作，以及供断言读取的 Controller/生命周期状态。
- 详情数据保存物品快照、稳定实例 token、窗口位置，并在 open 返回后绑定自身 serial ID 的 close/refocus 动作。
- 覆盖层数据保存关闭动作和状态标签。

每次点击物品都调用 `OpenFairyUIFormAsync(105, detailData)` 创建新实例。点击 `windowFrame` 调用数据对象中的 refocus 动作，最终由 `GameEntry.UI.RefocusUIForm(detailForm, detailData)` 重排 Pop 组并触发 `OnRefocus`。Presenter 的按钮回调只调用数据对象提供的同步动作；异步打开由拥有者方法捕获异常并记录，避免 `async void` 和未观察的 `.Forget()` 异常。

## 6. Validation Design

新增 `FairyGUIFinalIntegrationAgent`，保持 Editor-only，并通过运行时发现后的完整 AgentCallable ID 调用：

1. `ValidatePublishedForms`：校验 103–106 descriptor、四个背包组件、`category` Controller 页面、官方绑定和资源集合。
2. `ValidateInventoryController`：依次切换四个页面，断言 selectedPage、按钮状态、GList 数据和重复切换稳定。
3. `ValidateInventoryDetailWindows`：通过三个物品打开三个 105，断言 serial、GComponent、Presenter/item/token 独立；按中间→最旧→最新顺序点击窗口并断言 GF depth、FairyGUI child index 与 `OnRefocus` 次数一致，再乱序关闭并回到基线。
4. `ValidateStackLifecycle`：关闭详情后打开 104/106，断言真实 pause/cover、resume/reveal/refocus、visible/touchable 和最终基线。
5. `ValidateFailureCleanup`：在 `finally` 可恢复的临时 descriptor/Presenter factory 范围内覆盖缺失、漂移、取消及并发一成一败，确认资源和 Git 工作树未残留测试破坏。
6. 回归现有 103 探针、100 次生命周期、shutdown、输入与三种宽高比截图。

临时 descriptor 测试保存原始字节和导入状态，逐例修改、导入、执行、恢复并再次导入；任何中间失败都在 `finally` 恢复。测试结束后运行 manifest/descriptor `-Check` 和 `git diff`，禁止把损坏夹具带入提交。

## 7. Compatibility and Rollback

- UI ID 103 保持原身份；104 背包、105 多实例详情窗、106 背包覆盖层均为新增且当前未占用，不重新解释既有配置。
- GameHot 与 ET 表保持同一 UI 身份，但新 Presenter 只在 GameHot 编译边界实现，不复制 ETUI 页面。
- 所有新增资源留在已有 `UI.FairyGUI` 规则内，不创建新的资源组。
- 若 Editor 创建或发布失败，使用 Bridge undo/redo 或删除本次新组件并保存；未执行 `FromEditor` 前仓库事实源不受影响。
- 若导入或运行验证失败，回滚新增 Excel/GDK 映射、Presenter 和测试，重新生成即可恢复到 103 单界面基线。
- 外部 CLI 工具目录可独立删除，不影响仓库或 FairyGUI 工程插件。

## 8. Risks

- 当前 Excel 103 漂移说明上游合并后生成物可能未同步；必须把源/输出一致性作为首个门禁。
- Editor 插件与新克隆 CLI 必须版本匹配；不以升级插件作为静默修复。
- FairyGUI Editor 打开时不能执行写同步；发布结束后需要关闭 Editor，才能执行 `FromEditor`。
- `.NET` 构建不能证明 Unity 编译和运行行为；最终证据必须来自 Unity Agent Bridge。
