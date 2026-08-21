using System;
using FairyGUI;
using GameFramework;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
    public sealed class FairyDemoForm : AFairyUIForm
    {
        private GButton m_RefreshButton;
        private GTextField m_StatusText;
        private GTextField m_CheckCountText;
        private int m_CheckCount;

        protected override string fairyPackageName => "Package1";

        protected override string fairyComponentName => "MainView";

        protected override void OnFairyViewReady()
        {
            m_RefreshButton = GetRequiredChild<GButton>("refreshButton");
            m_StatusText = GetRequiredChild<GTextField>("statusText");
            m_CheckCountText = GetRequiredChild<GTextField>("checkCountText");

            m_CheckCount = 0;
            m_RefreshButton.onClick.Add(OnRefreshButtonClick);
            UpdateStatus("FairyGUI 资源包已就绪");
            Log.Info("FairyGUI demo view opened through UGF UIForm.");
        }

        protected override void OnFairyViewClosing()
        {
            m_RefreshButton?.onClick.Remove(OnRefreshButtonClick);
            m_RefreshButton = null;
            m_StatusText = null;
            m_CheckCountText = null;
        }

        private T GetRequiredChild<T>(string childName) where T : GObject
        {
            T child = fairyView.GetChild(childName) as T;
            if (child == null)
            {
                throw new GameFrameworkException(
                    $"FairyGUI child '{childName}' is missing or is not a {typeof(T).Name}.");
            }

            return child;
        }

        private void OnRefreshButtonClick()
        {
            ++m_CheckCount;
            UpdateStatus($"UGF 生命周期检查通过 {DateTime.Now:HH:mm:ss}");
            Log.Info("FairyGUI refresh interaction handled. Count: {0}.", m_CheckCount);
        }

        private void UpdateStatus(string status)
        {
            m_StatusText.text = status;
            m_CheckCountText.text = m_CheckCount.ToString();
        }
    }
}
