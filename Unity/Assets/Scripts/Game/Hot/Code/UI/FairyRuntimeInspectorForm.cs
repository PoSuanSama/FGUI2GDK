using System;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace Game.Hot
{
    [FairyUIPresenter(UIFormId.FairyRuntimeInspectorForm)]
    public sealed class FairyRuntimeInspectorForm : IFairyUIPresenter
    {
        private UIRuntimeInspectorView m_View;
        private GTextField m_InfoText;

        public void OnViewReady(GComponent view)
        {
            m_View = view as UIRuntimeInspectorView;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI RuntimeInspector requires '{typeof(UIRuntimeInspectorView).FullName}', found '{view?.GetType().FullName}'.");
            }

            m_InfoText = new GTextField();
            m_InfoText.name = "runtimeInfo";
            m_InfoText.SetXY(40, 72);
            m_InfoText.SetSize(720, 470);
            m_View.AddChild(m_InfoText);

            m_View.CloseButton.onClick.Add(OnCloseClick);
            RefreshRuntimeInfo();
        }

        public void OnOpen(object userData)
        {
            RefreshRuntimeInfo();
        }

        public void OnClose(bool isShutdown, object userData)
        {
            if (m_View != null)
            {
                m_View.CloseButton.onClick.Remove(OnCloseClick);
                m_View = null;
            }

            m_InfoText = null;
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
            RefreshRuntimeInfo();
        }

        private void RefreshRuntimeInfo()
        {
            if (m_InfoText == null)
            {
                return;
            }

            FairyUIManager uiManager = FairyUIManager.Instance;
            int loadedForms = uiManager.GetAllLoadedUIForms().Length;
            int loadingForms = uiManager.GetAllLoadingUIFormSerialIds().Length;
            int tableCount = 0;
            if (HotEntry.Tables != null && HotEntry.Tables.DataTables != null)
            {
                foreach (var _ in HotEntry.Tables.DataTables)
                {
                    tableCount++;
                }
            }

            m_InfoText.text = string.Join(
                Environment.NewLine,
                "FairyGUI RuntimeInspector",
                $"Loaded UIForms: {loadedForms}",
                $"Loading UIForms: {loadingForms}",
                $"GameHot Luban Tables: {tableCount}",
                "Package: Package1",
                "Component: RuntimeInspectorView");
        }

        private void OnCloseClick()
        {
            FairyUIManager.Instance.CloseUIForm(
                FairyUIManager.Instance.GetUIForm("Assets/Res/UI/FairyGUI/FairyRuntimeInspectorForm.json"));
        }
    }
}
