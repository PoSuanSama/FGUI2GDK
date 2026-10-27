using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using GameFramework.UI;

namespace Game
{
    /// <summary>
    /// 一次打开请求的载体。经 GF 的 userData 通道从 OpenUIForm 入口原样传到
    /// OnInit、OnOpen 和打开结果事件，取代按 serialId 关联的全局状态表。
    /// </summary>
    internal sealed class FairyOpenRequest
    {
        internal FairyOpenRequest(FairyUIFormPendingState state)
        {
            State = state ?? throw new ArgumentNullException(nameof(state));
        }

        internal FairyUIFormPendingState State { get; }

        internal object UserData => State.UserData;

        internal UniTaskCompletionSource<FairyUIForm> Completion { get; } =
            new UniTaskCompletionSource<FairyUIForm>();

        /// <summary>
        /// 可选的进度回调。仅在调用方显式传入时非空。
        /// </summary>
        internal IProgress<FairyUIOpenProgress> Progress { get; set; }

        /// <summary>
        /// 上报阶段进度。Value 单调递增，避免 GF 的资源回调导致进度回退。
        /// </summary>
        internal void ReportProgress(FairyUIOpenStage stage, float value, string packageName = null)
        {
            if (Progress == null)
            {
                return;
            }

            if (value <= m_ReportedProgress)
            {
                return;
            }

            m_ReportedProgress = value;
            Progress.Report(new FairyUIOpenProgress(stage, value, packageName));
        }

        private float m_ReportedProgress;
    }

    /// <summary>
    /// 打开 FairyGUI 界面前已就绪的资源与视图，等待 UIManager 回调时被 FairyUIForm 采纳。
    /// </summary>
    internal sealed class FairyUIFormPendingState
    {
        public FairyUIFormPendingState(
            long operationId,
            string descriptorKey,
            FairyUIFormDescriptor descriptor,
            FairyPackageLease packageLease,
            GComponent view,
            IFairyUIPresenter presenter,
            FairyUIFormContext context,
            object userData)
        {
            OperationId = operationId;
            DescriptorKey = descriptorKey;
            Descriptor = descriptor;
            PackageLease = packageLease;
            View = view;
            Presenter = presenter;
            Context = context;
            UserData = userData;
        }

        public string DescriptorKey { get; }
        public long OperationId { get; }
        public FairyUIFormDescriptor Descriptor { get; }
        public FairyPackageLease PackageLease { get; }
        public GComponent View { get; }
        public IFairyUIPresenter Presenter { get; }
        public FairyUIFormContext Context { get; }
        public object UserData { get; }
        public bool IsAdopted { get; private set; }
        private bool m_ReleaseStarted;

        public void MarkAdopted()
        {
            if (IsAdopted)
            {
                throw new FairyUIException(
                    FairyUIError.FormOpenFailed,
                    $"FairyGUI pending state '{DescriptorKey}' (operation {OperationId}) was adopted more than once.");
            }

            IsAdopted = true;
        }

        internal bool TryBeginRelease()
        {
            if (m_ReleaseStarted)
            {
                return false;
            }

            m_ReleaseStarted = true;
            return true;
        }
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
        private FairyUIFormContext m_Context;
        private FairyPackageLease m_PackageLease;
        private FairyUIFormDescriptor m_Descriptor;
        private object m_UserData;
        private CancellationTokenRegistration m_OwnerCancellationRegistration;
        private bool m_HasOwnerCancellationRegistration;
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
        public FairyUIFormContext Context => m_Context;
        public FairyUIFormDescriptor Descriptor => m_Descriptor;

