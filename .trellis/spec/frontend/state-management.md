# Unity State Management

Use the state owner already established by the subsystem:

- Game flow belongs to UGF Procedures and FSM data. `ProcedureMenu.cs` changes state through `ChangeState<ProcedureChangeScene>` and stores transition data on the procedure owner.
- GameHot shared components are resolved during `HotEntry.InitComponents`; `Start`, `Update`, and `OnDestroy` initialize, tick, and shut down `HotComponentEntry`.
- ET state belongs to Entities/components in Model/ModelView; behavior mutates it through generated Entity Systems in Hotfix/HotfixView.
- Resource/entity lifetime groups belong in containers such as `EntityContainer` and `ResourceContainer`.

Pass transient open/show data through the framework `userData` boundary and validate/cast it at entry. Keep persistent configuration in Luban tables, not ad hoc static fields.

Avoid new global singletons, duplicated state between GameHot and ET, or static references whose shutdown/domain-reload lifecycle is unclear. When state crosses client/server or serialization boundaries, define compatibility and migration behavior before changing its shape.
