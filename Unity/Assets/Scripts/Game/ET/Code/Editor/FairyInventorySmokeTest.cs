using System;
using System.Collections.Generic;
using AgentBridge;
using Cysharp.Threading.Tasks;
using ET.Client;
using Game;
using UnityEditor;

namespace ET
{
    public static class FairyInventorySmokeTest
    {
        private const string InventoryAsset = "Assets/Res/UI/FairyGUI/FairyInventoryForm.json";
        private const string DemoAsset = "Assets/Res/UI/FairyGUI/FairyDemoForm.json";
        private const string DetailAsset = "Assets/Res/UI/FairyGUI/FairyItemDetailForm.json";
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

            FairyUIForm demoForm = uiManager.GetUIForm(DemoAsset);
            ET.Client.FairyDemoPresenter demoPresenter = demoForm?.Presenter as ET.Client.FairyDemoPresenter;
            ET.Client.UIComponent owner = demoPresenter?.LastOpenUserData as ET.Client.UIComponent;
            if (owner == null || owner.IsDisposed || !owner.OwnsFairyUIForm(demoForm.SerialId))
            {
                throw new InvalidOperationException("ET FairyGUI demo is not owned by a live UIComponent.");
            }

            await ET.Client.FairyInventoryFlow.OpenInventoryAsync(owner);
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
                await ET.Client.FairyInventoryFlow.OpenDetailAsync(owner, itemData);
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
            if (!owner.RefocusFairyUIForm(detailForm.SerialId))
            {
                throw new InvalidOperationException("ET UI owner could not refocus its detail serial.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (detailPresenter.RefocusCount != refocusBefore + 1)
            {
                throw new InvalidOperationException("ET detail form did not refocus.");
            }

            int coverBase = detailPresenter.CoverCount;
            int pauseBase = detailPresenter.PauseCount;
            int revealBase = detailPresenter.RevealCount;
            int resumeBase = detailPresenter.ResumeCount;

            await ET.Client.FairyInventoryFlow.OpenOverlayAsync(owner);
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

            if (!owner.CloseFairyUIForm(overlayForm.SerialId))
            {
                throw new InvalidOperationException("ET UI owner could not close its overlay serial.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (detailPresenter.RevealCount != revealBase + 1 || detailPresenter.ResumeCount != resumeBase + 1)
            {
                throw new InvalidOperationException("ET detail form was not revealed and resumed after the overlay closed.");
            }

            if (!owner.CloseFairyUIForm(inventoryForm.SerialId))
            {
                throw new InvalidOperationException("ET UI owner could not close its inventory serial.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (ET.Client.FairyInventoryFlow.OpenDetailCount != 0)
            {
                throw new InvalidOperationException("Closing the owned inventory did not close all owned detail forms.");
            }

            await ValidateOwnedLifecycleAsync(owner, itemData, uiManager);
        }

        private static async UniTask ValidateOwnedLifecycleAsync(
            ET.Client.UIComponent owner,
            ET.Client.FairyInventoryItemData itemData,
            FairyUIManager uiManager)
        {
            int ownedBaseline = owner.GetOwnedFairyUIFormCount();
            List<FairyUIForm> forms = new List<FairyUIForm>();
            for (int i = 0; i < 3; i++)
            {
                ET.Client.FairyItemDetailOpenData openData =
                    new ET.Client.FairyItemDetailOpenData(owner, itemData, 10000 + i);
                FairyUIForm form = await owner.OpenFairyUIFormAsync(
                    UGFUIFormId.FairyItemDetailForm,
                    openData);
                openData.Attach(form);
                forms.Add(form);
            }

            if (owner.GetOwnedFairyUIFormCount() != ownedBaseline + 3)
            {
                throw new InvalidOperationException("Three same-asset FairyGUI forms did not receive independent ET ownership.");
            }

            int middleSerialId = forms[1].SerialId;
            if (!owner.CloseFairyUIForm(middleSerialId) || owner.CloseFairyUIForm(middleSerialId))
            {
                throw new InvalidOperationException("Owned FairyGUI close is not idempotent by serial ID.");
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (!uiManager.HasUIForm(forms[0].SerialId) ||
                uiManager.HasUIForm(middleSerialId) ||
                !uiManager.HasUIForm(forms[2].SerialId))
            {
                throw new InvalidOperationException("Closing one same-asset serial affected another ET-owned instance.");
            }

            Scene root = owner.Root();
            ET.Client.FairyInventoryOverlayOpenData pendingData =
                new ET.Client.FairyInventoryOverlayOpenData(owner);
            UniTask<FairyUIForm> pendingOpen = owner.OpenFairyUIFormAsync(
                UGFUIFormId.FairyInventoryOverlayForm,
                pendingData);
            int firstSerialId = forms[0].SerialId;
            int thirdSerialId = forms[2].SerialId;

            owner.Dispose();
            FairyUIForm pendingForm = null;
            try
            {
                pendingForm = await pendingOpen;
            }
            catch (OperationCanceledException)
            {
            }

            await UniTask.Yield(PlayerLoopTiming.Update);
            if (uiManager.HasUIForm(firstSerialId) ||
                uiManager.HasUIForm(thirdSerialId) ||
                (pendingForm != null && uiManager.HasUIForm(pendingForm.SerialId)))
            {
                throw new InvalidOperationException("Destroying UIComponent left an owned FairyGUI serial open.");
            }

            ET.Client.UIComponent replacementOwner = root.AddComponent<ET.Client.UIComponent>();
            FairyUIForm replacementDemo = await replacementOwner.OpenFairyUIFormAsync(
                UGFUIFormId.FairyDemoForm,
                replacementOwner);
            if (!replacementOwner.OwnsFairyUIForm(replacementDemo.SerialId) ||
                uiManager.GetUIForm(DetailAsset) != null)
            {
                throw new InvalidOperationException("Replacement ET UI owner did not return to a clean FairyGUI baseline.");
            }
        }
    }
}
