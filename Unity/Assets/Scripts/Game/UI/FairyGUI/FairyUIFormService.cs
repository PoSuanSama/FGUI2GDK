using System;
using System.IO;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using UnityGameFramework.Extension;
using UnityGameFramework.Runtime;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Orchestrates the async FairyGUI UIForm open flow.
    /// </summary>
    public static class FairyUIFormService
    {
        private const string DescriptorAssetRoot = "Assets/Res/UI/FairyGUI";
        private const int DesignResolutionX = 1280;
        private const int DesignResolutionY = 720;

        public static async UniTask<UIForm> OpenFairyUIFormAsync(
            int uiId,
            object userData = null,
            CancellationToken ownerToken = default)
        {
            DRUIForm uiForm = GameEntry.Tables.DTUIForm.GetOrDefault(uiId);
            if (uiForm == null)
            {
                throw new GameFrameworkException($"Can not load FairyGUI UI form '{uiId}' from data table.");
            }

            string descriptorAssetName = GetDescriptorAssetName(uiForm.AssetName);
            if (!uiForm.AllowMultiInstance &&
                (GameEntry.UI.IsLoadingUIForm(descriptorAssetName) || GameEntry.UI.HasUIForm(descriptorAssetName)))
            {
                throw new GameFrameworkException(
                    $"FairyGUI UI form '{descriptorAssetName}' is loading or already open.");
            }

            TextAsset descriptorAsset = null;
            FairyPackageLease packageLease = null;
            GComponent pendingView = null;
            FairyUIFormPreparedState preparedState = null;
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
                        Utility.Text.Format(
                            "FairyGUI presenter is not registered for UI '{0}'.",
                            uiId));
                }

                string descriptorKey = Path.GetFileNameWithoutExtension(descriptorAssetName);
                preparedState = new FairyUIFormPreparedState(
                    descriptorKey,
                    descriptor,
                    packageLease,
                    pendingView,
                    presenter,
                    userData);
                packageLease = null;
                pendingView = null;

                presenter.OnViewReady(preparedState.View);
                preparedState.MarkPresenterReady();
                await FairyPackageManager.WaitForPendingAssetsAsync(preparedState, ownerToken);
                ownerToken.ThrowIfCancellationRequested();

                FairyUIRootService.Instance.EnsureInitialized(DesignResolutionX, DesignResolutionY);
                FairyUIRootService.Instance.ValidateGroup(
                    GameEntry.UI.GetUIGroup(uiForm.UIGroupName));
                UIForm openedForm = await OpenPreparedUIFormAsync(
                    descriptorAssetName,
                    uiForm.UIGroupName,
                    Constant.AssetPriority.UIFormAsset,
                    uiForm.PauseCoveredUIForm,
                    userData,
                    preparedState,
                    ownerToken);

                if (openedForm.Logic is not FairyUIFormLogic logic ||
                    !ReferenceEquals(logic.PreparedState, preparedState) ||
                    !preparedState.IsPresenterOpened)
                {
                    if (GameEntry.UI.HasUIForm(openedForm.SerialId))
                    {
                        GameEntry.UI.CloseUIForm(openedForm.SerialId);
                    }

                    throw new GameFrameworkException(
                        $"GF UI form '{openedForm.SerialId}' did not adopt the expected FairyGUI prepared state.");
                }

                logic.ObserveOwnerCancellation(ownerToken);
                preparedState = null;
                return openedForm;
            }
            finally
            {
                FairyUIFormPreparedRegistry.TryRemove(preparedState);
                bool shouldRollBackPreparedState = preparedState != null && !preparedState.IsAdopted;
                if (shouldRollBackPreparedState)
                {
                    try
                    {
                        preparedState.Dispose();
                    }
                    catch (Exception exception)
                    {
                        Log.Error("Failed to roll back FairyGUI prepared state for UI '{0}': {1}", uiId, exception);
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

        private static async UniTask<UIForm> OpenPreparedUIFormAsync(
            string descriptorAssetName,
            string uiGroupName,
            int priority,
            bool pauseCoveredUIForm,
            object userData,
            FairyUIFormPreparedState preparedState,
            CancellationToken ownerToken)
        {
            int serialId;
            using (FairyUIFormPreparedRegistry.BeginOpen(preparedState))
            {
                serialId = GameEntry.UI.OpenUIForm(
                    descriptorAssetName,
                    uiGroupName,
                    priority,
                    pauseCoveredUIForm,
                    userData);
                FairyUIFormPreparedRegistry.BindSerialId(serialId, preparedState);
            }

            while (true)
            {
                if (ownerToken.IsCancellationRequested)
                {
                    if (GameEntry.UI.HasUIForm(serialId) || GameEntry.UI.IsLoadingUIForm(serialId))
                    {
                        GameEntry.UI.CloseUIForm(serialId);
                    }

                    ownerToken.ThrowIfCancellationRequested();
                }

                if (GameEntry.UI.IsLoadingUIForm(serialId))
                {
                    await UniTask.Yield(PlayerLoopTiming.Update);
                    continue;
                }

                UIForm uiForm = GameEntry.UI.GetUIForm(serialId);
                if (uiForm == null)
                {
                    throw new GameFrameworkException(
                        Utility.Text.Format(
                            "Open FairyGUI UI form task failed, asset name '{0}', UI group name '{1}', pause covered UI form '{2}'.",
                            descriptorAssetName,
                            uiGroupName,
                            pauseCoveredUIForm));
                }

                return uiForm;
            }
        }

        private static string GetDescriptorAssetName(string uiAssetName)
        {
            if (string.IsNullOrWhiteSpace(uiAssetName))
            {
                throw new GameFrameworkException("FairyGUI UI form AssetName is required.");
            }

            string fileName = Path.GetFileName(uiAssetName.Replace('\\', '/'));
            if (string.IsNullOrWhiteSpace(fileName))
            {
                throw new GameFrameworkException(
                    $"FairyGUI UI form AssetName '{uiAssetName}' has no descriptor basename.");
            }

            return $"{DescriptorAssetRoot}/{fileName}.json";
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
                    $"FairyGUI descriptor identity or GF policy drifted from Luban UI '{uiId}'.");
            }
        }
    }
}
