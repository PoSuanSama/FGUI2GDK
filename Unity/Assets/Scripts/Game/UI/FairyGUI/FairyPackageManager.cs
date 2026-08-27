using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Extension;
using UnityGameFramework.Runtime;

namespace Game
{
    /// <summary>
    /// A reference-counted lease for a FairyGUI package and its dependencies.
    /// </summary>
    internal sealed class FairyPackageLease : IDisposable
    {
        private FairyPackageManager.PackageState[] m_States;

        internal FairyPackageLease(FairyPackageManager.PackageState[] states)
        {
            m_States = states;
        }

        public UIPackage Package => m_States == null || m_States.Length == 0
            ? null
            : m_States[m_States.Length - 1].Package;

        internal FairyPackageManager.PackageState[] States => m_States;

        public void Dispose()
        {
            FairyPackageManager.PackageState[] states = m_States;
            if (states == null)
            {
                return;
            }

            m_States = null;
            for (int i = states.Length - 1; i >= 0; i--)
            {
                FairyPackageManager.Release(states[i]);
            }
        }
    }

    /// <summary>
    /// Loads FairyGUI packages through the GDK ResourceComponent and owns their lifetime.
    /// </summary>
    internal static class FairyPackageManager
    {
        private const string PackageAssetRoot = "Assets/Res/UI/FairyGUI";
        private const string ManifestAssetPath = "Assets/Res/UI/FairyGUI/GDKFairyManifest.json";

        private static readonly Dictionary<string, PackageState> s_States =
            new Dictionary<string, PackageState>(StringComparer.Ordinal);
        private static readonly Dictionary<string, Exception> s_LastErrors =
            new Dictionary<string, Exception>(StringComparer.Ordinal);

        private static FairyPackageCatalog s_Catalog;
        private static UniTaskCompletionSource<FairyPackageCatalog> s_CatalogLoading;
        private static long s_NextGeneration;

        internal static async UniTask<FairyPackageLease> AcquireAsync(
            string packageName,
            CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("FairyGUI package name is required.", nameof(packageName));
            }

            cancellationToken.ThrowIfCancellationRequested();
            FairyPackageCatalog catalog = await GetCatalogAsync(cancellationToken);
            IReadOnlyList<FairyPackageCatalog.PackageDefinition> loadOrder = catalog.GetLoadOrder(packageName);
            List<PackageState> acquiredStates = new List<PackageState>(loadOrder.Count);
            try
            {
                foreach (FairyPackageCatalog.PackageDefinition package in loadOrder)
                {
                    acquiredStates.Add(await AcquireSingleAsync(package.Name, cancellationToken));
                }

                return new FairyPackageLease(acquiredStates.ToArray());
            }
            catch
            {
                for (int i = acquiredStates.Count - 1; i >= 0; i--)
                {
                    Release(acquiredStates[i]);
                }

                throw;
            }
        }

        internal static async UniTask WaitForPendingAssetsAsync(
            FairyUIFormPreparedState preparedState,
            CancellationToken cancellationToken)
        {
            FairyPackageLease packageLease = preparedState?.PackageLease;
            PackageState[] states = packageLease?.States;
            if (states == null)
            {
                throw new GameFrameworkException("FairyGUI prepared state has no package lease.");
            }

            foreach (PackageState state in states)
            {
                await WaitForPendingAssetsAsync(state, cancellationToken);
            }
        }

        internal static async UniTask WaitForPendingAssetsAsync(
            FairyPackageLease packageLease,
            CancellationToken cancellationToken)
        {
            PackageState[] states = packageLease?.States;
            if (states == null)
            {
                throw new GameFrameworkException("FairyGUI package lease has no states.");
            }

            foreach (PackageState state in states)
            {
                await WaitForPendingAssetsAsync(state, cancellationToken);
            }
        }

        internal static IReadOnlyList<FairyPackageDiagnostic> GetDiagnostics()
        {
            List<FairyPackageDiagnostic> diagnostics = new List<FairyPackageDiagnostic>(s_States.Count);
            foreach (PackageState state in s_States.Values)
            {
                diagnostics.Add(new FairyPackageDiagnostic(
                    state.Name,
                    state.Status,
                    state.Generation,
                    state.ReferenceCount,
                    state.LoadedAssets.Count,
                    state.LastError?.Message));
            }

            diagnostics.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return diagnostics;
        }

        internal static Exception GetLastError(string packageName)
        {
            s_LastErrors.TryGetValue(packageName, out Exception exception);
            return exception;
        }

