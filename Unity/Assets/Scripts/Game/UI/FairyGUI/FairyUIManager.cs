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
                m_UIManager.OpenUIFormFailure += OnOpenUIFormFailure;
                m_UIManager.CloseUIFormComplete += OnCloseUIFormComplete;
                m_EventsAttached = true;
            }

            GRoot root = GRoot.inst;
            root.SetContentScaleFactor(
                DesignResolutionX,
                DesignResolutionY,
                UIContentScaler.ScreenMatchMode.MatchWidthOrHeight);

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
                descriptorAsset = await GameEntry.Resource.LoadAssetAsync<TextAsset>(
                    descriptorAssetName,
                    cancellationToken: ownerToken);
                FairyUIFormDescriptor descriptor = FairyUIFormDescriptor.Parse(descriptorAsset.text);
                ValidateDescriptor(descriptor, uiId, uiForm);

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

        private void OnOpenUIFormFailure(object sender, OpenUIFormFailureEventArgs args)
        {
            OpenUIFormFailure?.Invoke(sender, args);
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
