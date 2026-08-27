using System;
using FairyGUI;
using Game.Hot.FairyGUI.Package1;

namespace Game.Hot
{
    public sealed class FairyInventoryOverlayForm : IFairyUIPresenter
    {
        private FairyInventoryOverlayOpenData m_OpenData;
        private UIInventoryOverlayView m_View;

        public void OnViewReady(GComponent view)
        {
            m_View = view as UIInventoryOverlayView;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI inventory overlay requires '{typeof(UIInventoryOverlayView).FullName}', found '{view?.GetType().FullName}'.");
            }

            m_View.CloseButton.onClick.Add(OnCloseClick);
        }

        public void OnOpen(object userData)
        {
            m_OpenData = userData as FairyInventoryOverlayOpenData;
            if (m_OpenData == null)
            {
                throw new InvalidOperationException("FairyGUI inventory overlay requires FairyInventoryOverlayOpenData.");
            }
        }

        public void OnClose(bool isShutdown, object userData)
        {
            if (m_View != null)
            {
                m_View.CloseButton.onClick.Remove(OnCloseClick);
                m_View = null;
            }

            m_OpenData = null;
        }

        public void OnPause()
        {
        }

        public void OnResume()
        {
        }

        public void OnCover()
        {
        }

        public void OnReveal()
        {
        }

        public void OnRefocus(object userData)
        {
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        private void OnCloseClick()
        {
            FairyInventoryFlow.Close(m_OpenData);
        }
    }
}
