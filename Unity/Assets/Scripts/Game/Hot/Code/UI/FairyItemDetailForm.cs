using System;
using System.Collections.Generic;
using FairyGUI;
using Game.Hot.FairyGUI.Package1;
using UnityEngine;

namespace Game.Hot
{
    public sealed class FairyItemDetailForm : IFairyUIPresenter
    {
        private readonly List<GObject> m_WindowParts = new List<GObject>();
        private FairyItemDetailOpenData m_OpenData;
        private UIItemDetailWindow m_View;
        private float m_LastDragX;
        private float m_LastDragY;

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

            CollectWindowParts();
            m_View.WindowFrame.draggable = true;
            m_View.WindowFrame.dragBounds = new Rect(0f, 0f, m_View.width, m_View.height);
            m_View.WindowFrame.onDragStart.Add(OnWindowDragStart);
            m_View.WindowFrame.onDragMove.Add(OnWindowDragMove);
            m_View.WindowFrame.onDragEnd.Add(OnWindowDragEnd);
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
            m_View.DescriptionText.text = GameFramework.Utility.Text.Format("{0}\n点击窗口主体可拖动，单击会置顶。", item.Description);
            m_View.FocusStatusText.text = "已打开，等待点击聚焦";
        }

        public void OnClose(bool isShutdown, object userData)
        {
            if (m_View != null)
            {
                m_View.WindowFrame.onDragStart.Remove(OnWindowDragStart);
                m_View.WindowFrame.onDragMove.Remove(OnWindowDragMove);
                m_View.WindowFrame.onDragEnd.Remove(OnWindowDragEnd);
                m_View.WindowFrame.onClick.Remove(OnWindowClick);
                m_View.OpenOverlayButton.onClick.Remove(OnOpenOverlayClick);
                m_View.CloseButton.onClick.Remove(OnCloseClick);
                m_View.WindowFrame.draggable = false;
                m_View = null;
            }

            m_WindowParts.Clear();

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

        private void CollectWindowParts()
        {
            m_WindowParts.Clear();
            m_WindowParts.Add(m_View.WindowFrame);
            m_WindowParts.Add(m_View.WindowTopbar);
            m_WindowParts.Add(m_View.TitleText);
            m_WindowParts.Add(m_View.FocusTokenText);
            m_WindowParts.Add(m_View.ItemNameText);
            m_WindowParts.Add(m_View.ItemTypeText);
            m_WindowParts.Add(m_View.DetailLine);
            m_WindowParts.Add(m_View.DescriptionText);
            m_WindowParts.Add(m_View.FocusStatusText);
            m_WindowParts.Add(m_View.OpenOverlayButton);
            m_WindowParts.Add(m_View.CloseButton);
        }

        private void OnWindowDragStart(EventContext context)
        {
            m_LastDragX = m_View.WindowFrame.x;
            m_LastDragY = m_View.WindowFrame.y;
        }

        private void OnWindowDragMove(EventContext context)
        {
            float dx = m_View.WindowFrame.x - m_LastDragX;
            float dy = m_View.WindowFrame.y - m_LastDragY;
            if (dx == 0f && dy == 0f)
            {
                return;
            }

            foreach (GObject part in m_WindowParts)
            {
                if (part == m_View.WindowFrame)
                {
                    continue;
                }

                part.x += dx;
                part.y += dy;
            }

            m_LastDragX = m_View.WindowFrame.x;
            m_LastDragY = m_View.WindowFrame.y;
        }

        private void OnWindowDragEnd(EventContext context)
        {
            m_LastDragX = m_View.WindowFrame.x;
            m_LastDragY = m_View.WindowFrame.y;
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