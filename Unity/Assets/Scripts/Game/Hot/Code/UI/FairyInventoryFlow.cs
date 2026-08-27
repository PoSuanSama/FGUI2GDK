using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public static class FairyInventoryFlow
    {
        private const string InventoryDescriptor = "Assets/Res/UI/FairyGUI/FairyInventoryForm.json";
        private const string OverlayDescriptor = "Assets/Res/UI/FairyGUI/FairyInventoryOverlayForm.json";

        private static readonly Dictionary<int, UIForm> s_DetailForms = new Dictionary<int, UIForm>();
        private static int s_NextDetailToken;

        public static event Action<int> DetailCountChanged;

        public static int OpenDetailCount => s_DetailForms.Count;

        /// <summary>
        /// 当前仍打开的多实例详情窗体的只读快照，供状态展示与流程冒烟测试观察。
        /// </summary>
        public static IReadOnlyCollection<UIForm> OpenDetailForms => s_DetailForms.Values;

        public static async UniTask OpenInventoryAsync()
        {
            UIForm existing = GameEntry.UI.GetUIForm(InventoryDescriptor);
            if (existing != null)
            {
                GameEntry.UI.RefocusUIForm(existing);
                return;
            }

            FairyInventoryOpenData openData = new FairyInventoryOpenData();
            UIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                UIFormId.FairyInventoryForm,
                openData);
            openData.Attach(uiForm);
        }

        public static async UniTask OpenDetailAsync(FairyInventoryItemData item)
        {
            int token = ++s_NextDetailToken;
            FairyItemDetailOpenData openData = new FairyItemDetailOpenData(item, token);
            UIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                UIFormId.FairyItemDetailForm,
                openData);
            openData.Attach(uiForm);

            if (!GameEntry.UI.HasUIForm(uiForm.SerialId))
            {
                return;
            }

            s_DetailForms.Add(token, uiForm);
            DetailCountChanged?.Invoke(s_DetailForms.Count);
        }

        public static async UniTask OpenOverlayAsync()
        {
            UIForm existing = GameEntry.UI.GetUIForm(OverlayDescriptor);
            if (existing != null)
            {
                GameEntry.UI.RefocusUIForm(existing);
                return;
            }

            FairyInventoryOverlayOpenData openData = new FairyInventoryOverlayOpenData();
            UIForm uiForm = await FairyUIFormService.OpenFairyUIFormAsync(
                UIFormId.FairyInventoryOverlayForm,
                openData);
            openData.Attach(uiForm);
        }

        public static void Close(FairyFormInstanceData openData)
        {
            UIForm uiForm = openData?.UIForm;
            if (uiForm != null &&
                (GameEntry.UI.HasUIForm(uiForm.SerialId) || GameEntry.UI.IsLoadingUIForm(uiForm.SerialId)))
            {
                GameEntry.UI.CloseUIForm(uiForm.SerialId);
            }
        }

        public static void Refocus(FairyItemDetailOpenData openData)
        {
            UIForm uiForm = openData?.UIForm;
            if (uiForm != null && GameEntry.UI.HasUIForm(uiForm.SerialId))
            {
                GameEntry.UI.RefocusUIForm(uiForm, openData);
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

            List<UIForm> forms = new List<UIForm>(s_DetailForms.Values);
            foreach (UIForm uiForm in forms)
            {
                if (uiForm != null && GameEntry.UI.HasUIForm(uiForm.SerialId))
                {
                    GameEntry.UI.CloseUIForm(uiForm.SerialId);
                }
            }
        }
    }
}