        internal static IReadOnlyList<string> ValidateCatalogAndGetLoadOrder(string json, string packageName)
        {
            IReadOnlyList<FairyPackageCatalog.PackageDefinition> definitions =
                FairyPackageCatalog.Parse(json).GetLoadOrder(packageName);
            List<string> names = new List<string>(definitions.Count);
            foreach (FairyPackageCatalog.PackageDefinition definition in definitions)
            {
                names.Add(definition.Name);
            }

            return names;
        }

        internal static void Release(PackageState state)
        {
            if (state == null || state.ReferenceCount <= 0)
            {
                return;
            }

            state.ReferenceCount--;
            if (state.ReferenceCount > 0 || !state.IsActive)
            {
                return;
            }

            if (state.Status == FairyPackageStatus.Loading)
            {
                InvalidateLoadingState(state);
                return;
            }

            ReleaseReadyState(state);
        }

        private static async UniTask<FairyPackageCatalog> GetCatalogAsync(CancellationToken cancellationToken)
        {
            if (s_Catalog != null)
            {
                return s_Catalog;
            }

            UniTaskCompletionSource<FairyPackageCatalog> loading = s_CatalogLoading;
            if (loading == null)
            {
                loading =
                    new UniTaskCompletionSource<FairyPackageCatalog>();
                s_CatalogLoading = loading;
                LoadCatalogAsync(loading).Forget();
            }

            cancellationToken.ThrowIfCancellationRequested();
            UniTask<FairyPackageCatalog> task = loading.Task;
            return cancellationToken.CanBeCanceled
                ? await task.AttachCancellation(cancellationToken)
                : await task;
        }

        private static async UniTaskVoid LoadCatalogAsync(
            UniTaskCompletionSource<FairyPackageCatalog> loading)
        {
            TextAsset manifest = null;
            try
            {
                manifest = await GameEntry.Resource.LoadAssetAsync<TextAsset>(ManifestAssetPath);
                FairyPackageCatalog catalog = FairyPackageCatalog.Parse(manifest.text);
                s_Catalog = catalog;
                loading.TrySetResult(catalog);
            }
            catch (Exception exception)
            {
                loading.TrySetException(exception);
            }
            finally
            {
                if (manifest != null)
                {
                    GameEntry.Resource.UnloadAsset(manifest);
                }

                if (ReferenceEquals(s_CatalogLoading, loading))
                {
                    s_CatalogLoading = null;
                }
            }
        }

