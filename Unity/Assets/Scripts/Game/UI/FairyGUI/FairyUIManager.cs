using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using GameFramework.ObjectPool;
using GameFramework.Resource;
using GameFramework.UI;
using UnityEngine;
using UnityGameFramework.Extension;
using UnityGameFramework.Runtime;

namespace Game
{
    /// <summary>
    /// FairyGUI 原生窗口管理入口：驱动 GameFramework.UI 语义层，封装资源加载、包租约与界面组。
    /// 替代 UGUI 的 UIComponent + GDKUIFormHelper 寄生路径。
    /// </summary>
    public sealed class FairyUIManager
    {
        public static FairyUIManager Instance { get; } = new FairyUIManager();

        public static Func<int, DRUIForm> UIFormTableProvider;

        /// <summary>
        /// ET Runner invokes this after disposing the ET World and shutting down FairyUIManager.
        /// Subscribers are one-shot so they do not retain a completed runtime generation.
        /// </summary>
        public static event Action ETRuntimeShutdownCompleted;

#if UNITY_EDITOR
        /// <summary>
        /// Editor 回归测试专用的单次打开钩子：在业务 OnViewReady 之后、
        /// 向 GF 请求 serial 之前触发一次，触发后自动清除。
        /// 只影响下一次打开；Player 构建不编译。
        /// </summary>
        public static Action<IFairyUIPresenter> AfterViewReadyForTesting;
#endif

        private const string DescriptorAssetRoot = "Assets/Res/UI/FairyGUI";
        private const int DesignResolutionX = 1280;
        private const int DesignResolutionY = 720;

        private IUIManager m_UIManager;
        private FairyUIFormHelper m_UIFormHelper;
        private readonly Dictionary<string, FairyUIGroupHelper> m_Groups =
            new Dictionary<string, FairyUIGroupHelper>(StringComparer.Ordinal);
        private bool m_EventsAttached;
        private bool m_Initialized;
        private long m_NextOperationId;
        private long m_LifecycleGeneration;
        private CancellationTokenSource m_LifecycleCancellation;

        public void Initialize()
        {
            AssertMainThread(nameof(Initialize));
            if (m_Initialized)
            {
                return;
            }

            IUIManager uiManager = GameFrameworkEntry.GetModule<IUIManager>();
            if (uiManager == null)
            {
                throw new FairyUIException(FairyUIError.ManagerNotInitialized, "UI manager is invalid.");
            }

            if (m_UIManager != null && !ReferenceEquals(m_UIManager, uiManager))
            {
                DetachUIManagerEvents(m_UIManager);
                DisposeGroupHelpers();
            }

            m_UIManager = uiManager;

            IResourceManager resourceManager = GameEntry.Base != null && GameEntry.Base.EditorResourceMode
                ? GameEntry.Base.EditorResourceHelper
                : GameFrameworkEntry.GetModule<IResourceManager>();
            if (resourceManager != null)
            {
                m_UIManager.SetResourceManager(resourceManager);
            }

            IObjectPoolManager objectPoolManager = GameFrameworkEntry.GetModule<IObjectPoolManager>();
            if (objectPoolManager != null && !objectPoolManager.HasObjectPool(IsUIInstancePool))
            {
                m_UIManager.SetObjectPoolManager(objectPoolManager);
            }

            ResourceComponent resourceComponent = GameEntry.Resource;
            m_UIFormHelper = new FairyUIFormHelper(asset => ReleaseAsset(resourceComponent, asset));
            m_UIManager.SetUIFormHelper(m_UIFormHelper);

            if (!m_EventsAttached)
            {
                m_UIManager.OpenUIFormSuccess += OnOpenUIFormSuccess;
                m_UIManager.OpenUIFormFailure += OnOpenUIFormFailure;
                m_UIManager.OpenUIFormUpdate += OnOpenUIFormUpdate;
                m_UIManager.OpenUIFormDependencyAsset += OnOpenUIFormDependencyAsset;
                m_UIManager.CloseUIFormComplete += OnCloseUIFormComplete;
                m_EventsAttached = true;
            }

            GRoot root = GRoot.inst;
            root.SetContentScaleFactor(
                DesignResolutionX,
                DesignResolutionY,
                UIContentScaler.ScreenMatchMode.MatchWidthOrHeight);

            AttachStageToBuiltinUI();

            foreach (FairyUIGroupHelper group in m_Groups.Values)
            {
                group.AttachToRoot(GRoot.inst);
            }

            ReorderGroups();
            m_LifecycleCancellation?.Dispose();
            m_LifecycleCancellation = new CancellationTokenSource();
            Interlocked.Increment(ref m_LifecycleGeneration);
            m_Initialized = true;

        }

