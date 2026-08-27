using GameFramework.UI;
using System.Threading;
using UnityGameFramework.Runtime;

namespace Game
{
    /// <summary>
    /// Hosts a FairyGUI descriptor, package lease, view and presenter on a GF UIForm.
    /// </summary>
    public sealed class FairyUIFormLogic : UIFormLogic
    {
        private FairyUIFormPreparedState m_PreparedState;

        public FairyUIFormDescriptor Descriptor => m_PreparedState?.Descriptor;

        public global::FairyGUI.GComponent View => m_PreparedState?.View;

        public IFairyUIPresenter Presenter => m_PreparedState?.Presenter;

        internal FairyUIFormPreparedState PreparedState => m_PreparedState;

        internal void Adopt(FairyUIFormPreparedState preparedState, IUIGroup uiGroup)
        {
            if (preparedState == null)
            {
                throw new System.ArgumentNullException(nameof(preparedState));
            }

            if (m_PreparedState != null)
            {
                throw new GameFramework.GameFrameworkException("FairyGUI UI form already owns prepared state.");
            }

            preparedState.Adopt(uiGroup);
            m_PreparedState = preparedState;
        }

        internal void ObserveOwnerCancellation(CancellationToken ownerToken)
        {
            m_PreparedState?.ObserveOwnerCancellation(UIForm, ownerToken);
        }

        protected override void OnInit(object userData)
        {
            base.OnInit(userData);

            FairyUIFormHost host = GetComponent<FairyUIFormHost>();
            if (host == null)
            {
                throw new GameFramework.GameFrameworkException(
                    "FairyGUI UI form host is missing during initialization.");
            }

            FairyUIFormPreparedState preparedState =
                FairyUIFormPreparedRegistry.ConsumeNewInstance(
                    UIForm.SerialId,
                    host.DescriptorKey,
                    userData);
            try
            {
                Adopt(preparedState, UIForm.UIGroup);
            }
            catch
            {
                preparedState.Dispose();
                throw;
            }
        }

        protected override void OnOpen(object userData)
        {
            base.OnOpen(userData);

            FairyUIFormPreparedState preparedState = m_PreparedState;
            if (preparedState == null)
            {
                return;
            }

            preparedState.Presenter.OnOpen(userData);
            preparedState.MarkPresenterOpened();
        }

        protected override void OnClose(bool isShutdown, object userData)
        {
            try
            {
                base.OnClose(isShutdown, userData);
            }
            finally
            {
                ReleasePreparedState(isShutdown, userData);
            }
        }

        protected override void OnPause()
        {
            base.OnPause();
            m_PreparedState?.Presenter.OnPause();
        }

        protected override void OnResume()
        {
            base.OnResume();
            m_PreparedState?.Presenter.OnResume();
        }

        protected override void OnCover()
        {
            base.OnCover();
            m_PreparedState?.Presenter.OnCover();
        }

        protected override void OnReveal()
        {
            base.OnReveal();
            m_PreparedState?.Presenter.OnReveal();
        }

        protected override void OnRefocus(object userData)
        {
            base.OnRefocus(userData);
            m_PreparedState?.Presenter.OnRefocus(userData);
        }

        protected override void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            base.OnUpdate(elapseSeconds, realElapseSeconds);
            m_PreparedState?.Presenter.OnUpdate(elapseSeconds, realElapseSeconds);
        }

        protected override void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            base.OnDepthChanged(uiGroupDepth, depthInUIGroup);
            m_PreparedState?.SetDepth(uiGroupDepth, depthInUIGroup);
        }

        protected override void OnRecycle()
        {
            ReleasePreparedState(false, m_PreparedState?.UserData);
            base.OnRecycle();
        }

        protected override void InternalSetVisible(bool visible)
        {
            m_PreparedState?.SetVisible(visible);
        }

        private void ReleasePreparedState(bool isShutdown, object userData)
        {
            FairyUIFormPreparedState preparedState = m_PreparedState;
            m_PreparedState = null;
            preparedState?.Release(isShutdown, userData);
        }
    }
}
