using System;
using AgentBridge;
using Cysharp.Threading.Tasks;
using Game;
using UnityEditor;

namespace ET
{
    public static class FairyInventorySmokeTest
    {
        private const string InventoryAsset = "Assets/Res/UI/FairyGUI/FairyInventoryForm.json";
        private const string OverlayAsset = "Assets/Res/UI/FairyGUI/FairyInventoryOverlayForm.json";

        [AgentCallable("ET 模式完整复刻 FairyGUI 背包演示：打开背包、多实例详情浮窗、点击置顶与遮罩覆盖/恢复。", 120)]
        public static async UniTask RunFairyInventorySmokeTest()
        {
            if (!EditorApplication.isPlaying)
            {
                throw new InvalidOperationException("ET FairyGUI inventory smoke test requires PlayMode.");
            }

            await ET.Client.FairyGUIBootstrap.InitializeAsync();

            FairyUIManager uiManager = FairyUIManager.Instance;
            if (!uiManager.HasUIGroup("Default") || !uiManager.HasUIGroup("Pop"))
            {
                throw new InvalidOperationException("FairyUIManager is missing Default or Pop UI group.");
            }

            await ET.Client.FairyInventoryFlow.OpenInventoryAsync();
            FairyUIForm inventoryForm = uiManager.GetUIForm(InventoryAsset);
            if (inventoryForm == null)
            {
                throw new InvalidOperationException("ET FairyGUI inventory form did not open.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (Tables.Instance.DTInventory == null || Tables.Instance.DTInventory.DataList.Count <= 0)
            {
                throw new InvalidOperationException("ET inventory Luban table is not loaded.");
            }

            DRInventory firstRow = Tables.Instance.DTInventory.DataList[0];
            ET.Client.FairyInventoryCategory category = (ET.Client.FairyInventoryCategory)firstRow.Category;
            ET.Client.FairyInventoryItemData itemData = new ET.Client.FairyInventoryItemData(
                firstRow.Id,
                firstRow.Name,
                category,
                firstRow.Count,
                firstRow.Description);

            for (int i = 0; i < 3; i++)
            {
                await ET.Client.FairyInventoryFlow.OpenDetailAsync(itemData);
            }

            if (ET.Client.FairyInventoryFlow.OpenDetailCount != 3)
            {
                throw new InvalidOperationException(
                    $"Expected 3 ET detail forms, found {ET.Client.FairyInventoryFlow.OpenDetailCount}.");
            }

            FairyUIForm detailForm = null;
            foreach (FairyUIForm form in ET.Client.FairyInventoryFlow.OpenDetailForms)
            {
                detailForm = form;
                break;
            }

            ET.Client.FairyItemDetailPresenter detailPresenter = detailForm?.Presenter as ET.Client.FairyItemDetailPresenter;
            if (detailPresenter == null)
            {
                throw new InvalidOperationException("Could not locate an ET detail presenter.");
            }

            int refocusBefore = detailPresenter.RefocusCount;
            uiManager.RefocusUIForm(detailForm);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (detailPresenter.RefocusCount != refocusBefore + 1)
            {
                throw new InvalidOperationException("ET detail form did not refocus.");
            }

            int coverBase = detailPresenter.CoverCount;
            int pauseBase = detailPresenter.PauseCount;
            int revealBase = detailPresenter.RevealCount;
            int resumeBase = detailPresenter.ResumeCount;

            await ET.Client.FairyInventoryFlow.OpenOverlayAsync();
            FairyUIForm overlayForm = uiManager.GetUIForm(OverlayAsset);
            if (overlayForm == null)
            {
                throw new InvalidOperationException("ET FairyGUI overlay form did not open.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (detailPresenter.CoverCount != coverBase + 1 || detailPresenter.PauseCount != pauseBase + 1)
            {
                throw new InvalidOperationException("ET detail form was not covered and paused by the overlay.");
            }

            uiManager.CloseUIForm(overlayForm.SerialId);
            await UniTask.Yield(PlayerLoopTiming.Update);
            if (detailPresenter.RevealCount != revealBase + 1 || detailPresenter.ResumeCount != resumeBase + 1)
            {
                throw new InvalidOperationException("ET detail form was not revealed and resumed after the overlay closed.");
            }
        }
    }
}