        /// <summary>
        /// 把 FairyGUI Stage 归位到 GameEntry 下 Builtin(GameFramework 实例根)的 UI 节点下,
        /// 与旧 UGUI 布局一致(UIComponent 曾挂在 Builtin/UI,Canvas 都在其下)。
        ///
        /// 注意:运行时有两个 Builtin 节点——
        /// - GameEntry 直接子节点 Builtin:GameFramework 嵌套 prefab 实例根被改名而来,
        ///   承载 DataNode/Resource/Scene 等 UGF 运行时组件;
        /// - Game/Builtin:GameEntry.prefab 的静态节点(Builtin 组件已移除,节点保留)。
        /// 旧 UGUI 的 UI 节点在第一个 Builtin 下,故经其子组件(DataNodeComponent)
        /// 定位实例根。非 GameHot 流程(组件未注册)时跳过。
        /// Stage 自建时挂场景根并 DontDestroyOnLoad;这里只换父。
        /// </summary>
        private void AttachStageToBuiltinUI()
        {
            Stage stage = Stage.inst;
            if (stage == null || stage.gameObject == null)
            {
                return;
            }

            // Game 命名空间内的 GameEntry 是 MonoBehaviour 单例,遮蔽了 UGF 静态入口;
            // 这里必须全限定使用 UnityGameFramework.Runtime.GameEntry。
            DataNodeComponent dataNode =
                UnityGameFramework.Runtime.GameEntry.GetComponent<DataNodeComponent>();
            Transform builtinRoot = dataNode != null ? dataNode.transform.parent : null;
            if (builtinRoot == null)
            {
                return;
            }

            // UI 节点静态存在于 GameFramework.prefab(根下,与 UI Form Instances 同层);
            // 运行时只做 Stage 的挂载,不再动态创建节点。
            Transform uiNode = builtinRoot.Find("UI");
            if (uiNode == null)
            {
                // 静态节点缺失属配置错误,记录诊断;不动态生成以免破坏 prefab 权威。
                Log.Warning("FairyGUI Stage parent UI node is missing under the GameFramework Builtin root; the GameFramework.prefab UI node may have been removed.");
                return;
            }

            if (!ReferenceEquals(stage.gameObject.transform.parent, uiNode))
            {
                stage.gameObject.transform.SetParent(uiNode, false);
            }
        }

        public bool AddUIGroup(string name, int depth)
        {
            IUIManager uiManager = GetRequiredUIManager();
            if (m_Groups.ContainsKey(name))
            {
                return false;
            }

            FairyUIGroupHelper helper = new FairyUIGroupHelper(name);
            if (!uiManager.AddUIGroup(name, depth, helper))
            {
                helper.Dispose();
                return false;
            }

            m_Groups.Add(name, helper);
            helper.AttachToRoot(GRoot.inst);
            ReorderGroups();
            return true;
        }

        /// <summary>
        /// GF 的 UI 管理器。查询、关闭、层级操作直接走这里，不再逐方法转发。
        /// 访问会校验初始化状态与主线程。
        /// </summary>
        public IUIManager Manager => GetRequiredUIManager();

        public bool HasUIGroup(string name) => Manager.HasUIGroup(name);

        public IUIGroup GetUIGroup(string name) => Manager.GetUIGroup(name);

        public bool HasUIForm(int serialId) => Manager.HasUIForm(serialId);

        public bool HasUIForm(string assetName) => Manager.HasUIForm(assetName);

        public bool IsLoadingUIForm(int serialId) => Manager.IsLoadingUIForm(serialId);

        public bool IsLoadingUIForm(string assetName) => Manager.IsLoadingUIForm(assetName);

        public FairyUIForm GetUIForm(int serialId) => Manager.GetUIForm(serialId) as FairyUIForm;

        public FairyUIForm GetUIForm(string assetName) => Manager.GetUIForm(assetName) as FairyUIForm;

