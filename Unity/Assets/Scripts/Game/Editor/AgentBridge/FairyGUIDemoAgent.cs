using System;
using System.Collections.Generic;
using System.Threading;
using AgentBridge;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using UnityEditor;
using UnityEngine;
using UnityGameFramework.Editor.ResourceTools;
using UnityGameFramework.Extension;
using UnityGameFramework.Extension.Editor;
using UnityGameFramework.Runtime;

namespace Game.Editor
{
    /// <summary>
    /// Editor 侧校验入口：统一经原生 <see cref="FairyUIManager"/> 驱动 FairyGUI 界面，
    /// 并覆盖资源规则与包生命周期验证。不再依赖 UGUI 宿主。
    /// </summary>
    public static class FairyGUIDemoAgent
    {
        private const string FairyGUIResourceDirectory = "Assets/Res/UI/FairyGUI";
        private const string FairyGUIResourceName = "UI.FairyGUI";
        private const string ResourceCollectionPath = "Assets/Res/Editor/Config/ResourceCollection.xml";
        private const string DescriptorAsset = "Assets/Res/UI/FairyGUI/FairyDemoForm.json";
        private const int FairyDemoUIId = 103;
        private const int FairyItemDetailUIId = 105;

        [AgentCallable("Switch GDK to GameHot mode through the repository's Define Symbol menu.", 60)]
        public static void SwitchToGameHot()
        {
            if (!EditorApplication.ExecuteMenuItem("Game/Define Symbol/Add UNITY_GAMEHOT"))
            {
                throw new InvalidOperationException("Unable to execute the GameHot define-symbol menu item.");
            }
        }

        [AgentCallable("Switch GDK to ET mode through the repository's Define Symbol menu.", 60)]
        public static void SwitchToET()
        {
            if (!EditorApplication.ExecuteMenuItem("Game/Define Symbol/Add UNITY_ET"))
            {
                throw new InvalidOperationException("Unable to execute the ET define-symbol menu item.");
            }
        }

        [AgentCallable("Add the FairyGUI directory to GameHot and ET resource rules, regenerate each collection, and verify every runtime asset is collected.", 120)]
        public static void ConfigureFairyGUIResourceRules()
        {
            string[] ruleConfigPaths =
            {
                ResourceRuleTool.ResourceRuleAsset_GameHot,
                ResourceRuleTool.ResourceRuleAsset_ET,
            };

            foreach (string configPath in ruleConfigPaths)
            {
                EnsureFairyGUIResourceRule(configPath);
            }

            AssetDatabase.SaveAssets();
            foreach (string configPath in ruleConfigPaths)
            {
                ResourceRuleEditorUtility.RefreshResourceCollectionWithOptimize(configPath);
                VerifyFairyGUIResourceCollection(configPath);
            }

            ResourceRuleEditorUtility.RefreshResourceCollectionWithOptimize(
                ResourceRuleTool.ResourceRuleAsset_GameHot);
            VerifyFairyGUIResourceCollection(ResourceRuleTool.ResourceRuleAsset_GameHot);
            AssetDatabase.ImportAsset(ResourceCollectionPath, ImportAssetOptions.ForceUpdate);
        }

        [AgentCallable("Open and interact with the FairyGUI demo through the native FairyUIManager host.", 30)]
        public static async UniTask OpenFairyDemoForm()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI demo smoke test requires PlayMode.");
            }

            FairyUIForm existing = FairyUIManager.Instance.GetUIForm(DescriptorAsset);
            if (existing == null)
            {
                existing = await FairyUIManager.Instance.OpenFairyUIFormAsync(FairyDemoUIId, "editor-demo");
            }

            if (existing == null)
            {
                throw new InvalidOperationException("FairyUIManager rejected the FairyGUI demo form.");
            }

            GComponent view = existing.View;
            if (view == null ||
                view.GetType().FullName != "Game.FairyGUI.Package1.UIMainView")
            {
                throw new InvalidOperationException("Native FairyGUI host did not expose the generated UIMainView binding.");
            }

            if (existing.Presenter == null || existing.Descriptor == null)
            {
                throw new InvalidOperationException("Native FairyGUI form is missing its presenter or descriptor.");
            }

