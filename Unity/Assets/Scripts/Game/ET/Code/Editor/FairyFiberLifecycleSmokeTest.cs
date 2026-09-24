using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using AgentBridge;
using Cysharp.Threading.Tasks;
using ET.Client;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;
using GameFramework;
using GameFramework.UI;
using UnityEditor;

namespace ET
{
    /// <summary>
    /// ET 真实 Fiber Remove 生命周期门禁:
    /// 在独立 fiber 的 root 上创建 UIComponent owner 并打开 FairyGUI 窗体,
    /// FiberManager.Remove 后断言窗体/Widget/包租约全部回到基线。
    ///
    /// 使用 SceneType.NetClient 是因为 EventSystem.Invoke 对无 FiberInit 处理器的
    /// SceneType 会抛异常,而 NetClient 有现成的 FiberInit_NetClient 处理器。
    /// FiberManager.Get 为 internal,测试用反射获取 fiber 以拿到其 root Scene
    /// (与 Main fiber 的 EntryEvent 流程同构:root.AddComponent&lt;UIComponent&gt; + owner 打开)。
    /// </summary>
    public static class FairyFiberLifecycleSmokeTest
    {
        private const string DemoAsset = "Assets/Res/UI/FairyGUI/FairyDemoForm.json";
        private const int OwnerLifecyclePressureRepeatCount = 100;

        [AgentCallable("ET owner 在 OnViewReady 完成且 GF serial 尚未分配时销毁：断言业务 OnClose 恰好一次并回到资源基线。", 120)]
        public static async UniTask RunFairyPreSerialOwnerDestroySmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI pre-serial owner destroy smoke test requires PlayMode.");
            }

            await ET.Client.FairyGUIBootstrap.InitializeAsync();

            FairyUIManager uiManager = FairyUIManager.Instance;
            FairyUIForm mainDemo = uiManager.GetUIForm(DemoAsset);
            UIComponent mainOwner = null;
            int mainDemoSerial = 0;
            if (mainDemo?.Presenter is FairyUIPresenterAdapter mainAdapter && mainAdapter.Component != null)
            {
                mainOwner = mainAdapter.Component.Parent as UIComponent;
            }

            int originalLoadedForms = uiManager.GetAllLoadedUIForms().Length;
            int originalLoadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            int originalRootChildren = GRoot.inst?.numChildren ?? 0;
            bool originalPackageRegistered = UIPackage.GetByName("Package1") != null;
            IReadOnlyList<FairyPackageDiagnostic> originalPackageDiagnostics =
                FairyPackageManager.GetDiagnostics();
            bool restoreMainDemo = mainOwner != null &&
                mainDemo != null &&
                mainOwner.OwnsFairyUIForm(mainDemo.SerialId);
            if (mainDemo != null && !restoreMainDemo)
            {
                throw new InvalidOperationException(
                    "The existing FairyGUI demo is not owned by a live ET UIComponent.");
            }

            if (restoreMainDemo)
            {
                mainDemoSerial = mainDemo.SerialId;
                if (!mainOwner.CloseFairyUIForm(mainDemoSerial))
                {
                    throw new InvalidOperationException(
                        "Failed to close the main owner demo before the pre-serial timing test.");
                }

                await WaitForUIFormClosedAsync(uiManager, mainDemoSerial);
            }