        public FairyUIForm[] GetAllLoadedUIForms()
        {
            IUIForm[] loaded = Manager.GetAllLoadedUIForms();
            FairyUIForm[] result = new FairyUIForm[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
            {
                result[i] = loaded[i] as FairyUIForm;
            }

            return result;
        }

        public int[] GetAllLoadingUIFormSerialIds() => Manager.GetAllLoadingUIFormSerialIds();

        public void CloseUIForm(int serialId) => Manager.CloseUIForm(serialId);

        public void CloseUIForm(FairyUIForm form) => Manager.CloseUIForm(form);

        public void RefocusUIForm(FairyUIForm form) => Manager.RefocusUIForm(form);

        public void RefocusUIForm(FairyUIForm form, object userData) =>
            Manager.RefocusUIForm(form, userData);

        /// <summary>
        /// GF 语义层透出:界面实例对象池自动释放间隔秒数。
        /// </summary>
        public float InstanceAutoReleaseInterval
        {
            get => Manager.InstanceAutoReleaseInterval;
            set => Manager.InstanceAutoReleaseInterval = value;
        }

        /// <summary>
        /// GF 语义层透出:界面实例对象池容量。
        /// </summary>
        public int InstanceCapacity
        {
            get => Manager.InstanceCapacity;
            set => Manager.InstanceCapacity = value;
        }

        /// <summary>
        /// GF 语义层透出:界面实例对象池对象过期秒数。
        /// </summary>
        public float InstanceExpireTime
        {
            get => Manager.InstanceExpireTime;
            set => Manager.InstanceExpireTime = value;
        }

        /// <summary>
        /// GF 语义层透出:界面实例对象池默认优先级(区别于 SetUIFormInstancePriority 的实例级优先级)。
        /// </summary>
        public int InstancePriority
        {
            get => Manager.InstancePriority;
            set => Manager.InstancePriority = value;
        }


        public int UIGroupCount => Manager.UIGroupCount;

        public IUIGroup[] GetAllUIGroups() => Manager.GetAllUIGroups();

        public void CloseAllLoadedUIForms() => Manager.CloseAllLoadedUIForms();

        public void CloseAllLoadedUIForms(object userData) => Manager.CloseAllLoadedUIForms(userData);

        public void CloseAllLoadingUIForms() => Manager.CloseAllLoadingUIForms();

        public void Shutdown()
        {
            AssertMainThread(nameof(Shutdown));
            m_Initialized = false;
            Interlocked.Increment(ref m_LifecycleGeneration);

            CancellationTokenSource lifecycleCancellation = m_LifecycleCancellation;
            m_LifecycleCancellation = null;
            if (lifecycleCancellation != null)
            {
                try
                {
                    lifecycleCancellation.Cancel();
                }
                catch (Exception exception)
                {
                    Log.Error("Failed to cancel FairyGUI manager lifetime during shutdown: {0}", exception);
                }
            }

            try
            {
                IUIManager uiManager = m_UIManager;
                if (uiManager != null)
                {
                    IUIForm[] forms = uiManager.GetAllLoadedUIForms();
                    foreach (IUIForm form in forms)
                    {
                        if (form != null && uiManager.HasUIForm(form.SerialId))
                        {
                            try
                            {
                                uiManager.CloseUIForm(form.SerialId);
                            }
                            catch (Exception exception)
                            {
                                Log.Error("Failed to close FairyGUI form '{0}' during shutdown: {1}", form.SerialId, exception);
                            }
                        }
                    }

                    try
                    {
                        uiManager.CloseAllLoadingUIForms();
                    }
                    catch (Exception exception)
                    {
                        Log.Error("Failed to close loading FairyGUI forms during shutdown: {0}", exception);
                    }

                    DetachUIManagerEvents(uiManager);
                }

                // 未完成的打开操作由 lifecycle token 取消,其 finally 负责回滚,
                // 不需要额外的全局 pending 表做同步清理。

                FairyInputService.Instance.Shutdown();
                FairySound.Shutdown();
                FairyLocalization.Reset();
                FairyPackageManager.ClearWarmPackages();
                FairyPackageManager.Shutdown();
            }
            finally
            {
                UIFormTableProvider = null;
                lifecycleCancellation?.Dispose();
            }
        }

        public static void NotifyETRuntimeShutdownCompleted()
        {
            Action callbacks = ETRuntimeShutdownCompleted;
            ETRuntimeShutdownCompleted = null;
            if (callbacks == null)
            {
                return;
            }

            foreach (Action callback in callbacks.GetInvocationList())
            {
                try
                {
                    callback();
                }
                catch (Exception exception)
                {
                    Log.Error("FairyGUI ET runtime shutdown callback failed: {0}", exception);
                }
            }
        }

        /// <summary>
        /// 预热并常驻一个 FairyGUI 包（含依赖闭包）。已常驻时直接返回。
        /// 常驻后窗体全部关闭也不会卸载包，重新打开可省去 AddPackage 与纹理重建。
        /// </summary>
        public async UniTask WarmPackageAsync(
            string packageName,
            CancellationToken cancellationToken = default)
        {
            AssertMainThread(nameof(WarmPackageAsync));
            GetRequiredUIManager();
            await FairyPackageManager.WarmPackageAsync(packageName, cancellationToken);
        }

        /// <summary>
        /// 解除指定包的常驻。返回是否确实解除。
        /// </summary>
        public bool ReleaseWarmPackage(string packageName)
        {
            AssertMainThread(nameof(ReleaseWarmPackage));
            return FairyPackageManager.ReleaseWarmPackage(packageName);
        }

        public bool IsPackageWarm(string packageName)
        {
            return FairyPackageManager.IsPackageWarm(packageName);
        }

        public async UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData = null,
            CancellationToken ownerToken = default)
        {
            return await OpenFairyUIFormAsync(uiId, userData, ownerToken, null, null);
        }

        /// <summary>
        /// 带进度回调的打开重载。进度单调递增到 1.0，取消与失败不会推送 Completed。
        /// </summary>
        public async UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData,
            IProgress<FairyUIOpenProgress> progress,
            CancellationToken ownerToken = default)
        {
            return await OpenFairyUIFormAsync(uiId, userData, ownerToken, null, progress);
        }

