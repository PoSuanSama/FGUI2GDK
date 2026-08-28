using System;
using System.Reflection;
using AgentBridge;
using Cysharp.Threading.Tasks;
using ET.Client;
using Game;
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
    }
}
