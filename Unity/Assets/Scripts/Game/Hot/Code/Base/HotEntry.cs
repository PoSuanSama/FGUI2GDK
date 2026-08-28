using System;
using System.Collections.Generic;
using Game.FairyGUI.Package1;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public sealed class HotEntry : MonoBehaviour
    {
        /// <summary>
        /// 程序入口
        /// </summary>
        /// <returns></returns>
        private void Start()
        {
            Log.Info("Game.Hot.Code Start!");
            
            InitComponents();
            InitializeFairyGUI();
            
            HotComponentEntry.Initialize();
            
            // 开启流程（入口）
            Procedure.StartProcedure<ProcedureLaunch>();
        }

        private void Update()
        {
            HotComponentEntry.Update(Time.deltaTime, Time.unscaledDeltaTime);
        }

        private void OnDestroy()
        {
            FairyUIPresenterRegistry.PreparePackage = null;
            FairyUIPresenterRegistry.CreatePresenter = null;
            HotComponentEntry.Shutdown();
        }

        public static ProcedureComponent Procedure { get; private set; }
        public static TablesComponent Tables { get; private set; }

        #region Custom Components
        #endregion

        private void InitComponents()
        {
            Procedure = HotComponentEntry.GetComponent<ProcedureComponent>();
            Tables = HotComponentEntry.GetComponent<TablesComponent>();

            #region Custom Components
            #endregion
        }

        private static void InitializeFairyGUI()
        {
            IReadOnlyDictionary<int, Func<IFairyUIPresenter>> presenterFactories =
                FairyUIPresenterRegistryBuilder.Build(typeof(HotEntry).Assembly);

            FairyUIPresenterRegistry.PreparePackage = descriptor =>
            {
                if (!string.Equals(descriptor.PackageName, "Package1", System.StringComparison.Ordinal))
                {
                    throw new GameFrameworkException(
                        $"No FairyGUI package binder is registered for UI '{descriptor.CsName}'.");
                }

                Package1Binder.BindAll();
            };
            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            // 声音桥:按钮/transition 播放统一重定向到 GDK Sound 组。
            FairySound.Initialize();
            // 输入/焦点/手柄桥:方向导航 + 确认/取消映射。
            FairyInputService.Instance.Initialize();
            uiManager.AddUIGroup("Default", 0);
            uiManager.AddUIGroup("Pop", 100);
            uiManager.AddUIGroup("Message", 200);
            uiManager.AddUIGroup("Guide", 300);
            uiManager.AddUIGroup("RuntimeInspector", 400);

            FairyUIPresenterRegistry.CreatePresenter = descriptor =>
            {
                if (presenterFactories.TryGetValue(descriptor.UiId, out Func<IFairyUIPresenter> factory))
                {
                    return factory();
                }

                throw new GameFrameworkException(
                    $"No FairyGUI presenter is registered for UI '{descriptor.UiId}'.");
            };
        }
    }
}
