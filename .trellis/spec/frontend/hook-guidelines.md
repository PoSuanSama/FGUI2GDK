# Unity Lifecycle And Async Ownership

Every setup action needs cleanup in the matching lifecycle:

- Subscribe in `OnEnter`/`OnOpen`; unsubscribe in `OnLeave`/`OnClose`.
- Allocate or show owned resources/entities when the owner becomes active; release, hide, or recycle them on close/destroy.
- Cancel outstanding `UniTask` work when its owner is hidden or destroyed.

`Unity/Assets/Scripts/Game/Hot/Code/Procedure/ProcedureMenu.cs` pairs the UI-open event subscription with `OnLeave` cleanup. `Unity/Assets/Scripts/Game/Container/EntityContainer.cs` tracks entity IDs, unsubscribes events in `Clear`, and cancels its `CancellationTokenSource` when hiding all entities.

Use `UniTask` for Unity/ET asynchronous work. Await work when the caller owns its result. Use `.Forget()` only where the invoked method owns and reports failure; never let an unobserved operation outlive its UI, entity, scene, or fiber owner.

Test repeated open/close, show/hide, scene transitions, cancellation after `await`, pool reuse, and shutdown paths. Do not rely on a single successful first-open path.

## Scenario: Prepared FairyGUI Form Ownership

### 1. Scope / Trigger

Apply this contract when a GF `UIForm` owns a FairyGUI package, `GComponent`, presenter, or UIGroup adapter.
The integration crosses GF lifecycle/resource ownership, FairyGUI's logical display tree, and Unity's physical
Transform tree.

### 2. Signatures

- `FairyUIFormService.OpenFairyUIFormAsync(int uiId, object userData, CancellationToken ownerToken)` prepares
  all fallible state before entering GF's synchronous open boundary.
- `FairyUIRootService.GetOrCreateGroup(IUIGroup uiGroup)` returns one container per GF UIGroup.
- `FairyUIGroupContainer.AddForm/RemoveForm` attach and detach one unique `GComponent` instance.
- `FairyPackageLease.Dispose()` releases package states in reverse dependency order and is idempotent.

### 3. Contracts

- The service keeps a local pending-view owner until `FairyUIFormLogic` adopts the prepared state. Every
  exception before transfer, including presenter factory and presenter-ready failures, disposes it in `finally`.
- GF can call `CreateUIForm` synchronously before `OpenUIForm` returns when its instance pool is hit, while a
  newly loaded asset calls the helper later. Correlate the pooled path with a scoped synchronous-open state and
  the new-instance path with the returned GF serial ID. A descriptor/userData FIFO is not a valid correlation
  key because concurrent multi-instance opens may use the same descriptor and the same or null userData.
- Pass the caller's original `userData` unchanged into GF. Internal prepared-state identity must travel through
  the correlation registry rather than a wrapper visible to GF events or presenters.
- The two hierarchies intentionally differ:

  | Hierarchy | Required parent chain |
  | --- | --- |
  | FairyGUI logical display tree | `GRoot.container -> Container(UIGroup GameObject) -> MainView.displayObject` |
  | Unity Transform tree | `UI Group - <name> -> MainView` |
  | GF pooled host | `UI Group - <name> -> hidden FairyDemoForm host` |

- `Container(GameObject)` marks the external GameObject as `UserGameObject`; FairyGUI does not reparent that
  Transform. The adapter must synchronize the UIGroup world position, rotation, lossy scale, layer, and logical
  size with `GRoot`, while keeping `MainView` a direct Unity child of `UI Group - <name>`.
- Each form follows root size through `RelationType.Size`; remove the relation before normal detach/dispose.
- `UniTaskCompletionSource.TrySetResult/TrySetCanceled` may run continuations synchronously. State cleanup must
  remain valid if a continuation releases the final lease before the producer resumes. Capture cancellation
  tokens before `Cancel()`, and make cancel/dispose paths idempotent.
- During Editor shutdown, FairyGUI may dispose Stage display objects before GF calls `OnPause/OnClose`. Treat an
  already-disposed display object or container as already detached, but still close the presenter, dispose the
  GObject, release package leases, clear pooled state, and restore the UIGroup Transform when it still exists.

### 4. Validation & Error Matrix

- Missing descriptor/package/component/binding/presenter -> fail the open; no GF form, view, or lease remains.
- Owner cancellation before or after an await -> throw cancellation and return diagnostics to baseline.
- View is not a direct Unity child of its GF UIGroup -> hierarchy validation fails.
- UIGroup world matrix/layer differs from GRoot while a form is open -> rendering validation fails.
- Display tree is already disposed during shutdown -> skip visibility/reparent operations; cleanup must not log.
- Final lease released from a completion continuation -> unload exactly once; no unobserved exception.

### 5. Good / Base / Bad Cases

- Good: open, interact, close, recycle, and release all diagnostics to baseline.
- Base: multiple forms/groups preserve GF depth and use one Stage/GRoot.
- Bad: adding `Fairy UI Group - <name>` or placing `MainView` physically under `GRoot` hides framework ownership.
- Bad: assuming a successful Agent method means `.Forget()` work produced no later Console Error.

### 6. Tests Required

- Compile in Unity Editor and query Error logs after async work has had a frame to settle.
- Assert logical parent, physical parent, world matrix, layer, renderer material, and StageCamera frustum.
- Exercise button input and 16:9, 19.5:9, and 4:3 GameView sizes.
- Run 100 open/close cycles with owner cancellation and verify GF, GRoot, UIPanel, StageCamera, package, and pooled
  state return to baseline.
- Reverse-consume two prepared states with the same descriptor and null userData by GF serial ID, and exercise
  the synchronous pooled-instance handoff with reference-identical userData.
- Include real GF cover/pause, reveal/resume, and refocus transitions in the repeated lifecycle probe; assert both
  presenter dispatch counts and FairyGUI visibility/touchability.
- Stop PlayMode while the form is still open, then assert shutdown produced no Error logs.

### 7. Wrong vs Correct

```csharp
// Wrong: changes only FairyGUI's logical parent and assumes Unity Transform inheritance.
GRoot.inst.container.AddChild(new Container(uiGroupGameObject));

// Correct: retain the logical link and explicitly synchronize the external UIGroup Transform.
Container group = new Container(uiGroupGameObject);
GRoot.inst.container.AddChild(group);
SynchronizeUIGroupWorldTransformWithGRoot();
```
