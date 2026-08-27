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

        private const string DescriptorAssetRoot = "Assets/Res/UI/FairyGUI";
        private const int DesignResolutionX = 1280;
        private const int DesignResolutionY = 720;

        private IUIManager m_UIManager;
        private FairyUIFormHelper m_UIFormHelper;
        private readonly Dictionary<string, FairyUIGroupHelper> m_Groups =
            new Dictionary<string, FairyUIGroupHelper>(StringComparer.Ordinal);

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

            FairyUIRootService.Instance.EnsureInitialized(DesignResolutionX, DesignResolutionY);
        }

        public bool AddUIGroup(string name, int depth)
        {
            if (m_Groups.ContainsKey(name))
            {
                return false;
            }

            FairyUIGroupHelper helper = new FairyUIGroupHelper(name);
            if (!m_UIManager.AddUIGroup(name, depth, helper))
            {
                helper.Dispose();
                return false;
            }

            m_Groups.Add(name, helper);
            helper.AttachToRoot(GRoot.inst);
            ReorderGroups();
            return true;
        }

        public bool HasUIGroup(string name) => m_UIManager.HasUIGroup(name);

        public IUIGroup GetUIGroup(string name) => m_UIManager.GetUIGroup(name);

        public bool HasUIForm(int serialId) => m_UIManager.HasUIForm(serialId);

        public bool HasUIForm(string assetName) => m_UIManager.HasUIForm(assetName);

        public bool IsLoadingUIForm(int serialId) => m_UIManager.IsLoadingUIForm(serialId);

        public bool IsLoadingUIForm(string assetName) => m_UIManager.IsLoadingUIForm(assetName);

        public FairyUIForm GetUIForm(int serialId) => m_UIManager.GetUIForm(serialId) as FairyUIForm;

        public FairyUIForm GetUIForm(string assetName) => m_UIManager.GetUIForm(assetName) as FairyUIForm;

        public FairyUIForm[] GetAllLoadedUIForms()
        {
            IUIForm[] loaded = m_UIManager.GetAllLoadedUIForms();
            FairyUIForm[] result = new FairyUIForm[loaded.Length];
            for (int i = 0; i < loaded.Length; i++)
            {
                result[i] = loaded[i] as FairyUIForm;
            }

            return result;
        }

        public int[] GetAllLoadingUIFormSerialIds() => m_UIManager.GetAllLoadingUIFormSerialIds();

        public void CloseUIForm(int serialId) => m_UIManager.CloseUIForm(serialId);

        public void CloseUIForm(FairyUIForm form) => m_UIManager.CloseUIForm(form);

        public void RefocusUIForm(FairyUIForm form) => m_UIManager.RefocusUIForm(form);

        public void RefocusUIForm(FairyUIForm form, object userData) => m_UIManager.RefocusUIForm(form, userData);

        public async UniTask<FairyUIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData = null,
            CancellationToken ownerToken = default)
        {
            DRUIForm uiForm = GameEntry.Tables.DTUIForm.GetOrDefault(uiId);
            if (uiForm == null)
            {
                throw new GameFrameworkException($"Can not load UI form '{uiId}' from data table.");
            }

            string descriptorAssetName = GetDescriptorAssetName(uiForm.AssetName);
            if (!uiForm.AllowMultiInstance &&
                (m_UIManager.IsLoadingUIForm(descriptorAssetName) || m_UIManager.HasUIForm(descriptorAssetName)))
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
                if (preparePackage == null || createPresenter == null)
                {
                    throw new GameFrameworkException(
                        "FairyGUI package binding and presenter factories must be registered before opening a form.");
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

                IFairyUIPresenter presenter = createPresenter(descriptor);
                if (presenter == null)
                {
                    throw new GameFrameworkException(
                        $"FairyGUI presenter is not registered for UI '{descriptor.CsName}'.");
                }

                string descriptorKey = Path.GetFileNameWithoutExtension(descriptorAssetName);
                pendingState = new FairyUIFormPendingState(
                    descriptorKey,
                    descriptor,
                    packageLease,
                    pendingView,
                    presenter,
                    userData);
                packageLease = null;
                pendingView = null;

                presenter.OnViewReady(pendingState.View);
                await FairyPackageManager.WaitForPendingAssetsAsync(pendingState.PackageLease, ownerToken);
                ownerToken.ThrowIfCancellationRequested();

                FairyUIRootService.Instance.EnsureInitialized(DesignResolutionX, DesignResolutionY);
                if (!m_UIManager.HasUIGroup(uiForm.UIGroupName))
                {
                    throw new GameFrameworkException(
                        $"FairyGUI UI group '{uiForm.UIGroupName}' is not registered.");
                }

                int serialId;
                using (FairyUIFormPendingRegistry.BeginOpen(pendingState))
                {
                    serialId = m_UIManager.OpenUIForm(
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
                        if (m_UIManager.HasUIForm(serialId) || m_UIManager.IsLoadingUIForm(serialId))
                        {
                            m_UIManager.CloseUIForm(serialId);
                        }

                        ownerToken.ThrowIfCancellationRequested();
                    }

                    if (m_UIManager.IsLoadingUIForm(serialId))
                    {
                        await UniTask.Yield(PlayerLoopTiming.Update);
                        continue;
                    }

                    FairyUIForm openedForm = m_UIManager.GetUIForm(serialId) as FairyUIForm;
                    if (openedForm == null)
                    {
                        throw new GameFrameworkException(
                            $"Open FairyGUI UI form failed, asset name '{descriptorAssetName}'.");
                    }

                    pendingState = null;
                    return openedForm;
                }
            }
            finally
            {
                if (pendingState != null)
                {
                    FairyUIFormPendingRegistry.TryRemove(pendingState);
                    pendingState.PackageLease?.Dispose();
                    pendingState.View?.Dispose();
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
