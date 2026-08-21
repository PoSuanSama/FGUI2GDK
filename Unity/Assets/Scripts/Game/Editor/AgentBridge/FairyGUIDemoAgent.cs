using System;
using System.Collections.Generic;
using System.Threading;
using AgentBridge;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Extension;
using UnityGameFramework.Runtime;

namespace Game.Editor
{
    public static class FairyGUIDemoAgent
    {
        [AgentCallable("Switch GDK to GameHot mode through the repository's Define Symbol menu.", 60)]
        public static void SwitchToGameHot()
        {
            if (!EditorApplication.ExecuteMenuItem("Game/Define Symbol/Add UNITY_GAMEHOT"))
            {
                throw new InvalidOperationException("Unable to execute the GameHot define-symbol menu item.");
            }
        }

        [AgentCallable("Open the FairyGUI demo UIForm host in PlayMode without relying on loaded Luban tables.", 30)]
        public static async UniTask OpenFairyDemoForm()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI demo smoke test requires PlayMode.");
            }

            if (GameEntry.UI == null)
            {
                throw new InvalidOperationException("GDK UI component is not initialized.");
            }

            UIForm uiForm = await GameEntry.UI.OpenUIFormAsync(
                "Assets/Res/UI/UIForm/Hot/FairyDemoForm.prefab",
                "Default",
                Constant.AssetPriority.UIFormAsset,
                true,
                null);
            if (uiForm == null)
            {
                throw new InvalidOperationException("GDK rejected the FairyGUI demo UIForm host.");
            }

            UIPanel panel = null;
            for (int i = 0; i < 600 && panel == null; i++)
            {
                UIPanel[] panels = UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None);
                foreach (UIPanel candidate in panels)
                {
                    if (candidate.packageName == "Package1" &&
                        candidate.componentName == "MainView" &&
                        candidate.isActiveAndEnabled &&
                        candidate.ui != null &&
                        candidate.ui.visible)
                    {
                        panel = candidate;
                        break;
                    }
                }

                if (panel == null)
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            if (panel == null || panel.ui == null)
            {
                throw new InvalidOperationException("FairyGUI demo UIForm did not create a ready UIPanel.");
            }

            Transform uiFormTransform = uiForm.Logic.transform;
            Transform isolationTransform = panel.transform.parent;
            if (!panel.transform.IsChildOf(uiFormTransform) ||
                isolationTransform == null ||
                isolationTransform.parent != uiFormTransform ||
                isolationTransform.name != "FairyGUI Transform Isolation")
            {
                throw new InvalidOperationException(
                    $"FairyGUI demo panel is outside its GF UIForm hierarchy. " +
                    $"UIForm='{GetHierarchyPath(uiFormTransform)}', panel='{GetHierarchyPath(panel.transform)}'.");
            }

            Vector3 isolationScale = isolationTransform.lossyScale;
            if (isolationTransform.position.sqrMagnitude > 0.000001f ||
                Quaternion.Angle(isolationTransform.rotation, Quaternion.identity) > 0.001f ||
                (isolationScale - Vector3.one).sqrMagnitude > 0.000001f)
            {
                throw new InvalidOperationException(
                    $"FairyGUI transform isolation is not identity in world space. " +
                    $"Position={isolationTransform.position}, rotation={isolationTransform.rotation.eulerAngles}, " +
                    $"scale={isolationScale}.");
            }

            GButton refreshButton = panel.ui.GetChild("refreshButton") as GButton;
            GTextField checkCountText = panel.ui.GetChild("checkCountText") as GTextField;
            if (refreshButton == null || checkCountText == null)
            {
                throw new InvalidOperationException("FairyGUI demo binding members are missing.");
            }

            if (checkCountText.text != "0")
            {
                throw new InvalidOperationException(
                    $"FairyGUI refresh counter has an unexpected initial value: '{checkCountText.text}'.");
            }

