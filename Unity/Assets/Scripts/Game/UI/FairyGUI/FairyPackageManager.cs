using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
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
        private bool m_Pinned;

        internal FairyPackageLease(FairyPackageManager.PackageState[] states)
        {
            m_States = states;
        }

        public UIPackage Package => m_States == null || m_States.Length == 0
            ? null
            : m_States[m_States.Length - 1].Package;

        internal FairyPackageManager.PackageState[] States => m_States;

        /// <summary>
        /// 把租约覆盖的包（含依赖闭包）标记为常驻。常驻后引用归零也不卸载，
        /// 直到 <see cref="UnpinAndDispose"/> 显式解除。
        /// </summary>
        internal void MarkPinned()
        {
            FairyPackageManager.PackageState[] states = m_States;
            if (states == null || m_Pinned)
            {
                return;
            }

            m_Pinned = true;
            for (int i = 0; i < states.Length; i++)
            {
                states[i].PinCount++;
            }
        }

        /// <summary>
        /// 解除常驻并释放租约。解除后若引用计数与常驻计数均为零，包会被卸载。
        /// </summary>
        internal void UnpinAndDispose()
        {
            FairyPackageManager.PackageState[] states = m_States;
            if (states != null && m_Pinned)
            {
                m_Pinned = false;
                for (int i = 0; i < states.Length; i++)
                {
                    if (states[i].PinCount > 0)
                    {
                        states[i].PinCount--;
                    }
                }
            }

            Dispose();
        }

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

        /// <summary>
        /// 常驻包名 → 持有中的租约。租约不释放，包因此保持 Ready。
        /// </summary>
        private static readonly Dictionary<string, FairyPackageLease> s_WarmLeases =
            new Dictionary<string, FairyPackageLease>(StringComparer.Ordinal);

        private static FairyPackageCatalog s_Catalog;
        private static UniTaskCompletionSource<FairyPackageCatalog> s_CatalogLoading;
        private static long s_NextGeneration;
        private static long s_CatalogGeneration;

#if UNITY_EDITOR
        // Editor-only failure seam used by AgentCallable lifecycle probes. Player builds
        // always use ResourceComponent directly and therefore have no test override state.
        internal static Func<
            ResourceComponent,
            string,
            CancellationToken,
            UniTask<TextAsset>> DescriptorLoaderOverride;
