using System;
using System.Collections.Generic;
using System.IO;
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
        /// GF 打开失败事件桥:业务可以订阅稳定回调,不再依赖打开轮询兜底。
        /// 参数按引用持有且不可变——回调内只读,不要自行 Release。
        /// </summary>
        public static event EventHandler<OpenUIFormFailureEventArgs> OpenUIFormFailure;

        /// <summary>
        /// GF 关闭完成事件桥:回调参数为被关闭的 serial ID。
        /// </summary>
        public static event Action<int> CloseUIFormComplete;

        /// <summary>
        /// GF 打开成功事件桥:业务可订阅打开完成的稳定回调,不再依赖打开轮询兜底。
        /// </summary>
        public static event EventHandler<OpenUIFormSuccessEventArgs> OpenUIFormSuccess;

        /// <summary>
        /// GF 打开更新事件桥:打开流程的进度回调。
        /// </summary>
        public static event EventHandler<OpenUIFormUpdateEventArgs> OpenUIFormUpdate;

        /// <summary>
        /// GF 打开依赖资源事件桥:打开流程加载依赖资源时回调。
        /// </summary>
        public static event EventHandler<OpenUIFormDependencyAssetEventArgs> OpenUIFormDependencyAsset;

        private const string DescriptorAssetRoot = "Assets/Res/UI/FairyGUI";
        private const int DesignResolutionX = 1280;
        private const int DesignResolutionY = 720;

        private IUIManager m_UIManager;
        private FairyUIFormHelper m_UIFormHelper;
        private readonly Dictionary<string, FairyUIGroupHelper> m_Groups =
            new Dictionary<string, FairyUIGroupHelper>(StringComparer.Ordinal);
        private bool m_EventsAttached;

        public void Initialize()
        {
            m_UIManager = GameFrameworkEntry.GetModule<IUIManager>();
            if (m_UIManager == null)
            {
                throw new GameFrameworkException("UI manager is invalid.");
            }

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

            m_UIFormHelper = new FairyUIFormHelper(ReleaseAsset);
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

        public bool HasUIGroup(string name) => GetRequiredUIManager().HasUIGroup(name);

        public IUIGroup GetUIGroup(string name) => GetRequiredUIManager().GetUIGroup(name);

        public bool HasUIForm(int serialId) => GetRequiredUIManager().HasUIForm(serialId);

        public bool HasUIForm(string assetName) => GetRequiredUIManager().HasUIForm(assetName);

        public bool IsLoadingUIForm(int serialId) => GetRequiredUIManager().IsLoadingUIForm(serialId);

        public bool IsLoadingUIForm(string assetName) => GetRequiredUIManager().IsLoadingUIForm(assetName);

        public FairyUIForm GetUIForm(int serialId) => GetRequiredUIManager().GetUIForm(serialId) as FairyUIForm;

        public FairyUIForm GetUIForm(string assetName) => GetRequiredUIManager().GetUIForm(assetName) as FairyUIForm;

        public FairyUIForm[] GetAllLoadedUIForms()
        {
            IUIForm[] loaded = GetRequiredUIManager().GetAllLoadedUIForms();
            FairyUIForm[] result = new FairyUIForm[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
            {
                result[i] = loaded[i] as FairyUIForm;
            }

            return result;
        }

        public int[] GetAllLoadingUIFormSerialIds() => GetRequiredUIManager().GetAllLoadingUIFormSerialIds();

        public void CloseUIForm(int serialId) => GetRequiredUIManager().CloseUIForm(serialId);

        public void CloseUIForm(FairyUIForm form) => GetRequiredUIManager().CloseUIForm(form);

        public void RefocusUIForm(FairyUIForm form) => GetRequiredUIManager().RefocusUIForm(form);

        public void RefocusUIForm(FairyUIForm form, object userData) =>
            GetRequiredUIManager().RefocusUIForm(form, userData);

        /// <summary>
        /// GF 语义层透出:界面实例加锁/解锁(锁定后回收的对象池实例不会被复用于其他界面)。
        /// </summary>
        public void SetUIFormInstanceLocked(object uiFormInstance, bool locked) =>
            GetRequiredUIManager().SetUIFormInstanceLocked(uiFormInstance, locked);

        /// <summary>
        /// GF 语义层透出:设置界面实例优先级(影响对象池回收排序)。
        /// </summary>
        public void SetUIFormInstancePriority(object uiFormInstance, int priority) =>
            GetRequiredUIManager().SetUIFormInstancePriority(uiFormInstance, priority);

        /// <summary>
        /// GF 语义层透出:界面实例对象池自动释放间隔秒数。
        /// </summary>
        public float InstanceAutoReleaseInterval
        {
            get => GetRequiredUIManager().InstanceAutoReleaseInterval;
            set => GetRequiredUIManager().InstanceAutoReleaseInterval = value;
        }

        /// <summary>
        /// GF 语义层透出:界面实例对象池容量。
        /// </summary>
        public int InstanceCapacity
        {
            get => GetRequiredUIManager().InstanceCapacity;
            set => GetRequiredUIManager().InstanceCapacity = value;
        }

        /// <summary>
        /// GF 语义层透出:界面实例对象池对象过期秒数。
        /// </summary>
        public float InstanceExpireTime
        {
            get => GetRequiredUIManager().InstanceExpireTime;
            set => GetRequiredUIManager().InstanceExpireTime = value;
        }

        /// <summary>
        /// GF 语义层透出:界面实例对象池默认优先级(区别于 SetUIFormInstancePriority 的实例级优先级)。
        /// </summary>
        public int InstancePriority
        {
            get => GetRequiredUIManager().InstancePriority;
            set => GetRequiredUIManager().InstancePriority = value;
        }

        public bool IsValidUIForm(FairyUIForm form) => GetRequiredUIManager().IsValidUIForm(form);

        public int UIGroupCount => GetRequiredUIManager().UIGroupCount;

        public IUIGroup[] GetAllUIGroups() => GetRequiredUIManager().GetAllUIGroups();

        public void CloseAllLoadedUIForms() => GetRequiredUIManager().CloseAllLoadedUIForms();

        public void CloseAllLoadedUIForms(object userData) => GetRequiredUIManager().CloseAllLoadedUIForms(userData);

        public void CloseAllLoadingUIForms() => GetRequiredUIManager().CloseAllLoadingUIForms();

        public async UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData = null,
            CancellationToken ownerToken = default)
        {
            return await OpenFairyUIFormAsync(uiId, userData, ownerToken, null);
        }

        internal async UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData,
            CancellationToken ownerToken,
            Func<FairyUIFormDescriptor, IFairyUIPresenter> presenterFactory)
        {
            IUIManager uiManager = GetRequiredUIManager();
            DRUIForm uiForm = UIFormTableProvider != null
                ? UIFormTableProvider(uiId)
                : GameEntry.Tables.DTUIForm.GetOrDefault(uiId);
            if (uiForm == null)
            {
                throw new GameFrameworkException($"Can not load UI form '{uiId}' from data table.");
            }

            string descriptorAssetName = GetDescriptorAssetName(uiForm.AssetName);
            if (!uiForm.AllowMultiInstance &&
                (uiManager.IsLoadingUIForm(descriptorAssetName) || uiManager.HasUIForm(descriptorAssetName)))
            {
                throw new GameFrameworkException(
                    $"FairyGUI UI form '{descriptorAssetName}' is loading or already open.");
            }

            TextAsset descriptorAsset = null;
            FairyPackageLease packageLease = null;
            GComponent pendingView = null;
            FairyUIFormPendingState pendingState = null;
            try
            {
                ownerToken.ThrowIfCancellationRequested();
                descriptorAsset = await LoadDescriptorTextAsync(descriptorAssetName, ownerToken);
                FairyUIFormDescriptor descriptor = FairyUIFormDescriptor.Parse(descriptorAsset.text);
                ValidateDescriptor(descriptor, uiId, uiForm);

                // descriptor 解析后原始 TextAsset 立即释放回池:GF OpenUIForm 稍后会对同一
                // descriptorAssetName 再发起一次加载(作为窗体资产 token)。这里用无类型加载,
                // 与 GF UIManager 的加载共享同一对象池键 (assetName, null)——Editor Resource
                // Mode 不经过 Asset Pool 而不暴露差异;AssetBundle 模式若用有类型加载
                // (assetName, typeof(TextAsset)) 会与 GF 的键分裂,同一个 Unity 对象被 Register
                // 两次并抛同 key ArgumentException。解析后释放让 GF 的后续加载直接 Spawn 复用。
                GameEntry.Resource.UnloadAsset(descriptorAsset);
                descriptorAsset = null;

                Action<FairyUIFormDescriptor> preparePackage = FairyUIPresenterRegistry.PreparePackage;
                Func<FairyUIFormDescriptor, IFairyUIPresenter> createPresenter =
                    FairyUIPresenterRegistry.CreatePresenter;
                if (preparePackage == null)
                {
                    throw new GameFrameworkException(
                        "FairyGUI package binding must be registered before opening a form.");
                }

                if (presenterFactory == null && createPresenter == null)
                {
                    throw new GameFrameworkException(
                        "Either a per-open presenter factory or a class presenter registry must be available before opening a form.");
                }

                packageLease = await FairyPackageManager.AcquireAsync(descriptor.PackageName, ownerToken);
                await FairyLocalization.ApplyAsync(descriptor.PackageName, ownerToken);
                preparePackage(descriptor);
                pendingView = UIPackage.CreateObject(
                    descriptor.PackageName,
                    descriptor.ComponentName) as GComponent;
                if (pendingView == null)
                {
                    throw new GameFrameworkException(
                        $"Unable to create FairyGUI component '{descriptor.PackageName}/{descriptor.ComponentName}'.");
                }

                if (!string.Equals(pendingView.GetType().FullName, descriptor.BindingType, StringComparison.Ordinal))
                {
                    throw new GameFrameworkException(
                        Utility.Text.Format(
                            "FairyGUI binding type mismatch for UI '{0}': expected '{1}', found '{2}'.",
                            uiId,
                            descriptor.BindingType,
                            pendingView.GetType().FullName));
                }

                // per-open 工厂优先(ET Component/System 路径);返回 null 时回退类 Presenter 注册表。
                IFairyUIPresenter presenter = null;
                if (presenterFactory != null)
                {
                    presenter = presenterFactory(descriptor);
                }

                if (presenter == null && createPresenter != null)
                {
                    presenter = createPresenter(descriptor);
                }

                if (presenter == null)
                {
                    throw new GameFrameworkException(
                        $"FairyGUI presenter is not registered for UI '{descriptor.CsName}'.");
                }

                FairyUIFormContext context = new FairyUIFormContext
                {
                    View = pendingView,
                    UIId = uiId
                };
                string descriptorKey = Path.GetFileNameWithoutExtension(descriptorAssetName);
                pendingState = new FairyUIFormPendingState(
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
                await FairyPackageManager.WaitForPendingAssetsAsync(pendingState.PackageLease, ownerToken);
                ownerToken.ThrowIfCancellationRequested();

                if (!uiManager.HasUIGroup(uiForm.UIGroupName))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI UI group '{uiForm.UIGroupName}' is not registered.");
                }

                int serialId;
                using (FairyUIFormPendingRegistry.BeginOpen(pendingState))
                {
                    serialId = uiManager.OpenUIForm(
                        descriptorAssetName,
                        uiForm.UIGroupName,
                        Constant.AssetPriority.UIFormAsset,
                        uiForm.PauseCoveredUIForm,
                        userData);
                    FairyUIFormPendingRegistry.BindSerialId(serialId, pendingState);
                }

                while (true)
                {
                    if (ownerToken.IsCancellationRequested)
                    {
                        if (uiManager.HasUIForm(serialId) || uiManager.IsLoadingUIForm(serialId))
                        {
                            uiManager.CloseUIForm(serialId);
                        }

                        ownerToken.ThrowIfCancellationRequested();
                    }

                    if (uiManager.IsLoadingUIForm(serialId))
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        continue;
                    }

                    FairyUIForm openedForm = uiManager.GetUIForm(serialId) as FairyUIForm;
                    if (openedForm == null)
                    {
                        throw new GameFrameworkException(
                            $"Open FairyGUI UI form failed, asset name '{descriptorAssetName}'.");
                    }

                    pendingState = null;
                    try
                    {
                        openedForm.AttachOwnerCancellation(ownerToken, RequestCloseOwnedUIForm);
                        ownerToken.ThrowIfCancellationRequested();
                        return openedForm;
                    }
                    catch
                    {
                        CloseOwnedUIForm(serialId);
                        throw;
                    }
                }
            }
            finally
            {
                if (pendingState != null)
                {
                    FairyUIFormPendingRegistry.TryRemove(pendingState);
                    if (!pendingState.IsAdopted)
                    {
                        pendingState.PackageLease?.Dispose();
                        pendingState.View?.Dispose();
                    }
                }

                pendingView?.Dispose();
                packageLease?.Dispose();
                if (descriptorAsset != null)
                {
                    GameEntry.Resource.UnloadAsset(descriptorAsset);
                }
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

        private IUIManager GetRequiredUIManager()
        {
            return m_UIManager ?? throw new GameFrameworkException(
                "FairyUIManager is not initialized. Call Initialize before using it.");
        }

        private void OnOpenUIFormSuccess(object sender, OpenUIFormSuccessEventArgs args)
        {
            OpenUIFormSuccess?.Invoke(sender, args);
        }

        private void OnOpenUIFormFailure(object sender, OpenUIFormFailureEventArgs args)
        {
            OpenUIFormFailure?.Invoke(sender, args);
        }

        private void OnOpenUIFormUpdate(object sender, OpenUIFormUpdateEventArgs args)
        {
            OpenUIFormUpdate?.Invoke(sender, args);
        }

        private void OnOpenUIFormDependencyAsset(object sender, OpenUIFormDependencyAssetEventArgs args)
        {
            OpenUIFormDependencyAsset?.Invoke(sender, args);
        }

        private void OnCloseUIFormComplete(object sender, CloseUIFormCompleteEventArgs args)
        {
            CloseUIFormComplete?.Invoke(args.SerialId);
        }

        private void RequestCloseOwnedUIForm(int serialId)
        {
            if (PlayerLoopHelper.IsMainThread)
            {
                CloseOwnedUIForm(serialId);
                return;
            }

            PlayerLoopHelper.AddContinuation(
                PlayerLoopTiming.Update,
                () => CloseOwnedUIForm(serialId));
        }

        private void CloseOwnedUIForm(int serialId)
        {
            IUIManager uiManager = m_UIManager;
            if (uiManager == null ||
                (!uiManager.HasUIForm(serialId) && !uiManager.IsLoadingUIForm(serialId)))
            {
                return;
            }

            uiManager.CloseUIForm(serialId);
        }

        private static bool IsUIInstancePool(ObjectPoolBase pool)
        {
            return pool != null && string.Equals(pool.Name, "UI Instance Pool", StringComparison.Ordinal);
        }

        private void ReleaseAsset(object asset)
        {
            if (asset is UnityEngine.Object unityAsset)
            {
                GameEntry.Resource.UnloadAsset(unityAsset);
            }
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
            CancellationToken cancellationToken)
        {
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

            GameEntry.Resource.LoadAsset(
                assetName,
                new LoadAssetCallbacks(
                    (loadedName, asset, duration, userData) =>
                    {
                        if (finished)
                        {
                            // await 已被取消或已失败,释放迟到结果。
                            GameEntry.Resource.UnloadAsset(asset);
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
                            GameEntry.Resource.UnloadAsset(asset);
                            completion.TrySetException(new GameFrameworkException(
                                Utility.Text.Format(
                                    "FairyGUI descriptor asset '{0}' has unexpected type '{1}'.",
                                    loadedName,
                                    asset?.GetType().FullName ?? "null")));
                        }
                    },
                    (failedName, status, errorMessage, userData) =>
                    {
                        Finish();
                        completion.TrySetException(new GameFrameworkException(
                            Utility.Text.Format(
                                "Can not load FairyGUI descriptor '{0}': {1}.",
                                failedName,
                                errorMessage)));
                    },
                    null,
                    null));

            cancellationRegistration = cancellationToken.Register(() =>
            {
                Finish();
                if (loadedAsset != null)
                {
                    GameEntry.Resource.UnloadAsset(loadedAsset);
                    loadedAsset = null;
                }

                completion.TrySetCanceled(cancellationToken);
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
                throw new GameFrameworkException(
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
