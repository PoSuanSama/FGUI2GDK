using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;

namespace Game.Hot
{
    public static class FairyInventoryFlow
    {
        private const string InventoryDescriptor = "Assets/Res/UI/FairyGUI/FairyInventoryForm.json";
        private const string OverlayDescriptor = "Assets/Res/UI/FairyGUI/FairyInventoryOverlayForm.json";

        private static readonly Dictionary<int, FairyUIForm> s_DetailForms = new Dictionary<int, FairyUIForm>();
        private static int s_NextDetailToken;

        public static event Action<int> DetailCountChanged;

        public static int OpenDetailCount => s_DetailForms.Count;

        /// <summary>
        /// 当前仍打开的多实例详情窗体的只读快照，供状态展示与流程冒烟测试观察。
        /// </summary>
        public static IReadOnlyCollection<FairyUIForm> OpenDetailForms => s_DetailForms.Values;

        public static async UniTask OpenInventoryAsync()
        {
            FairyUIForm existing = FairyUIManager.Instance.GetUIForm(InventoryDescriptor);
            if (existing != null)
            {
                FairyUIManager.Instance.RefocusUIForm(existing);
                return;
            }

            FairyInventoryOpenData openData = new FairyInventoryOpenData();
            FairyUIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                UIFormId.FairyInventoryForm,
                openData);
            openData.Attach(uiForm);
        }

        public static async UniTask OpenDetailAsync(FairyInventoryItemData item)
        {
            int token = ++s_NextDetailToken;
            FairyItemDetailOpenData openData = new FairyItemDetailOpenData(item, token);
            FairyUIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                UIFormId.FairyItemDetailForm,
                openData);
            openData.Attach(uiForm);

            if (!FairyUIManager.Instance.HasUIForm(uiForm.SerialId))
            {
                return;
            }

            s_DetailForms.Add(token, uiForm);
            DetailCountChanged?.Invoke(s_DetailForms.Count);
        }

        public static async UniTask OpenOverlayAsync()
        {
            FairyUIForm existing = FairyUIManager.Instance.GetUIForm(OverlayDescriptor);
            if (existing != null)
            {
                FairyUIManager.Instance.RefocusUIForm(existing);
                return;
            }

            FairyInventoryOverlayOpenData openData = new FairyInventoryOverlayOpenData();
            FairyUIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                UIFormId.FairyInventoryOverlayForm,
                openData);
            openData.Attach(uiForm);
        }

        public static void Close(FairyFormInstanceData openData)
        {
            FairyUIForm uiForm = openData?.UIForm;
            if (uiForm != null &&
                (FairyUIManager.Instance.HasUIForm(uiForm.SerialId) || FairyUIManager.Instance.IsLoadingUIForm(uiForm.SerialId)))
            {
                FairyUIManager.Instance.CloseUIForm(uiForm.SerialId);
            }
        }

        public static void Refocus(FairyItemDetailOpenData openData)
        {
            FairyUIForm uiForm = openData?.UIForm;
            if (uiForm != null && FairyUIManager.Instance.HasUIForm(uiForm.SerialId))
            {
                FairyUIManager.Instance.RefocusUIForm(uiForm, openData);
            }
        }

        public static void NotifyDetailClosed(FairyItemDetailOpenData openData)
        {
            if (openData == null || !openData.TryMarkClosed())
            {
                return;
            }

            s_DetailForms.Remove(openData.Token);
            DetailCountChanged?.Invoke(s_DetailForms.Count);
        }

        public static void CloseAllDetails()
        {
            if (s_DetailForms.Count == 0)
            {
                return;
            }

            List<FairyUIForm> forms = new List<FairyUIForm>(s_DetailForms.Values);
            foreach (FairyUIForm uiForm in forms)
            {
                if (uiForm != null && FairyUIManager.Instance.HasUIForm(uiForm.SerialId))
                {
                    FairyUIManager.Instance.CloseUIForm(uiForm.SerialId);
                }
            }

            s_DetailForms.Clear();
            DetailCountChanged?.Invoke(0);
        }
    }
}