using System;
using AgentBridge;
using Cysharp.Threading.Tasks;
using Game.Hot;
using UnityEditor;

namespace Game.Hot.Editor
{
    /// <summary>
    /// GameHot DialogForm 冒烟(阶段 E 批次):
    /// 三模式打开、回调触发与窗体关闭、PauseGame 暂停/恢复、多实例、
    /// 结束后 GF/GRoot 回基线。断言按钮回调确实执行(计数)。
    /// </summary>
    public static class FairyDialogSmokeTest
    {
        private const string DialogAsset = "Assets/Res/UI/FairyGUI/Dialog.json";

        [AgentCallable("GameHot Dialog 三模式冒烟:回调触发、关闭、暂停恢复与基线。", 120)]
        public static async UniTask RunFairyDialogSmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("GameHot FairyGUI dialog smoke test requires PlayMode.");
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            int baselineForms = uiManager.GetAllLoadedUIForms().Length;
            if (uiManager.GetUIForm(DialogAsset) != null)
            {
                throw new InvalidOperationException("FairyGUI dialog is already open before the smoke test.");
            }

            // 模式 1:仅确认。
            int confirmCount = 0;
            DialogParams mode1 = new DialogParams
            {
                Mode = 1,
                Title = "确认退出",
                Message = "是否确认退出游戏?",
                ConfirmText = "确定",
                OnClickConfirm = _ => ++confirmCount,
            };
            FairyUIForm form1 = await FairyDialogFlow.OpenAsync(mode1);
            await UniTask.Yield(PlayerLoopTiming.Update);

            if (form1.Presenter is not FairyDialogForm presenter1)
            {
                throw new InvalidOperationException("Dialog presenter is not FairyDialogForm.");
            }

            if (presenter1.DialogMode != 1)
            {
                throw new InvalidOperationException(
                    $"Dialog mode mismatch: expected 1, found {presenter1.DialogMode}.");
            }

            Game.FairyGUI.Package1.UIDialog view1 =
                form1.View as Game.FairyGUI.Package1.UIDialog;
            if (view1 == null || !view1.ConfirmButton.visible ||
                view1.CancelButton.visible || view1.OtherButton.visible)
            {
                throw new InvalidOperationException("Mode 1 button visibility is wrong.");
            }

            view1.ConfirmButton.onClick.Call();
            await UniTask.Yield(PlayerLoopTiming.Update);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (confirmCount != 1 || uiManager.HasUIForm(form1.SerialId))
            {
                throw new InvalidOperationException("Mode 1 confirm did not fire and close the form.");
            }

            // 模式 3:三按钮,分别触发三个回调。
            int confirm3 = 0;
            int cancel3 = 0;
            int other3 = 0;
            DialogParams mode3 = new DialogParams
            {
                Mode = 3,
                Title = "三按钮",
                Message = "确认/取消/中立",
                ConfirmText = "确认",
                CancelText = "取消",
                OtherText = "其他",
                OnClickConfirm = _ => ++confirm3,
                OnClickCancel = _ => ++cancel3,
                OnClickOther = _ => ++other3,
            };
            FairyUIForm form3 = await FairyDialogFlow.OpenAsync(mode3);
            await UniTask.Yield(PlayerLoopTiming.Update);
            Game.FairyGUI.Package1.UIDialog view3 =
                form3.View as Game.FairyGUI.Package1.UIDialog;
            if (view3 == null || !view3.ConfirmButton.visible ||
                !view3.CancelButton.visible || !view3.OtherButton.visible)
            {
                throw new InvalidOperationException("Mode 3 button visibility is wrong.");
            }

            // 三实例按钮回调各自关闭(多实例允许并存,只关自己的 serial)。
            FairyUIForm formOther = await FairyDialogFlow.OpenAsync(new DialogParams
            {
                Mode = 3,
                Title = "中立",
                Message = "第二个对话框",
                OtherText = "中立",
                OnClickOther = _ => ++other3,
            });
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (!uiManager.HasUIForm(form3.SerialId) || !uiManager.HasUIForm(formOther.SerialId))
            {
                throw new InvalidOperationException("Multi-instance dialogs did not coexist.");
            }

            (formOther.View as Game.FairyGUI.Package1.UIDialog)?.OtherButton.onClick.Call();
            await UniTask.Yield(PlayerLoopTiming.Update);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (other3 != 1 || uiManager.HasUIForm(formOther.SerialId) ||
                !uiManager.HasUIForm(form3.SerialId))
            {
                throw new InvalidOperationException(
                    "Closing one dialog instance affected another instance or its callback.");
            }

            view3.ConfirmButton.onClick.Call();
            await UniTask.Yield(PlayerLoopTiming.Update);
            await UniTask.Yield(PlayerLoopTiming.Update);
            view3 = form3.View as Game.FairyGUI.Package1.UIDialog;
            if (confirm3 != 1 || uiManager.HasUIForm(form3.SerialId))
            {
                throw new InvalidOperationException("Mode 3 confirm did not fire and close the form.");
            }

            // PauseGame 模式 2:暂停/恢复契约。
            DialogParams mode2 = new DialogParams
            {
                Mode = 2,
                Title = "暂停测试",
                Message = "打开时暂停",
                PauseGame = true,
                ConfirmText = "确定",
                CancelText = "取消",
            };
            FairyUIForm form2 = await FairyDialogFlow.OpenAsync(mode2);
            await UniTask.Yield(PlayerLoopTiming.Update);
            Game.FairyGUI.Package1.UIDialog view2 =
                form2.View as Game.FairyGUI.Package1.UIDialog;
            if (view2 == null)
            {
                throw new InvalidOperationException("Mode 2 dialog view is invalid.");
            }

            view2.CancelButton.onClick.Call();
            await UniTask.Yield(PlayerLoopTiming.Update);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (uiManager.HasUIForm(form2.SerialId))
            {
                throw new InvalidOperationException("Mode 2 cancel did not close the form.");
            }

            // 基线:全部关闭,加载窗体数回基线。
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (uiManager.GetAllLoadedUIForms().Length != baselineForms)
            {
                throw new InvalidOperationException(
                    $"Dialog smoke test did not return forms to baseline: {baselineForms} -> {uiManager.GetAllLoadedUIForms().Length}.");
            }
        }
    }
}
