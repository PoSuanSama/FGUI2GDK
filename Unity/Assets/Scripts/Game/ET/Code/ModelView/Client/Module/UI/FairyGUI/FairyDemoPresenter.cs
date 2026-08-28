using System;
using Cysharp.Threading.Tasks;
using FairyGUI;
using Game;
using Game.FairyGUI.Package1;

namespace ET.Client
{
    [FairyUIPresenter(UGFUIFormId.FairyDemoForm)]
    [global::ET.EnableClass]
    public sealed class FairyDemoPresenter : IFairyUIPresenter
    {
        private int m_CheckCount;
        private EntityRef<UIComponent> m_Owner;
        private UIMainView m_View;
        private FairyUIWidgetContainer m_WidgetContainer;
        private FairyInventoryItemWidget m_ItemWidget;

        public int PauseCount { get; private set; }
        public int ResumeCount { get; private set; }
        public int CoverCount { get; private set; }
        public int RevealCount { get; private set; }
        public int RefocusCount { get; private set; }
        public object LastOpenUserData { get; private set; }
        public object LastRefocusUserData { get; private set; }

        public void OnViewReady(FairyUIFormContext context)
        {
            m_View = context.View as UIMainView;
            if (m_View == null)
            {
                throw new InvalidOperationException(
                    $"FairyGUI demo requires '{typeof(UIMainView).FullName}', found '{context?.View?.GetType().FullName}'.");
            }

            m_CheckCount = 0;
            // Widget 容器由宿主 FairyUIForm 持有并自动级联 Pause/Cover/Refocus/Update,关闭时统一回收。
            m_WidgetContainer = context.Widgets;
            m_ItemWidget = FairyInventoryItemWidget.Create();
            m_WidgetContainer.AddWidget(m_ItemWidget);
            m_WidgetContainer.OpenWidget(m_ItemWidget);
            m_View.OpenInventoryButton.onClick.Add(OnOpenInventoryButtonClick);
            m_View.RefreshButton.onClick.Add(OnRefreshButtonClick);
            UpdateStatus("FairyGUI 资源包已就绪");
        }

        public void OnOpen(object userData)
        {
            UIComponent owner = userData as UIComponent;
            if (owner == null || owner.IsDisposed)
            {
                throw new InvalidOperationException("ET FairyGUI demo requires a live UIComponent owner.");
            }

            m_Owner = owner;
            LastOpenUserData = userData;
            Log.Info("ET FairyGUI demo presenter opened through FairyUIManager.");
        }

        public void OnClose(bool isShutdown, object userData)
        {
            // Widget 回收由宿主上下文统一执行(Presenter.OnClose 之后),这里只清本地引用。
            m_WidgetContainer = null;
            m_ItemWidget = null;
            m_Owner = default;
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
            UpdateStatus($"ET 生命周期检查通过 {DateTime.Now:HH:mm:ss}");
            Log.Info($"ET FairyGUI refresh interaction handled. Count: {m_CheckCount}.");
        }

        private void OnOpenInventoryButtonClick()
        {
            OpenInventoryAsync().Forget();
        }

        private async UniTaskVoid OpenInventoryAsync()
        {
            try
            {
                UIComponent owner = m_Owner;
                if (owner == null)
                {
                    return;
                }

                await FairyInventoryFlow.OpenInventoryAsync(owner);
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }
        }

        private void UpdateStatus(string status)
        {
            m_View.StatusText.text = status;
            m_View.CheckCountText.text = m_CheckCount.ToString();
        }
    }
}