        internal void Adopt(FairyUIFormPendingState pendingState)
        {
            if (pendingState == null)
            {
                throw new ArgumentNullException(nameof(pendingState));
            }

            if (m_PendingState != null)
            {
                throw new FairyUIException(
                    FairyUIError.FormOpenFailed,
                    "FairyUI form already adopted a pending state.");
            }

            pendingState.MarkAdopted();
            m_PendingState = pendingState;
            m_Descriptor = pendingState.Descriptor;
            m_View = pendingState.View;
            m_Presenter = pendingState.Presenter;
            m_Context = pendingState.Context;
            m_PackageLease = pendingState.PackageLease;
            m_UserData = pendingState.UserData;
        }

        internal void AttachOwnerCancellation(
            CancellationToken ownerToken,
            Action<int> closeBySerialId)
        {
            if (!ownerToken.CanBeCanceled)
            {
                return;
            }

            if (closeBySerialId == null)
            {
                throw new ArgumentNullException(nameof(closeBySerialId));
            }

            if (m_Disposed || !m_Opened || SerialId <= 0)
            {
                throw new FairyUIException(
                    FairyUIError.FormOpenFailed,
                    "Owner cancellation can only be attached to an opened FairyGUI form.");
            }

            DisposeOwnerCancellationRegistration();
            int serialId = SerialId;
            OwnerCancellationState cancellationState = new OwnerCancellationState(
                serialId,
                closeBySerialId);
            CancellationTokenRegistration registration = ownerToken.Register(
                static state => ((OwnerCancellationState)state).Close(),
                cancellationState);

            // 已取消的 token 会在注册期间同步回调，此时池化宿主可能已关闭并等待回收。
            if (m_Disposed || !m_Opened || SerialId != serialId)
            {
                registration.Dispose();
                return;
            }

            m_OwnerCancellationRegistration = registration;
            m_HasOwnerCancellationRegistration = true;
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

            if (userData is not FairyOpenRequest request)
            {
                throw new FairyUIException(
                    FairyUIError.FormOpenFailed,
                    $"FairyGUI form '{uiFormAssetName}' must be opened through FairyUIManager.OpenFairyUIFormAsync.");
            }

            Adopt(request.State);

            try
            {
                m_GroupHelper = uiGroup.Helper as FairyUIGroupHelper;
                if (m_GroupHelper == null)
                {
                    throw new FairyUIException(
                        FairyUIError.UIGroupMissing,
                        $"UI group '{uiGroup.Name}' must use '{typeof(FairyUIGroupHelper).FullName}'.");
                }

                if (m_Context != null)
                {
                    m_Context.Form = this;
                    m_Context.SerialId = serialId;
                    m_Context.OperationId = m_PendingState.OperationId;
                    m_Context.UIGroupName = uiGroup.Name;
                    m_Context.PauseCoveredUIForm = pauseCoveredUIForm;
                }

                // 安全区桥(design §10.3):默认挂安全区容器;descriptor fullScreen
                // 的覆盖层/背景界面挂全屏容器。
                bool attachToSafeArea = !(m_Descriptor?.FullScreen ?? false);
                m_GroupHelper.AddForm(m_View, 0, attachToSafeArea);
                SetVisible(false);
            }
            catch
            {
                // GF 在 OnInit 抛异常时只发失败事件、不回收实例,这里自行释放已采纳的资源。
                Release(isShutdown: false, userData: m_UserData);
                throw;
            }
        }

        public void OnRecycle()
        {
            Release(isShutdown: false, userData: m_UserData);
            SerialId = 0;
            UIFormAssetName = null;
            UIGroup = null;
            DepthInUIGroup = 0;
            PauseCoveredUIForm = true;
            m_Descriptor = null;
            m_UserData = null;
        }

        public void OnOpen(object userData)
        {
            // GF 传入的是打开请求载体，业务参数已在 OnInit 中解包。
            SetVisible(true);
            m_Opened = true;
            m_Presenter.OnOpen(m_UserData);
        }

        public void OnClose(bool isShutdown, object userData)
        {
            Release(isShutdown, userData);
        }

        public void OnPause()
        {
            SetVisible(false);
            m_Presenter.OnPause();
            CascadeWidgets(container => container.PauseAllWidgets());
        }

