# Unity Components, UI, And Entities

## GameHot

GameHot UI logic derives from the local base (`StarForceUIForm` or another established form base) and implements UGF lifecycle methods. `Unity/Assets/Scripts/Game/Hot/Code/UI/StarForceUIForm.cs` initializes shared view state in `OnInit` and resets presentation in `OnOpen`/`OnResume`.

Entity logic and data remain separate under `Entity/EntityLogic/` and `Entity/EntityData/`. Open/show forms and entities through `GameEntry` using generated IDs, never hard-coded asset paths or numeric IDs.

## ET Client/View

State components live in ModelView and declare ownership/lifecycles, for example:

```csharp
[ComponentOf]
public class UIComponent : Entity, IAwake, IDestroy { }
```

Behavior lives in a matching HotfixView `static partial` system with `[EntitySystemOf]`. UGF bridges use `[UGFUIFormSystem]` or `[UGFEntitySystem]`, as shown by `UIFormLoginComponentSystem.cs` and `UGFEntityTestSystem.cs`.

## Avoid

- Putting mutable behavior into ET component data classes.
- Referencing `UnityEditor` from runtime assemblies.
- Bypassing CodeBind/generated view bindings with fragile hierarchy searches.
- Editing generated `*Id.cs` or `*.Bind.cs` files directly.
