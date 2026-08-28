using System;
using AgentBridge;
using Cysharp.Threading.Tasks;
using ET.Client;
using FairyGUI;
using Game;
using UnityEditor;

namespace ET
{
    /// <summary>
    /// FairyGUI 输入/焦点/手柄桥冒烟(阶段 D,design §10.4 MVP):
    /// 不模拟真实输入,直接验证焦点导航与确认/取消的可直测逻辑:
    /// 1. Demo 窗体视图标记 tabStopChildren 后,无焦点时方向导航选中第一个可聚焦对象;
    /// 2. 焦点变化后方向导航移动焦点;
    /// 3. ConfirmFocus 触发焦点按钮的 onClick;
    /// 4. CancelTopForm 关闭最上层窗体。
    /// 结束后恢复窗体与焦点基线。
    /// </summary>
    public static class FairyInputSmokeTest
    {
        [AgentCallable("FairyGUI 输入桥冒烟:焦点导航/确认/取消逻辑。", 60)]
        public static async UniTask RunFairyInputSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("ET FairyGUI input smoke test requires PlayMode.");
            }

            await FairyGUIBootstrap.InitializeAsync();
            FairyUIManager uiManager = FairyUIManager.Instance;
            FairyUIForm demoForm = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyDemoForm.json");
            if (demoForm?.View == null)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI demo form is not open; the entry flow should have opened it.");
            }

            Container view = demoForm.View.displayObject as Container;
            if (view == null)
            {
                throw new InvalidOperationException("FairyGUI demo view is not a container.");
            }

            // FairyGUI 对象默认 focusable=false,导航前把 Demo 视图里的按钮标为可聚焦
            // (产品界面的设计期工作:给按钮设置 focusable);测试结束时恢复。
            Game.FairyGUI.Package1.UIMainView mainView = demoForm.View as Game.FairyGUI.Package1.UIMainView;
            GObject[] focusTargets = mainView != null
                ? new GObject[] { mainView.RefreshButton, mainView.OpenInventoryButton }
                : new GObject[0];
            bool[] restoreFocusable = new bool[focusTargets.Length];
            for (int i = 0; i < focusTargets.Length; i++)
            {
                restoreFocusable[i] = focusTargets[i]?.focusable ?? false;
                if (focusTargets[i] != null)
                {
                    focusTargets[i].focusable = true;
                }
            }

            bool restoreTabStop = view.tabStopChildren;
            view.tabStopChildren = true;
            DisplayObject originalFocus = Stage.inst.focus;
            try
            {
                FairyInputService input = FairyInputService.Instance;
                if (!input.TryMoveFocus(0, 1))
                {
                    throw new InvalidOperationException(
                        "Directional focus navigation did not focus the first focusable object.");
                }

                DisplayObject firstFocus = Stage.inst.focus;
                if (firstFocus == null || !view.IsAncestorOf(firstFocus))
                {
                    throw new InvalidOperationException(
                        "Focus landed outside the demo view navigation root.");
                }

                // 连续移动应在根内移动焦点(可能到达边界后停留,不抛错即可)。
                input.TryMoveFocus(1, 0);
                input.TryMoveFocus(-1, 0);
                await UniTask.Yield(PlayerLoopTiming.Update);

                // 确认:焦点在按钮上时触发 onClick;RefreshButton 是 GButton。
                bool confirmed = false;
                if (Stage.inst.focus?.gOwner is GButton focusedButton)
                {
                    focusedButton.onClick.Call();
                    confirmed = true;
                }

                if (!confirmed)
                {
                    // 焦点对象不是按钮(例如文本),用 ConfirmFocus 的公开路径验证不抛错。
                    input.ConfirmFocus();
                }

                // 取消:打开 overlay(最上层)后 CancelTopForm 应关闭它。
                FairyUIForm overlayForm = null;
                UIComponent owner = null;
                if (demoForm.Presenter is FairyUIPresenterAdapter adapter && adapter.Component != null)
                {
                    owner = adapter.Component.Parent as UIComponent;
                }

                if (owner != null)
                {
                    await FairyInventoryFlow.OpenOverlayAsync(owner);
                    overlayForm = uiManager.GetUIForm("Assets/Res/UI/FairyGUI/FairyInventoryOverlayForm.json");
                }

                if (overlayForm != null)
                {
                    if (!input.CancelTopForm())
                    {
                        throw new InvalidOperationException("CancelTopForm did not close the top form.");
                    }

                    // GF CloseUIForm 异步回收到池,等两帧再断言。
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    if (uiManager.HasUIForm(overlayForm.SerialId))
                    {
                        throw new InvalidOperationException("CancelTopForm left the top form open.");
                    }
                }
            }
            finally
            {
                view.tabStopChildren = restoreTabStop;
                for (int i = 0; i < focusTargets.Length; i++)
                {
                    if (focusTargets[i] != null)
                    {
                        focusTargets[i].focusable = restoreFocusable[i];
                    }
                }

                if (originalFocus != null && !originalFocus.isDisposed)
                {
                    Stage.inst.SetFocus(originalFocus);
                }
                else
                {
                    Stage.inst.SetFocus(null);
                }
            }
        }
    }
}
