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
        private EntityRef<UIComponent> m_Owner;
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
            UIComponent owner = userData as UIComponent;
            if (owner == null || owner.IsDisposed)
            {
                throw new InvalidOperationException(
                    "ET FairyGUI RuntimeInspector requires a live UIComponent owner.");
            }

            m_Owner = owner;
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
            m_Owner = default;
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
            if (Tables.Instance != null && Tables.Instance.DataTables != null)
            {
                foreach (var _ in Tables.Instance.DataTables)
                {
                    tableCount++;
                }
            }

            m_InfoText.text = string.Join(
                Environment.NewLine,
                "FairyGUI RuntimeInspector",
                $"Loaded UIForms: {loadedForms}",
                $"Loading UIForms: {loadingForms}",
                $"ET Luban Tables: {tableCount}",
                "Package: Package1",
                "Component: RuntimeInspectorView");
        }

        private void OnCloseClick()
        {
            UIComponent owner = m_Owner;
            FairyUIForm form = FairyUIManager.Instance.GetUIForm(
                "Assets/Res/UI/FairyGUI/FairyRuntimeInspectorForm.json");
            if (owner != null && form != null)
            {
                UIComponentFairyUIBridge.CloseBySerialId(owner, form.SerialId);
            }
        }
    }
}