            int baselineLoadedForms = uiManager.GetAllLoadedUIForms().Length;
            int baselineLoadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            int baselineRootChildren = GRoot.inst?.numChildren ?? 0;
            UIPackage baselinePackage = UIPackage.GetByName("Package1");
            bool baselinePackageRegistered = baselinePackage != null;
            IReadOnlyList<FairyPackageDiagnostic> baselinePackageDiagnostics =
                FairyPackageManager.GetDiagnostics();
            int fiberId = 0;
            try
            {
                fiberId = await FiberManager.Instance.Create(
                    SchedulerType.Main,
                    0,
                    SceneType.NetClient,
                    "FairyPreSerialOwnerDestroyTest");
                Scene root = GetFiber(FiberManager.Instance, fiberId).Root;
                int rootComponentBaseline = root.ComponentsCount();
                UIComponent owner = root.AddComponent<UIComponent>();

                FairyDemoFormComponent component = null;
                FairyUIFormContext context = null;
                UIMainView view = null;
                FairyInventoryItemWidget widget = null;
                CancellationToken lifetimeToken = default;
                int cancellationCallbackCount = 0;
                int onCloseCountWhenLifetimeCanceled = -1;
                int cancellationThreadId = -1;
                int viewReadyHookCount = 0;
                bool canceled = false;
                try
                {
                    await owner.OpenFairyUIFormAfterViewReadyForTestingAsync(
                        UGFUIFormId.FairyDemoForm,
                        owner,
                        candidate =>
                        {
                            int hookThreadId = Thread.CurrentThread.ManagedThreadId;
                            if (!PlayerLoopHelper.IsMainThread)
                            {
                                throw new InvalidOperationException(
                                    "The pre-serial timing hook did not run on the Unity main thread.");
                            }

                            ++viewReadyHookCount;
                            component = candidate as FairyDemoFormComponent
                                ?? throw new InvalidOperationException(
                                    "The pre-serial timing hook did not receive a FairyDemoFormComponent.");
                            context = component.Context;
                            view = component.View as UIMainView;
                            widget = component.ItemWidget;
                            if (context == null || view == null || widget == null)
                            {
                                throw new InvalidOperationException(
                                    "OnViewReady did not finish creating the ET view state before the timing hook.");
                            }

                            lifetimeToken = context.LifetimeToken;
                            if (lifetimeToken.IsCancellationRequested ||
                                context.Form != null || context.SerialId != 0 || component.SerialId != 0)
                            {
                                throw new InvalidOperationException(
                                    "The timing hook ran after GF assigned a FairyGUI serial.");
                            }

                            if (owner.GetPendingFairyUIOpenCount() != 1 ||
                                owner.GetOwnedFairyUIFormCount() != 0 ||
                                owner.ChildrenCount() != 1 ||
                                uiManager.GetAllLoadedUIForms().Length != baselineLoadedForms ||
                                uiManager.GetAllLoadingUIFormSerialIds().Length != baselineLoadingForms ||
                                UIPackage.GetByName("Package1") == null)
                            {
                                throw new InvalidOperationException(
                                    "The timing hook did not stop at the expected pending-open ownership boundary.");
                            }

                            if (component.OnCloseCount != 0 ||
                                component.OnOpenCount != 0 ||
                                component.OpenInventoryClick == null ||
                                component.RefreshClick == null ||
                                view.OpenInventoryButton.onClick.isEmpty ||
                                view.RefreshButton.onClick.isEmpty ||
                                !widget.Opened ||
                                widget.View == null)
                            {
                                throw new InvalidOperationException(
                                    "OnViewReady subscriptions or Widget state were incomplete before owner destroy.");
                            }

                            using CancellationTokenRegistration cancellationRegistration =
                                lifetimeToken.Register(() =>
                                {
                                    ++cancellationCallbackCount;
                                    onCloseCountWhenLifetimeCanceled = component.OnCloseCount;
                                    cancellationThreadId = Thread.CurrentThread.ManagedThreadId;
                                });
                            owner.Dispose();

                            if (cancellationCallbackCount != 1 ||
                                onCloseCountWhenLifetimeCanceled != 0 ||
                                component.OnCloseCount != 1 ||
                                component.OnOpenCount != 0 ||
                                cancellationThreadId != hookThreadId)
                            {
                                throw new InvalidOperationException(
                                    $"Expected lifetime cancellation before exactly one pre-serial business OnClose, " +
                                    $"actual callbacks/cancel-close/close/open/thread={cancellationCallbackCount}/" +
                                    $"{onCloseCountWhenLifetimeCanceled}/{component.OnCloseCount}/" +
                                    $"{component.OnOpenCount}/{cancellationThreadId}, hook thread={hookThreadId}.");
                            }

                            if (!lifetimeToken.IsCancellationRequested ||
                                component.OpenInventoryClick != null ||
                                component.RefreshClick != null ||
                                !view.OpenInventoryButton.onClick.isEmpty ||
                                !view.RefreshButton.onClick.isEmpty ||
                                owner.ChildrenCount() != 0)
                            {
                                throw new InvalidOperationException(
                                    "Owner destroy did not cancel lifetime and remove OnViewReady subscriptions synchronously.");
                            }
                        });

                    throw new InvalidOperationException(
                        "The pre-serial FairyGUI open unexpectedly completed after owner destroy.");
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                await WaitForFairyPackageDiagnosticsAsync(
                    baselinePackageDiagnostics,
                    baselinePackageRegistered);
                await UniTask.Yield(PlayerLoopTiming.Update);

                if (!ReferenceEquals(UIPackage.GetByName("Package1"), baselinePackage))
                {
                    throw new InvalidOperationException(
                        "The pre-serial owner destroy path did not restore the exact Package1 registration baseline.");
                }

                if (!canceled || viewReadyHookCount != 1 || component == null || context == null)
                {
                    throw new InvalidOperationException(
                        "The pre-serial owner destroy path did not cancel through one deterministic timing hook.");
                }

                if (!component.IsDisposed ||
                    component.OnCloseCount != 1 ||
                    component.OnOpenCount != 0 ||
                    component.Context != null ||
                    component.View != null ||
                    component.FairyForm != null ||
                    context.IsAlive ||
                    context.Form != null ||
                    context.SerialId != 0 ||
                    !view.isDisposed ||
                    widget.Opened ||
                    widget.View != null)
                {
                    throw new InvalidOperationException(
                        "The canceled pre-serial open did not release its Component, Context, view, or Widget exactly once.");
                }

                if (!owner.IsDisposed ||
                    owner.GetPendingFairyUIOpenCount() != 0 ||
                    owner.GetOwnedFairyUIFormCount() != 0 ||
                    root.ComponentsCount() != rootComponentBaseline ||
                    uiManager.GetAllLoadedUIForms().Length != baselineLoadedForms ||
                    uiManager.GetAllLoadingUIFormSerialIds().Length != baselineLoadingForms ||
                    (GRoot.inst?.numChildren ?? 0) != baselineRootChildren)
                {
                    throw new InvalidOperationException(
                        "The pre-serial owner destroy path did not return ET, GF, or GRoot state to baseline.");
                }
            }
            finally
            {
                if (fiberId != 0)
                {
                    await FiberManager.Instance.Remove(fiberId);
                }

                if (restoreMainDemo && mainOwner != null && !mainOwner.IsDisposed)
                {
                    FairyUIForm restored = await mainOwner.OpenFairyUIFormAsync(
                        UGFUIFormId.FairyDemoForm,
                        mainOwner);
                    if (!mainOwner.OwnsFairyUIForm(restored.SerialId) ||
                        !uiManager.HasUIForm(restored.SerialId))
                    {
                        throw new InvalidOperationException(
                            "Failed to restore the main owner demo after the pre-serial timing test.");
                    }
                }

                await WaitForFairyPackageDiagnosticsAsync(
                    originalPackageDiagnostics,
                    originalPackageRegistered,
                    compareGeneration: false);
                if (uiManager.GetAllLoadedUIForms().Length != originalLoadedForms ||
                    uiManager.GetAllLoadingUIFormSerialIds().Length != originalLoadingForms ||
                    (GRoot.inst?.numChildren ?? 0) != originalRootChildren)
                {
                    throw new InvalidOperationException(
                        "The pre-serial timing test did not restore the original FairyGUI runtime baseline.");
                }
            }
        }