        private static async UniTask<PackageState> AcquireSingleAsync(
            string packageName,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!s_States.TryGetValue(packageName, out PackageState state))
            {
                state = new PackageState(packageName, ++s_NextGeneration);
                s_States.Add(packageName, state);
                state.ReferenceCount++;
                LoadPackageAsync(state).Forget();
            }
            else
            {
                state.ReferenceCount++;
            }
            try
            {
                if (state.Status == FairyPackageStatus.Loading)
                {
                    UniTask<UIPackage> task = state.Loading.Task;
                    if (cancellationToken.CanBeCanceled)
                    {
                        await task.AttachCancellation(cancellationToken);
                    }
                    else
                    {
                        await task;
                    }
                }

                if (!state.IsActive || state.Status != FairyPackageStatus.Ready || state.Package == null)
                {
                    throw new GameFrameworkException(
                        $"FairyGUI package '{packageName}' did not reach the Ready state.");
                }

                return state;
            }
            catch
            {
                Release(state);
                throw;
            }
        }

        private static async UniTaskVoid LoadPackageAsync(PackageState state)
        {
            UniTaskCompletionSource<UIPackage> loading = state.Loading;
            try
            {
                IReadOnlyList<FairyPackageCatalog.PackageDefinition> definitions =
                    (await GetCatalogAsync(state.LoadCancellation.Token)).GetLoadOrder(state.Name);
                FairyPackageCatalog.PackageDefinition definition = definitions[definitions.Count - 1];
                string descriptorPath = definition.DescriptorAsset;
                TextAsset descriptor = await GameEntry.Resource.LoadAssetAsync<TextAsset>(
                    descriptorPath,
                    cancellationToken: state.LoadCancellation.Token);
                if (!IsCurrent(state))
                {
                    GameEntry.Resource.UnloadAsset(descriptor);
                    throw new OperationCanceledException(state.LoadCancellation.Token);
                }

                state.Descriptor = descriptor;
                string assetNamePrefix = $"{PackageAssetRoot}/{state.Name}";
                UIPackage package = UIPackage.AddPackage(
                    descriptor.bytes,
                    assetNamePrefix,
                    (name, extension, type, item) => LoadPackageResourceAsync(state, name, extension, type, item));
                if (package == null)
                {
                    throw new GameFrameworkException(
                        $"Unable to register FairyGUI package '{state.Name}'.");
                }

                if (!IsCurrent(state))
                {
                    if (UIPackage.GetByName(state.Name) == package)
                    {
                        UIPackage.RemovePackage(state.Name);
                    }

                    throw new OperationCanceledException(state.LoadCancellation.Token);
                }

                state.Package = package;
                await WaitForPendingAssetsAsync(state, state.LoadCancellation.Token);
                if (state.PendingAssetError != null)
                {
                    throw new GameFrameworkException(
                        $"Failed to load an external asset for FairyGUI package '{state.Name}'.",
                        state.PendingAssetError);
                }

                state.Status = FairyPackageStatus.Ready;
                state.LastError = null;
                s_LastErrors.Remove(state.Name);
                loading.TrySetResult(package);
                if (state.ReferenceCount == 0 && state.IsActive)
                {
                    ReleaseReadyState(state);
                }
            }
            catch (Exception exception)
            {
                state.LastError = exception;
                if (exception is not OperationCanceledException)
                {
                    s_LastErrors[state.Name] = exception;
                }

                if (exception is OperationCanceledException canceledException)
                {
                    loading.TrySetCanceled(canceledException.CancellationToken);
                }
                else
                {
                    loading.TrySetException(exception);
                }

                CleanupFailedState(state);
            }
            finally
            {
                if (ReferenceEquals(state.Loading, loading))
                {
                    state.Loading = null;
                }
            }
        }

        private static bool IsCurrent(PackageState state)
        {
            return state.IsActive &&
                   s_States.TryGetValue(state.Name, out PackageState currentState) &&
                   ReferenceEquals(currentState, state);
        }

        private static void InvalidateLoadingState(PackageState state)
        {
            RemoveCurrentState(state);
            state.IsActive = false;
            state.Status = FairyPackageStatus.Releasing;
            CancellationToken loadCancellationToken = state.LoadCancellation.Token;
            CancelLoading(state);
            state.Loading?.TrySetCanceled(loadCancellationToken);
        }

        private static void ReleaseReadyState(PackageState state)
        {
            RemoveCurrentState(state);
            state.IsActive = false;
            state.Status = FairyPackageStatus.Releasing;
            CancelLoading(state);
            if (state.Package != null && UIPackage.GetByName(state.Name) == state.Package)
            {
                UIPackage.RemovePackage(state.Name);
            }

            state.Package = null;
            ReleaseAssets(state);
            state.Status = FairyPackageStatus.Unloaded;
            DisposeLoadingCancellation(state);
        }

        private static void CleanupFailedState(PackageState state)
        {
            RemoveCurrentState(state);
            state.IsActive = false;
            CancelLoading(state);
            if (state.Package != null && UIPackage.GetByName(state.Name) == state.Package)
            {
                UIPackage.RemovePackage(state.Name);
            }

            state.Package = null;
            ReleaseAssets(state);
            state.Status = FairyPackageStatus.Unloaded;
            DisposeLoadingCancellation(state);
        }

        private static void CancelLoading(PackageState state)
        {
            if (!state.LoadCancellationDisposed && !state.LoadCancellation.IsCancellationRequested)
            {
                state.LoadCancellation.Cancel();
            }
        }

        private static void DisposeLoadingCancellation(PackageState state)
        {
            if (state.LoadCancellationDisposed)
            {
                return;
            }

            state.LoadCancellation.Dispose();
            state.LoadCancellationDisposed = true;
        }

        private static void RemoveCurrentState(PackageState state)
        {
            if (s_States.TryGetValue(state.Name, out PackageState currentState) &&
                ReferenceEquals(currentState, state))
            {
                s_States.Remove(state.Name);
            }
        }

        private static void ReleaseAssets(PackageState state)
        {
            if (state.Descriptor != null)
            {
                GameEntry.Resource.UnloadAsset(state.Descriptor);
                state.Descriptor = null;
            }

            foreach (UnityEngine.Object asset in state.LoadedAssets)
            {
                if (asset != null)
                {
                    GameEntry.Resource.UnloadAsset(asset);
                }
            }

            state.LoadedAssets.Clear();
        }

        private static void LoadPackageResourceAsync(
            PackageState state,
            string name,
            string extension,
            Type type,
            PackageItem item)
        {
            if (!IsCurrent(state))
            {
                return;
            }

            if (state.PendingAssetLoads == 0)
            {
                state.PendingAssetsDrained = new UniTaskCompletionSource<bool>();
                state.PendingAssetError = null;
            }

            state.PendingAssetLoads++;
            LoadPackageResourceAsyncCore(state, name, extension, type, item).Forget();
        }

        private static async UniTask LoadPackageResourceAsyncCore(
            PackageState state,
            string name,
            string extension,
            Type type,
            PackageItem item)
        {
            string assetPath = null;
            try
            {
                assetPath = s_Catalog.ResolveRuntimeAsset(state.Name, name, extension);
                UnityEngine.Object asset = await LoadAssetAsync(
                    assetPath,
                    type,
                    state.LoadCancellation.Token);
                if (!IsCurrent(state) || state.Package == null)
                {
                    GameEntry.Resource.UnloadAsset(asset);
                    return;
                }

                state.LoadedAssets.Add(asset);
                item.owner.SetItemAsset(item, asset, DestroyMethod.None);
            }
            catch (OperationCanceledException) when (state.LoadCancellation.IsCancellationRequested)
            {
            }
            catch (Exception exception)
            {
                if (state.IsActive)
                {
                    state.LastError = exception;
                    state.PendingAssetError ??= exception;
                    s_LastErrors[state.Name] = exception;
                    Log.Error(
                        "Failed to load FairyGUI package asset '{0}' ({1}): {2}",
                        assetPath ?? Utility.Text.Format("{0}{1}", name, extension),
                        type.Name,
                        exception);
                }
            }
            finally
            {
                state.PendingAssetLoads--;
                if (state.PendingAssetLoads == 0)
                {
                    state.PendingAssetsDrained?.TrySetResult(true);
                }
            }
        }

        private static async UniTask WaitForPendingAssetsAsync(
            PackageState state,
            CancellationToken cancellationToken)
        {
            while (state.PendingAssetLoads > 0)
            {
                UniTask<bool> task = state.PendingAssetsDrained.Task;
                if (cancellationToken.CanBeCanceled)
                {
                    await task.AttachCancellation(cancellationToken);
                }
                else
                {
                    await task;
                }
            }

            if (state.PendingAssetError != null)
            {
                throw new GameFrameworkException(
                    $"Failed to load an external asset for FairyGUI package '{state.Name}'.",
                    state.PendingAssetError);
            }
        }

        private static async UniTask<UnityEngine.Object> LoadAssetAsync(
            string assetPath,
            Type type,
            CancellationToken cancellationToken)
        {
            if (type == typeof(Texture))
            {
                return await GameEntry.Resource.LoadAssetAsync<Texture>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            if (type == typeof(AudioClip))
            {
                return await GameEntry.Resource.LoadAssetAsync<AudioClip>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            if (type == typeof(TextAsset))
            {
                return await GameEntry.Resource.LoadAssetAsync<TextAsset>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            if (type == typeof(Font))
            {
                return await GameEntry.Resource.LoadAssetAsync<Font>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            return await GameEntry.Resource.LoadAssetAsync<UnityEngine.Object>(
                assetPath,
                cancellationToken: cancellationToken);
        }

        internal sealed class PackageState
        {
            internal readonly string Name;
            internal readonly long Generation;
            internal readonly List<UnityEngine.Object> LoadedAssets = new List<UnityEngine.Object>();
            internal readonly CancellationTokenSource LoadCancellation = new CancellationTokenSource();
            internal TextAsset Descriptor;
            internal UIPackage Package;
            internal UniTaskCompletionSource<UIPackage> Loading = new UniTaskCompletionSource<UIPackage>();
            internal UniTaskCompletionSource<bool> PendingAssetsDrained = new UniTaskCompletionSource<bool>();
            internal Exception LastError;
            internal Exception PendingAssetError;
            internal int PendingAssetLoads;
            internal int ReferenceCount;
            internal bool LoadCancellationDisposed;
            internal bool IsActive = true;
            internal FairyPackageStatus Status = FairyPackageStatus.Loading;

            internal PackageState(string name, long generation)
            {
                Name = name;
                Generation = generation;
            }
        }
    }

    internal enum FairyPackageStatus
    {
        Unloaded,
        Loading,
        Ready,
        Releasing,
    }

    internal readonly struct FairyPackageDiagnostic
    {
        internal string Name { get; }
        internal FairyPackageStatus Status { get; }
        internal long Generation { get; }
        internal int ReferenceCount { get; }
        internal int LoadedAssetCount { get; }
        internal string LastError { get; }

        internal FairyPackageDiagnostic(
            string name,
            FairyPackageStatus status,
            long generation,
            int referenceCount,
            int loadedAssetCount,
            string lastError)
        {
            Name = name;
            Status = status;
            Generation = generation;
            ReferenceCount = referenceCount;
            LoadedAssetCount = loadedAssetCount;
            LastError = lastError;
        }
    }
}