        internal async UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData,
            CancellationToken ownerToken,
            Func<FairyUIFormDescriptor, IFairyUIPresenter> presenterFactory,
            IProgress<FairyUIOpenProgress> progress = null)
        {
            IUIManager uiManager = GetRequiredUIManager();
            CancellationTokenSource lifecycleCancellation = m_LifecycleCancellation;
            if (lifecycleCancellation == null)
            {
                throw new FairyUIException(
                    FairyUIError.ManagerNotInitialized,
                    "FairyUIManager has no active lifecycle token.");
            }

            long lifecycleGeneration = Interlocked.Read(ref m_LifecycleGeneration);
            CancellationTokenSource linkedCancellation =
                CancellationTokenSource.CreateLinkedTokenSource(lifecycleCancellation.Token);
            CancellationToken openToken = linkedCancellation.Token;
            CancellationTokenRegistration ownerCancellationRegistration = default;
            int ownerCancellationActive = 1;

            void CancelOpen()
            {
                if (Volatile.Read(ref ownerCancellationActive) == 0)
                {
                    return;
                }

                try
                {
                    linkedCancellation.Cancel();
                }
                catch (ObjectDisposedException)
                {
                }
            }

            void RequestOpenCancellation()
            {
                if (PlayerLoopHelper.IsMainThread)
                {
                    CancelOpen();
                    return;
                }

                PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, CancelOpen);
            }

            FairyPackageLease packageLease = null;
            GComponent pendingView = null;
            FairyUIFormPendingState pendingState = null;
            int serialId = 0;
            bool adopted = false;
            long operationId = Interlocked.Increment(ref m_NextOperationId);

            // 进度只增不减：GF 的资源回调与各阶段上报共用同一条单调序列。
            float lastReportedProgress = 0f;
            void Report(FairyUIOpenStage stage, float value, string packageName = null)
            {
                if (progress == null || value <= lastReportedProgress)
                {
                    return;
                }

                lastReportedProgress = value;
                progress.Report(new FairyUIOpenProgress(stage, value, packageName));
            }

            try
            {
                if (ownerToken.CanBeCanceled)
                {
                    ownerCancellationRegistration = ownerToken.Register(RequestOpenCancellation);
                }

                ThrowIfOpenInvalidated(lifecycleGeneration, uiManager, openToken);
                DRUIForm uiForm = await ResolveUIFormAsync(uiId, openToken);
                if (uiForm == null)
                {
                    throw new FairyUIException(
                        FairyUIError.UIFormNotConfigured,
                        $"Can not load UI form '{uiId}' from data table.");
                }

                Report(FairyUIOpenStage.Descriptor, 0.05f);

                string descriptorAssetName = GetDescriptorAssetName(uiForm.AssetName);
                if (!uiForm.AllowMultiInstance &&
                    (uiManager.IsLoadingUIForm(descriptorAssetName) || uiManager.HasUIForm(descriptorAssetName)))
                {
                    throw new FairyUIException(
                        FairyUIError.FormAlreadyOpen,
                        $"FairyGUI UI form '{descriptorAssetName}' is loading or already open.");
                }

                ResourceComponent descriptorResource = GameEntry.Resource;
                if (descriptorResource == null)
                {
                    throw new FairyUIException(
                        FairyUIError.ResourceLoadFailed,
                        "FairyGUI resource component is unavailable.");
                }

                TextAsset descriptorAsset = await LoadDescriptorTextAsync(
                    descriptorAssetName,
                    descriptorResource,
                    openToken);
                Report(FairyUIOpenStage.Descriptor, 0.1f);

                // descriptor 解析后原始 TextAsset 立即释放回池:GF OpenUIForm 稍后会对同一
                // descriptorAssetName 再发起一次加载(作为窗体资产 token)。这里用无类型加载,
                // 与 GF UIManager 的加载共享同一对象池键 (assetName, null)——Editor Resource
                // Mode 不经过 Asset Pool 而不暴露差异;AssetBundle 模式若用有类型加载
                // (assetName, typeof(TextAsset)) 会与 GF 的键分裂,同一个 Unity 对象被 Register
                // 两次并抛同 key ArgumentException。解析后释放让 GF 的后续加载直接 Spawn 复用。
                FairyUIFormDescriptor descriptor;
                try
                {
                    descriptor = FairyUIFormDescriptor.Parse(descriptorAsset.text);
                }
                finally
                {
                    UnloadAsset(descriptorResource, descriptorAsset);
                }

                ValidateDescriptor(descriptor, uiId, uiForm);

                Action<FairyUIFormDescriptor> preparePackage = FairyUIPresenterRegistry.PreparePackage;
                Func<FairyUIFormDescriptor, IFairyUIPresenter> createPresenter =
                    FairyUIPresenterRegistry.CreatePresenter;
                if (preparePackage == null)
                {
                    throw new FairyUIException(
                        FairyUIError.PresenterMissing,
                        "FairyGUI package binding must be registered before opening a form.");
                }

                if (presenterFactory == null && createPresenter == null)
                {
                    throw new FairyUIException(
                        FairyUIError.PresenterMissing,
                        "Either a per-open presenter factory or a class presenter registry must be available before opening a form.");
                }

                packageLease = await FairyPackageManager.AcquireAsync(descriptor.PackageName, openToken);
                Report(FairyUIOpenStage.Package, 0.55f, descriptor.PackageName);
                FairyPackageManager.ValidateDescriptorIdentity(descriptor);
                await FairyLocalization.ApplyAsync(descriptor.PackageName, openToken);
                Report(FairyUIOpenStage.Localization, 0.6f, descriptor.PackageName);
                preparePackage(descriptor);
                pendingView = UIPackage.CreateObject(
                    descriptor.PackageName,
                    descriptor.ComponentName) as GComponent;
                if (pendingView == null)
                {
                    throw new FairyUIException(
                        FairyUIError.FormOpenFailed,
                        $"Unable to create FairyGUI component '{descriptor.PackageName}/{descriptor.ComponentName}'.");
                }

                if (!string.Equals(pendingView.GetType().FullName, descriptor.BindingType, StringComparison.Ordinal))
                {
                    throw new FairyUIException(
                        FairyUIError.BindingTypeMismatch,
                        Utility.Text.Format(
                            "FairyGUI binding type mismatch for UI '{0}': expected '{1}', found '{2}'.",
                            uiId,
                            descriptor.BindingType,
                            pendingView.GetType().FullName));
                }

                // per-open 工厂优先(ET Component/System 路径);返回 null 时回退类 Presenter 注册表。
                IFairyUIPresenter presenter = presenterFactory?.Invoke(descriptor);
                if (presenter == null)
                {
                    presenter = createPresenter?.Invoke(descriptor);
                }

                if (presenter == null)
                {
                    throw new FairyUIException(
                        FairyUIError.PresenterMissing,
                        $"FairyGUI presenter is not registered for UI '{descriptor.CsName}'.");
                }

                FairyUIFormContext context = new FairyUIFormContext
                {
                    View = pendingView,
                    UIId = uiId,
                    OperationId = operationId,
                };
                string descriptorKey = Path.GetFileNameWithoutExtension(descriptorAssetName);
                pendingState = new FairyUIFormPendingState(
                    operationId,
                    descriptorKey,
                    descriptor,
                    packageLease,
                    pendingView,
                    presenter,
                    context,
                    userData);
                packageLease = null;
                pendingView = null;

                presenter.OnViewReady(pendingState.Context);
#if UNITY_EDITOR
                if (AfterViewReadyForTesting != null)
                {
                    Action<IFairyUIPresenter> afterViewReady = AfterViewReadyForTesting;
                    AfterViewReadyForTesting = null;
                    afterViewReady(presenter);
                }
#endif
                Report(FairyUIOpenStage.View, 0.7f, descriptor.PackageName);
                await FairyPackageManager.WaitForPendingAssetsAsync(pendingState.PackageLease, openToken);

                if (!uiManager.HasUIGroup(uiForm.UIGroupName))
                {
                    throw new FairyUIException(
                        FairyUIError.UIGroupMissing,
                        $"FairyGUI UI group '{uiForm.UIGroupName}' is not registered.");
                }

                ThrowIfOpenInvalidated(lifecycleGeneration, uiManager, openToken);

                // userData 承载请求对象,GF 会把它原样传给 OnInit、OnOpen 和结果事件,
                // 因此同步与异步打开共用一条完成路径,不需要 serialId 关联表或轮询。
                FairyOpenRequest request = new FairyOpenRequest(pendingState)
                {
                    Progress = progress,
                };
                serialId = uiManager.OpenUIForm(
                    descriptorAssetName,
                    uiForm.UIGroupName,
                    Constant.AssetPriority.UIFormAsset,
                    uiForm.PauseCoveredUIForm,
                    request);

                FairyUIForm openedForm = await request.Completion.Task.AttachCancellation(openToken);
                ThrowIfOpenInvalidated(lifecycleGeneration, uiManager, openToken);

                try
                {
                    openedForm.AttachOwnerCancellation(
                        ownerToken,
                        ownedSerialId => RequestCloseOwnedUIForm(
                            uiManager,
                            lifecycleGeneration,
                            ownedSerialId));
                }
                catch
                {
                    CloseOwnedUIForm(uiManager, lifecycleGeneration, openedForm.SerialId);
                    throw;
                }

                pendingState = null;
                adopted = true;
                Report(FairyUIOpenStage.Completed, 1f, descriptor.PackageName);
                return openedForm;
            }
            finally
            {
                try
                {
                    if (!adopted)
                    {
                        if (serialId > 0 &&
                            (uiManager.HasUIForm(serialId) || uiManager.IsLoadingUIForm(serialId)))
                        {
                            // GF 已接管 serialId:关闭会走 FairyUIForm.Release,
                            // 未被采纳的 state 由下面的幂等释放兜底。
                            try
                            {
                                uiManager.CloseUIForm(serialId);
                            }
                            catch (Exception exception)
                            {
                                Log.Error(
                                    "Failed to close FairyGUI operation '{0}' (serial {1}) during rollback: {2}",
                                    operationId,
                                    serialId,
                                    exception);
                            }
                        }

                        if (pendingState != null)
                        {
                            ReleasePendingState(pendingState);
                        }
                    }

                    pendingView?.Dispose();
                    packageLease?.Dispose();
                }
                finally
                {
                    Interlocked.Exchange(ref ownerCancellationActive, 0);
                    ownerCancellationRegistration.Dispose();
                    linkedCancellation.Dispose();
                }
            }
        }

        /// <summary>
        /// 在进入 GF 打开与等待结果两个边界校验上下文仍然有效。
        /// 中间 await 的取消由 linked token 负责，不需要逐行检查。
        /// </summary>
        private void ThrowIfOpenInvalidated(
            long lifecycleGeneration,
            IUIManager uiManager,
            CancellationToken openToken)
        {
            openToken.ThrowIfCancellationRequested();
            if (!m_Initialized ||
                lifecycleGeneration != Interlocked.Read(ref m_LifecycleGeneration) ||
                !ReferenceEquals(m_UIManager, uiManager))
            {
                throw new OperationCanceledException(
                    "FairyGUI manager lifecycle changed while opening a form.",
                    openToken);
            }
        }

        private static void ReleasePendingState(FairyUIFormPendingState pendingState)
        {
            if (pendingState == null || !pendingState.TryBeginRelease())
            {
                return;
            }

            TryRelease(
                () => pendingState.Context?.CancelLifetime(),
                pendingState.DescriptorKey,
                "context lifetime");
            TryRelease(
                () => pendingState.Presenter?.OnClose(false, pendingState.UserData),
                pendingState.DescriptorKey,
                "presenter");
            TryRelease(
                () => pendingState.Context?.Clear(),
                pendingState.DescriptorKey,
                "context");
            TryRelease(
                () => pendingState.View?.Dispose(),
                pendingState.DescriptorKey,
                "view");
            TryRelease(
                () => pendingState.PackageLease?.Dispose(),
                pendingState.DescriptorKey,
                "package lease");
        }

        private static void TryRelease(Action release, string descriptorKey, string resourceKind)
        {
            try
            {
                release();
            }
            catch (Exception exception)
            {
                Log.Error(
                    "Failed to release FairyGUI pending {0} for '{1}': {2}",
                    resourceKind,
                    descriptorKey,
                    exception);
            }
        }

        private void ReorderGroups()
        {
            if (m_Groups.Count == 0)
            {
                return;
            }

            GRoot root = GRoot.inst;
            if (root == null || root.isDisposed || root.container == null || root.container.isDisposed)
            {
                return;
            }

            Container rootContainer = root.container;
            List<FairyUIGroupHelper> groups = new List<FairyUIGroupHelper>(m_Groups.Values);
            groups.Sort((left, right) =>
            {
                int depthComparison = left.Depth.CompareTo(right.Depth);
                return depthComparison != 0
                    ? depthComparison
                    : string.CompareOrdinal(left.Name, right.Name);
            });

            int firstIndex = rootContainer.numChildren;
            foreach (FairyUIGroupHelper group in groups)
            {
                int currentIndex = rootContainer.GetChildIndex(group.Container);
                if (currentIndex >= 0 && currentIndex < firstIndex)
                {
                    firstIndex = currentIndex;
                }
            }

            for (int i = 0; i < groups.Count; i++)
            {
                rootContainer.SetChildIndex(groups[i].Container, firstIndex + i);
            }
        }

        private IUIManager GetRequiredUIManager([CallerMemberName] string member = null)
        {
            AssertMainThread(member);
            return m_Initialized && m_UIManager != null
                ? m_UIManager
                : throw new FairyUIException(
                FairyUIError.ManagerNotInitialized,
                "FairyUIManager is not initialized. Call Initialize before using it.");
        }

        /// <summary>
        /// 编辑器下校验调用线程。所有公开入口都经 <see cref="GetRequiredUIManager"/>，
        /// 因此这里一次断言即可覆盖全部转发方法；非编辑器构建被编译器移除，零开销。
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        private static void AssertMainThread(string member)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new FairyUIException(
                    FairyUIError.ThreadViolation,
                    $"FairyUIManager.{member} must be called on the Unity main thread.");
            }
        }

        private void OnOpenUIFormSuccess(object sender, OpenUIFormSuccessEventArgs args)
        {
            if (args.UserData is FairyOpenRequest request)
            {
                if (args.UIForm is FairyUIForm form)
                {
                    request.Completion.TrySetResult(form);
                }
                else
                {
                    request.Completion.TrySetException(new FairyUIException(
                        FairyUIError.FormOpenFailed,
                        Utility.Text.Format(
                            "FairyGUI open request for '{0}' completed with unexpected form type '{1}'.",
                            args.UIForm?.UIFormAssetName ?? "unknown",
                            args.UIForm?.GetType().FullName ?? "null")));
                }
            }

        }

        private void DetachUIManagerEvents(IUIManager uiManager)
        {
            if (!m_EventsAttached || uiManager == null)
            {
                return;
            }

            uiManager.OpenUIFormSuccess -= OnOpenUIFormSuccess;
            uiManager.OpenUIFormFailure -= OnOpenUIFormFailure;
            uiManager.OpenUIFormUpdate -= OnOpenUIFormUpdate;
            uiManager.OpenUIFormDependencyAsset -= OnOpenUIFormDependencyAsset;
            uiManager.CloseUIFormComplete -= OnCloseUIFormComplete;
            m_EventsAttached = false;
        }

        private void DisposeGroupHelpers()
        {
            foreach (FairyUIGroupHelper helper in m_Groups.Values)
            {
                helper.Dispose();
            }

            m_Groups.Clear();
        }

        private void OnOpenUIFormFailure(object sender, OpenUIFormFailureEventArgs args)
        {
            if (args.UserData is FairyOpenRequest request)
            {
                request.Completion.TrySetException(new FairyUIException(
                    FairyUIError.FormOpenFailed,
                    Utility.Text.Format(
                        "FairyGUI UI form '{0}' (operation {1}) failed to open: {2}",
                        args.UIFormAssetName,
                        request.State.OperationId,
                        args.ErrorMessage)));
            }

        }

        private static async UniTask<DRUIForm> ResolveUIFormAsync(
            int uiId,
            CancellationToken cancellationToken)
        {
            if (UIFormTableProvider != null)
            {
                return UIFormTableProvider(uiId);
            }

            TablesComponent tables = GameEntry.Tables;
            int waitFrames = 0;
            while ((tables == null || tables.DTUIForm == null) && waitFrames < 120)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await UniTask.Yield(PlayerLoopTiming.Update);
                tables = GameEntry.Tables ??
                    UnityGameFramework.Runtime.GameEntry.GetComponent<TablesComponent>();
                waitFrames++;
            }

            if (tables == null || tables.DTUIForm == null)
            {
                throw new FairyUIException(
                    FairyUIError.UIFormTableNotReady,
                    "FairyGUI UI form table is not ready within 120 frames. Initialize TablesComponent or set UIFormTableProvider before opening a form.");
            }

            return tables.DTUIForm.GetOrDefault(uiId);
        }

        private void OnOpenUIFormUpdate(object sender, OpenUIFormUpdateEventArgs args)
        {
            if (args.UserData is FairyOpenRequest request)
            {
                request.ReportProgress(
                    FairyUIOpenStage.Asset,
                    0.7f + (0.3f * Mathf.Clamp01(args.Progress)));
            }
        }

        private void OnOpenUIFormDependencyAsset(object sender, OpenUIFormDependencyAssetEventArgs args)
        {
        }

        private void OnCloseUIFormComplete(object sender, CloseUIFormCompleteEventArgs args)
        {
        }

        private void RequestCloseOwnedUIForm(
            IUIManager ownerUIManager,
            long ownerGeneration,
            int serialId)
        {
            if (PlayerLoopHelper.IsMainThread)
            {
                CloseOwnedUIForm(ownerUIManager, ownerGeneration, serialId);
                return;
            }

            PlayerLoopHelper.AddContinuation(
                PlayerLoopTiming.Update,
                () => CloseOwnedUIForm(ownerUIManager, ownerGeneration, serialId));
        }

        private void CloseOwnedUIForm(
            IUIManager ownerUIManager,
            long ownerGeneration,
            int serialId)
        {
            if (!m_Initialized ||
                ownerGeneration != Interlocked.Read(ref m_LifecycleGeneration) ||
                !ReferenceEquals(m_UIManager, ownerUIManager) ||
                ownerUIManager == null ||
                (!ownerUIManager.HasUIForm(serialId) && !ownerUIManager.IsLoadingUIForm(serialId)))
            {
                return;
            }

            ownerUIManager.CloseUIForm(serialId);
        }

        private static bool IsUIInstancePool(ObjectPoolBase pool)
        {
            return pool != null && string.Equals(pool.Name, "UI Instance Pool", StringComparison.Ordinal);
        }

        private static void ReleaseAsset(ResourceComponent resourceComponent, object asset)
        {
            if (asset is UnityEngine.Object unityAsset)
            {
                UnloadAsset(resourceComponent, unityAsset);
            }
        }

        private static void UnloadAsset(ResourceComponent resourceComponent, UnityEngine.Object asset)
        {
            if (resourceComponent == null || asset == null)
            {
                return;
            }

            resourceComponent.UnloadAsset(asset);
        }


        /// <summary>
        /// 用 GF 无类型 LoadAsset 等待加载 descriptor TextAsset。
        ///
        /// 必须无类型:GF UIManager 打开窗体时对同一 assetName 发起的是无类型加载,
        /// 对象池键为 (assetName, null)。有类型加载会产生 (assetName, typeof(TextAsset))
        /// 的第二个池键,AssetBundle 模式下同一个 Unity 对象会被 Register 两次并抛
        /// 同 key ArgumentException。取消语义与 Awaitable 扩展对齐:加载完成后取消会
        /// 释放资产再抛 OperationCanceledException,取消后迟到的结果会被释放。
        /// </summary>
        private static UniTask<TextAsset> LoadDescriptorTextAsync(
            string assetName,
            ResourceComponent resourceComponent,
            CancellationToken cancellationToken)
        {
            if (resourceComponent == null)
            {
                return UniTask.FromException<TextAsset>(
                    new FairyUIException(
                        FairyUIError.ResourceLoadFailed,
                        "FairyGUI resource component is unavailable."));
            }

            if (cancellationToken.IsCancellationRequested)
            {
                return UniTask.FromCanceled<TextAsset>(cancellationToken);
            }

            UniTaskCompletionSource<TextAsset> completion = new UniTaskCompletionSource<TextAsset>();
            TextAsset loadedAsset = null;
            bool finished = false;
            CancellationTokenRegistration cancellationRegistration = default;

            void Finish()
            {
                if (finished)
                {
                    return;
                }

                finished = true;
                cancellationRegistration.Dispose();
            }

            resourceComponent.LoadAsset(
                assetName,
                new LoadAssetCallbacks(
                    (loadedName, asset, duration, userData) =>
                    {
                        if (finished)
                        {
                            // await 已被取消或已失败,释放迟到结果。
                            UnloadAsset(resourceComponent, asset as UnityEngine.Object);
                            return;
                        }

                        if (asset is TextAsset textAsset)
                        {
                            loadedAsset = textAsset;
                            Finish();
                            completion.TrySetResult(textAsset);
                        }
                        else
                        {
                            Finish();
                            UnloadAsset(resourceComponent, asset as UnityEngine.Object);
                            completion.TrySetException(new FairyUIException(
                                FairyUIError.DescriptorLoadFailed,
                                Utility.Text.Format(
                                    "FairyGUI descriptor asset '{0}' has unexpected type '{1}'.",
                                    loadedName,
                                    asset?.GetType().FullName ?? "null")));
                        }
                    },
                    (failedName, status, errorMessage, userData) =>
                    {
                        Finish();
                        completion.TrySetException(new FairyUIException(
                            FairyUIError.DescriptorLoadFailed,
                            Utility.Text.Format(
                                "Can not load FairyGUI descriptor '{0}': {1}.",
                                failedName,
                                errorMessage)));
                    },
                    null,
                    null));

            void CancelLoad()
            {
                if (finished)
                {
                    return;
                }

                Finish();
                if (loadedAsset != null)
                {
                    UnloadAsset(resourceComponent, loadedAsset);
                    loadedAsset = null;
                }

                completion.TrySetCanceled(cancellationToken);
            }

            cancellationRegistration = cancellationToken.Register(() =>
            {
                if (PlayerLoopHelper.IsMainThread)
                {
                    CancelLoad();
                    return;
                }

                PlayerLoopHelper.AddContinuation(PlayerLoopTiming.Update, CancelLoad);
            });

            return completion.Task;
        }

        private static void ValidateDescriptor(FairyUIFormDescriptor descriptor, int uiId, DRUIForm uiForm)
        {
            if (descriptor.UiId != uiId ||
                !string.Equals(descriptor.UiAssetName, uiForm.AssetName, StringComparison.Ordinal) ||
                !string.Equals(descriptor.UiGroupName, uiForm.UIGroupName, StringComparison.Ordinal) ||
                descriptor.AllowMultiInstance != uiForm.AllowMultiInstance ||
                descriptor.PauseCoveredUIForm != uiForm.PauseCoveredUIForm)
            {
                throw new FairyUIException(
                    FairyUIError.DescriptorInvalid,
                    Utility.Text.Format("FairyGUI descriptor identity or GF policy drifted from Luban UI '{0}'.", uiId));
            }
        }

        private static string GetDescriptorAssetName(string uiAssetName)
        {
            string fileName = Path.GetFileName(uiAssetName.Replace('\\', '/'));
            return $"{DescriptorAssetRoot}/{fileName}.json";
        }
    }
}
