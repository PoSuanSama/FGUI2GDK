using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using AgentBridge;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using GameFramework.UI;
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
        private const string FailureProbeDescriptorAsset = "Assets/Res/UI/FairyGUI/Dialog.json";
        private const int FailureProbeUIId = 1;
        private const int FailureProbeRepeatCount = 100;
        private const string FairyInventoryOverlayDescriptorAsset =
            "Assets/Res/UI/FairyGUI/FairyInventoryOverlayForm.json";
        private const int FairyInventoryOverlayUIId = 106;

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

        [AgentCallable("Switch Standalone to dual-symbol mode (UNITY_ET + UNITY_GAMEHOT) so ET validation can run alongside the GameHot GameEntry flow.", 60)]
        public static void SwitchToDualSymbols()
        {
            // 仓库的 Add UNITY_ET / Add UNITY_GAMEHOT 菜单互相移除对方(且内部 domain reload
            // 会中断后续符号写入),无法产生双符号状态;ET 冒烟需要 GameHot 流程初始化
            // GameEntry 组件 + ET 流程跑 UI,因此直接给 Standalone 写入两个符号。
            UnityGameFramework.Editor.ScriptingDefineSymbols.AddScriptingDefineSymbol(
                BuildTargetGroup.Standalone, "UNITY_ET");
            UnityGameFramework.Editor.ScriptingDefineSymbols.AddScriptingDefineSymbol(
                BuildTargetGroup.Standalone, "UNITY_GAMEHOT");
        }

        [AgentCallable("Restore the repository's default per-platform symbol layout: client platforms (Android/Standalone/WebGL/WSA/iPhone) carry UNITY_GAMEHOT, Server carries UNITY_ET.", 60)]
        public static void RestoreDefaultSymbols()
        {
            // 与仓库 DefineSymbolTool 菜单同源的分平台写法(菜单已验证可写;
            // PlayerSettings.SetScriptingDefineSymbols 在本机静默无效,不要用它)。
            // helper 的 BuildTargetGroups 不含 Server,默认布局自动保持 Server=UNITY_ET。
            BuildTargetGroup[] clientGroups =
            {
                BuildTargetGroup.Standalone,
                BuildTargetGroup.iOS,
                BuildTargetGroup.Android,
                BuildTargetGroup.WSA,
                BuildTargetGroup.WebGL,
            };
            foreach (BuildTargetGroup group in clientGroups)
            {
                UnityGameFramework.Editor.ScriptingDefineSymbols.RemoveScriptingDefineSymbol(
                    group, "UNITY_ET");
                UnityGameFramework.Editor.ScriptingDefineSymbols.AddScriptingDefineSymbol(
                    group, "UNITY_GAMEHOT");
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

        [AgentCallable("Build the Windows64 IL2CPP Player package: HybridCLR Do All -> resource build -> Launcher-scene Player build (standard one-click build flow).", 3600)]
        public static void BuildWindows64PlayerPkg()
        {
            if (EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("Windows64 Player build requires EditMode.");
            }

            // 与 Book/一键打包.md 相同的标准流程:
            // 0. 首次构建前必须安装 HybridCLR(克隆 hybridclr/il2cpp_plus 到本机
            //    HybridCLRData,gitignored;CheckSettings 会拦截未安装状态);
            // 1. 资源规则补齐(否则 HotEntry.prefab 等热更入口不进包);
            // 2. HybridCLR 准备(Define Symbol Refresh + GameHot Compile Dll + Generate/All + CopyAotDlls);
            // 3. BuildHelper.BuildPkg(资源收集 + AssetBundle 构建 + Launcher 场景 Player 构建)。
            HybridCLR.Editor.Installer.InstallerController installer =
                new HybridCLR.Editor.Installer.InstallerController();
            if (!installer.HasInstalledHybridCLR())
            {
                Log.Info("HybridCLR is not initialized; installing from the configured repos first.");
                installer.InstallDefaultHybridCLR();
            }

            EnsureGameHotHotEntryResourceRule();
            HybridCLREditor.HybridCLRDoAll();
            BuildHelper.BuildPkg(Platform.Windows64);
        }

        [AgentCallable("Add the GameHot.Prefab resource rule for Assets/Res/Hot root prefabs (HotEntry.prefab was not collected by any rule).", 60)]
        public static void EnsureGameHotHotEntryResourceRule()
        {
            const string RuleName = "GameHot.Prefab";
            const string ConfigPath = "Assets/Res/Editor/Config/ResourceRuleEditor_GameHot.asset";

            UnityGameFramework.Extension.Editor.ResourceRuleEditorData data =
                AssetDatabase.LoadAssetAtPath<UnityGameFramework.Extension.Editor.ResourceRuleEditorData>(
                    ConfigPath);
            if (data == null)
            {
                throw new InvalidOperationException($"GameHot resource rule asset is missing: {ConfigPath}");
            }

            if (data.Rules.Exists(rule =>
                    string.Equals(rule.Name, RuleName, StringComparison.Ordinal)))
            {
                Log.Info($"{RuleName} resource rule already exists.");
                return;
            }

            data.Rules.Add(new UnityGameFramework.Extension.Editor.ResourceRule
            {
                Valid = true,
                Name = RuleName,
                FileSystem = "GameData",
                AssetsDirectoryPath = "Assets/Res/Hot",
                LoadType = UnityGameFramework.Editor.ResourceTools.LoadType.LoadFromFile,
                Packed = false,
                // SearchPatterns 只匹配 prefab:Hot 根下只有 HotEntry.prefab,
                // Code/Luban 子目录里是 dll.bytes/luban bytes,不会被 *.prefab 命中,
                // 与 GameHot.Code/GameHot.Luban 规则无重复收集。
                FilterType = UnityGameFramework.Extension.Editor.ResourceFilterType.Root,
                SearchPatterns = "*.prefab",
            });
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();
            Log.Info($"Added {RuleName} resource rule for Assets/Res/Hot root prefabs.");
        }

        [AgentCallable("Remove the UI.UXTool rule entry from the GameHot and ET resource rule assets (the UXTool asset directory was deleted in the zero-UGUI cleanup).", 60)]
        public static void RemoveObsoleteUXToolResourceRule()
        {
            string[] ruleConfigPaths =
            {
                ResourceRuleTool.ResourceRuleAsset_GameHot,
                ResourceRuleTool.ResourceRuleAsset_ET,
            };

            foreach (string configPath in ruleConfigPaths)
            {
                UnityGameFramework.Extension.Editor.ResourceRuleEditorData data =
                    AssetDatabase.LoadAssetAtPath<UnityGameFramework.Extension.Editor.ResourceRuleEditorData>(
                        configPath);
                if (data == null)
                {
                    throw new InvalidOperationException($"Resource rule asset is missing: {configPath}");
                }

                int removed = data.Rules.RemoveAll(rule =>
                    string.Equals(rule.Name, "UI.UXTool", StringComparison.Ordinal));
                EditorUtility.SetDirty(data);
                Log.Info($"Removed {removed} obsolete UI.UXTool rule from {configPath}.");
            }

            AssetDatabase.SaveAssets();
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

            const string ValidManifest = "{\"schemaVersion\":2,\"packages\":[{\"id\":\"a\",\"name\":\"PackageA\",\"descriptorAsset\":\"Assets/Res/UI/FairyGUI/A.bytes\",\"runtimeAssets\":[],\"dependencies\":[\"b\"]},{\"id\":\"b\",\"name\":\"PackageB\",\"descriptorAsset\":\"Assets/Res/UI/FairyGUI/B.bytes\",\"runtimeAssets\":[],\"dependencies\":[]}]}";
            IReadOnlyList<string> loadOrder = FairyPackageManager.ValidateCatalogAndGetLoadOrder(
                ValidManifest,
                "PackageA");
            if (loadOrder.Count != 2 || loadOrder[0] != "PackageB" || loadOrder[1] != "PackageA")
            {
                throw new InvalidOperationException(
                    $"FairyGUI dependency order is invalid: {string.Join(", ", loadOrder)}.");
            }

            const string CycleManifest = "{\"schemaVersion\":2,\"packages\":[{\"id\":\"a\",\"name\":\"PackageA\",\"descriptorAsset\":\"Assets/Res/UI/FairyGUI/A.bytes\",\"runtimeAssets\":[],\"dependencies\":[\"b\"]},{\"id\":\"b\",\"name\":\"PackageB\",\"descriptorAsset\":\"Assets/Res/UI/FairyGUI/B.bytes\",\"runtimeAssets\":[],\"dependencies\":[\"a\"]}]}";
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

        [AgentCallable("Validate FairyGUI manifest SHA-256 acceptance and mismatch rejection without loading Unity assets.", 30)]
        public static void ValidateFairyPackageHashContract()
        {
            const string EmptySha256 = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855";
            string manifest =
                $"{{\"schemaVersion\":2,\"packages\":[{{\"id\":\"a\",\"name\":\"PackageA\",\"descriptorAsset\":\"Assets/Res/UI/FairyGUI/A.bytes\",\"descriptorSha256\":\"{EmptySha256}\",\"runtimeAssets\":[],\"dependencies\":[]}}]}}";
            FairyPackageCatalog catalog = FairyPackageCatalog.Parse(manifest);
            catalog.VerifyAssetHash("PackageA", "Assets/Res/UI/FairyGUI/A.bytes", Array.Empty<byte>());

            bool rejected = false;
            try
            {
                catalog.VerifyAssetHash("PackageA", "Assets/Res/UI/FairyGUI/A.bytes", new byte[] { 1 });
            }
            catch (GameFrameworkException exception) when (exception.Message.Contains("hash mismatch"))
            {
                rejected = true;
            }

            if (!rejected)
            {
                throw new InvalidOperationException("FairyGUI manifest hash mismatch was not rejected.");
            }
        }

        [AgentCallable("Validate FairyGUI open failure rollback and closed Context invalidation across 100 retries per case.", 600)]
        public static async UniTask ValidateFairyUIOpenFailureCleanup()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI failure cleanup validation requires PlayMode.");
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            for (int frame = 0;
                 frame < 120 &&
                 (FairyUIPresenterRegistry.PreparePackage == null || !uiManager.HasUIGroup("Default"));
                 frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (FairyUIPresenterRegistry.PreparePackage == null || !uiManager.HasUIGroup("Default"))
            {
                throw new InvalidOperationException(
                    "FairyGUI package binding and the Default UI group must be initialized before failure cleanup validation.");
            }

            if (uiManager.IsLoadingUIForm(FailureProbeDescriptorAsset))
            {
                throw new InvalidOperationException(
                    "FairyGUI failure cleanup validation requires the dialog probe form to be idle.");
            }

            int baselineLoadedForms = uiManager.GetAllLoadedUIForms().Length;
            int baselineLoadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics = FairyPackageManager.GetDiagnostics();
            int baselineRootChildren = GRoot.inst.numChildren;
            bool baselinePackageRegistered = UIPackage.GetByName("Package1") != null;
            Action<FairyUIFormDescriptor> originalPreparePackage = FairyUIPresenterRegistry.PreparePackage;

            try
            {
                await AssertFairyUIOpenFailureRepeatedly(
                    uiManager,
                    "OnViewReady",
                    FailureProbeStage.OnViewReady,
                    baselineLoadedForms,
                    baselineLoadingForms,
                    baselineDiagnostics,
                    baselineRootChildren,
                    baselinePackageRegistered);

                await AssertFairyUIOpenFailureRepeatedly(
                    uiManager,
                    "OnOpen",
                    FailureProbeStage.OnOpen,
                    baselineLoadedForms,
                    baselineLoadingForms,
                    baselineDiagnostics,
                    baselineRootChildren,
                    baselinePackageRegistered);

                FairyUIPresenterRegistry.PreparePackage = descriptor =>
                    throw new InvalidOperationException("FairyGUI failure probe binding preparation failed.");
                await AssertFairyUIOpenFailureRepeatedly(
                    uiManager,
                    "binding preparation",
                    FailureProbeStage.None,
                    baselineLoadedForms,
                    baselineLoadingForms,
                    baselineDiagnostics,
                    baselineRootChildren,
                    baselinePackageRegistered);
            }
            finally
            {
                FairyUIPresenterRegistry.PreparePackage = originalPreparePackage;
            }

            for (int attempt = 0; attempt < FailureProbeRepeatCount; attempt++)
            {
                FairyUIForm form = await FairyUIFormService.OpenFairyUIFormAsync(
                    FailureProbeUIId,
                    new object(),
                    descriptor => new FailureProbePresenter(FailureProbeStage.None));
                FairyUIFormContext context = form.Context;
                int serial = form.SerialId;
                uiManager.CloseUIForm(serial);
                await WaitForFairyUIFormClosed(serial);
                if (context.IsAlive)
                {
                    throw new InvalidOperationException(
                        $"FairyGUI Context remained alive after its form closed on attempt {attempt}.");
                }

                bool contextRejected = false;
                try
                {
                    _ = context.LifetimeToken;
                }
                catch (ObjectDisposedException)
                {
                    contextRejected = true;
                }

                if (!contextRejected)
                {
                    throw new InvalidOperationException(
                        $"A closed FairyGUI Context still exposed its lifetime token on attempt {attempt}.");
                }

                await AssertFairyUIOpenFailureBaseline(
                    uiManager,
                    baselineLoadedForms,
                    baselineLoadingForms,
                    baselineDiagnostics,
                    baselineRootChildren,
                    baselinePackageRegistered,
                    $"closed Context validation attempt {attempt}");
            }
        }

        [AgentCallable("Validate FairyGUI descriptor resource-load failure cleanup across 100 retries.", 300)]
        public static async UniTask ValidateFairyUIResourceFailureCleanup()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI resource failure validation requires PlayMode.");
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            for (int frame = 0;
                 frame < 120 &&
                 (FairyUIPresenterRegistry.PreparePackage == null || !uiManager.HasUIGroup("Default"));
                 frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (FairyUIPresenterRegistry.PreparePackage == null || !uiManager.HasUIGroup("Default"))
            {
                throw new InvalidOperationException(
                    "FairyGUI package binding and the Default UI group must be initialized before resource failure validation.");
            }

            uiManager.CloseAllLoadedUIForms();
            uiManager.CloseAllLoadingUIForms();
            for (int frame = 0;
                 frame < 300 &&
                 (uiManager.GetAllLoadedUIForms().Length != 0 ||
                  uiManager.GetAllLoadingUIFormSerialIds().Length != 0 ||
                  FairyPackageManager.GetDiagnostics().Count != 0);
                 frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            int baselineLoadedForms = uiManager.GetAllLoadedUIForms().Length;
            int baselineLoadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics = FairyPackageManager.GetDiagnostics();
            int baselineRootChildren = GRoot.inst.numChildren;
            bool baselinePackageRegistered = UIPackage.GetByName("Package1") != null;
            if (baselineLoadedForms != 0 || baselineLoadingForms != 0 || baselineDiagnostics.Count != 0 ||
                baselinePackageRegistered)
            {
                throw new InvalidOperationException(
                    "FairyGUI resource failure validation could not establish an unloaded baseline.");
            }

            Func<ResourceComponent, string, CancellationToken, UniTask<TextAsset>> originalLoader =
                FairyPackageManager.DescriptorLoaderOverride;
            try
            {
                FairyPackageManager.Shutdown();
                FairyPackageManager.DescriptorLoaderOverride = (resource, path, cancellationToken) =>
                    UniTask.FromException<TextAsset>(
                        new GameFrameworkException(
                            $"FairyGUI resource failure probe rejected descriptor '{path}'."));

                for (int attempt = 0; attempt < FailureProbeRepeatCount; attempt++)
                {
                    bool observedFailure = false;
                    try
                    {
                        await FairyUIFormService.OpenFairyUIFormAsync(
                            FairyDemoUIId,
                            new object());
                    }
                    catch (Exception exception) when (exception.ToString().Contains("resource failure probe"))
                    {
                        observedFailure = true;
                    }

                    if (!observedFailure)
                    {
                        throw new InvalidOperationException(
                            $"FairyGUI descriptor resource failure was not observable on attempt {attempt}.");
                    }

                    await AssertFairyUIOpenFailureBaseline(
                        uiManager,
                        baselineLoadedForms,
                        baselineLoadingForms,
                        baselineDiagnostics,
                        baselineRootChildren,
                        baselinePackageRegistered,
                        $"descriptor resource failure attempt {attempt}");
                }
            }
            finally
            {
                FairyPackageManager.DescriptorLoaderOverride = originalLoader;
                FairyPackageManager.Shutdown();
            }
        }

        [AgentCallable("Validate FairyGUI generated binding type mismatch rollback across 100 retries.", 300)]
        public static async UniTask ValidateFairyUIBindingTypeMismatchCleanup()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "FairyGUI binding mismatch validation requires PlayMode.");
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            for (int frame = 0;
                 frame < 120 &&
                 (FairyUIPresenterRegistry.PreparePackage == null || !uiManager.HasUIGroup("Default"));
                 frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (FairyUIPresenterRegistry.PreparePackage == null || !uiManager.HasUIGroup("Default"))
            {
                throw new InvalidOperationException(
                    "FairyGUI package binding and the Default UI group must be initialized before binding mismatch validation.");
            }

            int baselineLoadedForms = uiManager.GetAllLoadedUIForms().Length;
            int baselineLoadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics = FairyPackageManager.GetDiagnostics();
            int baselineRootChildren = GRoot.inst.numChildren;
            bool baselinePackageRegistered = UIPackage.GetByName("Package1") != null;
            Action<FairyUIFormDescriptor> originalPreparePackage = FairyUIPresenterRegistry.PreparePackage;

            try
            {
                FairyUIPresenterRegistry.PreparePackage = descriptor =>
                {
                    originalPreparePackage(descriptor);
                    if (descriptor.UiId == FailureProbeUIId &&
                        string.Equals(descriptor.PackageName, "Package1", StringComparison.Ordinal) &&
                        string.Equals(descriptor.ComponentName, "Dialog", StringComparison.Ordinal))
                    {
                        // Keep the generated binder as the source of truth, then replace only
                        // this probe's extension with a generic component. The manager must
                        // reject it before adopting the view or presenter.
                        UIObjectFactory.SetPackageItemExtension(
                            global::Game.FairyGUI.Package1.UIDialog.URL,
                            typeof(GComponent));
                    }
                };

                for (int attempt = 0; attempt < FailureProbeRepeatCount; attempt++)
                {
                    bool observedFailure = false;
                    FairyUIForm unexpectedForm = null;
                    try
                    {
                        unexpectedForm = await FairyUIFormService.OpenFairyUIFormAsync(
                            FailureProbeUIId,
                            new object(),
                            descriptor => new FailureProbePresenter(FailureProbeStage.None));
                    }
                    catch (Exception exception) when (
                        exception.ToString().Contains("FairyGUI binding type mismatch"))
                    {
                        observedFailure = true;
                    }

                    if (unexpectedForm != null)
                    {
                        int unexpectedSerialId = unexpectedForm.SerialId;
                        uiManager.CloseUIForm(unexpectedSerialId);
                        await WaitForFairyUIFormClosed(unexpectedSerialId);
                    }

                    if (!observedFailure)
                    {
                        throw new InvalidOperationException(
                            $"FairyGUI generated binding mismatch was not observable on attempt {attempt}.");
                    }

                    await AssertFairyUIOpenFailureBaseline(
                        uiManager,
                        baselineLoadedForms,
                        baselineLoadingForms,
                        baselineDiagnostics,
                        baselineRootChildren,
                        baselinePackageRegistered,
                        $"generated binding mismatch attempt {attempt}");
                }
            }
            finally
            {
                FairyUIPresenterRegistry.PreparePackage = originalPreparePackage;
                // UIObjectFactory keeps extension creators in a process-wide map. Rebind the
                // generated table even when Package1 was unloaded during rollback.
                global::Game.FairyGUI.Package1.Package1Binder.BindAll();
            }
        }

        private static async UniTask AssertFairyUIOpenFailure(
            FairyUIManager uiManager,
            string failureName,
            FailureProbeStage failureStage,
            int baselineLoadedForms,
            int baselineLoadingForms,
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics,
            int baselineRootChildren,
            bool baselinePackageRegistered)
        {
            bool observedFailure = false;
            try
            {
                await FairyUIFormService.OpenFairyUIFormAsync(
                    FailureProbeUIId,
                    new object(),
                    descriptor => new FailureProbePresenter(failureStage));
            }
            catch (Exception exception) when (exception.ToString().Contains("failure probe"))
            {
                observedFailure = true;
            }

            if (!observedFailure)
            {
                throw new InvalidOperationException(
                    $"FairyGUI {failureName} failure probe did not propagate an observable exception.");
            }

            await AssertFairyUIOpenFailureBaseline(
                uiManager,
                baselineLoadedForms,
                baselineLoadingForms,
                baselineDiagnostics,
                baselineRootChildren,
                baselinePackageRegistered,
                failureName);
        }

        private static async UniTask AssertFairyUIOpenFailureRepeatedly(
            FairyUIManager uiManager,
            string failureName,
            FailureProbeStage failureStage,
            int baselineLoadedForms,
            int baselineLoadingForms,
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics,
            int baselineRootChildren,
            bool baselinePackageRegistered)
        {
            for (int attempt = 0; attempt < FailureProbeRepeatCount; attempt++)
            {
                await AssertFairyUIOpenFailure(
                    uiManager,
                    $"{failureName} attempt {attempt}",
                    failureStage,
                    baselineLoadedForms,
                    baselineLoadingForms,
                    baselineDiagnostics,
                    baselineRootChildren,
                    baselinePackageRegistered);
            }
        }

        private static async UniTask AssertFairyUIOpenFailureBaseline(
            FairyUIManager uiManager,
            int baselineLoadedForms,
            int baselineLoadingForms,
            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics,
            int baselineRootChildren,
            bool baselinePackageRegistered,
            string failureName)
        {
            await WaitForFairyPackageDiagnostics(baselineDiagnostics);
            if (uiManager.GetAllLoadedUIForms().Length != baselineLoadedForms ||
                uiManager.GetAllLoadingUIFormSerialIds().Length != baselineLoadingForms ||
                GRoot.inst.numChildren != baselineRootChildren ||
                (UIPackage.GetByName("Package1") != null) != baselinePackageRegistered)
            {
                throw new InvalidOperationException(
                    $"FairyGUI {failureName} failure probe left UI, package, or root state behind.");
            }
        }

        private enum FailureProbeStage
        {
            None,
            OnViewReady,
            OnOpen,
        }

        private sealed class FailureProbePresenter : IFairyUIPresenter
        {
            private readonly FailureProbeStage m_Stage;

            public FailureProbePresenter(FailureProbeStage stage)
            {
                m_Stage = stage;
            }

            public void OnViewReady(FairyUIFormContext context)
            {
                if (m_Stage == FailureProbeStage.OnViewReady)
                {
                    throw new InvalidOperationException("FairyGUI failure probe OnViewReady failed.");
                }
            }

            public void OnOpen(object userData)
            {
                if (m_Stage == FailureProbeStage.OnOpen)
                {
                    throw new InvalidOperationException("FairyGUI failure probe OnOpen failed.");
                }
            }

            public void OnClose(bool isShutdown, object userData) { }

            public void OnPause() { }

            public void OnResume() { }

            public void OnCover() { }

            public void OnReveal() { }

            public void OnRefocus(object userData) { }

            public void OnUpdate(float elapseSeconds, float realElapseSeconds) { }
        }

        [AgentCallable("Validate FairyGUI mixed full-screen and safe-area depth ordering plus invalid safe-area bounds.", 120)]
        public static async UniTask ValidateFairyUIMixedDepthAndSafeArea()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI mixed depth validation requires PlayMode.");
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            for (int frame = 0;
                 frame < 120 &&
                 (FairyUIPresenterRegistry.PreparePackage == null ||
                  !uiManager.HasUIGroup("Default") ||
                  !uiManager.HasUIGroup("Pop"));
                frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (FairyUIPresenterRegistry.PreparePackage == null ||
                !uiManager.HasUIGroup("Default") ||
                !uiManager.HasUIGroup("Pop"))
            {
                throw new InvalidOperationException(
                    "FairyGUI package binding and the Default/Pop UI groups must be initialized before mixed depth validation.");
            }

            if (uiManager.IsLoadingUIForm(FairyInventoryOverlayDescriptorAsset) ||
                uiManager.HasUIForm(FairyInventoryOverlayDescriptorAsset))
            {
                throw new InvalidOperationException(
                    "FairyGUI mixed depth validation requires the overlay form to be idle.");
            }

            IReadOnlyList<FairyPackageDiagnostic> baselineDiagnostics = FairyPackageManager.GetDiagnostics();
            int baselineLoadedForms = uiManager.GetAllLoadedUIForms().Length;
            int baselineLoadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            int baselineRootChildren = GRoot.inst.numChildren;
            bool baselinePackageRegistered = UIPackage.GetByName("Package1") != null;
            FairyUIGroupHelper groupHelper = uiManager.GetUIGroup("Pop").Helper as FairyUIGroupHelper;
            if (groupHelper == null)
            {
                throw new InvalidOperationException("FairyGUI Pop group does not use FairyUIGroupHelper.");
            }

            FairyUIForm overlay = null;
            FairyUIForm firstDetail = null;
            FairyUIForm secondDetail = null;
            try
            {
                overlay = await OpenFairyUIProbeForm(FairyInventoryOverlayUIId);
                firstDetail = await OpenFairyUIProbeForm(FairyItemDetailUIId);
                secondDetail = await OpenFairyUIProbeForm(FairyItemDetailUIId);
                await UniTask.Yield(PlayerLoopTiming.Update);

                AssertFairyUIHostOrdering(groupHelper, overlay, firstDetail, secondDetail);

                int firstDetailIndex = groupHelper.SafeAreaContainer.GetChildIndex(firstDetail.View);
                int secondDetailIndex = groupHelper.SafeAreaContainer.GetChildIndex(secondDetail.View);
                if (firstDetailIndex < 0 || secondDetailIndex <= firstDetailIndex ||
                    firstDetail.DepthInUIGroup >= secondDetail.DepthInUIGroup)
                {
                    throw new InvalidOperationException(
                        "FairyGUI safe-area forms did not preserve increasing depth order.");
                }

                uiManager.RefocusUIForm(firstDetail, new object());
                await UniTask.Yield(PlayerLoopTiming.Update);
                int refocusedDetailIndex = groupHelper.SafeAreaContainer.GetChildIndex(firstDetail.View);
                if (refocusedDetailIndex <= groupHelper.SafeAreaContainer.GetChildIndex(secondDetail.View) ||
                    firstDetail.DepthInUIGroup <= secondDetail.DepthInUIGroup)
                {
                    throw new InvalidOperationException(
                        "FairyGUI Refocus did not move the safe-area form to the top of its group.");
                }

                groupHelper.ApplySafeAreaRect(new Rect(-1000f, -1000f, -400f, -300f));
                AssertSafeAreaBounds(groupHelper, "negative safe area");
                groupHelper.ApplySafeAreaRect(
                    new Rect(-200f, -200f, Screen.width + 1000f, Screen.height + 1000f));
                AssertSafeAreaBounds(groupHelper, "oversized safe area");
                float clippedLeft = Mathf.Min(100f, Screen.width);
                float clippedRight = Mathf.Max(1f, Screen.width * 0.25f);
                float clippedTop = Mathf.Min(100f, Screen.height * 0.25f);
                float clippedBottom = Mathf.Max(clippedTop + 1f, Screen.height * 0.75f);
                Rect partiallyClipped = new Rect(
                    -clippedLeft,
                    clippedTop,
                    clippedLeft + clippedRight,
                    clippedBottom - clippedTop);
                groupHelper.ApplySafeAreaRect(partiallyClipped);
                AssertSafeAreaRect(groupHelper, partiallyClipped, "partially clipped safe area");
                GComponent safeArea = groupHelper.SafeAreaContainer;
                float previousSafeAreaX = safeArea.x;
                float previousSafeAreaY = safeArea.y;
                float previousSafeAreaWidth = safeArea.width;
                float previousSafeAreaHeight = safeArea.height;
                groupHelper.ApplySafeAreaRect(new Rect(float.NaN, 0f, 100f, 100f));
                if (!Mathf.Approximately(safeArea.x, previousSafeAreaX) ||
                    !Mathf.Approximately(safeArea.y, previousSafeAreaY) ||
                    !Mathf.Approximately(safeArea.width, previousSafeAreaWidth) ||
                    !Mathf.Approximately(safeArea.height, previousSafeAreaHeight))
                {
                    throw new InvalidOperationException(
                        "FairyGUI invalid safe area input changed the last valid layout.");
                }

                groupHelper.ApplySafeAreaRect(Screen.safeArea);
                await UniTask.Yield(PlayerLoopTiming.Update);

                await CloseFairyUIFormIfOpen(uiManager, secondDetail);
                await CloseFairyUIFormIfOpen(uiManager, firstDetail);
                await CloseFairyUIFormIfOpen(uiManager, overlay);
                secondDetail = null;
                firstDetail = await OpenFairyUIProbeForm(FairyItemDetailUIId);
                overlay = await OpenFairyUIProbeForm(FairyInventoryOverlayUIId);
                await UniTask.Yield(PlayerLoopTiming.Update);
                AssertFairyUIHostOrdering(groupHelper, overlay, firstDetail);
            }
            finally
            {
                try
                {
                    await CloseFairyUIFormIfOpen(uiManager, secondDetail);
                    await CloseFairyUIFormIfOpen(uiManager, firstDetail);
                    await CloseFairyUIFormIfOpen(uiManager, overlay);
                }
                finally
                {
                    groupHelper.ApplySafeAreaRect(Screen.safeArea);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                }
            }

            await AssertFairyUIOpenFailureBaseline(
                uiManager,
                baselineLoadedForms,
                baselineLoadingForms,
                baselineDiagnostics,
                baselineRootChildren,
                baselinePackageRegistered,
                "mixed depth");
        }

        private static UniTask<FairyUIForm> OpenFairyUIProbeForm(int uiId)
        {
            return FairyUIFormService.OpenFairyUIFormAsync(
                uiId,
                new object(),
                descriptor => new FailureProbePresenter(FailureProbeStage.None));
        }

        private static async UniTask CloseFairyUIFormIfOpen(FairyUIManager uiManager, FairyUIForm form)
        {
            if (form == null || (!uiManager.HasUIForm(form.SerialId) &&
                                 !uiManager.IsLoadingUIForm(form.SerialId)))
            {
                return;
            }

            int serialId = form.SerialId;
            uiManager.CloseUIForm(serialId);
            await WaitForFairyUIFormClosed(serialId);
        }

        private static void AssertSafeAreaBounds(FairyUIGroupHelper groupHelper, string label)
        {
            GComponent safeArea = groupHelper.SafeAreaContainer;
            if (safeArea == null || safeArea.x < -0.01f || safeArea.y < -0.01f ||
                safeArea.width < -0.01f || safeArea.height < -0.01f ||
                safeArea.x + safeArea.width > groupHelper.Container.width + 0.01f ||
                safeArea.y + safeArea.height > groupHelper.Container.height + 0.01f)
            {
                throw new InvalidOperationException(
                    $"FairyGUI {label} produced out-of-bounds safe area: " +
                    $"rect=({safeArea?.x},{safeArea?.y},{safeArea?.width},{safeArea?.height}), " +
                    $"container=({groupHelper.Container.width},{groupHelper.Container.height}).");
            }
        }

        private static void AssertSafeAreaRect(
            FairyUIGroupHelper groupHelper,
            Rect pixelSafeArea,
            string label)
        {
            float scaleFactor = GRoot.contentScaleFactor;
            float screenWidth = Screen.width;
            float screenHeight = Screen.height;
            float pixelXMin = Mathf.Clamp(pixelSafeArea.xMin, 0f, screenWidth);
            float pixelYMin = Mathf.Clamp(pixelSafeArea.yMin, 0f, screenHeight);
            float pixelXMax = Mathf.Clamp(pixelSafeArea.xMax, 0f, screenWidth);
            float pixelYMax = Mathf.Clamp(pixelSafeArea.yMax, 0f, screenHeight);
            float expectedX = pixelXMin / scaleFactor;
            float expectedY = (screenHeight - pixelYMax) / scaleFactor;
            float expectedWidth = (pixelXMax - pixelXMin) / scaleFactor;
            float expectedHeight = (pixelYMax - pixelYMin) / scaleFactor;
            GComponent safeArea = groupHelper.SafeAreaContainer;
            if (safeArea == null ||
                !Mathf.Approximately(safeArea.x, expectedX) ||
                !Mathf.Approximately(safeArea.y, expectedY) ||
                !Mathf.Approximately(safeArea.width, expectedWidth) ||
                !Mathf.Approximately(safeArea.height, expectedHeight))
            {
                throw new InvalidOperationException(
                    $"FairyGUI {label} mismatch: actual=({safeArea?.x},{safeArea?.y}," +
                    $"{safeArea?.width},{safeArea?.height}), " +
                    $"expected=({expectedX},{expectedY},{expectedWidth},{expectedHeight}).");
            }
        }

        private static void AssertFairyUIHostOrdering(
            FairyUIGroupHelper groupHelper,
            FairyUIForm overlay,
            params FairyUIForm[] safeAreaForms)
        {
            if (overlay == null || groupHelper.SafeAreaContainer == null ||
                !ReferenceEquals(overlay.View.displayObject.parent, groupHelper.Container))
            {
                throw new InvalidOperationException(
                    "FairyGUI full-screen form was attached to an unexpected group container.");
            }

            int safeContainerIndex = groupHelper.Container.GetChildIndex(
                groupHelper.SafeAreaContainer.displayObject);
            int overlayIndex = overlay.View.displayObject.parent.GetChildIndex(overlay.View.displayObject);
            if (safeContainerIndex < 0 || overlayIndex <= safeContainerIndex)
            {
                throw new InvalidOperationException(
                    $"FairyGUI full-screen ordering is invalid: safeArea={safeContainerIndex}, overlay={overlayIndex}.");
            }

            for (int i = 0; i < safeAreaForms.Length; i++)
            {
                FairyUIForm safeAreaForm = safeAreaForms[i];
                if (safeAreaForm == null ||
                    !ReferenceEquals(
                        safeAreaForm.View.displayObject.parent,
                        groupHelper.SafeAreaContainer.displayObject))
                {
                    throw new InvalidOperationException(
                        "FairyGUI safe-area form was attached to an unexpected group container.");
                }
            }
        }

        [AgentCallable("Cancel an in-flight FairyGUI open during shutdown, reinitialize in-place, and verify UI groups, Stage parents, package reload, and cleanup.", 120)]
        public static async UniTask ValidateFairyUIManagerShutdownReinitialize()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException(
                    "FairyGUI manager shutdown validation requires PlayMode.");
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            if (uiManager.UIGroupCount == 0)
            {
                throw new InvalidOperationException(
                    "FairyGUI manager shutdown validation requires registered UI groups.");
            }

            IUIGroup[] baselineGroups = uiManager.GetAllUIGroups();
            FairyUIForm[] loadedForms = uiManager.GetAllLoadedUIForms();
            for (int i = 0; i < loadedForms.Length; i++)
            {
                if (loadedForms[i] != null && uiManager.HasUIForm(loadedForms[i].SerialId))
                {
                    uiManager.CloseUIForm(loadedForms[i].SerialId);
                }
            }

            uiManager.CloseAllLoadingUIForms();
            for (int frame = 0; frame < 300 &&
                 (uiManager.GetAllLoadedUIForms().Length > 0 ||
                  uiManager.GetAllLoadingUIFormSerialIds().Length > 0); frame++)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
            }

            if (uiManager.GetAllLoadedUIForms().Length > 0 ||
                uiManager.GetAllLoadingUIFormSerialIds().Length > 0)
            {
                throw new InvalidOperationException(
                    "FairyGUI forms did not quiesce before manager shutdown validation.");
            }

            UniTask<FairyUIForm> pendingOpen = uiManager.OpenFairyUIFormAsync(FairyItemDetailUIId);
            if (pendingOpen.Status != UniTaskStatus.Pending)
            {
                throw new InvalidOperationException(
                    "FairyGUI shutdown cancellation probe did not reach an in-flight open state.");
            }

            uiManager.Shutdown();
            try
            {
                await pendingOpen;
                throw new InvalidOperationException(
                    "FairyGUI open completed after its manager was shut down.");
            }
            catch (OperationCanceledException)
            {
            }

            uiManager.Initialize();

            if (uiManager.UIGroupCount != baselineGroups.Length)
            {
                throw new InvalidOperationException(
                    $"FairyGUI UI group count changed after in-place shutdown: " +
                    $"expected={baselineGroups.Length}, actual={uiManager.UIGroupCount}.");
            }

            for (int i = 0; i < baselineGroups.Length; i++)
            {
                IUIGroup group = baselineGroups[i];
                if (group == null || !uiManager.HasUIGroup(group.Name))
                {
                    throw new InvalidOperationException(
                        $"FairyGUI UI group '{group?.Name}' was lost after in-place shutdown.");
                }

                FairyUIGroupHelper helper = uiManager.GetUIGroup(group.Name).Helper as FairyUIGroupHelper;
                if (helper == null || helper.Container.isDisposed ||
                    !ReferenceEquals(helper.Container.parent, GRoot.inst.container))
                {
                    throw new InvalidOperationException(
                        $"FairyGUI UI group '{group.Name}' has a stale Stage parent after reinitialize.");
                }
            }

            FairyUIForm reopened = await OpenFairyUIProbeForm(FairyItemDetailUIId);
            try
            {
                if (UIPackage.GetByName("Package1") == null ||
                    !ReferenceEquals(reopened.View.displayObject.parent,
                        uiManager.GetUIGroup("Pop").Helper is FairyUIGroupHelper helper
                            ? helper.SafeAreaContainer.displayObject
                            : null))
                {
                    throw new InvalidOperationException(
                        "FairyGUI package or safe-area host did not recover after manager reinitialize.");
                }
            }
            finally
            {
                await CloseFairyUIFormIfOpen(uiManager, reopened);
            }

            if (FairyPackageManager.GetDiagnostics().Count != 0 ||
                UIPackage.GetByName("Package1") != null)
            {
                throw new InvalidOperationException(
                    "FairyGUI package state did not return to baseline after shutdown reinitialize validation.");
            }
        }

        [AgentCallable("Open, refocus, owner-cancel or close, and recycle the native FairyGUI form 100 times, then verify runtime diagnostics return to baseline.", 300)]
        public static async UniTask ValidateFairyUIFormLifecycleCycles()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI lifecycle cycles require PlayMode.");
            }

            // 本测试覆盖 GameHot 原生窗口路径(类 Presenter 注册表);ET 模式走 Component/System
            // 工厂链,生命周期由 ET.FairyInventorySmokeTest/FairyFiberLifecycleSmokeTest 覆盖。
            if (FairyUIPresenterRegistry.CreatePresenter == null)
            {
                Log.Info(
                    "FairyUIForm lifecycle cycles are not applicable in ET mode; " +
                    "ET ownership lifecycle is covered by the ET smoke suite.");
                return;
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
            bool baselinePackageRegistered = UIPackage.GetByName("Package1") != null;

            using (CancellationTokenSource backgroundOwnerCancellation = new CancellationTokenSource())
            {
                UniTask<FairyUIForm> pendingBackgroundOpen = FairyUIManager.Instance.OpenFairyUIFormAsync(
                    FairyItemDetailUIId,
                    CreateItemDetailOpenData(1004),
                    backgroundOwnerCancellation.Token);
                if (pendingBackgroundOpen.Status != UniTaskStatus.Pending)
                {
                    throw new InvalidOperationException(
                        "FairyGUI background cancellation probe did not reach an in-flight open state.");
                }

                Exception cancellationCallbackFailure = null;
                Thread cancellationThread = new Thread(() =>
                {
                    try
                    {
                        backgroundOwnerCancellation.Cancel();
                    }
                    catch (Exception exception)
                    {
                        cancellationCallbackFailure = exception;
                    }
                })
                {
                    IsBackground = true,
                };
                cancellationThread.Start();
                if (!cancellationThread.Join(TimeSpan.FromSeconds(5)))
                {
                    throw new InvalidOperationException(
                        "FairyGUI background cancellation callback did not return within five seconds.");
                }

                if (cancellationCallbackFailure != null)
                {
                    throw new InvalidOperationException(
                        "FairyGUI background cancellation callback failed.",
                        cancellationCallbackFailure);
                }

                bool cancellationObserved = false;
                try
                {
                    await pendingBackgroundOpen;
                }
                catch (OperationCanceledException)
                {
                    cancellationObserved = true;
                }

                if (!cancellationObserved)
                {
                    throw new InvalidOperationException(
                        "FairyGUI open did not observe cancellation from a background thread.");
                }
            }

            await WaitForFairyPackageDiagnostics(baselineDiagnostics);
            if (FairyUIManager.Instance.GetAllLoadedUIForms().Length != baselineLoadedForms ||
                FairyUIManager.Instance.GetAllLoadingUIFormSerialIds().Length != baselineLoadingForms ||
                GRoot.inst.numChildren != baselineRootChildren ||
                (UIPackage.GetByName("Package1") != null) != baselinePackageRegistered)
            {
                throw new InvalidOperationException(
                    "FairyGUI background cancellation did not return runtime state to baseline.");
            }

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