        public void OnResume()
        {
            SetVisible(true);
            m_Presenter.OnResume();
            CascadeWidgets(container => container.ResumeAllWidgets());
        }

        public void OnCover()
        {
            m_Presenter.OnCover();
            CascadeWidgets(container => container.CoverAllWidgets());
        }

        public void OnReveal()
        {
            m_Presenter.OnReveal();
            CascadeWidgets(container => container.RevealAllWidgets());
        }

        public void OnRefocus(object userData)
        {
            m_Presenter.OnRefocus(userData);
            CascadeWidgets(container => container.RefocusAllWidgets(userData));
        }

        public void OnUpdate(float elapseSeconds, float realElapseSeconds)
        {
            m_Presenter.OnUpdate(elapseSeconds, realElapseSeconds);
            CascadeWidgets(container => container.UpdateAllWidgets(elapseSeconds, realElapseSeconds));
        }

        public void OnDepthChanged(int uiGroupDepth, int depthInUIGroup)
        {
            DepthInUIGroup = depthInUIGroup;
            m_GroupHelper?.SetDepth(uiGroupDepth);
            if (m_GroupHelper != null && m_View != null)
            {
                m_GroupHelper.SetFormDepth(m_View, depthInUIGroup);
            }

            CascadeWidgets(container => container.OnDepthChanged(uiGroupDepth, depthInUIGroup));
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

        private void CascadeWidgets(Action<FairyUIWidgetContainer> cascade)
        {
            if (m_Context != null && m_Context.HasWidgets)
            {
                cascade(m_Context.Widgets);
            }
        }

        private void Release(bool isShutdown, object userData)
        {
            if (m_Disposed)
            {
                return;
            }

            m_Disposed = true;
            FairyUIFormPendingState pendingState = m_PendingState;
            if (pendingState != null && !pendingState.TryBeginRelease())
            {
                m_PendingState = null;
                m_GroupHelper = null;
                m_View = null;
                m_Presenter = null;
                m_Context = null;
                m_PackageLease = null;
                m_Opened = false;
                return;
            }

            Exception firstException = null;

            FairyUIFormContext context = m_Context;
            TryCleanup(() => context?.CancelLifetime(), ref firstException);

            IFairyUIPresenter presenter = m_Presenter;
            m_Presenter = null;
            if (presenter != null)
            {
                TryCleanup(() => presenter.OnClose(isShutdown, userData), ref firstException);
            }

            // Widget/事件/资源上下文在视图释放前清理:RecycleWidget 需要视图仍挂在父节点下。
            m_Context = null;
            if (context != null)
            {
                TryCleanup(() => context.Clear(), ref firstException);
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

            if (m_HasOwnerCancellationRegistration)
            {
                CancellationTokenRegistration registration = m_OwnerCancellationRegistration;
                m_OwnerCancellationRegistration = default;
                m_HasOwnerCancellationRegistration = false;
                TryCleanup(registration.Dispose, ref firstException);
            }

            m_PendingState = null;
            m_Opened = false;

            if (firstException != null)
            {
                throw firstException;
            }
        }

        private void DisposeOwnerCancellationRegistration()
        {
            if (!m_HasOwnerCancellationRegistration)
            {
                return;
            }

            CancellationTokenRegistration registration = m_OwnerCancellationRegistration;
            m_OwnerCancellationRegistration = default;
            m_HasOwnerCancellationRegistration = false;
            registration.Dispose();
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

        private sealed class OwnerCancellationState
        {
            private readonly int m_SerialId;
            private readonly Action<int> m_CloseBySerialId;

            internal OwnerCancellationState(int serialId, Action<int> closeBySerialId)
            {
                m_SerialId = serialId;
                m_CloseBySerialId = closeBySerialId;
            }

            internal void Close()
            {
                m_CloseBySerialId(m_SerialId);
            }
        }
    }
}
