using System;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    [FairyUIPresenter(UGFUIFormId.FairyRuntimeInspectorForm)]
    [global::ET.EnableClass]
    public sealed class FairyRuntimeInspectorPresenter : IFairyUIPresenter
    {
        private UIRuntimeInspectorView m_View;

        public void OnViewReady(GComponent view)
        {
            m_View = view as UIRuntimeInspectorView;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI RuntimeInspector requires '{typeof(UIRuntimeInspectorView).FullName}', found '{view?.GetType().FullName}'.");
            }

            m_View.CloseButton.onClick.Add(OnCloseClick);
        }

        public void OnOpen(object userData)
        {
        }

        public void OnClose(bool isShutdown, object userData)
        {
            if (m_View != null)
            {
                m_View.CloseButton.onClick.Remove(OnCloseClick);
                m_View = null;
            }
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
            FairyUIManager.Instance.CloseUIForm(
                FairyUIManager.Instance.GetUIForm("Assets/Res/UI/FairyGUI/FairyRuntimeInspectorForm.json"));
        }
    }
}
