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
- `FairyUIForm.AttachOwnerCancellation(CancellationToken ownerToken, Action<int> closeBySerialId)` transfers the
  successful open's cancellation registration to the form and binds its callback to that open's GF serial ID.
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
- After GF reports a successful open, `ownerToken` continues to own exactly that serial ID. Register against the
  captured serial, marshal a background-thread cancellation to Unity's PlayerLoop, and transfer the registration
  to `FairyUIForm`. While opening, check both the linked lifecycle token and the original `ownerToken` at each
  post-await boundary and before returning: PlayerLoop marshaling can leave the linked token uncanceled briefly
  after a background thread cancels the owner. `OnClose`/`OnRecycle`/failed return paths dispose the registration
  before a pooled host can represent a newer serial. Never close by asset name or by a captured pooled-form
  reference because both can identify another open.
- During Editor shutdown, FairyGUI may dispose Stage display objects before GF calls `OnPause/OnClose`. Treat an
  already-disposed display object or container as already detached, but still close the presenter, dispose the
  GObject, release package leases, clear pooled state, and restore the UIGroup Transform when it still exists.

### 4. Validation & Error Matrix

- Missing descriptor/package/component/binding/presenter -> fail the open; no GF form, view, or lease remains.
- Owner cancellation before or after an await -> throw cancellation and return diagnostics to baseline.
- Already-canceled token during registration -> close the just-opened serial, dispose the local registration, and
  throw cancellation; a stale token canceled after pool reuse -> old serial is a no-op and the new serial remains.
- View is not a direct Unity child of its GF UIGroup -> hierarchy validation fails.
- UIGroup world matrix/layer differs from GRoot while a form is open -> rendering validation fails.
- Display tree is already disposed during shutdown -> skip visibility/reparent operations; cleanup must not log.
- Final lease released from a completion continuation -> unload exactly once; no unobserved exception.

### 5. Good / Base / Bad Cases

- Good: open, interact, close, recycle, and release all diagnostics to baseline.
- Base: multiple forms/groups preserve GF depth and use one Stage/GRoot.
- Good: three forms with the same asset name retain independent owner tokens and serial IDs.
- Bad: adding `Fairy UI Group - <name>` or placing `MainView` physically under `GRoot` hides framework ownership.
- Bad: an old owner callback closes a pooled `FairyUIForm` instance after it has adopted a new serial ID.
- Bad: assuming a successful Agent method means `.Forget()` work produced no later Console Error.

### 6. Tests Required

- Compile in Unity Editor and query Error logs after async work has had a frame to settle.
- Assert logical parent, physical parent, world matrix, layer, renderer material, and StageCamera frustum.
- Exercise button input and 16:9, 19.5:9, and 4:3 GameView sizes.
- Run 100 open/close cycles with owner cancellation and verify GF, GRoot, UIPanel, StageCamera, package, and pooled
  state return to baseline.
- Reuse the same pooled form, cancel the old owner, and assert the current serial remains open. Open three
  same-asset multi-instances, cancel each owner in turn, and assert only its captured serial closes.
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

// Wrong: the pooled object may represent another open when cancellation runs.
ownerToken.Register(() => CloseUIForm(form.SerialId));

