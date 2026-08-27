using System;
using FairyGUI;
using Game.Hot.FairyGUI.Package1;

namespace Game.Hot
{
    public sealed class FairyItemDetailForm : IFairyUIPresenter
    {
        private FairyItemDetailOpenData m_OpenData;
        private UIItemDetailWindow m_View;

        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int CoverCount { get; private set; }
        public int RevealCount { get; private set; }
        public int RefocusCount { get; private set; }

        public void OnViewReady(GComponent view)
        {
            m_View = view as UIItemDetailWindow;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI item detail requires '{typeof(UIItemDetailWindow).FullName}', found '{view?.GetType().FullName}'.");
            }

            m_View.WindowFrame.onClick.Add(OnWindowClick);
            m_View.OpenOverlayButton.onClick.Add(OnOpenOverlayClick);
            m_View.CloseButton.onClick.Add(OnCloseClick);
        }

        public void OnOpen(object userData)
        {
            m_OpenData = userData as FairyItemDetailOpenData;
            if (m_OpenData == null)
            {
                throw new InvalidOperationException("FairyGUI item detail requires FairyItemDetailOpenData.");
            }

            FairyInventoryItemData item = m_OpenData.Item;
            int slot = (m_OpenData.Token - 1) % 5;
            m_View.SetXY((slot - 2) * 46, ((slot * 2) % 5 - 2) * 28);
            m_View.FocusTokenText.text = $"窗口 #{m_OpenData.Token}";
            m_View.ItemNameText.text = item.Name;
            m_View.ItemTypeText.text = $"{item.Category} / 数量 {item.Count}";
            m_View.DescriptionText.text = GameFramework.Utility.Text.Format("{0}\n点击窗口主体可将此实例提升到最上层。", item.Description);
            m_View.FocusStatusText.text = "已打开，等待点击聚焦";
        }

        public void OnClose(bool isShutdown, object userData)
        {
            if (m_View != null)
            {
                m_View.WindowFrame.onClick.Remove(OnWindowClick);
                m_View.OpenOverlayButton.onClick.Remove(OnOpenOverlayClick);
                m_View.CloseButton.onClick.Remove(OnCloseClick);
                m_View = null;
            }

            FairyItemDetailOpenData openData = m_OpenData;
            m_OpenData = null;
            FairyInventoryFlow.NotifyDetailClosed(openData);
        }

        public void OnPause()
        {
            ++PauseCount;
            UpdateLifecycleStatus("已暂停");
        }

        public void OnResume()
        {
            ++ResumeCount;
            UpdateLifecycleStatus("已恢复");
        }

        public void OnCover()
        {
            ++CoverCount;
            UpdateLifecycleStatus("已被覆盖");
        }

        public void OnReveal()
        {
            ++RevealCount;
            UpdateLifecycleStatus("已重新显示");
        }

        public void OnRefocus(object userData)
        {
            ++RefocusCount;
            UpdateLifecycleStatus($"已置顶 {RefocusCount} 次");
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        private void OnWindowClick()
        {
            FairyInventoryFlow.Refocus(m_OpenData);
        }

        private void OnOpenOverlayClick()
        {
            OpenOverlayAsync().Forget();
        }

        private void OnCloseClick()
        {
            FairyInventoryFlow.Close(m_OpenData);
        }

        private void UpdateLifecycleStatus(string status)
        {
            if (m_View != null)
            {
                m_View.FocusStatusText.text = status;
            }
        }

        private static async Cysharp.Threading.Tasks.UniTaskVoid OpenOverlayAsync()
        {
            try
            {
                await FairyInventoryFlow.OpenOverlayAsync();
            }
            catch (Exception exception)
            {
                UnityGameFramework.Runtime.Log.Error(
                    "Failed to open FairyGUI inventory overlay: {0}",
                    exception);
            }
        }
    }
}