        [AgentCallable("ET FairyGUI owner 取消/Destroy 100 次压力矩阵：覆盖 pending open、owner Dispose、Fiber Remove 与完整资源基线。", 600)]
        public static async UniTask RunFairyOwnerLifecyclePressureSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI owner lifecycle pressure smoke test requires PlayMode.");
            }

            await ET.Client.FairyGUIBootstrap.InitializeAsync();

            FairyUIManager uiManager = FairyUIManager.Instance;
            FairyUIForm mainDemo = uiManager.GetUIForm(DemoAsset);
            UIComponent mainOwner = null;
            if (mainDemo?.Presenter is FairyUIPresenterAdapter mainAdapter && mainAdapter.Component != null)
            {
                mainOwner = mainAdapter.Component.Parent as UIComponent;
            }

            bool restoreMainDemo = mainDemo != null &&
                mainOwner != null &&
                !mainOwner.IsDisposed &&
                mainOwner.OwnsFairyUIForm(mainDemo.SerialId);
            if (mainDemo != null && !restoreMainDemo)
            {
                throw new InvalidOperationException(
                    "The existing FairyGUI demo is not owned by a live ET UIComponent.");
            }

            FairyRuntimeBaseline originalBaseline = CaptureFairyRuntimeBaseline(uiManager);
            if (restoreMainDemo)
            {
                int serialId = mainDemo.SerialId;
                if (!mainOwner.CloseFairyUIForm(serialId))
                {
                    throw new InvalidOperationException(
                        "Failed to close the main owner demo before the ET owner pressure matrix.");
                }

                await WaitForUIFormClosedAsync(uiManager, serialId);
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            FairyRuntimeBaseline baseline = CaptureFairyRuntimeBaseline(uiManager);
            try
            {
                for (int iteration = 0; iteration < OwnerLifecyclePressureRepeatCount; iteration++)
                {
                    await RunPendingOwnerDestroyPressureIterationAsync(
                        uiManager,
                        baseline,
                        iteration);
                    await RunOpenedOwnerDestroyPressureIterationAsync(
                        uiManager,
                        baseline,
                        iteration,
                        removeFiber: iteration % 2 == 1);
                }
            }
            finally
            {
                if (restoreMainDemo && mainOwner != null && !mainOwner.IsDisposed)
                {
                    FairyUIForm restored = await mainOwner.OpenFairyUIFormAsync(
                        UGFUIFormId.FairyDemoForm,
                        mainOwner);
                    if (!mainOwner.OwnsFairyUIForm(restored.SerialId) ||
                        !uiManager.HasUIForm(restored.SerialId))
                    {
                        throw new InvalidOperationException(
                            "Failed to restore the main owner demo after the ET owner pressure matrix.");
                    }
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
                await WaitForFairyPackageDiagnosticsAsync(
                    originalBaseline.PackageDiagnostics,
                    originalBaseline.Package != null,
                    compareGeneration: false);
                AssertFairyRuntimeGlobalBaseline(
                    uiManager,
                    originalBaseline,
                    "pressure matrix finalization",
                    requireExactPackage: false,
                    compareGeneration: false);
            }
        }

        [AgentCallable("ET Fiber Remove 生命周期验证:独立 fiber 打开 FairyGUI 窗体后 Remove,断言窗体与 Widget 全清理。", 120)]
        public static async UniTask RunFairyFiberLifecycleSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("ET FairyGUI fiber lifecycle smoke test requires PlayMode.");
            }

            await ET.Client.FairyGUIBootstrap.InitializeAsync();

            FairyUIManager uiManager = FairyUIManager.Instance;

            // Demo 窗体是单实例,入口流程已为主 owner 打开;先记录并关闭,
            // 测试结束后重开,保证运行时回到基线。
            FairyUIForm mainDemo = uiManager.GetUIForm(DemoAsset);
            UIComponent mainOwner = null;
            int mainDemoSerial = 0;
            if (mainDemo?.Presenter is FairyUIPresenterAdapter mainAdapter && mainAdapter.Component != null)
            {
                mainOwner = mainAdapter.Component.Parent as UIComponent;
            }

            if (mainOwner != null && mainOwner.OwnsFairyUIForm(mainDemo.SerialId))
            {
                mainDemoSerial = mainDemo.SerialId;
                mainOwner.CloseFairyUIForm(mainDemoSerial);
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (uiManager.HasUIForm(mainDemoSerial))
                {
                    throw new InvalidOperationException("Failed to close the main owner demo before the fiber test.");
                }
            }

            int baselineForms = uiManager.GetAllLoadedUIForms().Length;

            int fiberId = await FiberManager.Instance.Create(
                SchedulerType.Main,
                0,
                SceneType.NetClient,
                "FairyFiberTest");
            Scene root = GetFiber(FiberManager.Instance, fiberId).Root;

            FairyUIForm demoForm = null;
            Game.FairyInventoryItemWidget demoWidget = null;
            try
            {
                UIComponent owner = root.AddComponent<UIComponent>();
                demoForm = await owner.OpenFairyUIFormAsync(
                    UGFUIFormId.FairyDemoForm,
                    owner);
                if (!owner.OwnsFairyUIForm(demoForm.SerialId) ||
                    !uiManager.HasUIForm(demoForm.SerialId))
                {
                    throw new InvalidOperationException(
                        "Fiber UI owner did not open and own the FairyGUI demo serial.");
                }

                if (uiManager.GetAllLoadedUIForms().Length != baselineForms + 1)
                {
                    throw new InvalidOperationException(
                        "Fiber demo open did not add exactly one loaded UI form.");
                }

                FairyDemoFormComponent demoComponent =
                    demoForm.Presenter is FairyUIPresenterAdapter adapter
                        ? adapter.Component as FairyDemoFormComponent
                        : null;
                demoWidget = demoComponent?.ItemWidget;
                if (demoWidget == null || demoWidget.Opened == false)
                {
                    throw new InvalidOperationException(
                        "Fiber demo widget was not created through the host context.");
                }
            }
            catch
            {
                await FiberManager.Instance.Remove(fiberId);
                throw;
            }

            await FiberManager.Instance.Remove(fiberId);
            await UniTask.Yield(PlayerLoopTiming.Update);

            if (uiManager.HasUIForm(demoForm.SerialId))
            {
                throw new InvalidOperationException(
                    "Fiber Remove left the owned FairyGUI serial open.");
            }

            if (demoWidget.Opened || demoWidget.View != null)
            {
                throw new InvalidOperationException(
                    "Fiber Remove did not recycle the demo widget through the host context.");
            }

            if (uiManager.GetAllLoadedUIForms().Length != baselineForms)
            {
                throw new InvalidOperationException(
                    "Fiber Remove did not return loaded UI forms to baseline.");
            }

            // 恢复主 owner 的 Demo 窗体基线。
            if (mainOwner != null && mainDemoSerial != 0)
            {
                FairyUIForm restored = await mainOwner.OpenFairyUIFormAsync(
                    UGFUIFormId.FairyDemoForm,
                    mainOwner);
                if (!mainOwner.OwnsFairyUIForm(restored.SerialId) ||
                    !uiManager.HasUIForm(restored.SerialId))
                {
                    throw new InvalidOperationException(
                        "Failed to restore the main owner demo after the fiber test.");
                }
            }
        }

        [AgentCallable("ET FairyGUI Runner shutdown 后复位静态注册并重复初始化，再由新 owner 打开关闭窗体。", 180)]
        public static async UniTask RunFairyBootstrapShutdownReinitializeSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI bootstrap shutdown smoke test requires PlayMode.");
            }

            await FairyGUIBootstrap.InitializeAsync();

            FairyUIManager uiManager = FairyUIManager.Instance;
            IUIManager frameworkUIManager = GameFrameworkEntry.GetModule<IUIManager>();
            if (frameworkUIManager == null)
            {
                throw new InvalidOperationException("GameFramework IUIManager is unavailable.");
            }

            FairyUIForm mainDemo = uiManager.GetUIForm(DemoAsset);
            UIComponent mainOwner = mainDemo?.Presenter is FairyUIPresenterAdapter mainAdapter &&
                mainAdapter.Component != null
                    ? mainAdapter.Component.Parent as UIComponent
                    : null;
            bool restoreMainDemo = mainDemo != null &&
                mainOwner != null &&
                !mainOwner.IsDisposed &&
                mainOwner.OwnsFairyUIForm(mainDemo.SerialId);
            if (mainDemo != null && !restoreMainDemo)
            {
                throw new InvalidOperationException(
                    "The main ET FairyGUI demo is not owned by a live UIComponent.");
            }

            int originalDemoSerial = restoreMainDemo ? mainDemo.SerialId : 0;
            int originalGroupCount = frameworkUIManager.UIGroupCount;
            int fiberId = 0;
            FairyRuntimeBaseline baseline = default;
            try
            {
                if (restoreMainDemo)
                {
                    if (!mainOwner.CloseFairyUIForm(originalDemoSerial))
                    {
                        throw new InvalidOperationException(
                            "Failed to close the main demo before ET bootstrap shutdown.");
                    }

                    await WaitForUIFormClosedAsync(uiManager, originalDemoSerial);
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
                if (uiManager.GetAllLoadedUIForms().Length != 0 ||
                    uiManager.GetAllLoadingUIFormSerialIds().Length != 0)
                {
                    throw new InvalidOperationException(
                        "ET FairyGUI bootstrap shutdown test requires an otherwise idle UI manager.");
                }

                baseline = CaptureFairyRuntimeBaseline(uiManager);
                if (!FairyUIFormComponentRegistry.TryGet(
                        UGFUIFormId.FairyDemoForm,
                        out _) ||
                    !IsFairyBootstrapPreparePackageRegistered() ||
                    UIComponentFairyUIBridge.Open == null ||
                    UIComponentFairyUIBridge.Close == null ||
                    UIComponentFairyUIBridge.Refocus == null)
                {
                    throw new InvalidOperationException(
                        "ET FairyGUI bootstrap was not fully registered before shutdown.");
                }

                try
                {
                    uiManager.Shutdown();
                }
                finally
                {
                    FairyUIManager.NotifyETRuntimeShutdownCompleted();
                }

                if (FairyUIFormComponentRegistry.TryGet(UGFUIFormId.FairyDemoForm, out _) ||
                    IsFairyBootstrapPreparePackageRegistered() ||
                    UIComponentFairyUIBridge.Open != null ||
                    UIComponentFairyUIBridge.Close != null ||
                    UIComponentFairyUIBridge.Refocus != null ||
                    FairyUIManager.UIFormTableProvider != null)
                {
                    throw new InvalidOperationException(
                        "ET FairyGUI shutdown left bootstrap factories, delegates, or table provider registered.");
                }

                if (frameworkUIManager.GetAllLoadedUIForms().Length != 0 ||
                    frameworkUIManager.GetAllLoadingUIFormSerialIds().Length != 0)
                {
                    throw new InvalidOperationException(
                        "ET FairyGUI shutdown left a loaded or loading GF form.");
                }

                await WaitForFairyPackageDiagnosticsAsync(
                    Array.Empty<FairyPackageDiagnostic>(),
                    expectedPackageRegistered: false);
                if ((GRoot.inst?.numChildren ?? 0) != baseline.RootChildren)
                {
                    throw new InvalidOperationException(
                        "ET FairyGUI shutdown changed the root child baseline after the demo was closed.");
                }

                await FairyGUIBootstrap.InitializeAsync();
                await FairyGUIBootstrap.InitializeAsync();
                if (!FairyUIFormComponentRegistry.TryGet(UGFUIFormId.FairyDemoForm, out _) ||
                    !IsFairyBootstrapPreparePackageRegistered() ||
                    FairyUIManager.UIFormTableProvider == null ||
                    frameworkUIManager.UIGroupCount != originalGroupCount)
                {
                    throw new InvalidOperationException(
                        "Repeated ET FairyGUI bootstrap initialization did not restore one stable registration set.");
                }

                if (UIComponentFairyUIBridge.Open != null ||
                    UIComponentFairyUIBridge.Close != null ||
                    UIComponentFairyUIBridge.Refocus != null)
                {
                    throw new InvalidOperationException(
                        "ET FairyGUI bridge was rebound before a new owner initialized.");
                }

                fiberId = await FiberManager.Instance.Create(
                    SchedulerType.Main,
                    0,
                    SceneType.NetClient,
                    "FairyBootstrapShutdownReinitializeTest");
                Scene root = GetFiber(FiberManager.Instance, fiberId).Root;
                UIComponent owner = root.AddComponent<UIComponent>();
                if (UIComponentFairyUIBridge.Open == null ||
                    UIComponentFairyUIBridge.Close == null ||
                    UIComponentFairyUIBridge.Refocus == null)
                {
                    throw new InvalidOperationException(
                        "A new ET UIComponent did not bind the FairyGUI bridge after bootstrap reinitialization.");
                }

                FairyUIForm form = await owner.OpenFairyUIFormAsync(UGFUIFormId.FairyDemoForm, owner);
                int serialId = form.SerialId;
                if (!owner.OwnsFairyUIForm(serialId) ||
                    !uiManager.HasUIForm(serialId) ||
                    uiManager.GetAllLoadedUIForms().Length != baseline.LoadedForms + 1)
                {
                    throw new InvalidOperationException(
                        "A new ET owner could not open a form after bootstrap shutdown and reinitialization.");
                }

                if (!owner.CloseFairyUIForm(serialId))
                {
                    throw new InvalidOperationException(
                        "A new ET owner could not close its form after bootstrap reinitialization.");
                }

                await WaitForUIFormClosedAsync(uiManager, serialId);
                await FiberManager.Instance.Remove(fiberId);
                fiberId = 0;
                await WaitForFairyPackageDiagnosticsAsync(
                    baseline.PackageDiagnostics,
                    baseline.Package != null);
                AssertFairyRuntimeGlobalBaseline(
                    uiManager,
                    baseline,
                    "bootstrap shutdown/reinitialize smoke test",
                    requireExactPackage: true);
            }
            finally
            {
                if (fiberId != 0)
                {
                    await FiberManager.Instance.Remove(fiberId);
                }

                await FairyGUIBootstrap.InitializeAsync();
                if (restoreMainDemo && mainOwner != null && !mainOwner.IsDisposed &&
                    uiManager.GetUIForm(DemoAsset) == null)
                {
                    if (UIComponentFairyUIBridge.Open == null)
                    {
                        int bridgeFiberId = await FiberManager.Instance.Create(
                            SchedulerType.Main,
                            0,
                            SceneType.NetClient,
                            "FairyBootstrapBridgeRestore");
                        GetFiber(FiberManager.Instance, bridgeFiberId).Root.AddComponent<UIComponent>();
                        await FiberManager.Instance.Remove(bridgeFiberId);
                    }

                    FairyUIForm restored = await mainOwner.OpenFairyUIFormAsync(
                        UGFUIFormId.FairyDemoForm,
                        mainOwner);
                    if (!mainOwner.OwnsFairyUIForm(restored.SerialId) ||
                        !uiManager.HasUIForm(restored.SerialId))
                    {
                        throw new InvalidOperationException(
                            "Failed to restore the main ET demo after bootstrap shutdown testing.");
                    }
                }
            }
        }

        private static bool IsFairyBootstrapPreparePackageRegistered()
        {
            Action<FairyUIFormDescriptor> preparePackage = FairyUIPresenterRegistry.PreparePackage;
            return preparePackage != null &&
                preparePackage.Method.DeclaringType == typeof(FairyGUIBootstrap);
        }

        private static async UniTask RunPendingOwnerDestroyPressureIterationAsync(
            FairyUIManager uiManager,
            FairyRuntimeBaseline baseline,
            int iteration)
        {
            int fiberId = 0;
            Scene root = null;
            UIComponent owner = null;
            int rootComponentBaseline = 0;
            FairyDemoFormComponent component = null;
            FairyUIFormContext context = null;
            UIMainView view = null;
            FairyInventoryItemWidget widget = null;
            bool canceled = false;

            try
            {
                fiberId = await FiberManager.Instance.Create(
                    SchedulerType.Main,
                    0,
                    SceneType.NetClient,
                    $"FairyOwnerPendingPressure-{iteration}");
                root = GetFiber(FiberManager.Instance, fiberId).Root;
                rootComponentBaseline = root.ComponentsCount();
                owner = root.AddComponent<UIComponent>();
                if (owner.ChildrenCount() != 0 ||
                    owner.GetPendingFairyUIOpenCount() != 0 ||
                    owner.GetOwnedFairyUIFormCount() != 0)
                {
                    throw new InvalidOperationException(
                        $"Pending pressure iteration {iteration} started with a non-empty ET owner.");
                }

                try
                {
                    await owner.OpenFairyUIFormAfterViewReadyForTestingAsync(
                        UGFUIFormId.FairyDemoForm,
                        owner,
                        candidate =>
                        {
                            component = candidate as FairyDemoFormComponent
                                ?? throw new InvalidOperationException(
                                    $"Pending pressure iteration {iteration} did not create FairyDemoFormComponent.");
                            context = component.Context;
                            view = component.View as UIMainView;
                            widget = component.ItemWidget;
                            if (context == null || view == null || widget == null)
                            {
                                throw new InvalidOperationException(
                                    $"Pending pressure iteration {iteration} did not finish OnViewReady.");
                            }

                            if (context.Form != null ||
                                context.SerialId != 0 ||
                                component.SerialId != 0 ||
                                owner.GetPendingFairyUIOpenCount() != 1 ||
                                owner.GetOwnedFairyUIFormCount() != 0 ||
                                owner.ChildrenCount() != 1 ||
                                root.ComponentsCount() != rootComponentBaseline + 1 ||
                                uiManager.GetAllLoadedUIForms().Length != baseline.LoadedForms ||
                                uiManager.GetAllLoadingUIFormSerialIds().Length != baseline.LoadingForms ||
                                UIPackage.GetByName("Package1") == null)
                            {
                                throw new InvalidOperationException(
                                    $"Pending pressure iteration {iteration} did not stop at the pending ownership boundary.");
                            }

                            CancellationToken lifetimeToken = context.LifetimeToken;
                            owner.Dispose();

                            if (!lifetimeToken.IsCancellationRequested ||
                                !owner.IsDisposed ||
                                owner.ChildrenCount() != 0 ||
                                owner.GetPendingFairyUIOpenCount() != 0 ||
                                owner.GetOwnedFairyUIFormCount() != 0)
                            {
                                throw new InvalidOperationException(
                                    $"Pending pressure iteration {iteration} did not synchronously cancel and detach the owner.");
                            }

                            if (!component.IsDisposed ||
                                component.OnCloseCount != 1 ||
                                component.OnOpenCount != 0 ||
                                component.Context != null ||
                                component.View != null ||
                                component.FairyForm != null)
                            {
                                throw new InvalidOperationException(
                                    $"Pending pressure iteration {iteration} did not synchronously release its ET Component.");
                            }
                        });

                    throw new InvalidOperationException(
                        $"Pending pressure iteration {iteration} unexpectedly completed after owner disposal.");
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
                await WaitForFairyPackageDiagnosticsAsync(
                    baseline.PackageDiagnostics,
                    baseline.Package != null);
                AssertReleasedFairyComponent(
                    component,
                    context,
                    view,
                    widget,
                    0,
                    $"pending pressure iteration {iteration} completion");
                AssertFairyRuntimeBaseline(
                    uiManager,
                    baseline,
                    root,
                    owner,
                    rootComponentBaseline,
                    $"pending pressure iteration {iteration}");

                if (!canceled)
                {
                    throw new InvalidOperationException(
                        $"Pending pressure iteration {iteration} did not finish with OperationCanceledException.");
                }
            }
            finally
            {
                if (fiberId != 0)
                {
                    await FiberManager.Instance.Remove(fiberId);
                }
            }
        }

        private static async UniTask RunOpenedOwnerDestroyPressureIterationAsync(
            FairyUIManager uiManager,
            FairyRuntimeBaseline baseline,
            int iteration,
            bool removeFiber)
        {
            int fiberId = 0;
            Scene root = null;
            UIComponent owner = null;
            int rootComponentBaseline = 0;
            FairyUIForm form = null;
            FairyDemoFormComponent component = null;
            FairyUIFormContext context = null;
            UIMainView view = null;
            FairyInventoryItemWidget widget = null;
            int serialId = 0;

            try
            {
                fiberId = await FiberManager.Instance.Create(
                    SchedulerType.Main,
                    0,
                    SceneType.NetClient,
                    $"FairyOwnerOpenedPressure-{iteration}");
                root = GetFiber(FiberManager.Instance, fiberId).Root;
                rootComponentBaseline = root.ComponentsCount();
                owner = root.AddComponent<UIComponent>();
                form = await owner.OpenFairyUIFormAsync(UGFUIFormId.FairyDemoForm, owner);
                serialId = form.SerialId;
                component = form.Presenter is FairyUIPresenterAdapter adapter
                    ? adapter.Component as FairyDemoFormComponent
                    : null;
                context = component?.Context;
                view = component?.View as UIMainView;
                widget = component?.ItemWidget;
                if (component == null || context == null || view == null || widget == null)
                {
                    throw new InvalidOperationException(
                        $"Opened pressure iteration {iteration} did not create the complete ET FairyGUI state.");
                }

                if (!owner.OwnsFairyUIForm(serialId) ||
                    owner.GetPendingFairyUIOpenCount() != 0 ||
                    owner.GetOwnedFairyUIFormCount() != 1 ||
                    owner.ChildrenCount() != 1 ||
                    root.ComponentsCount() != rootComponentBaseline + 1 ||
                    !uiManager.HasUIForm(serialId) ||
                    uiManager.GetAllLoadedUIForms().Length != baseline.LoadedForms + 1 ||
                    uiManager.IsLoadingUIForm(serialId) ||
                    !context.IsAlive ||
                    context.Form != form ||
                    context.SerialId != serialId ||
                    component.SerialId != serialId ||
                    !widget.Opened ||
                    widget.View == null ||
                    UIPackage.GetByName("Package1") == null)
                {
                    throw new InvalidOperationException(
                        $"Opened pressure iteration {iteration} did not establish the expected owned state.");
                }

                if (removeFiber)
                {
                    await FiberManager.Instance.Remove(fiberId);
                    fiberId = 0;
                }
                else
                {
                    owner.Dispose();
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
                await WaitForFairyPackageDiagnosticsAsync(
                    baseline.PackageDiagnostics,
                    baseline.Package != null);
                AssertReleasedFairyComponent(
                    component,
                    context,
                    view,
                    widget,
                    1,
                    $"opened pressure iteration {iteration} ({(removeFiber ? "Fiber Remove" : "owner Dispose")})");
                AssertFairyRuntimeBaseline(
                    uiManager,
                    baseline,
                    root,
                    owner,
                    rootComponentBaseline,
                    $"opened pressure iteration {iteration} ({(removeFiber ? "Fiber Remove" : "owner Dispose")})",
                    removeFiber);

                if (uiManager.HasUIForm(serialId) || uiManager.IsLoadingUIForm(serialId))
                {
                    throw new InvalidOperationException(
                        $"Opened pressure iteration {iteration} left serial {serialId} in GF loaded/loading state.");
                }
            }
            finally
            {
                if (fiberId != 0)
                {
                    await FiberManager.Instance.Remove(fiberId);
                }
            }
        }

        private static FairyRuntimeBaseline CaptureFairyRuntimeBaseline(FairyUIManager uiManager)
        {
            return new FairyRuntimeBaseline(
                uiManager.GetAllLoadedUIForms().Length,
                uiManager.GetAllLoadingUIFormSerialIds().Length,
                GRoot.inst?.numChildren ?? 0,
                UIPackage.GetByName("Package1"),
                FairyPackageManager.GetDiagnostics(),
                FairyInventoryFlow.OpenDetailCount);
        }

        private static void AssertFairyRuntimeBaseline(
            FairyUIManager uiManager,
            FairyRuntimeBaseline baseline,
            Scene root,
            UIComponent owner,
            int rootComponentBaseline,
            string phase,
            bool rootRemoved = false)
        {
            if (owner == null ||
                !owner.IsDisposed ||
                owner.GetPendingFairyUIOpenCount() != 0 ||
                owner.GetOwnedFairyUIFormCount() != 0 ||
                owner.ChildrenCount() != 0)
            {
                throw new InvalidOperationException(
                    $"{phase} left ET owner pending/owned/child state behind.");
            }

            if (rootRemoved)
            {
                if (root == null || !root.IsDisposed)
                {
                    throw new InvalidOperationException(
                        $"{phase} did not dispose the ET fiber root.");
                }
            }
            else if (root == null || root.IsDisposed || root.ComponentsCount() != rootComponentBaseline)
            {
                throw new InvalidOperationException(
                    $"{phase} changed the ET root component count from {rootComponentBaseline} to " +
                    $"{root?.ComponentsCount() ?? -1}.");
            }

            AssertFairyRuntimeGlobalBaseline(uiManager, baseline, phase);
        }

        private static void AssertFairyRuntimeGlobalBaseline(
            FairyUIManager uiManager,
            FairyRuntimeBaseline baseline,
            string phase,
            bool requireExactPackage = true,
            bool compareGeneration = true)
        {
            UIPackage currentPackage = UIPackage.GetByName("Package1");
            bool packageMatches = requireExactPackage
                ? ReferenceEquals(currentPackage, baseline.Package)
                : (currentPackage != null) == (baseline.Package != null);
            if (uiManager.GetAllLoadedUIForms().Length != baseline.LoadedForms ||
                uiManager.GetAllLoadingUIFormSerialIds().Length != baseline.LoadingForms ||
                (GRoot.inst?.numChildren ?? 0) != baseline.RootChildren ||
                FairyInventoryFlow.OpenDetailCount != baseline.DetailCount ||
                !packageMatches ||
                !FairyPackageDiagnosticsMatch(
                    baseline.PackageDiagnostics,
                    FairyPackageManager.GetDiagnostics(),
                    compareGeneration))
            {
                IReadOnlyList<FairyPackageDiagnostic> diagnostics = FairyPackageManager.GetDiagnostics();
                throw new InvalidOperationException(
                    $"{phase} did not return FairyGUI global state to baseline: " +
                    $"loaded/loading/root/detail={uiManager.GetAllLoadedUIForms().Length}/" +
                    $"{uiManager.GetAllLoadingUIFormSerialIds().Length}/{GRoot.inst?.numChildren ?? 0}/" +
                    $"{FairyInventoryFlow.OpenDetailCount}, package-ref=" +
                    $"{packageMatches}, " +
                    $"diagnostics={diagnostics.Count}/{baseline.PackageDiagnostics.Count}.");
            }
        }

        private static void AssertReleasedFairyComponent(
            FairyDemoFormComponent component,
            FairyUIFormContext context,
            UIMainView view,
            FairyInventoryItemWidget widget,
            int expectedOnOpenCount,
            string phase)
        {
            if (component == null ||
                context == null ||
                view == null ||
                widget == null ||
                !component.IsDisposed ||
                component.OnCloseCount != 1 ||
                component.OnOpenCount != expectedOnOpenCount ||
                component.Context != null ||
                component.View != null ||
                component.FairyForm != null ||
                context.IsAlive ||
                context.Form != null ||
                context.SerialId != 0 ||
                !view.isDisposed ||
                widget.Opened ||
                widget.View != null)
            {
                throw new InvalidOperationException(
                    $"{phase} did not release ET Component, Context, view, and Widget exactly once.");
            }

            try
            {
                _ = context.LifetimeToken;
                throw new InvalidOperationException(
                    $"{phase} left Context.LifetimeToken accessible after cleanup.");
            }
            catch (ObjectDisposedException)
            {
                // Expected: Context access is invalid immediately after owner/form cleanup.
            }
        }

        private sealed class FairyRuntimeBaseline
        {
            internal FairyRuntimeBaseline(
                int loadedForms,
                int loadingForms,
                int rootChildren,
                UIPackage package,
                IReadOnlyList<FairyPackageDiagnostic> packageDiagnostics,
                int detailCount)
            {
                LoadedForms = loadedForms;
                LoadingForms = loadingForms;
                RootChildren = rootChildren;
                Package = package;
                PackageDiagnostics = packageDiagnostics;
                DetailCount = detailCount;
            }

            internal int LoadedForms { get; }
            internal int LoadingForms { get; }
            internal int RootChildren { get; }
            internal UIPackage Package { get; }
            internal IReadOnlyList<FairyPackageDiagnostic> PackageDiagnostics { get; }
            internal int DetailCount { get; }
        }

        private static Fiber GetFiber(FiberManager manager, int fiberId)
        {
            // FiberManager.Get 是 internal;测试专用反射访问,与运行时流程同源。
            MethodInfo getMethod = typeof(FiberManager).GetMethod(
                "Get",
                BindingFlags.Instance | BindingFlags.NonPublic);
            if (getMethod == null)
            {
                throw new InvalidOperationException(
                    "FiberManager internal Get method is not available for the fiber lifecycle test.");
            }

            return getMethod.Invoke(manager, new object[] { fiberId }) as Fiber
                ?? throw new InvalidOperationException(
                    $"Fiber '{fiberId}' is not available after creation.");
        }

        private static async UniTask WaitForUIFormClosedAsync(FairyUIManager uiManager, int serialId)
        {
            for (int frame = 0; frame < 120; frame++)
            {
                if (!uiManager.HasUIForm(serialId) && !uiManager.IsLoadingUIForm(serialId))
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            throw new InvalidOperationException(
                $"FairyGUI serial '{serialId}' did not close before the timing test.");
        }

        private static async UniTask WaitForFairyPackageDiagnosticsAsync(
            IReadOnlyList<FairyPackageDiagnostic> expected,
            bool expectedPackageRegistered,
            bool compareGeneration = true)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (FairyPackageDiagnosticsMatch(
                        expected,
                        FairyPackageManager.GetDiagnostics(),
                        compareGeneration) &&
                    (UIPackage.GetByName("Package1") != null) == expectedPackageRegistered)
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            IReadOnlyList<FairyPackageDiagnostic> remaining = FairyPackageManager.GetDiagnostics();
            throw new InvalidOperationException(
                $"FairyGUI package diagnostics did not return to the timing-test baseline. " +
                $"Expected entries/registered={expected.Count}/{expectedPackageRegistered}, " +
                $"actual entries/registered={remaining.Count}/{UIPackage.GetByName("Package1") != null}.");
        }

        private static bool FairyPackageDiagnosticsMatch(
            IReadOnlyList<FairyPackageDiagnostic> expected,
            IReadOnlyList<FairyPackageDiagnostic> actual,
            bool compareGeneration = true)
        {
            if (expected.Count != actual.Count)
            {
                return false;
            }

            for (int i = 0; i < expected.Count; i++)
            {
                FairyPackageDiagnostic expectedItem = expected[i];
                FairyPackageDiagnostic actualItem = actual[i];
                if (!string.Equals(expectedItem.Name, actualItem.Name, StringComparison.Ordinal) ||
                    expectedItem.Status != actualItem.Status ||
                    (compareGeneration && expectedItem.Generation != actualItem.Generation) ||
                    expectedItem.ReferenceCount != actualItem.ReferenceCount ||
                    expectedItem.LoadedAssetCount != actualItem.LoadedAssetCount ||
                    !string.Equals(expectedItem.LastError, actualItem.LastError, StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
