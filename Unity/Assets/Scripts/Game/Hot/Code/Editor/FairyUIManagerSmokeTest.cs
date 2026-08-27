using System;
using System.Collections.Generic;
using AgentBridge;
using Cysharp.Threading.Tasks;
using Game.FairyGUI.Package1;
using UnityEditor;

namespace Game.Hot.Editor
{
    /// <summary>
    /// Stage 1 最小冒烟：经新的 FairyUIManager 原生窗口入口打开/关闭一个 FairyGUI 界面，
    /// 验证 GameFramework.UI 语义层与 FairyGUI GComponent 之间的异步桥接。
    /// </summary>
    public static class FairyUIManagerSmokeTest
    {
        [AgentCallable("Stage 1 最小冒烟：经 FairyUIManager 原生入口打开/关闭 FairyDemoForm，验证异步桥接。", 60)]
        public static async UniTask RunFairyUIManagerSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyUI manager smoke test requires PlayMode.");
            }

            EnsurePresenterRegistry();

            FairyUIManager uiManager = FairyUIManager.Instance;
            uiManager.Initialize();
            EnsureFairyUIGroup(uiManager, "Default", 0);
            EnsureFairyUIGroup(uiManager, "Pop", 100);


            FairyUIForm existingForm = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyDemoForm.json");
            if (existingForm != null)
            {
                uiManager.CloseUIForm(existingForm.SerialId);
            }

            FairyUIForm form = null;
            try
            {
                form = await uiManager.OpenFairyUIFormAsync(UIFormId.FairyDemoForm, "smoke-user-data");
                if (form == null)
                {
                    throw new InvalidOperationException("OpenFairyUIFormAsync returned null.");
                }

                if (form.View == null)
                {
                    throw new InvalidOperationException("Opened FairyUI form has no GComponent view.");
                }

                if (form.Presenter == null)
                {
                    throw new InvalidOperationException("Opened FairyUI form has no presenter.");
                }

                if (!uiManager.HasUIForm(form.SerialId))
                {
                    throw new InvalidOperationException("Opened FairyUI form is not tracked by the UI manager.");
                }

                await UniTask.Yield(PlayerLoopTiming.Update);
            }
            finally
            {
                if (form != null && uiManager.HasUIForm(form.SerialId))
                {
                    uiManager.CloseUIForm(form.SerialId);
                }
            }
        }

        private static void EnsureFairyUIGroup(FairyUIManager uiManager, string name, int depth)
        {
            if (uiManager.HasUIGroup(name))
            {
                return;
            }

            if (!uiManager.AddUIGroup(name, depth))
            {
                throw new InvalidOperationException($"Failed to add FairyGUI UI group '{name}'.");
            }
        }

        private static void EnsurePresenterRegistry()
        {
            if (FairyUIPresenterRegistry.PreparePackage != null &&
                FairyUIPresenterRegistry.CreatePresenter != null)
            {
                return;
            }

            IReadOnlyDictionary<int, Func<IFairyUIPresenter>> factories =
                FairyUIPresenterRegistryBuilder.Build(typeof(FairyUIManagerSmokeTest).Assembly);

            FairyUIPresenterRegistry.PreparePackage = descriptor =>
            {
                if (!string.Equals(descriptor.PackageName, "Package1", StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        $"No FairyGUI package binder for package '{descriptor.PackageName}'.");
                }

                Package1Binder.BindAll();
            };

            FairyUIPresenterRegistry.CreatePresenter = descriptor =>
            {
                if (factories.TryGetValue(descriptor.UiId, out Func<IFairyUIPresenter> factory))
                {
                    return factory();
                }

                throw new InvalidOperationException(
                    $"No FairyGUI presenter registered for UI '{descriptor.UiId}'.");
            };
        }
    }
}
