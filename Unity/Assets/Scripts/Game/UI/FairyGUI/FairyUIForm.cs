using System;
using FairyGUI;
using GameFramework;
using GameFramework.UI;

namespace Game
{
    /// <summary>
    /// 打开 FairyGUI 界面前已就绪的资源与视图，等待 UIManager 回调时被 FairyUIForm 采纳。
    /// </summary>
    internal sealed class FairyUIFormPendingState
    {
        public FairyUIFormPendingState(
            string descriptorKey,
            FairyUIFormDescriptor descriptor,
            FairyPackageLease packageLease,
            GComponent view,
            IFairyUIPresenter presenter,
            object userData)
        {
            DescriptorKey = descriptorKey;
            Descriptor = descriptor;
            PackageLease = packageLease;
            View = view;
            Presenter = presenter;
            UserData = userData;
        }

        public string DescriptorKey { get; }
        public FairyUIFormDescriptor Descriptor { get; }
        public FairyPackageLease PackageLease { get; }
        public GComponent View { get; }
        public IFairyUIPresenter Presenter { get; }
        public object UserData { get; }
    }

    /// <summary>
    /// FairyGUI 原生 IUIForm 实现：窗口实例直接持有 GComponent 视图、Presenter 与包租约，
    /// 不再经过 UGUI 的 MonoBehaviour + Canvas 宿主。
    /// </summary>
    public sealed class FairyUIForm : IUIForm
    {
        private FairyUIFormPendingState m_PendingState;
        private FairyUIGroupHelper m_GroupHelper;
        private GComponent m_View;
        private IFairyUIPresenter m_Presenter;
        private FairyPackageLease m_PackageLease;
        private FairyUIFormDescriptor m_Descriptor;
        private object m_UserData;
        private bool m_Opened;
        private bool m_Disposed;

        public int SerialId { get; private set; }
        public string UIFormAssetName { get; private set; }
        public object Handle => this;
        public IUIGroup UIGroup { get; private set; }
        public int DepthInUIGroup { get; private set; }
        public bool PauseCoveredUIForm { get; private set; }

        public GComponent View => m_View;
        public IFairyUIPresenter Presenter => m_Presenter;
        public FairyUIFormDescriptor Descriptor => m_Descriptor;

        internal void Adopt(FairyUIFormPendingState pendingState)
        {
            if (pendingState == null)
            {
                throw new ArgumentNullException(nameof(pendingState));
            }

            if (m_PendingState != null)
            {
                throw new GameFrameworkException("FairyUI form already adopted a pending state.");
            }

            m_PendingState = pendingState;
            m_Descriptor = pendingState.Descriptor;
            m_View = pendingState.View;
            m_Presenter = pendingState.Presenter;
            m_PackageLease = pendingState.PackageLease;
            m_UserData = pendingState.UserData;
        }

        public void OnInit(int serialId, string uiFormAssetName, IUIGroup uiGroup, bool pauseCoveredUIForm, bool isNewInstance, object userData)
        {
            SerialId = serialId;
            UIFormAssetName = uiFormAssetName;
            UIGroup = uiGroup;
            PauseCoveredUIForm = pauseCoveredUIForm;
            DepthInUIGroup = 0;
            m_Opened = false;
            m_Disposed = false;

            string descriptorKey = System.IO.Path.GetFileNameWithoutExtension(uiFormAssetName);
            FairyUIFormPendingState pendingState = FairyUIFormPendingRegistry.ConsumeNewInstance(
                serialId,
                descriptorKey,
                userData);
            Adopt(pendingState);

            m_GroupHelper = uiGroup.Helper as FairyUIGroupHelper;
            if (m_GroupHelper == null)
            {
                throw new GameFrameworkException(
                    $"UI group '{uiGroup.Name}' must use '{typeof(FairyUIGroupHelper).FullName}'.");
            }

            m_GroupHelper.AddForm(m_View, 0);
            SetVisible(false);
        }

        public void OnRecycle()
        {
            Release(isShutdown: false, userData: m_UserData);
            SerialId = 0;
            DepthInUIGroup = 0;
            PauseCoveredUIForm = true;
        }

        public void OnOpen(object userData)
        {
            SetVisible(true);
            m_Opened = true;
            m_Presenter.OnOpen(userData);
        }

        public void OnClose(bool isShutdown, object userData)
        {
            Release(isShutdown, userData);
        }

        public void OnPause()
        {
            SetVisible(false);
            m_Presenter.OnPause();
        }

        public void OnResume()
        {
            SetVisible(true);
            m_Presenter.OnResume();
        }

        public void OnCover()
        {
            m_Presenter.OnCover();
        }

        public void OnReveal()
        {
            m_Presenter.OnReveal();
        }

        public void OnRefocus(object userData)
        {
            m_Presenter.OnRefocus(userData);
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            m_Presenter.OnUpdate(elapseSeconds, realElapseSeconds);
        }

        public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            DepthInUIGroup = depthInUIGroup;
            m_GroupHelper?.SetDepth(uiGroupDepth);
            if (m_GroupHelper != null && m_View != null)
            {
                m_GroupHelper.SetFormDepth(m_View, depthInUIGroup);
            }
        }

        private void SetVisible(bool visible)
        {
            if (m_View == null ||
                m_View.isDisposed ||
                m_View.displayObject == null ||
                m_View.displayObject.isDisposed)
            {
                return;
            }

            m_View.visible = visible;
            m_View.touchable = visible;
        }

        private void Release(bool isShutdown, object userData)
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            Exception firstException = null;

            IFairyUIPresenter presenter = m_Presenter;
            m_Presenter = null;
            if (presenter != null && m_Opened)
            {
                TryCleanup(() => presenter.OnClose(isShutdown, userData), ref firstException);
            }

            GComponent view = m_View;
            m_View = null;
            FairyUIGroupHelper groupHelper = m_GroupHelper;
            m_GroupHelper = null;
            if (view != null)
            {
                TryCleanup(() => groupHelper?.RemoveForm(view), ref firstException);
                TryCleanup(view.Dispose, ref firstException);
            }

            FairyPackageLease packageLease = m_PackageLease;
            m_PackageLease = null;
            TryCleanup(() => packageLease?.Dispose(), ref firstException);

            m_PendingState = null;
            m_Opened = false;

            if (firstException != null)
            {
                throw firstException;
            }
        }

        private static void TryCleanup(Action cleanup, ref Exception firstException)
        {
            try
            {
                cleanup();
            }
            catch (Exception exception)
            {
                firstException ??= exception;
            }
        }
    }
}