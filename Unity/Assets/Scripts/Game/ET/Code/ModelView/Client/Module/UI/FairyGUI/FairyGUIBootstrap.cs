using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using Game;
using Game.FairyGUI.Package1;
using GameFramework;
using UnityGameFramework.Runtime;

namespace ET.Client
{
    public static class FairyGUIBootstrap
    {
        [global::ET.StaticField]
        private static bool s_Initialized;

        public static async UniTask InitializeAsync()
        {
            if (s_Initialized)
            {
                return;
            }

            // Component/System 打开链的映射登记:UI ID -> Component 工厂(泛型 AddChild)。
            // 后续四个界面迁移时在此逐项登记;全部迁移后删除下方类 Presenter 反射扫描。
            FairyUIFormComponentRegistry.Register(
                UGFUIFormId.FairyDemoForm,
                static owner => owner.AddChild<FairyDemoFormComponent>());

            IReadOnlyDictionary<int, Func<IFairyUIPresenter>> factories =
                FairyUIPresenterRegistryBuilder.Build(typeof(FairyGUIBootstrap).Assembly);

            FairyUIPresenterRegistry.PreparePackage = descriptor =>
            {
                if (!string.Equals(descriptor.PackageName, "Package1", StringComparison.Ordinal))
                {
                    throw new GameFrameworkException(
                        $"No FairyGUI package binder is registered for UI '{descriptor.CsName}'.");
                }

                Package1Binder.BindAll();
            };

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();

            TablesComponent tables = UnityGameFramework.Runtime.GameEntry.GetComponent<TablesComponent>();
            if (tables == null)
            {
                throw new GameFrameworkException("ET FairyGUI bootstrap requires a common TablesComponent.");
            }

            await tables.LoadAllAsync();
            FairyUIManager.UIFormTableProvider = uiId => tables.DTUIForm.GetOrDefault(uiId);

            EnsureGroup(uiManager, "Default", 0);
            EnsureGroup(uiManager, "Pop", 100);
            EnsureGroup(uiManager, "Message", 200);
            EnsureGroup(uiManager, "Guide", 300);
            EnsureGroup(uiManager, "RuntimeInspector", 400);

            FairyUIPresenterRegistry.CreatePresenter = descriptor =>
            {
                if (factories.TryGetValue(descriptor.UiId, out Func<IFairyUIPresenter> factory))
                {
                    return factory();
                }

                throw new GameFrameworkException(
                    $"No FairyGUI presenter is registered for UI '{descriptor.UiId}'.");
            };

            s_Initialized = true;
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
