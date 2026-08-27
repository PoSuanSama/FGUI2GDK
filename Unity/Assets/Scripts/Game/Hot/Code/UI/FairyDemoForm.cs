using System;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Game.Hot.FairyGUI.Package1;
using UnityGameFramework.Runtime;

namespace Game.Hot
{
        [FairyUIPresenter(UIFormId.FairyDemoForm)]
    public sealed class FairyDemoForm : IFairyUIPresenter
    {
        private int m_CheckCount;
        private UIMainView m_View;

        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int CoverCount { get; private set; }
        public int RevealCount { get; private set; }
        public int RefocusCount { get; private set; }
        public object LastOpenUserData { get; private set; }
        public object LastRefocusUserData { get; private set; }

        public void OnViewReady(GComponent view)
        {
            m_View = view as UIMainView;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI demo requires '{typeof(UIMainView).FullName}', found '{view?.GetType().FullName}'.");
            }

            m_CheckCount = 0;
            m_View.OpenInventoryButton.onClick.Add(OnOpenInventoryButtonClick);
            m_View.RefreshButton.onClick.Add(OnRefreshButtonClick);
            UpdateStatus("FairyGUI 资源包已就绪");
        }

        public void OnOpen(object userData)
        {
            LastOpenUserData = userData;
            Log.Info("FairyGUI demo presenter opened through the unified GF UIForm host.");
        }

        public void OnClose(bool isShutdown, object userData)
        {
            if (m_View != null)
            {
                m_View.OpenInventoryButton.onClick.Remove(OnOpenInventoryButtonClick);
                m_View.RefreshButton.onClick.Remove(OnRefreshButtonClick);
                m_View = null;
            }
        }

        public void OnPause()
        {
            ++PauseCount;
        }

        public void OnResume()
        {
            ++ResumeCount;
        }

        public void OnCover()
        {
            ++CoverCount;
        }

        public void OnReveal()
        {
            ++RevealCount;
        }

        public void OnRefocus(object userData)
        {
            ++RefocusCount;
            LastRefocusUserData = userData;
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
        }

        private void OnRefreshButtonClick()
        {
            ++m_CheckCount;
            UpdateStatus($"UGF 生命周期检查通过 {DateTime.Now:HH:mm:ss}");
            Log.Info("FairyGUI refresh interaction handled. Count: {0}.", m_CheckCount);
        }

        private void OnOpenInventoryButtonClick()
        {
            OpenInventoryAsync().Forget();
        }

        private static async UniTaskVoid OpenInventoryAsync()
        {
            try
            {
                await FairyInventoryFlow.OpenInventoryAsync();
            }
            catch (Exception exception)
            {
                Log.Error("Failed to open FairyGUI inventory: {0}", exception);
            }
        }

        private void UpdateStatus(string status)
        {
            m_View.StatusText.text = status;
            m_View.CheckCountText.text = m_CheckCount.ToString();
        }
    }
}