            refreshButton.onClick.Call();
            if (checkCountText.text != "1")
            {
                throw new InvalidOperationException(
                    $"FairyGUI refresh click did not update the counter. Actual value: '{checkCountText.text}'.");
            }
        }

        [AgentCallable("Validate FairyGUI package manifest topology, cancellation, coalescing, and reverse release.", 60)]
        public static async UniTask ValidateFairyPackageManagerLifecycle()
        {
            if (!UnityGameFramework.Extension.Awaitable.IsValid)
            {
                UnityGameFramework.Extension.Awaitable.SubscribeEvent();
            }

            string validManifest =
                "{\"schemaVersion\":1,\"packages\":[" +
                "{\"id\":\"a\",\"name\":\"PackageA\",\"dependencies\":[\"b\"]}," +
                "{\"id\":\"b\",\"name\":\"PackageB\",\"dependencies\":[]}]" +
                "}";
            IReadOnlyList<string> loadOrder = FairyPackageManager.ValidateCatalogAndGetLoadOrder(
                validManifest,
                "PackageA");
            if (loadOrder.Count != 2 || loadOrder[0] != "PackageB" || loadOrder[1] != "PackageA")
            {
                throw new InvalidOperationException(
                    $"FairyGUI dependency order is invalid: {string.Join(", ", loadOrder)}.");
            }

            string cycleManifest =
                "{\"schemaVersion\":1,\"packages\":[" +
                "{\"id\":\"a\",\"name\":\"PackageA\",\"dependencies\":[\"b\"]}," +
                "{\"id\":\"b\",\"name\":\"PackageB\",\"dependencies\":[\"a\"]}]" +
                "}";
            bool cycleRejected = false;
            try
            {
                FairyPackageManager.ValidateCatalogAndGetLoadOrder(cycleManifest, "PackageA");
            }
            catch (GameFrameworkException exception) when (exception.Message.Contains("cycle"))
            {
                cycleRejected = true;
            }

            if (!cycleRejected)
            {
                throw new InvalidOperationException("FairyGUI dependency cycle was not rejected.");
            }

            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics = FairyPackageManager.GetDiagnostics();
            int baselineReferenceCount = 0;
            foreach (FairyPackageDiagnostic diagnostic in baselineDiagnostics)
            {
                if (diagnostic.Name == "Package1")
                {
                    baselineReferenceCount = diagnostic.ReferenceCount;
                }
            }

            using (CancellationTokenSource cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                bool canceled = false;
                try
                {
                    await FairyPackageManager.AcquireAsync("Package1", cancellation.Token);
                }
                catch (OperationCanceledException)
                {
                    canceled = true;
                }

                IReadOnlyList<FairyPackageDiagnostic> afterCancellation = FairyPackageManager.GetDiagnostics();
                int afterCancellationReferenceCount = 0;
                foreach (FairyPackageDiagnostic diagnostic in afterCancellation)
                {
                    if (diagnostic.Name == "Package1")
                    {
                        afterCancellationReferenceCount = diagnostic.ReferenceCount;
                    }
                }

                if (!canceled ||
                    afterCancellation.Count != baselineDiagnostics.Count ||
                    afterCancellationReferenceCount != baselineReferenceCount)
                {
                    throw new InvalidOperationException(
                        $"Canceled FairyGUI acquire changed the baseline. " +
                        $"Expected count/ref {baselineDiagnostics.Count}/{baselineReferenceCount}, " +
                        $"actual {afterCancellation.Count}/{afterCancellationReferenceCount}.");
                }
            }

            FairyPackageLease firstLease = null;
            FairyPackageLease secondLease = null;
            try
            {
                UniTask<FairyPackageLease> firstTask = FairyPackageManager.AcquireAsync("Package1");
                UniTask<FairyPackageLease> secondTask = FairyPackageManager.AcquireAsync("Package1");
                firstLease = await firstTask;
                secondLease = await secondTask;

                IReadOnlyList<FairyPackageDiagnostic> diagnostics = FairyPackageManager.GetDiagnostics();
                if (diagnostics.Count != 1 ||
                    diagnostics[0].Status != FairyPackageStatus.Ready ||
                    diagnostics[0].ReferenceCount != baselineReferenceCount + 2)
                {
                    throw new InvalidOperationException("Concurrent FairyGUI acquire was not coalesced correctly.");
                }

                firstLease.Dispose();
                firstLease = null;
                diagnostics = FairyPackageManager.GetDiagnostics();
                if (diagnostics.Count != 1 || diagnostics[0].ReferenceCount != baselineReferenceCount + 1)
                {
                    throw new InvalidOperationException("FairyGUI lease release changed the shared reference count incorrectly.");
                }
            }
            finally
            {
                firstLease?.Dispose();
                secondLease?.Dispose();
            }

            for (int i = 0; i < 120 && FairyPackageManager.GetDiagnostics().Count != 0; i++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            IReadOnlyList<FairyPackageDiagnostic> finalDiagnostics = FairyPackageManager.GetDiagnostics();
            int finalReferenceCount = 0;
            foreach (FairyPackageDiagnostic diagnostic in finalDiagnostics)
            {
                if (diagnostic.Name == "Package1")
                {
                    finalReferenceCount = diagnostic.ReferenceCount;
                }
            }

            if (finalDiagnostics.Count != baselineDiagnostics.Count ||
                finalReferenceCount != baselineReferenceCount ||
                (baselineReferenceCount == 0 && UIPackage.GetByName("Package1") != null))
            {
                throw new InvalidOperationException(
                    $"FairyGUI package state did not return to baseline after release. " +
                    $"Expected count/ref {baselineDiagnostics.Count}/{baselineReferenceCount}, " +
                    $"actual {finalDiagnostics.Count}/{finalReferenceCount}.");
            }
        }

        [AgentCallable("Inspect FairyGUI demo Stage Camera, UIPanel renderer, and runtime visibility state.", 30)]
        public static void InspectFairyDemoRendering()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI rendering inspection requires PlayMode.");
            }

            UIPanel[] panels = UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None);
            if (panels.Length == 0)
            {
                throw new InvalidOperationException("No FairyGUI UIPanel exists in PlayMode.");
            }

            UIPanel panel = null;
            foreach (UIPanel candidate in panels)
            {
                if (candidate.packageName == "Package1" &&
                    candidate.componentName == "MainView" &&
                    candidate.isActiveAndEnabled &&
                    candidate.ui != null &&
                    candidate.ui.visible)
                {
                    panel = candidate;
                    break;
                }
            }

            if (panel == null)
            {
                throw new InvalidOperationException("No active Package1/MainView UIPanel exists in PlayMode.");
            }

            Camera stageCamera = StageCamera.main;
            Camera renderCamera = panel.container?.GetRenderCamera();
            MeshRenderer[] renderers = panel.GetComponentsInChildren<MeshRenderer>(true);
            MeshRenderer renderer = null;
            foreach (MeshRenderer candidate in renderers)
            {
                if (candidate.enabled && candidate.sharedMaterial != null)
                {
                    renderer = candidate;
                    break;
                }
            }

            if (stageCamera == null || renderCamera == null || renderers.Length == 0)
            {
                throw new InvalidOperationException(
                    $"FairyGUI renderer prerequisites are missing. stageCamera={stageCamera != null}, " +
                    $"renderCamera={renderCamera != null}, renderers={renderers.Length}.");
            }

            if (panel.ui == null || !panel.ui.visible || renderer == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI renderer is not visible. ui={panel.ui != null}, visible={panel.ui?.visible}, " +
                    $"rendererCount={renderers.Length}, enabledRenderer={renderer != null}, " +
                    $"material={renderer?.sharedMaterial != null}.");
            }

            int panelLayerMask = 1 << panel.gameObject.layer;
            if ((stageCamera.cullingMask & panelLayerMask) == 0)
            {
                throw new InvalidOperationException(
                    $"FairyGUI Stage Camera does not render panel layer {panel.gameObject.layer}. " +
                    $"Culling mask: {stageCamera.cullingMask}.");
            }

            Bounds rendererBounds = renderer.bounds;
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(stageCamera);
            if (!GeometryUtility.TestPlanesAABB(frustumPlanes, rendererBounds))
            {
                throw new InvalidOperationException(
                    $"FairyGUI renderer is outside the Stage Camera frustum. Bounds: {rendererBounds}.");
            }

            Log.Info(
                "FairyGUI rendering inspection passed. stageCamera={0}, renderCamera={1}, panelLayer={2}, " +
                "cameraCullingMask={3}, rendererEnabled={4}, material={5}, uiSize={6}x{7}.",
                stageCamera.name,
                renderCamera.name,
                panel.gameObject.layer,
                stageCamera.cullingMask,
                renderer.enabled,
                renderer.sharedMaterial.name,
                panel.ui.width,
                panel.ui.height);
        }

        private static string GetHierarchyPath(Transform target)
        {
            string path = target.name;
            while (target.parent != null)
            {
                target = target.parent;
                path = $"{target.name}/{path}";
            }

            return path;
        }
    }
}
