# Unity Lifecycle And Async Ownership

Every setup action needs cleanup in the matching lifecycle:

- Subscribe in `OnEnter`/`OnOpen`; unsubscribe in `OnLeave`/`OnClose`.
- Allocate or show owned resources/entities when the owner becomes active; release, hide, or recycle them on close/destroy.
- Cancel outstanding `UniTask` work when its owner is hidden or destroyed.

`Unity/Assets/Scripts/Game/Hot/Code/Procedure/ProcedureMenu.cs` pairs the UI-open event subscription with `OnLeave` cleanup. `Unity/Assets/Scripts/Game/Container/EntityContainer.cs` tracks entity IDs, unsubscribes events in `Clear`, and cancels its `CancellationTokenSource` when hiding all entities.

Use `UniTask` for Unity/ET asynchronous work. Await work when the caller owns its result. Use `.Forget()` only where the invoked method owns and reports failure; never let an unobserved operation outlive its UI, entity, scene, or fiber owner.

Test repeated open/close, show/hide, scene transitions, cancellation after `await`, pool reuse, and shutdown paths. Do not rely on a single successful first-open path.
