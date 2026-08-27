using Game.Hot.FairyGUI.Package1;
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
        public static HPBarComponent HPBar { get; private set; }
        #endregion

        private void InitComponents()
        {
            Procedure = HotComponentEntry.GetComponent<ProcedureComponent>();
            Tables = HotComponentEntry.GetComponent<TablesComponent>();

            #region Custom Components
            HPBar = HotComponentEntry.GetComponent<HPBarComponent>();
            #endregion
        }

        private static void InitializeFairyGUI()
        {
            FairyUIPresenterRegistry.PreparePackage = descriptor =>
            {
                if (!string.Equals(descriptor.CsName, nameof(FairyDemoForm), System.StringComparison.Ordinal))
                {
                    throw new GameFrameworkException(
                        $"No FairyGUI package binder is registered for UI '{descriptor.CsName}'.");
                }

                Package1Binder.BindAll();
            };
            FairyUIPresenterRegistry.CreatePresenter = descriptor =>
            {
                if (string.Equals(descriptor.CsName, nameof(FairyDemoForm), System.StringComparison.Ordinal))
                {
                    return new FairyDemoForm();
                }

                throw new GameFrameworkException(
                    $"No FairyGUI presenter is registered for UI '{descriptor.CsName}'.");
            };
        }
    }
}
