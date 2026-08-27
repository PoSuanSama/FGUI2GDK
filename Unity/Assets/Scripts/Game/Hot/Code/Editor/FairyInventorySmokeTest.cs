using System;
using System.Collections.Generic;
using AgentBridge;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Game.Hot.FairyGUI.Package1;
using UnityEditor;

namespace Game.Hot.Editor
{
    public static class FairyInventorySmokeTest
    {
        private const string InventoryAsset = "Assets/Res/UI/FairyGUI/FairyInventoryForm.json";
        private const string OverlayAsset = "Assets/Res/UI/FairyGUI/FairyInventoryOverlayForm.json";

        [AgentCallable("在 PlayMode 验证背包界面 Controller、原生 GList、多实例详情浮窗、点击置顶与覆盖/恢复流程。", 120)]
        public static async UniTask RunFairyInventorySmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("FairyGUI inventory smoke test requires PlayMode.");
            }

            try
            {
                await VerifyInventoryAndMultiWindowFlow();
            }
            finally
            {
                TryCloseAll();
            }
        }

        private static async UniTask VerifyInventoryAndMultiWindowFlow()
        {
            await FairyInventoryFlow.OpenInventoryAsync();

            FairyUIForm inventoryForm = FairyUIManager.Instance.GetUIForm(InventoryAsset);
            if (inventoryForm == null)
            {
                throw new InvalidOperationException("Inventory FairyGUI form did not open.");
            }

            UIInventoryView inventoryView = inventoryForm.View as UIInventoryView;
            if (inventoryView == null)
            {
                throw new InvalidOperationException("Inventory form did not expose UIInventoryView.");
            }

            if (inventoryView.Category == null || inventoryView.ItemList == null)
            {
                throw new InvalidOperationException("Inventory controller or list is missing.");
            }

            if (HotEntry.Tables.DTInventory == null || HotEntry.Tables.DTInventory.DataList.Count <= 0)
            {
                throw new InvalidOperationException("Inventory Luban table is not loaded.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (inventoryView.ItemList.numChildren <= 0)
            {
                throw new InvalidOperationException("Inventory list is empty after layout.");
            }

            UIInventoryItem firstItem = inventoryView.ItemList.GetChildAt(0) as UIInventoryItem;
            FairyInventoryItemData itemData = firstItem?.data as FairyInventoryItemData;
            if (itemData == null)
            {
                throw new InvalidOperationException("Inventory item is missing bound item data.");
            }

            for (int i = 0; i < 3; i++)
            {
                await FairyInventoryFlow.OpenDetailAsync(itemData);
            }

            if (FairyInventoryFlow.OpenDetailCount != 3)
            {
                throw new InvalidOperationException($"Expected 3 detail forms, found {FairyInventoryFlow.OpenDetailCount}.");
            }

            List<FairyUIForm> detailForms = new List<FairyUIForm>(FairyInventoryFlow.OpenDetailForms);
            if (detailForms.Count != 3)
            {
                throw new InvalidOperationException("Open detail form snapshot does not match the expected count.");
            }

            FairyItemDetailForm targetPresenter = null;
            GGraph targetFrame = null;
            UIItemDetailWindow targetDetailView = null;
            foreach (FairyUIForm detailForm in detailForms)
            {
                FairyItemDetailForm presenter = detailForm.Presenter as FairyItemDetailForm;
                if (presenter != null && detailForm.View is UIItemDetailWindow detailView)
                {
                    targetPresenter = presenter;
                    targetFrame = detailView.WindowFrame;
                    targetDetailView = detailView;
                    break;
                }
            }

            if (targetPresenter == null || targetFrame == null || targetDetailView == null)
            {
                throw new InvalidOperationException("Could not locate a detail presenter and window frame.");
            }

            if (!targetDetailView.WindowFrame.draggable || targetDetailView.WindowFrame.dragBounds == null)
            {
                throw new InvalidOperationException("Detail window is not draggable.");
            }

            int refocusBefore = targetPresenter.RefocusCount;
            targetFrame.onClick.Call();
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (targetPresenter.RefocusCount != refocusBefore + 1)
            {
                throw new InvalidOperationException("Clicking the detail frame did not refocus the form.");
            }

            int coverBase = targetPresenter.CoverCount;
            int pauseBase = targetPresenter.PauseCount;
            int revealBase = targetPresenter.RevealCount;
            int resumeBase = targetPresenter.ResumeCount;

            await FairyInventoryFlow.OpenOverlayAsync();
            FairyUIForm overlayForm = FairyUIManager.Instance.GetUIForm(OverlayAsset);
            if (overlayForm == null)
            {
                throw new InvalidOperationException("Overlay FairyGUI form did not open.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (targetPresenter.CoverCount != coverBase + 1 || targetPresenter.PauseCount != pauseBase + 1)
            {
                throw new InvalidOperationException("Detail form was not covered and paused by the overlay.");
            }

            FairyUIManager.Instance.CloseUIForm(overlayForm.SerialId);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (targetPresenter.RevealCount != revealBase + 1 || targetPresenter.ResumeCount != resumeBase + 1)
            {
                throw new InvalidOperationException("Detail form was not revealed and resumed after the overlay closed.");
            }
        }

        private static void TryCloseAll()
        {
            try
            {
                FairyInventoryFlow.CloseAllDetails();
            }
            catch
            {
            }

            TryClose(OverlayAsset);
            TryClose(InventoryAsset);
        }

        private static void TryClose(string assetName)
        {
            try
            {
                FairyUIForm uiForm = FairyUIManager.Instance.GetUIForm(assetName);
                if (uiForm != null && FairyUIManager.Instance.HasUIForm(uiForm.SerialId))
                {
                    FairyUIManager.Instance.CloseUIForm(uiForm.SerialId);
                }
            }
            catch
            {
            }
        }
    }
}