            if (view.displayObject?.parent == null ||
                view.displayObject.stage != Stage.inst ||
                !view.visible ||
                !view.touchable)
            {
                throw new InvalidOperationException("FairyGUI view is not interactive under the single GRoot.");
            }

            GButton refreshButton = view.GetChild("refreshButton") as GButton;
            GTextField checkCountText = view.GetChild("checkCountText") as GTextField;
            if (refreshButton == null || checkCountText == null)
            {
                throw new InvalidOperationException("FairyGUI smoke probe could not locate generated binding members.");
            }

            if (!int.TryParse(checkCountText.text, out int beforeCount))
            {
                throw new InvalidOperationException($"FairyGUI refresh counter is invalid: '{checkCountText.text}'.");
            }

            refreshButton.onClick.Call();
            if (!int.TryParse(checkCountText.text, out int afterCount) || afterCount != beforeCount + 1)
            {
                throw new InvalidOperationException(
                    $"FairyGUI refresh click did not increment the generated binding counter. " +
                    $"Before={beforeCount}, after='{checkCountText.text}'.");
            }
        }

        [AgentCallable("Validate FairyGUI package manifest topology, cancellation, coalescing, and reverse release.", 60)]
        public static async UniTask ValidateFairyPackageManagerLifecycle()
        {
            if (!UnityGameFramework.Extension.Awaitable.IsValid)
            {
                UnityGameFramework.Extension.Awaitable.SubscribeEvent();
            }

            const string ValidManifest = "{\"schemaVersion\":2,\"packages\":[{\"id\":\"a\",\"name\":\"PackageA\",\"descriptorAsset\":\"Assets/A.bytes\",\"runtimeAssets\":[],\"dependencies\":[\"b\"]},{\"id\":\"b\",\"name\":\"PackageB\",\"descriptorAsset\":\"Assets/B.bytes\",\"runtimeAssets\":[],\"dependencies\":[]}]}";
            IReadOnlyList<string> loadOrder = FairyPackageManager.ValidateCatalogAndGetLoadOrder(
                ValidManifest,
                "PackageA");
            if (loadOrder.Count != 2 || loadOrder[0] != "PackageB" || loadOrder[1] != "PackageA")
            {
                throw new InvalidOperationException(
                    $"FairyGUI dependency order is invalid: {string.Join(", ", loadOrder)}.");
            }

            const string CycleManifest = "{\"schemaVersion\":2,\"packages\":[{\"id\":\"a\",\"name\":\"PackageA\",\"descriptorAsset\":\"Assets/A.bytes\",\"runtimeAssets\":[],\"dependencies\":[\"b\"]},{\"id\":\"b\",\"name\":\"PackageB\",\"descriptorAsset\":\"Assets/B.bytes\",\"runtimeAssets\":[],\"dependencies\":[\"a\"]}]}";
            bool cycleRejected = false;
            try
            {
                FairyPackageManager.ValidateCatalogAndGetLoadOrder(CycleManifest, "PackageA");
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

            FairyPackageLease preloadLease = null;
            try
            {
                preloadLease = await FairyPackageManager.AcquireAsync("Package1");

                IReadOnlyList<FairyPackageDiagnostic> handoffDiagnostics = FairyPackageManager.GetDiagnostics();
                int handoffReferenceCount = 0;
                foreach (FairyPackageDiagnostic diagnostic in handoffDiagnostics)
                {
                    if (diagnostic.Name == "Package1")
                    {
                        handoffReferenceCount = diagnostic.ReferenceCount;
                    }
                }

                if (handoffReferenceCount != baselineReferenceCount + 1)
                {
                    throw new InvalidOperationException("FairyGUI prepared ownership reference count is invalid.");
                }

                preloadLease.Dispose();
                preloadLease = null;
                preloadLease?.Dispose();
            }
            finally
            {
                preloadLease?.Dispose();
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

        [AgentCallable("Open, refocus, owner-cancel or close, and recycle the native FairyGUI form 100 times, then verify runtime diagnostics return to baseline.", 300)]
        public static async UniTask ValidateFairyUIFormLifecycleCycles()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI lifecycle cycles require PlayMode.");
            }

            FairyUIForm existing = FairyUIManager.Instance.GetUIForm(DescriptorAsset);
            if (existing != null)
            {
                FairyUIManager.Instance.CloseUIForm(existing.SerialId);
                await WaitForFairyUIFormClosed(existing.SerialId);
            }

            using (CancellationTokenSource canceledBeforeOpen = new CancellationTokenSource())
            {
                canceledBeforeOpen.Cancel();
                bool cancellationObserved = false;
                try
                {
                    await FairyUIManager.Instance.OpenFairyUIFormAsync(
                        FairyDemoUIId,
                        new object(),
                        canceledBeforeOpen.Token);
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved = true;
                }

                if (!cancellationObserved)
                {
                    throw new InvalidOperationException("A pre-canceled FairyGUI open was not canceled.");
                }
            }

            FairyUIForm warmupForm = await FairyUIManager.Instance.OpenFairyUIFormAsync(
                FairyDemoUIId,
                new object());
            FairyUIManager.Instance.CloseUIForm(warmupForm.SerialId);
            await WaitForFairyUIFormClosed(warmupForm.SerialId);

            using (CancellationTokenSource staleOwnerCancellation = new CancellationTokenSource())
            using (CancellationTokenSource currentOwnerCancellation = new CancellationTokenSource())
            {
                FairyUIForm staleOwnerForm = await FairyUIManager.Instance.OpenFairyUIFormAsync(
                    FairyDemoUIId,
                    new object(),
                    staleOwnerCancellation.Token);
                int staleSerialId = staleOwnerForm.SerialId;
                FairyUIManager.Instance.CloseUIForm(staleSerialId);
                await WaitForFairyUIFormClosed(staleSerialId);

                FairyUIForm currentOwnerForm = await FairyUIManager.Instance.OpenFairyUIFormAsync(
                    FairyDemoUIId,
                    new object(),
                    currentOwnerCancellation.Token);
                if (!ReferenceEquals(staleOwnerForm, currentOwnerForm))
                {
                    throw new InvalidOperationException(
                        "FairyGUI owner cancellation regression did not exercise a pooled form reuse.");
                }

                int currentSerialId = currentOwnerForm.SerialId;
                staleOwnerCancellation.Cancel();
                await UniTask.Yield(PlayerLoopTiming.Update);
                if (!FairyUIManager.Instance.HasUIForm(currentSerialId))
                {
                    throw new InvalidOperationException(
                        "A stale FairyGUI owner token closed the pooled form's current serial ID.");
                }

                FairyUIManager.Instance.CloseUIForm(currentSerialId);
                currentOwnerCancellation.Cancel();
                await WaitForFairyUIFormClosed(currentSerialId);
            }

            using (CancellationTokenSource firstInstanceOwner = new CancellationTokenSource())
            using (CancellationTokenSource secondInstanceOwner = new CancellationTokenSource())
            using (CancellationTokenSource thirdInstanceOwner = new CancellationTokenSource())
            {
                FairyUIForm firstInstance = await FairyUIManager.Instance.OpenFairyUIFormAsync(
                    FairyItemDetailUIId,
                    CreateItemDetailOpenData(1001),
                    firstInstanceOwner.Token);
                FairyUIForm secondInstance = await FairyUIManager.Instance.OpenFairyUIFormAsync(
                    FairyItemDetailUIId,
                    CreateItemDetailOpenData(1002),
                    secondInstanceOwner.Token);
                FairyUIForm thirdInstance = await FairyUIManager.Instance.OpenFairyUIFormAsync(
                    FairyItemDetailUIId,
                    CreateItemDetailOpenData(1003),
                    thirdInstanceOwner.Token);

                await UniTask.Yield(PlayerLoopTiming.Update);
                firstInstanceOwner.Cancel();
                await WaitForFairyUIFormClosed(firstInstance.SerialId);
                if (!FairyUIManager.Instance.HasUIForm(secondInstance.SerialId) ||
                    !FairyUIManager.Instance.HasUIForm(thirdInstance.SerialId))
                {
                    throw new InvalidOperationException(
                        "Canceling one multi-instance FairyGUI owner closed a different serial ID.");
                }

                secondInstanceOwner.Cancel();
                await WaitForFairyUIFormClosed(secondInstance.SerialId);
                if (!FairyUIManager.Instance.HasUIForm(thirdInstance.SerialId))
                {
                    throw new InvalidOperationException(
                        "Canceling the second multi-instance FairyGUI owner closed the third serial ID.");
                }

                thirdInstanceOwner.Cancel();
                await WaitForFairyUIFormClosed(thirdInstance.SerialId);
            }

            await WaitForFairyPackageDiagnostics(Array.Empty<FairyPackageDiagnostic>());
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics = FairyPackageManager.GetDiagnostics();
            int baselineLoadedForms = FairyUIManager.Instance.GetAllLoadedUIForms().Length;
            int baselineLoadingForms = FairyUIManager.Instance.GetAllLoadingUIFormSerialIds().Length;
            int baselineRootChildren = GRoot.inst.numChildren;

            for (int cycle = 0; cycle < 100; cycle++)
            {
                using CancellationTokenSource ownerCancellation = new CancellationTokenSource();
                object openUserData = new object();
                FairyUIForm uiForm = await FairyUIManager.Instance.OpenFairyUIFormAsync(
                    FairyDemoUIId,
                    openUserData,
                    ownerCancellation.Token);
                if (uiForm?.View == null || uiForm.Presenter == null || uiForm.Descriptor == null)
                {
                    throw new InvalidOperationException(
                        $"FairyGUI lifecycle cycle {cycle} opened without a complete native state.");
                }

                AssertPresenterObject(
                    uiForm.Presenter,
                    "LastOpenUserData",
                    openUserData,
                    $"FairyGUI lifecycle cycle {cycle} replaced the original open userData.");

                if (cycle % 10 == 0)
                {
                    int refocusBefore = GetPresenterInt(uiForm.Presenter, "RefocusCount");
                    object refocusUserData = new object();
                    FairyUIManager.Instance.RefocusUIForm(uiForm, refocusUserData);
                    if (GetPresenterInt(uiForm.Presenter, "RefocusCount") != refocusBefore + 1)
                    {
                        throw new InvalidOperationException(
                            $"FairyGUI lifecycle cycle {cycle} did not dispatch refocus exactly once.");
                    }

                    AssertPresenterObject(
                        uiForm.Presenter,
                        "LastRefocusUserData",
                        refocusUserData,
                        $"FairyGUI lifecycle cycle {cycle} replaced refocus userData.");
                }

                if (cycle % 5 == 0)
                {
                    ownerCancellation.Cancel();
                }
                else
                {
                    FairyUIManager.Instance.CloseUIForm(uiForm.SerialId);
                }

                await WaitForFairyUIFormClosed(uiForm.SerialId);
            }

            await WaitForFairyPackageDiagnostics(baselineDiagnostics);
            int finalLoadedForms = FairyUIManager.Instance.GetAllLoadedUIForms().Length;
            int finalLoadingForms = FairyUIManager.Instance.GetAllLoadingUIFormSerialIds().Length;
            int finalRootChildren = GRoot.inst.numChildren;
            if (finalLoadedForms != baselineLoadedForms ||
                finalLoadingForms != baselineLoadingForms ||
                finalRootChildren != baselineRootChildren)
            {
                throw new InvalidOperationException(
                    "FairyGUI lifecycle cycles did not return to baseline. " +
                    $"Loaded {baselineLoadedForms}->{finalLoadedForms}, " +
                    $"loading {baselineLoadingForms}->{finalLoadingForms}, " +
                    $"root children {baselineRootChildren}->{finalRootChildren}.");
            }

            if (UIPackage.GetByName("Package1") != null)
            {
                throw new InvalidOperationException(
                    "FairyGUI Package1 remained registered after the 100-cycle lifecycle probe.");
            }
        }

        [AgentCallable("Inspect the native FairyGUI GRoot and runtime visibility state.", 30)]
        public static void InspectFairyDemoRendering()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI rendering inspection requires PlayMode.");
            }

            FairyUIForm uiForm = FairyUIManager.Instance.GetUIForm(DescriptorAsset);
            GComponent view = uiForm?.View;
            if (view == null ||
                view.GetType().FullName != "Game.FairyGUI.Package1.UIMainView")
            {
                throw new InvalidOperationException("No open native FairyGUI demo form exists in PlayMode.");
            }

            Camera stageCamera = StageCamera.main;
            if (stageCamera == null || GRoot.inst == null || view.displayObject?.stage != Stage.inst)
            {
                throw new InvalidOperationException(
                    $"FairyGUI root prerequisites are missing. stageCamera={stageCamera != null}, " +
                    $"root={GRoot.inst != null}, viewOnStage={view.displayObject?.stage == Stage.inst}.");
            }

            if (view.displayObject.parent == null || !view.visible || !view.touchable)
            {
                throw new InvalidOperationException(
                    $"FairyGUI demo is not interactive. displayParent={view.displayObject.parent != null}, " +
                    $"visible={view.visible}, touchable={view.touchable}.");
            }

            if (!Mathf.Approximately(view.width, GRoot.inst.width) ||
                !Mathf.Approximately(view.height, GRoot.inst.height))
            {
                throw new InvalidOperationException(
                    $"FairyGUI demo does not fill the logical root. " +
                    $"view={view.width}x{view.height}, root={GRoot.inst.width}x{GRoot.inst.height}.");
            }

            Log.Info(
                "Native FairyGUI rendering inspection passed. stageCamera={0}, serialId={1}, " +
                "group={2}, uiSize={3}x{4}.",
                stageCamera.name,
                uiForm.SerialId,
                uiForm.UIGroup.Name,
                view.width,
                view.height);
        }

        private static async UniTask WaitForFairyUIFormClosed(int serialId)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (!FairyUIManager.Instance.HasUIForm(serialId) &&
                    !FairyUIManager.Instance.IsLoadingUIForm(serialId))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            throw new InvalidOperationException(
                $"FairyGUI form '{serialId}' did not close and recycle within 300 frames.");
        }

        private static int GetPresenterInt(IFairyUIPresenter presenter, string propertyName)
        {
            object value = presenter.GetType().GetProperty(propertyName)?.GetValue(presenter);
            if (value is not int intValue)
            {
                throw new InvalidOperationException(
                    $"FairyGUI lifecycle presenter has no integer diagnostic property '{propertyName}'.");
            }

            return intValue;
        }

        private static void AssertPresenterObject(
            IFairyUIPresenter presenter,
            string propertyName,
            object expected,
            string error)
        {
            object actual = presenter.GetType().GetProperty(propertyName)?.GetValue(presenter);
            if (!ReferenceEquals(actual, expected))
            {
                throw new InvalidOperationException(error);
            }
        }

        private static object CreateItemDetailOpenData(int token)
        {
            Type itemType = FindLoadedType(
                "Game.Hot.FairyInventoryItemData",
                "ET.Client.FairyInventoryItemData");
            Type categoryType = FindLoadedType(
                "Game.Hot.FairyInventoryCategory",
                "ET.Client.FairyInventoryCategory");
            Type openDataType = FindLoadedType(
                "Game.Hot.FairyItemDetailOpenData",
                "ET.Client.FairyItemDetailOpenData");

            object item = Activator.CreateInstance(
                itemType,
                token,
                $"Owner lifecycle item {token}",
                Enum.ToObject(categoryType, 1),
                1,
                "Owner lifecycle regression probe");
            return Activator.CreateInstance(openDataType, item, token);
        }

        private static Type FindLoadedType(params string[] fullNames)
        {
            foreach (System.Reflection.Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                foreach (string fullName in fullNames)
                {
                    Type type = assembly.GetType(fullName, throwOnError: false);
                    if (type != null)
                    {
                        return type;
                    }
                }
            }

            throw new InvalidOperationException(
                $"Unable to locate a loaded FairyGUI lifecycle data type: {string.Join(", ", fullNames)}.");
        }

        private static async UniTask WaitForFairyPackageDiagnostics(
            IReadOnlyList<FairyPackageDiagnostic> expected)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                IReadOnlyList<FairyPackageDiagnostic> actual = FairyPackageManager.GetDiagnostics();
                if (FairyPackageDiagnosticsMatch(expected, actual))
                {
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            IReadOnlyList<FairyPackageDiagnostic> remaining = FairyPackageManager.GetDiagnostics();
            throw new InvalidOperationException(
                $"FairyGUI package diagnostics did not return to baseline. " +
                $"Expected entries={expected.Count}, actual entries={remaining.Count}.");
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

        private static void EnsureFairyGUIResourceRule(string configPath)
        {
            ResourceRuleEditorData ruleData =
                AssetDatabase.LoadAssetAtPath<ResourceRuleEditorData>(configPath);
            if (ruleData == null)
            {
                throw new InvalidOperationException($"Resource rule config does not exist: {configPath}");
            }

            ResourceRule fairyRule = null;
            foreach (ResourceRule rule in ruleData.Rules)
            {
                bool matchesName = string.Equals(rule.Name, FairyGUIResourceName, StringComparison.Ordinal);
                bool matchesDirectory = string.Equals(
                    rule.AssetsDirectoryPath,
                    FairyGUIResourceDirectory,
                    StringComparison.Ordinal);
                if (!matchesName && !matchesDirectory)
                {
                    continue;
                }

                if (!matchesName || !matchesDirectory || fairyRule != null)
                {
                    throw new InvalidOperationException(
                        $"Resource rule config '{configPath}' has a conflicting FairyGUI rule.");
                }

                fairyRule = rule;
            }

            Undo.RecordObject(ruleData, "Configure FairyGUI resource rule");
            if (fairyRule == null)
            {
                fairyRule = new ResourceRule();
                ruleData.Rules.Add(fairyRule);
            }

            fairyRule.Valid = true;
            fairyRule.Name = FairyGUIResourceName;
            fairyRule.Variant = null;
            fairyRule.FileSystem = string.Empty;
            fairyRule.Groups = string.Empty;
            fairyRule.AssetsDirectoryPath = FairyGUIResourceDirectory;
            fairyRule.LoadType = LoadType.LoadFromFile;
            fairyRule.Packed = false;
            fairyRule.FilterType = ResourceFilterType.Root;
            fairyRule.SearchPatterns = "*.*";
            EditorUtility.SetDirty(ruleData);
        }

        private static void VerifyFairyGUIResourceCollection(string configPath)
        {
            ResourceCollection resourceCollection = new ResourceCollection();
            if (!resourceCollection.Load())
            {
                throw new InvalidOperationException(
                    $"Unable to read ResourceCollection after refreshing '{configPath}'.");
            }

            Resource resource = resourceCollection.GetResource(FairyGUIResourceName, null);
            if (resource == null)
            {
                throw new InvalidOperationException(
                    $"Resource collection generated from '{configPath}' has no '{FairyGUIResourceName}' resource.");
            }

            string[] requiredRuntimeAssets =
            {
                "Assets/Res/UI/FairyGUI/FairyDemoForm.json",
                "Assets/Res/UI/FairyGUI/GDKFairyManifest.json",
                "Assets/Res/UI/FairyGUI/Package1_fui.bytes",
            };
            foreach (string requiredAsset in requiredRuntimeAssets)
            {
                VerifyFairyGUIResourceAsset(resourceCollection, resource, configPath, requiredAsset);
            }

            string[] assetGuids = AssetDatabase.FindAssets(string.Empty, new[] { FairyGUIResourceDirectory });
            foreach (string assetGuid in assetGuids)
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuid);
                if (!AssetDatabase.IsValidFolder(assetPath))
                {
                    VerifyFairyGUIResourceAsset(resourceCollection, resource, configPath, assetPath);
                }
            }
        }

        private static void VerifyFairyGUIResourceAsset(
            ResourceCollection resourceCollection,
            Resource expectedResource,
            string configPath,
            string assetPath)
        {
            string assetGuid = AssetDatabase.AssetPathToGUID(assetPath);
            Asset asset = resourceCollection.GetAsset(assetGuid);
            if (string.IsNullOrEmpty(assetGuid) || asset == null || asset.Resource != expectedResource)
            {
                throw new InvalidOperationException(
                    $"Resource collection generated from '{configPath}' did not collect '{assetPath}' " +
                    $"into '{FairyGUIResourceName}'.");
            }
        }
    }
}