// Correct: capture this open's immutable GF identity and transfer registration ownership.
int ownedSerialId = form.SerialId;
form.AttachOwnerCancellation(ownerToken, serialId => CloseUIForm(serialId));
```

## Scenario: ET-owned FairyGUI Forms

### 1. Scope / Trigger

Apply this contract when an ET `Scene`/`Fiber`/`Entity` owns a FairyGUI form opened through the shared GF host.
Calling `FairyUIFormService` from ET business code without recording ownership on an Entity is not an ET integration.

### 2. Signatures

- ModelView `UIComponent` stores `PendingFairyUIOpens`, `OwnedFairyUIForms`, and the next operation ID; all are
  runtime-only (`BsonIgnore` + `MemoryPackIgnore`).
- HotfixView `UIComponentSystem.OpenFairyUIFormAsync(UIComponent, int, object)` returns the opened form and transfers
  its per-open CTS from pending-operation ownership to GF serial ownership.
- Editor-only `OpenFairyUIFormAfterViewReadyForTestingAsync(UIComponent, int, object, Action<FairyUIFormComponent>)`
  injects one callback into one open after business `OnViewReady` dispatch and before GF requests a serial.
- `CloseFairyUIForm(UIComponent, int)` and `RefocusFairyUIForm(UIComponent, int, object)` act only on serial IDs owned
  by that component.
- `UIComponentFairyUIBridge` is the ModelView-callable delegate contract injected by HotfixView `Awake`; a missing
  injection throws a stable initialization error.

### 3. Contracts

- Create `UIComponent` before the first open. Across every `await`, hold it as `EntityRef<UIComponent>` and reject a
  continuation whose original Entity generation was disposed or pooled.
- Give each open its own CTS. Before the form exists it belongs to `PendingFairyUIOpens`; after success the same CTS
  belongs to `OwnedFairyUIForms[serialId]` and is also the shared host's owner token.
- Registered factories create non-pooled, per-open children with `owner.AddChild<T>()`. Do not switch them to
  `owner.AddChild<T>(true)` while `FairyUIPresenterAdapter` holds a raw component reference; pooling requires an
  `EntityRef` plus a generation check on every delayed callback before a component can be reused safely.
- ET `Entity.Dispose()` first marks the owner disposed, recursively destroys its children, and only then dispatches
  the owner's `Destroy` system. Every concrete `FairyUIFormComponent` type must therefore inherit `IDestroy` and
  declare its own exact-type `[EntitySystem] Destroy`; `TypeSystems` does not fall back to a base-type system.
- A form-component `Destroy` directly runs that form's `FairyUIFormOnClose` cleanup, clears component references,
  calls `FairyUIFormContext.CancelLifetime()` first, then closes its GF serial when one has already been assigned.
  It must also run cleanup after `OnViewReady` when
  GF has not reached `OnInit` and no serial exists yet. The later `UIComponent.Destroy` cancels pending opens and
  closes/cancels captured owned serials as the CTS/serial fallback. Never close by asset name.
- A deterministic timing probe must keep its hook on the per-open Presenter Adapter instance, compile the public
  test entry and callback storage only under `UNITY_EDITOR`, and invoke it synchronously on the Unity main thread.
  Do not use a static hook: concurrent opens and domain reload would leak test state into unrelated transactions.
- ModelView may retain presenter state, but Hotfix/HotfixView assemblies reject every property and non-const field via
  `ET0004`. Do not move a stateful `IFairyUIPresenter` class wholesale into HotfixView or suppress the analyzer.
  A future full Presenter split must introduce an explicit state/logic adapter or Entity/System dispatcher.

### 4. Validation & Error Matrix

- Owner disposed before/during open -> cancellation; no late form or package diagnostic remains.
- Concrete form type missing an exact `Destroy` system -> reject the integration; owner disposal can recycle the
  component before GF closes the host and can retain subscriptions installed by `OnViewReady`.
- Duplicate close of one serial -> first call closes, later calls are no-ops; sibling same-asset serials remain.
- Missing bridge injection -> stable `InvalidOperationException`, not a null reference.
- Stateful Presenter moved to HotfixView -> `ET0004`; restore the state boundary or implement an approved adapter.
- Static or Player-visible timing hook -> reject it; use the Editor-only per-open adapter callback and assert it
  fires exactly once before any serial or owned-form entry exists.

### 5. Good / Base / Bad Cases

- Good: three same-asset detail forms are tracked by three serial entries and can be closed independently.
- Good: an Editor timing callback belongs to one adapter/open and is absent from Player-facing entry points.
- Base: owner Destroy closes Demo/Inventory/Detail/Overlay and a replacement owner starts from a clean baseline.
- Bad: Entry opens through the global service and adds `UIComponent` afterwards.
- Bad: ModelView code references HotfixView extension methods directly; the assembly dependency is one-way.
- Bad: a global static callback pauses whichever open happens next and survives beyond the intended regression.

## Current FairyGUI transaction invariant

The current native host prepares a FairyUIFormPendingState before entering GF OpenUIForm. GF may invoke
OnInit synchronously from a pooled instance or later from an async resource callback, and GF may report an
OnOpen failure without calling the FairyGUI cleanup path. Therefore the pending registry must correlate both
the returned serial and the synchronous handoff, retain adopted state until the form closes, and route every GF
failure through one idempotent cleanup path. A successful C# build does not prove this behavior; run the focused
Unity lifecycle smoke and inspect Error logs after the async work settles.

### 6. Tests Required

- Compile with `UNITY_ET` in Unity Editor; a GameHot-only compile is not evidence.
- Exercise pending-open Destroy, three same-asset serials, idempotent close, owner Destroy, replacement owner, and
  PlayMode stop while a replacement form remains open.
- For every registered form-component factory, assert an exact concrete `IDestroySystem` exists. Include disposal
  after `OnViewReady` but before GF assigns a serial, and assert business `OnClose` cleanup runs exactly once.
- Assert GF forms, GRoot objects, presenters, package leases, pending operations, and owned serial collections return
  to the expected baseline; query Error logs after shutdown.

### 7. Wrong vs Correct

```csharp
// Wrong: the ET Entity owns neither the async operation nor the resulting serial.
await FairyUIFormService.OpenFairyUIFormAsync(uiId, userData);
root.AddComponent<UIComponent>();

// Correct: Entity exists first; HotfixView System owns both phases by exact serial.
UIComponent owner = root.AddComponent<UIComponent>();
EntityRef<UIComponent> ownerRef = owner;
FairyUIForm form = await owner.OpenFairyUIFormAsync(uiId, userData);
owner = ownerRef;
if (owner == null) throw new OperationCanceledException();

// Wrong: a base marker alone cannot be found for a concrete runtime type.
public class NewFormComponent : FairyUIFormComponent { }

// Correct: every registered concrete form has an exact generated DestroySystem<T>.
[EntitySystemOf(typeof(NewFormComponent))]
public static partial class NewFormComponentSystem
{
    [EntitySystem]
    private static void Destroy(this NewFormComponent self)
    {
        UIComponentSystem.CloseFairyUIFormBeforeComponentDestroy(self, FairyUIFormOnClose);
    }
}
```