#endif

        internal static async UniTask<FairyPackageLease> AcquireAsync(
            string packageName,
            CancellationToken cancellationToken = default)
        {
            AssertMainThread();
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

        /// <summary>
        /// 预热并常驻一个包（含其依赖闭包）。已常驻时直接返回。
        /// 预热等价于常驻：引用归零即卸载，因此「预热后立刻释放」没有意义。
        /// 不应用本地化——strings source 是全局单槽，多个包预热会互相覆盖。
        /// </summary>
        internal static async UniTask WarmPackageAsync(
            string packageName,
            CancellationToken cancellationToken = default)
        {
            AssertMainThread();
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("FairyGUI package name is required.", nameof(packageName));
            }

            if (s_WarmLeases.ContainsKey(packageName))
            {
                return;
            }

            FairyPackageLease lease = await AcquireAsync(packageName, cancellationToken);

            // await 期间可能被另一个 warm 抢先登记。
            if (s_WarmLeases.ContainsKey(packageName))
            {
                lease.Dispose();
                return;
            }

            lease.MarkPinned();
            s_WarmLeases.Add(packageName, lease);
        }

        /// <summary>
        /// 解除指定包的常驻。返回是否确实解除。
        /// </summary>
        internal static bool ReleaseWarmPackage(string packageName)
        {
            AssertMainThread();
            if (string.IsNullOrEmpty(packageName) ||
                !s_WarmLeases.TryGetValue(packageName, out FairyPackageLease lease))
            {
                return false;
            }

            s_WarmLeases.Remove(packageName);
            lease.UnpinAndDispose();
            return true;
        }

        internal static bool IsPackageWarm(string packageName)
        {
            return !string.IsNullOrEmpty(packageName) && s_WarmLeases.ContainsKey(packageName);
        }

        /// <summary>
        /// 解除全部常驻。必须在 Shutdown 之前调用，避免常驻状态跨运行时代残留。
        /// </summary>
        internal static void ClearWarmPackages()
        {
            AssertMainThread();
            if (s_WarmLeases.Count == 0)
            {
                return;
            }

            List<FairyPackageLease> leases = new List<FairyPackageLease>(s_WarmLeases.Values);
            s_WarmLeases.Clear();
            for (int i = 0; i < leases.Count; i++)
            {
                try
                {
                    leases[i].UnpinAndDispose();
                }
                catch (Exception exception)
                {
                    Log.Error("Failed to release a warm FairyGUI package: {0}", exception);
                }
            }
        }

        internal static async UniTask WaitForPendingAssetsAsync(
            FairyPackageLease packageLease,
            CancellationToken cancellationToken)
        {
            PackageState[] states = packageLease?.States;
            if (states == null)
            {
                throw new FairyUIException(
                    FairyUIError.PackageNotReady,
                    "FairyGUI package lease has no states.");
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
                    state.LastError?.Message,
                    state.PinCount > 0));
            }

            diagnostics.Sort((left, right) => string.CompareOrdinal(left.Name, right.Name));
            return diagnostics;
        }

        /// <summary>
        /// 编辑器下校验调用线程。包状态表是静态且无锁的，只在主线程维护；
        /// 非编辑器构建被编译器移除，零开销。
        /// </summary>
        [Conditional("UNITY_EDITOR")]
        private static void AssertMainThread([CallerMemberName] string member = null)
        {
            if (!PlayerLoopHelper.IsMainThread)
            {
                throw new FairyUIException(
                    FairyUIError.ThreadViolation,
                    $"FairyPackageManager.{member} must be called on the Unity main thread.");
            }
        }

        internal static void Shutdown()
        {
            AssertMainThread();

            // 租约随 state 一同销毁，这里只清表；state 随后被 ReleaseReadyState 释放。
            s_WarmLeases.Clear();

            List<PackageState> states = new List<PackageState>(s_States.Values);
            foreach (PackageState state in states)
            {
                state.PinCount = 0;
                state.ReferenceCount = Math.Max(1, state.ReferenceCount);
                if (state.Status == FairyPackageStatus.Loading)
                {
                    InvalidateLoadingState(state);
                }
                else
                {
                    ReleaseReadyState(state);
                }
            }

            s_States.Clear();
            s_Catalog = null;
            s_CatalogGeneration++;
            s_CatalogLoading?.TrySetCanceled();
            s_CatalogLoading = null;
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

        internal static void ValidateDescriptorIdentity(FairyUIFormDescriptor descriptor)
        {
            if (s_Catalog == null)
            {
                throw new FairyUIException(
                    FairyUIError.PackageManifestInvalid,
                    "FairyGUI package catalog is not loaded.");
            }

            s_Catalog.ValidateDescriptorIdentity(descriptor);
        }

        internal static void VerifyAssetHash(
            string packageName,
            string assetPath,
            byte[] bytes)
        {
            if (s_Catalog == null)
            {
                throw new FairyUIException(
                    FairyUIError.PackageManifestInvalid,
                    "FairyGUI package catalog is not loaded.");
            }

            s_Catalog.VerifyAssetHash(packageName, assetPath, bytes);
        }

        internal static void VerifyAssetHash(
            string packageName,
            string assetPath,
            UnityEngine.Object asset)
        {
            if (asset is TextAsset textAsset)
            {
                VerifyAssetHash(packageName, assetPath, textAsset.bytes);
                return;
            }

            string expectedHash = s_Catalog?.GetExpectedHash(packageName, assetPath);
            if (string.IsNullOrEmpty(expectedHash))
            {
                return;
            }

            throw new FairyUIException(
                FairyUIError.PackageManifestInvalid,
                Utility.Text.Format(
                    "FairyGUI asset '{0}' for package '{1}' declares a hash, but imported type '{2}' cannot expose source bytes.",
                    assetPath,
                    packageName,
                    asset?.GetType().Name ?? "null"));
        }

        internal static void Release(PackageState state)
        {
            if (state == null || state.ReferenceCount <= 0)
            {
                return;
            }

            state.ReferenceCount--;
            if (state.ReferenceCount > 0 || !state.IsActive || state.PinCount > 0)
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
                LoadCatalogAsync(loading, s_CatalogGeneration).Forget();
            }

            cancellationToken.ThrowIfCancellationRequested();
            UniTask<FairyPackageCatalog> task = loading.Task;
            return cancellationToken.CanBeCanceled
                ? await task.AttachCancellation(cancellationToken)
                : await task;
        }

        private static async UniTaskVoid LoadCatalogAsync(
            UniTaskCompletionSource<FairyPackageCatalog> loading,
            long generation)
        {
            TextAsset manifest = null;
            ResourceComponent resourceComponent = GameEntry.Resource;
            try
            {
                if (resourceComponent == null)
                {
                    throw new FairyUIException(
                        FairyUIError.ResourceLoadFailed,
                        "FairyGUI resource component is unavailable.");
                }

                manifest = await resourceComponent.LoadAssetAsync<TextAsset>(ManifestAssetPath);
                if (manifest == null)
                {
                    throw new FairyUIException(
                        FairyUIError.PackageManifestInvalid,
                        $"FairyGUI runtime manifest asset is missing: {ManifestAssetPath}.");
                }

                FairyPackageCatalog catalog = FairyPackageCatalog.Parse(manifest.text);
                if (generation != s_CatalogGeneration)
                {
                    loading.TrySetCanceled();
                }
                else
                {
                    s_Catalog = catalog;
                    loading.TrySetResult(catalog);
                }
            }
            catch (Exception exception)
            {
                loading.TrySetException(exception);
            }
            finally
            {
                if (manifest != null)
                {
                    UnloadAsset(resourceComponent, manifest);
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
            UniTask<UIPackage> loadingTask = default;
            bool waitForLoad = false;
            if (!s_States.TryGetValue(packageName, out PackageState state))
            {
                state = new PackageState(packageName, ++s_NextGeneration);
                s_States.Add(packageName, state);
                state.ReferenceCount++;
                // Capture the completion task before starting the producer. A descriptor
                // failure can complete synchronously and clear state.Loading before the
                // caller reaches the await below; retaining this task preserves the root
                // exception instead of degrading it to a generic "not Ready" error.
                loadingTask = state.Loading.Task;
                waitForLoad = true;
                LoadPackageAsync(state).Forget();
            }
            else
            {
                state.ReferenceCount++;
                if (state.Status == FairyPackageStatus.Loading)
                {
                    loadingTask = state.Loading.Task;
                    waitForLoad = true;
                }
            }
            try
            {
                if (waitForLoad)
                {
                    if (cancellationToken.CanBeCanceled)
                    {
                        await loadingTask.AttachCancellation(cancellationToken);
                    }
                    else
                    {
                        await loadingTask;
                    }
                }

                if (!state.IsActive || state.Status != FairyPackageStatus.Ready || state.Package == null)
                {
                    throw new FairyUIException(
                        FairyUIError.PackageNotReady,
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
                state.ResourceComponent = GameEntry.Resource;
                if (state.ResourceComponent == null)
                {
                    throw new FairyUIException(
                        FairyUIError.ResourceLoadFailed,
                        "FairyGUI resource component is unavailable.");
                }

                TextAsset descriptor;
#if UNITY_EDITOR
                Func<ResourceComponent, string, CancellationToken, UniTask<TextAsset>> descriptorLoader =
                    DescriptorLoaderOverride;
                descriptor = descriptorLoader != null
                    ? await descriptorLoader(
                        state.ResourceComponent,
                        descriptorPath,
                        state.LoadCancellation.Token)
                    : await state.ResourceComponent.LoadAssetAsync<TextAsset>(
                        descriptorPath,
                        cancellationToken: state.LoadCancellation.Token);
#else
                descriptor = await state.ResourceComponent.LoadAssetAsync<TextAsset>(
                    descriptorPath,
                    cancellationToken: state.LoadCancellation.Token);
#endif
                state.Descriptor = descriptor;
                if (descriptor == null)
                {
                    throw new FairyUIException(
                        FairyUIError.PackageLoadFailed,
                        $"FairyGUI package descriptor asset is missing: {descriptorPath}.");
                }

                if (!IsCurrent(state))
                {
                    throw new OperationCanceledException(state.LoadCancellation.Token);
                }

                VerifyAssetHash(state.Name, descriptorPath, descriptor.bytes);

                string assetNamePrefix = $"{PackageAssetRoot}/{state.Name}";
                UIPackage package = UIPackage.AddPackage(
                    descriptor.bytes,
                    assetNamePrefix,
                    (name, extension, type, item) => LoadPackageResourceAsync(state, name, extension, type, item));
                if (package == null)
                {
                    throw new FairyUIException(
                        FairyUIError.PackageLoadFailed,
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
                    throw new FairyUIException(
                        FairyUIError.ResourceLoadFailed,
                        $"Failed to load an external asset for FairyGUI package '{state.Name}'.",
                        state.PendingAssetError);
                }

                state.Status = FairyPackageStatus.Ready;
                state.LastError = null;
                loading.TrySetResult(package);
                if (state.ReferenceCount == 0 && state.IsActive)
                {
                    ReleaseReadyState(state);
                }
            }
            catch (Exception exception)
            {
                state.LastError = exception;

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
            ResourceComponent resourceComponent = state.ResourceComponent;
            if (state.Descriptor != null)
            {
                UnloadAsset(resourceComponent, state.Descriptor);
                state.Descriptor = null;
            }

            foreach (UnityEngine.Object asset in state.LoadedAssets)
            {
                if (asset != null)
                {
                    UnloadAsset(resourceComponent, asset);
                }
            }

            state.LoadedAssets.Clear();
            state.ResourceComponent = null;
        }

        private static void UnloadAsset(ResourceComponent resourceComponent, UnityEngine.Object asset)
        {
            if (resourceComponent == null || asset == null)
            {
                return;
            }

            resourceComponent.UnloadAsset(asset);
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
            ResourceComponent resourceComponent = state.ResourceComponent;
            string assetPath = null;
            try
            {
                assetPath = s_Catalog.ResolveRuntimeAsset(state.Name, name, extension);
                UnityEngine.Object asset = await LoadAssetAsync(
                    resourceComponent,
                    assetPath,
                    type,
                    state.LoadCancellation.Token);
                VerifyAssetHash(state.Name, assetPath, asset);
                if (!IsCurrent(state) || state.Package == null)
                {
                    UnloadAsset(resourceComponent, asset);
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
                throw new FairyUIException(
                    FairyUIError.ResourceLoadFailed,
                    $"Failed to load an external asset for FairyGUI package '{state.Name}'.",
                    state.PendingAssetError);
            }
        }

        private static async UniTask<UnityEngine.Object> LoadAssetAsync(
            ResourceComponent resourceComponent,
            string assetPath,
            Type type,
            CancellationToken cancellationToken)
        {
            if (resourceComponent == null)
            {
                throw new FairyUIException(
                    FairyUIError.ResourceLoadFailed,
                    "FairyGUI resource component is unavailable.");
            }

            if (type == typeof(Texture))
            {
                return await resourceComponent.LoadAssetAsync<Texture>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            if (type == typeof(AudioClip))
            {
                return await resourceComponent.LoadAssetAsync<AudioClip>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            if (type == typeof(TextAsset))
            {
                return await resourceComponent.LoadAssetAsync<TextAsset>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            if (type == typeof(Font))
            {
                return await resourceComponent.LoadAssetAsync<Font>(
                    assetPath,
                    cancellationToken: cancellationToken);
            }

            return await resourceComponent.LoadAssetAsync<UnityEngine.Object>(
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

            /// <summary>
            /// 常驻计数。大于 0 时引用归零也不卸载包，由 warm/白名单持有。
            /// </summary>
            internal int PinCount;

            internal bool LoadCancellationDisposed;
            internal bool IsActive = true;
            internal FairyPackageStatus Status = FairyPackageStatus.Loading;
            internal ResourceComponent ResourceComponent;

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
        internal bool IsPinned { get; }

        internal FairyPackageDiagnostic(
            string name,
            FairyPackageStatus status,
            long generation,
            int referenceCount,
            int loadedAssetCount,
            string lastError,
            bool isPinned)
        {
            Name = name;
            Status = status;
            Generation = generation;
            ReferenceCount = referenceCount;
            LoadedAssetCount = loadedAssetCount;
            LastError = lastError;
            IsPinned = isPinned;
        }
    }
}
