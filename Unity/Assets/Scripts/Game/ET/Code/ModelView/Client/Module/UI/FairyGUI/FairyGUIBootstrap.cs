using System;
using Cysharp.Threading.Tasks;
using Game;
using Game.FairyGUI;
using GameFramework;
using UnityGameFramework.Runtime;

namespace ET.Client
{
    public static class FairyGUIBootstrap
    {
        [global::ET.StaticField]
        private static bool s_Initialized;

        [global::ET.StaticField]
        private static long s_LifecycleGeneration;

        [global::ET.StaticField]
        private static readonly Action<FairyUIFormDescriptor> s_PreparePackage = PreparePackage;

        internal static bool IsPreparePackageRegistered =>
            FairyUIPresenterRegistry.PreparePackage == s_PreparePackage;

        public static async UniTask InitializeAsync()
        {
            if (s_Initialized)
            {
                return;
            }

            FairyUIManager.ETRuntimeShutdownCompleted -= HandleETRuntimeShutdownCompleted;
            FairyUIManager.ETRuntimeShutdownCompleted += HandleETRuntimeShutdownCompleted;
            long generation = s_LifecycleGeneration;

            // Component/System 打开链的映射登记:UI ID -> Component 工厂(泛型 AddChild)。
            FairyUIFormComponentRegistry.Register(
                UGFUIFormId.FairyDemoForm,
                static owner => owner.AddChild<FairyDemoFormComponent>());
            FairyUIFormComponentRegistry.Register(
                UGFUIFormId.FairyInventoryForm,
                static owner => owner.AddChild<FairyInventoryFormComponent>());
            FairyUIFormComponentRegistry.Register(
                UGFUIFormId.FairyItemDetailForm,
                static owner => owner.AddChild<FairyItemDetailFormComponent>());
            FairyUIFormComponentRegistry.Register(
                UGFUIFormId.FairyInventoryOverlayForm,
                static owner => owner.AddChild<FairyInventoryOverlayFormComponent>());
            FairyUIFormComponentRegistry.Register(
                UGFUIFormId.FairyRuntimeInspectorForm,
                static owner => owner.AddChild<FairyRuntimeInspectorFormComponent>());

            // ET 全部界面走 Component/System 打开链(per-open 工厂),不再注册类 Presenter;
            // 未命中注册表的打开会由 FairyUIManager 直接报稳定错误。
            FairyUIPresenterRegistry.PreparePackage = s_PreparePackage;

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            // 声音桥:按钮/transition 播放统一重定向到 GDK Sound 组。
            FairySound.Initialize();
            // 输入/焦点/手柄桥:方向导航 + 确认/取消映射。
            FairyInputService.Instance.Initialize();

            // 双符号启动竞态:GameEntry.Base/CodeRunner 就绪不代表 TablesComponent 已注册。
            // 有界等待 Tables 就绪,超时才抛稳定错误;避免首次调用半途失败留下半初始化状态。
            TablesComponent tables = UnityGameFramework.Runtime.GameEntry.GetComponent<TablesComponent>();
            int waitFrames = 0;
            while (tables == null && waitFrames < 120)
            {
                await UniTask.Yield(PlayerLoopTiming.Update);
                tables = UnityGameFramework.Runtime.GameEntry.GetComponent<TablesComponent>();
                waitFrames++;
            }

            ThrowIfLifecycleChanged(generation);
            if (tables == null)
            {
                throw new GameFrameworkException("ET FairyGUI bootstrap requires a common TablesComponent.");
            }

            await tables.LoadAllAsync();
            ThrowIfLifecycleChanged(generation);
            FairyUIManager.UIFormTableProvider = uiId => tables.DTUIForm.GetOrDefault(uiId);

            EnsureGroup(uiManager, "Default", 0);
            EnsureGroup(uiManager, "Pop", 100);
            EnsureGroup(uiManager, "Message", 200);
            EnsureGroup(uiManager, "Guide", 300);
            EnsureGroup(uiManager, "RuntimeInspector", 400);

            s_Initialized = true;
        }

        private static void PreparePackage(FairyUIFormDescriptor descriptor)
        {
            FairyPackageBinderRegistryGenerated.BindAllPackages(descriptor);
        }

        private static void HandleETRuntimeShutdownCompleted()
        {
            s_LifecycleGeneration++;
            s_Initialized = false;
            FairyUIFormComponentRegistry.Clear();
            UIComponentFairyUIBridge.Reset();
            if (FairyUIPresenterRegistry.PreparePackage == s_PreparePackage)
            {
                FairyUIPresenterRegistry.PreparePackage = null;
            }
        }

        private static void ThrowIfLifecycleChanged(long generation)
        {
            if (generation != s_LifecycleGeneration)
            {
                throw new OperationCanceledException(
                    "ET FairyGUI bootstrap belongs to a previous runtime generation.");
            }
        }

        private static void EnsureGroup(FairyUIManager uiManager, string name, int depth)
        {
            if (uiManager.HasUIGroup(name))
            {
                return;
            }

            if (!uiManager.AddUIGroup(name, depth))
            {
                throw new GameFrameworkException($"Failed to add FairyGUI UI group '{name}'.");
            }
        }
    }
}
