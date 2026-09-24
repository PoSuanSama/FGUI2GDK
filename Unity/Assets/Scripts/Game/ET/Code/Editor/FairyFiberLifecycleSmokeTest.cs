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
                    originalPackageRegistered);
                if (uiManager.GetAllLoadedUIForms().Length != originalLoadedForms ||
                    uiManager.GetAllLoadingUIFormSerialIds().Length != originalLoadingForms ||
                    (GRoot.inst?.numChildren ?? 0) != originalRootChildren)
                {
                    throw new InvalidOperationException(
                        "The pre-serial timing test did not restore the original FairyGUI runtime baseline.");
                }
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
            bool expectedPackageRegistered)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (FairyPackageDiagnosticsMatch(expected, FairyPackageManager.GetDiagnostics()) &&
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
            IReadOnlyList<FairyPackageDiagnostic> actual)
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
