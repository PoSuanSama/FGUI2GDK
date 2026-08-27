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
    public static class FairyGUIDemoAgent
    {
        private const string GameFrameworkPrefabPath = "Assets/Scripts/Library/UGF/GameFramework.prefab";
        private const string GameEntryPrefabPath = "Assets/Res/GameEntry.prefab";
        private const string FairyGUIResourceDirectory = "Assets/Res/UI/FairyGUI";
        private const string FairyGUIResourceName = "UI.FairyGUI";
        private const string ResourceCollectionPath = "Assets/Res/Editor/Config/ResourceCollection.xml";

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

        [AgentCallable("Configure the GameFramework prefab to use the unified GDK UIForm and UIGroup helpers, then verify both serialized values.", 60)]
        public static void ConfigureUnifiedUIFormHelper()
        {
            ConfigureUIHelpers(
                GameFrameworkPrefabPath,
                typeof(DefaultUIFormHelper),
                typeof(DefaultUIGroupHelper));
            ConfigureUIHelpers(
                GameEntryPrefabPath,
                typeof(GDKUIFormHelper),
                typeof(GDKUIGroupHelper));

            VerifyUIHelpers(
                GameFrameworkPrefabPath,
                typeof(DefaultUIFormHelper),
                typeof(DefaultUIGroupHelper));
            VerifyUIHelpers(
                GameEntryPrefabPath,
                typeof(GDKUIFormHelper),
                typeof(GDKUIGroupHelper));
        }

        private static void ConfigureUIHelpers(
            string prefabPath,
            Type formHelperType,
            Type groupHelperType)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                UIComponent uiComponent = root.GetComponentInChildren<UIComponent>(true);
                if (uiComponent == null)
                {
                    throw new InvalidOperationException(
                        $"Prefab has no UIComponent: {prefabPath}");
                }

                SerializedObject serializedObject = new SerializedObject(uiComponent);
                SerializedProperty formHelperTypeProperty =
                    serializedObject.FindProperty("m_UIFormHelperTypeName");
                SerializedProperty customFormHelper = serializedObject.FindProperty("m_CustomUIFormHelper");
                SerializedProperty groupHelperTypeProperty =
                    serializedObject.FindProperty("m_UIGroupHelperTypeName");
                SerializedProperty customGroupHelper = serializedObject.FindProperty("m_CustomUIGroupHelper");
                if (formHelperTypeProperty == null || customFormHelper == null ||
                    groupHelperTypeProperty == null || customGroupHelper == null)
                {
                    throw new InvalidOperationException("UIComponent helper serialization fields were not found.");
                }

                formHelperTypeProperty.stringValue = formHelperType.FullName;
                customFormHelper.objectReferenceValue = null;
                groupHelperTypeProperty.stringValue = groupHelperType.FullName;
                customGroupHelper.objectReferenceValue = null;
                serializedObject.ApplyModifiedPropertiesWithoutUndo();
                if (!PrefabUtility.SaveAsPrefabAsset(root, prefabPath))
                {
                    throw new InvalidOperationException(
                        $"Unable to save the prefab: {prefabPath}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.ImportAsset(prefabPath, ImportAssetOptions.ForceUpdate);
        }

        private static void VerifyUIHelpers(
            string prefabPath,
            Type formHelperType,
            Type groupHelperType)
        {
            GameObject savedPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            UIComponent savedUI = savedPrefab != null
                ? savedPrefab.GetComponentInChildren<UIComponent>(true)
                : null;
            SerializedObject savedSerializedObject = savedUI != null
                ? new SerializedObject(savedUI)
                : null;
            SerializedProperty savedFormType =
                savedSerializedObject?.FindProperty("m_UIFormHelperTypeName");
            SerializedProperty savedGroupType =
                savedSerializedObject?.FindProperty("m_UIGroupHelperTypeName");
            if (savedFormType == null ||
                savedFormType.stringValue != formHelperType.FullName ||
                savedGroupType == null ||
                savedGroupType.stringValue != groupHelperType.FullName)
            {
                throw new InvalidOperationException(
                    $"UIForm or UIGroup helper serialization did not persist for '{prefabPath}'.");
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

        [AgentCallable("Open and interact with the FairyGUI demo through the unified GF UIForm host.", 30)]
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

            const string DescriptorAsset = "Assets/Res/UI/FairyGUI/FairyDemoForm.json";
            UIForm uiForm = GameEntry.UI.GetUIForm(DescriptorAsset);
            if (uiForm == null)
            {
                uiForm = await FairyUIFormService.OpenFairyUIFormAsync(103);
            }

            if (uiForm == null)
            {
                throw new InvalidOperationException("GDK rejected the FairyGUI demo UIForm host.");
            }

            FairyUIFormLogic logic = uiForm.Logic as FairyUIFormLogic;
            GComponent view = logic?.View;
            if (view == null ||
                view.GetType().FullName != "Game.Hot.FairyGUI.Package1.UIMainView")
            {
                throw new InvalidOperationException("Unified FairyGUI host did not expose the generated UIMainView binding.");
            }

            GameObject host = logic.gameObject;
            if (host.GetComponent<Canvas>() != null ||
                host.GetComponent<RectTransform>() != null ||
                host.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null ||
                host.GetComponent<UIPanel>() != null)
            {
                throw new InvalidOperationException("Unified FairyGUI host contains a forbidden UGUI or UIPanel component.");
            }

            AssertUnifiedUIHierarchy(uiForm, logic, view);

            if (view.displayObject.parent == null ||
                view.displayObject.parent.parent != GRoot.inst.container ||
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
                throw new InvalidOperationException(
                    $"FairyGUI refresh counter is invalid: '{checkCountText.text}'.");
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

        [AgentCallable("Open, refocus, cover/reveal, owner-cancel or close, and recycle the unified FairyGUI form 100 times, then verify all runtime diagnostics return to baseline.", 300)]
        public static async UniTask ValidateFairyUIFormLifecycleCycles()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI lifecycle cycles require PlayMode.");
            }

            if (GameEntry.UI == null)
            {
                throw new InvalidOperationException("GDK UI component is not initialized.");
            }

            const int FairyDemoUIId = 103;
            const string DescriptorAsset = "Assets/Res/UI/FairyGUI/FairyDemoForm.json";
            UIForm existingForm = GameEntry.UI.GetUIForm(DescriptorAsset);
            if (existingForm != null)
            {
                GameEntry.UI.CloseUIForm(existingForm.SerialId);
                await WaitForFairyUIFormClosed(existingForm.SerialId, DescriptorAsset);
            }

            using (CancellationTokenSource canceledBeforeOpen = new CancellationTokenSource())
            {
                canceledBeforeOpen.Cancel();
                bool cancellationObserved = false;
                try
                {
                    await FairyUIFormService.OpenFairyUIFormAsync(
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

            UIForm warmupForm = await FairyUIFormService.OpenFairyUIFormAsync(
                FairyDemoUIId,
                new object());
            FairyUIFormLogic warmupLogic = warmupForm.Logic as FairyUIFormLogic;
            GameEntry.UI.CloseUIForm(warmupForm.SerialId);
            await WaitForFairyUIFormClosed(warmupForm.SerialId, DescriptorAsset);
            AssertFairyUIFormRecycled(warmupLogic, "warmup");

            await WaitForFairyPackageDiagnostics(Array.Empty<FairyPackageDiagnostic>());
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics = FairyPackageManager.GetDiagnostics();
            int baselineLoadedForms = GameEntry.UI.GetAllLoadedUIForms().Length;
            int baselineLoadingForms = GameEntry.UI.GetAllLoadingUIFormSerialIds().Length;
            int baselineRootChildren = GRoot.inst.numChildren;
            int baselinePanels = Resources.FindObjectsOfTypeAll<UIPanel>().Length;
            int baselineStageCameras = Resources.FindObjectsOfTypeAll<StageCamera>().Length;

            for (int cycle = 0; cycle < 100; cycle++)
            {
                using CancellationTokenSource ownerCancellation = new CancellationTokenSource();
                object openUserData = new object();
                UIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                    FairyDemoUIId,
                    openUserData,
                    ownerCancellation.Token);
                FairyUIFormLogic logic = uiForm.Logic as FairyUIFormLogic;
                if (logic?.View == null || logic.Presenter == null || logic.Descriptor == null)
                {
                    throw new InvalidOperationException(
                        $"FairyGUI lifecycle cycle {cycle} opened without a complete prepared state.");
                }

                AssertUnifiedUIHierarchy(uiForm, logic, logic.View);
                AssertPresenterObject(
                    logic.Presenter,
                    "LastOpenUserData",
                    openUserData,
                    $"FairyGUI lifecycle cycle {cycle} replaced the original open userData.");

                if (cycle % 10 == 0)
                {
                    await ExerciseStackLifecycle(uiForm, logic, cycle);
                }

                if (cycle % 5 == 0)
                {
                    ownerCancellation.Cancel();
                }
                else
                {
                    GameEntry.UI.CloseUIForm(uiForm.SerialId);
                }

                await WaitForFairyUIFormClosed(uiForm.SerialId, DescriptorAsset);
                AssertFairyUIFormRecycled(logic, cycle.ToString());
            }

            await WaitForFairyPackageDiagnostics(baselineDiagnostics);
            int finalLoadedForms = GameEntry.UI.GetAllLoadedUIForms().Length;
            int finalLoadingForms = GameEntry.UI.GetAllLoadingUIFormSerialIds().Length;
            int finalRootChildren = GRoot.inst.numChildren;
            int finalPanels = Resources.FindObjectsOfTypeAll<UIPanel>().Length;
            int finalStageCameras = Resources.FindObjectsOfTypeAll<StageCamera>().Length;
            if (finalLoadedForms != baselineLoadedForms ||
                finalLoadingForms != baselineLoadingForms ||
                finalRootChildren != baselineRootChildren ||
                finalPanels != baselinePanels ||
                finalStageCameras != baselineStageCameras)
            {
                throw new InvalidOperationException(
                    "FairyGUI lifecycle cycles did not return to baseline. " +
                    $"Loaded {baselineLoadedForms}->{finalLoadedForms}, " +
                    $"loading {baselineLoadingForms}->{finalLoadingForms}, " +
                    $"root children {baselineRootChildren}->{finalRootChildren}, " +
                    $"UIPanels {baselinePanels}->{finalPanels}, " +
                    $"StageCameras {baselineStageCameras}->{finalStageCameras}.");
            }

            if (UIPackage.GetByName("Package1") != null)
            {
                throw new InvalidOperationException(
                    "FairyGUI Package1 remained registered after the 100-cycle lifecycle probe.");
            }
        }

        [AgentCallable("Validate serial-ID and synchronous-pool prepared-state correlation without replacing the original userData.", 30)]
        public static void ValidateFairyUIPreparedStateCorrelation()
        {
            Type registryType = typeof(FairyUIFormLogic).Assembly.GetType(
                "Game.FairyUIFormPreparedRegistry",
                throwOnError: true);
            Type stateType = typeof(FairyUIFormLogic).Assembly.GetType(
                "Game.FairyUIFormPreparedState",
                throwOnError: true);
            const System.Reflection.BindingFlags StaticFlags =
                System.Reflection.BindingFlags.Static |
                System.Reflection.BindingFlags.NonPublic;
            const System.Reflection.BindingFlags InstanceFlags =
                System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.NonPublic;

            System.Reflection.ConstructorInfo constructor = stateType.GetConstructor(
                InstanceFlags,
                binder: null,
                new[]
                {
                    typeof(string),
                    typeof(FairyUIFormDescriptor),
                    typeof(FairyPackageLease),
                    typeof(GComponent),
                    typeof(IFairyUIPresenter),
                    typeof(object),
                },
                modifiers: null);
            System.Reflection.MethodInfo beginOpen = registryType.GetMethod("BeginOpen", StaticFlags);
            System.Reflection.MethodInfo bindSerialId = registryType.GetMethod("BindSerialId", StaticFlags);
            System.Reflection.MethodInfo consumeNew = registryType.GetMethod("ConsumeNewInstance", StaticFlags);
            System.Reflection.MethodInfo consumePooled = registryType.GetMethod("ConsumePooledInstance", StaticFlags);
            System.Reflection.MethodInfo tryRemove = registryType.GetMethod("TryRemove", StaticFlags);
            if (constructor == null || beginOpen == null || bindSerialId == null || consumeNew == null ||
                consumePooled == null || tryRemove == null)
            {
                throw new InvalidOperationException(
                    "FairyGUI prepared-state correlation probe could not resolve the internal handoff contract.");
            }

            object firstState = constructor.Invoke(new object[] { "FairyDemoForm", null, null, null, null, null });
            object secondState = constructor.Invoke(new object[] { "FairyDemoForm", null, null, null, null, null });
            object pooledUserData = new object();
            object pooledState = constructor.Invoke(
                new object[] { "FairyDemoForm", null, null, null, null, pooledUserData });
            try
            {
                using ((IDisposable)beginOpen.Invoke(null, new[] { firstState }))
                {
                    bindSerialId.Invoke(null, new[] { (object)71001, firstState });
                }

                using ((IDisposable)beginOpen.Invoke(null, new[] { secondState }))
                {
                    bindSerialId.Invoke(null, new[] { (object)71002, secondState });
                }

                object consumedSecond = consumeNew.Invoke(
                    null,
                    new object[] { 71002, "FairyDemoForm", null });
                object consumedFirst = consumeNew.Invoke(
                    null,
                    new object[] { 71001, "FairyDemoForm", null });
                if (!ReferenceEquals(consumedSecond, secondState) ||
                    !ReferenceEquals(consumedFirst, firstState))
                {
                    throw new InvalidOperationException(
                        "Concurrent FairyGUI opens with identical descriptor/userData were correlated by queue order instead of GF serial ID.");
                }

                using ((IDisposable)beginOpen.Invoke(null, new[] { pooledState }))
                {
                    object consumedPooled = consumePooled.Invoke(
                        null,
                        new[] { (object)"FairyDemoForm", pooledUserData });
                    if (!ReferenceEquals(consumedPooled, pooledState))
                    {
                        throw new InvalidOperationException(
                            "A pooled FairyGUI instance did not consume the exact synchronous prepared state.");
                    }
                }
            }
            finally
            {
                foreach (object state in new[] { firstState, secondState, pooledState })
                {
                    tryRemove.Invoke(null, new[] { state });
                    ((IDisposable)state).Dispose();
                }
            }
        }

        [AgentCallable("Inspect the unified FairyGUI GRoot, GF UIForm host, and runtime visibility state.", 30)]
        public static void InspectFairyDemoRendering()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI rendering inspection requires PlayMode.");
            }

            const string DescriptorAsset = "Assets/Res/UI/FairyGUI/FairyDemoForm.json";
            UIForm uiForm = GameEntry.UI.GetUIForm(DescriptorAsset);
            FairyUIFormLogic logic = uiForm?.Logic as FairyUIFormLogic;
            GComponent view = logic?.View;
            if (view == null ||
                view.GetType().FullName != "Game.Hot.FairyGUI.Package1.UIMainView")
            {
                throw new InvalidOperationException("No open unified FairyGUI demo UIForm exists in PlayMode.");
            }

            if (logic.GetComponent<UIPanel>() != null ||
                logic.GetComponent<Canvas>() != null ||
                logic.GetComponent<RectTransform>() != null ||
                logic.GetComponent<UnityEngine.UI.GraphicRaycaster>() != null)
            {
                throw new InvalidOperationException("Unified FairyGUI host contains legacy UIPanel or UGUI components.");
            }

            if (UnityEngine.Object.FindObjectsByType<UIPanel>(FindObjectsSortMode.None).Length != 0)
            {
                throw new InvalidOperationException("Legacy per-form FairyGUI UIPanel instances still exist in PlayMode.");
            }

            AssertUnifiedUIHierarchy(uiForm, logic, view);

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

            int viewLayerMask = 1 << view.displayObject.gameObject.layer;
            if ((stageCamera.cullingMask & viewLayerMask) == 0)
            {
                throw new InvalidOperationException(
                    $"FairyGUI Stage Camera does not render the MainView layer " +
                    $"'{LayerMask.LayerToName(view.displayObject.gameObject.layer)}'.");
            }

            Renderer[] renderers = view.displayObject.gameObject.GetComponentsInChildren<Renderer>(true);
            Plane[] frustumPlanes = GeometryUtility.CalculateFrustumPlanes(stageCamera);
            Renderer visibleRenderer = null;
            foreach (Renderer renderer in renderers)
            {
                if (renderer.enabled &&
                    renderer.gameObject.activeInHierarchy &&
                    renderer.sharedMaterial != null &&
                    GeometryUtility.TestPlanesAABB(frustumPlanes, renderer.bounds))
                {
                    visibleRenderer = renderer;
                    break;
                }
            }

            if (visibleRenderer == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI MainView has no enabled renderer inside the Stage Camera frustum. " +
                    $"Renderer count={renderers.Length}.");
            }

            Log.Info(
                "Unified FairyGUI rendering inspection passed. stageCamera={0}, serialId={1}, " +
                "group={2}, uiSize={3}x{4}, renderer={5}.",
                stageCamera.name,
                uiForm.SerialId,
                uiForm.UIGroup.Name,
                view.width,
                view.height,
                visibleRenderer.name);
        }

        private static async UniTask WaitForFairyUIFormClosed(int serialId, string descriptorAsset)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (!GameEntry.UI.HasUIForm(serialId) &&
                    !GameEntry.UI.IsLoadingUIForm(serialId) &&
                    !GameEntry.UI.HasUIForm(descriptorAsset) &&
                    !GameEntry.UI.IsLoadingUIForm(descriptorAsset))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            throw new InvalidOperationException(
                $"FairyGUI UIForm '{serialId}' did not close and recycle within 300 frames.");
        }

        private static async UniTask ExerciseStackLifecycle(
            UIForm uiForm,
            FairyUIFormLogic logic,
            int cycle)
        {
            IFairyUIPresenter presenter = logic.Presenter;
            int refocusBefore = GetPresenterInt(presenter, "RefocusCount");
            object refocusUserData = new object();
            GameEntry.UI.RefocusUIForm(uiForm, refocusUserData);
            if (GetPresenterInt(presenter, "RefocusCount") != refocusBefore + 1)
            {
                throw new InvalidOperationException(
                    $"FairyGUI lifecycle cycle {cycle} did not dispatch refocus exactly once.");
            }

            AssertPresenterObject(
                presenter,
                "LastRefocusUserData",
                refocusUserData,
                $"FairyGUI lifecycle cycle {cycle} replaced refocus userData.");

            int pauseBefore = GetPresenterInt(presenter, "PauseCount");
            int resumeBefore = GetPresenterInt(presenter, "ResumeCount");
            int coverBefore = GetPresenterInt(presenter, "CoverCount");
            int revealBefore = GetPresenterInt(presenter, "RevealCount");
            UIForm coveringForm = null;
            try
            {
                coveringForm = await GameEntry.UI.OpenUIFormAsync(1);
                if (GetPresenterInt(presenter, "PauseCount") != pauseBefore + 1 ||
                    GetPresenterInt(presenter, "CoverCount") != coverBefore + 1 ||
                    logic.View.visible ||
                    logic.View.touchable)
                {
                    throw new InvalidOperationException(
                        $"FairyGUI lifecycle cycle {cycle} did not map GF cover/pause to hidden, non-interactive state.");
                }
            }
            finally
            {
                if (coveringForm != null && GameEntry.UI.HasUIForm(coveringForm.SerialId))
                {
                    GameEntry.UI.CloseUIForm(coveringForm.SerialId);
                    await WaitForUIFormClosed(coveringForm.SerialId);
                }
            }

            if (GetPresenterInt(presenter, "ResumeCount") != resumeBefore + 1 ||
                GetPresenterInt(presenter, "RevealCount") != revealBefore + 1 ||
                !logic.View.visible ||
                !logic.View.touchable)
            {
                throw new InvalidOperationException(
                    $"FairyGUI lifecycle cycle {cycle} did not restore GF reveal/resume visibility and interaction.");
            }
        }

        private static async UniTask WaitForUIFormClosed(int serialId)
        {
            for (int frame = 0; frame < 300; frame++)
            {
                if (!GameEntry.UI.HasUIForm(serialId) && !GameEntry.UI.IsLoadingUIForm(serialId))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    return;
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            throw new InvalidOperationException(
                $"GF UIForm '{serialId}' did not close and recycle within 300 frames.");
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

        private static void AssertFairyUIFormRecycled(FairyUIFormLogic logic, string cycle)
        {
            if (logic == null || logic.View != null || logic.Presenter != null || logic.Descriptor != null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI UIForm logic retained prepared state after lifecycle cycle '{cycle}'.");
            }
        }

        private static void AssertUnifiedUIHierarchy(
            UIForm uiForm,
            FairyUIFormLogic logic,
            GComponent view)
        {
            if (uiForm.UIGroup.Helper is not GDKUIGroupHelper groupHelper)
            {
                throw new InvalidOperationException(
                    $"GF UI group '{uiForm.UIGroup.Name}' does not use the GDK FairyGUI group helper.");
            }

            Transform stageTransform = Stage.inst.gameObject.transform;
            Transform uiRoot = GameEntry.UI.transform;
            if (stageTransform.parent != uiRoot)
            {
                throw new InvalidOperationException(
                    $"FairyGUI Stage is not parented under the GF UI root. " +
                    $"stageParent='{stageTransform.parent?.name ?? "<scene root>"}', " +
                    $"uiRoot='{uiRoot?.name ?? "<missing>"}'.");
            }

            Transform rootContainerTransform = GRoot.inst.container.cachedTransform;
            if (rootContainerTransform.parent != stageTransform)
            {
                throw new InvalidOperationException(
                    $"FairyGUI GRoot is not a direct child of Stage. " +
                    $"groutParent='{rootContainerTransform.parent?.name ?? "<missing>"}', " +
                    $"stage='{stageTransform.name}'.");
            }

            Transform frameworkGroup = groupHelper.transform;
            string expectedFrameworkGroupName = $"UI Group - {uiForm.UIGroup.Name}";
            if (!string.Equals(frameworkGroup.name, expectedFrameworkGroupName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"GF UI group helper is named '{frameworkGroup.name}' instead of " +
                    $"'{expectedFrameworkGroupName}'.");
            }

            if (logic.transform.parent != frameworkGroup)
            {
                throw new InvalidOperationException(
                    $"FairyGUI host '{logic.name}' is not parented under GF UI group '{uiForm.UIGroup.Name}'.");
            }

            if ((logic.gameObject.hideFlags & HideFlags.HideInHierarchy) == 0)
            {
                throw new InvalidOperationException(
                    $"FairyGUI pooled host '{logic.name}' is visible in the runtime hierarchy.");
            }

            Transform viewTransform = view.displayObject?.gameObject?.transform;
            if (viewTransform == null ||
                !string.Equals(viewTransform.name, "MainView", StringComparison.Ordinal) ||
                viewTransform.parent != frameworkGroup)
            {
                throw new InvalidOperationException(
                    $"FairyGUI MainView is not a direct child of '{expectedFrameworkGroupName}'. " +
                    $"Actual view='{viewTransform?.name ?? "<missing>"}', " +
                    $"parent='{viewTransform?.parent?.name ?? "<missing>"}'.");
            }

            if (view.displayObject.parent == null ||
                view.displayObject.parent.parent != GRoot.inst.container ||
                view.displayObject.stage != Stage.inst)
            {
                throw new InvalidOperationException(
                    $"FairyGUI MainView is not connected to the single GRoot display tree through " +
                    $"'{expectedFrameworkGroupName}'.");
            }

            Container groupContainer = view.displayObject.parent;
            Transform rootTransform = GRoot.inst.container.cachedTransform;
            const float TransformTolerance = 0.000001f;
            if ((frameworkGroup.position - rootTransform.position).sqrMagnitude > TransformTolerance ||
                Quaternion.Angle(frameworkGroup.rotation, rootTransform.rotation) > 0.001f ||
                (frameworkGroup.lossyScale - rootTransform.lossyScale).sqrMagnitude > TransformTolerance)
            {
                throw new InvalidOperationException(
                    $"GF UI group '{expectedFrameworkGroupName}' does not match the GRoot transform. " +
                    $"Group position={frameworkGroup.position}, rotation={frameworkGroup.rotation.eulerAngles}, " +
                    $"scale={frameworkGroup.lossyScale}; root position={rootTransform.position}, " +
                    $"rotation={rootTransform.rotation.eulerAngles}, scale={rootTransform.lossyScale}.");
            }

            if (!Mathf.Approximately(groupContainer.width, GRoot.inst.width) ||
                !Mathf.Approximately(groupContainer.height, GRoot.inst.height))
            {
                throw new InvalidOperationException(
                    $"GF UI group '{expectedFrameworkGroupName}' does not match the GRoot logical size. " +
                    $"Group={groupContainer.width}x{groupContainer.height}, " +
                    $"root={GRoot.inst.width}x{GRoot.inst.height}.");
            }

            string forbiddenGroupName = $"Fairy UI Group - {uiForm.UIGroup.Name}";
            Transform fairyRoot = GRoot.inst.displayObject?.gameObject?.transform;
            Transform[] fairyNodes = fairyRoot != null
                ? fairyRoot.GetComponentsInChildren<Transform>(true)
                : Array.Empty<Transform>();
            foreach (Transform fairyNode in fairyNodes)
            {
                if (string.Equals(fairyNode.name, forbiddenGroupName, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"Legacy FairyGUI group '{forbiddenGroupName}' still exists under GRoot.");
                }
            }
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
