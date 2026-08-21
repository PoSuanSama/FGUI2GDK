using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FairyGUI;
using GameFramework;
using UnityEngine;
using UnityGameFramework.Extension;
using UnityGameFramework.Runtime;

namespace Game
{
    /// <summary>
    /// A reference-counted lease for a FairyGUI package.
    /// </summary>
    internal sealed class FairyPackageLease : IDisposable
    {
        private FairyPackageManager.PackageState m_State;

        internal FairyPackageLease(FairyPackageManager.PackageState state)
        {
            m_State = state;
        }

        public UIPackage Package => m_State?.Package;

        public void Dispose()
        {
            FairyPackageManager.PackageState state = m_State;
            if (state == null)
            {
                return;
            }

            m_State = null;
            FairyPackageManager.Release(state);
        }
    }

    /// <summary>
    /// Loads FairyGUI packages through the GDK ResourceComponent and owns their lifetime.
    /// </summary>
    internal static class FairyPackageManager
    {
        private const string PackageAssetRoot = "Assets/Res/UI/FairyGUI";

        private static readonly Dictionary<string, PackageState> s_States =
            new Dictionary<string, PackageState>(StringComparer.Ordinal);

        internal static async UniTask<FairyPackageLease> AcquireAsync(string packageName)
        {
            if (string.IsNullOrWhiteSpace(packageName))
            {
                throw new ArgumentException("FairyGUI package name is required.", nameof(packageName));
            }

            if (s_States.TryGetValue(packageName, out PackageState existingState))
            {
                if (existingState.Loading != null)
                {
                    await existingState.Loading.Task;
                }

                existingState.ReferenceCount++;
                return new FairyPackageLease(existingState);
            }

            PackageState state = new PackageState(packageName);
            state.Loading = new UniTaskCompletionSource<UIPackage>();
            s_States.Add(packageName, state);

            try
            {
                string descriptorPath = $"{PackageAssetRoot}/{packageName}_fui.bytes";
                TextAsset descriptor = await GameEntry.Resource.LoadAssetAsync<TextAsset>(descriptorPath);
                state.Descriptor = descriptor;

                string assetNamePrefix = $"{PackageAssetRoot}/{packageName}";
                UIPackage package = UIPackage.AddPackage(
                    descriptor.bytes,
                    assetNamePrefix,
                    (name, extension, type, item) => LoadPackageResourceAsync(state, name, extension, type, item));
                if (package == null)
                {
                    throw new GameFrameworkException($"Unable to register FairyGUI package '{packageName}'.");
                }

                state.Package = package;
                state.Loading.TrySetResult(package);
                state.Loading = null;
                state.ReferenceCount = 1;
                return new FairyPackageLease(state);
            }
            catch (Exception exception)
            {
                state.Loading?.TrySetException(exception);
                s_States.Remove(packageName);
                ReleaseAssets(state);
                throw;
            }
        }

        internal static void Release(PackageState state)
        {
            if (state.ReferenceCount <= 0)
            {
                return;
            }

            state.ReferenceCount--;
            if (state.ReferenceCount > 0)
            {
                return;
            }

            if (s_States.TryGetValue(state.Name, out PackageState currentState) && currentState == state)
            {
                s_States.Remove(state.Name);
            }

            state.IsActive = false;
            if (state.Package != null && UIPackage.GetByName(state.Name) == state.Package)
            {
                UIPackage.RemovePackage(state.Name);
            }

            ReleaseAssets(state);
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
            LoadPackageResourceAsyncCore(state, name, extension, type, item).Forget();
        }

        private static async UniTask LoadPackageResourceAsyncCore(
            PackageState state,
            string name,
            string extension,
            Type type,
            PackageItem item)
        {
            string assetPath = Utility.Text.Format("{0}{1}", name, extension);
            try
            {
                UnityEngine.Object asset = await LoadAssetAsync(assetPath, type);
                if (!state.IsActive || state.Package == null)
                {
                    GameEntry.Resource.UnloadAsset(asset);
                    return;
                }

                state.LoadedAssets.Add(asset);
                item.owner.SetItemAsset(item, asset, DestroyMethod.None);
            }
            catch (Exception exception)
            {
                if (state.IsActive)
                {
                    Log.Error(
                        "Failed to load FairyGUI package asset '{0}' ({1}): {2}",
                        assetPath,
                        type.Name,
                        exception);
                }
            }
        }

        private static async UniTask<UnityEngine.Object> LoadAssetAsync(string assetPath, Type type)
        {
            if (type == typeof(Texture))
            {
                return await GameEntry.Resource.LoadAssetAsync<Texture>(assetPath);
            }

            if (type == typeof(AudioClip))
            {
                return await GameEntry.Resource.LoadAssetAsync<AudioClip>(assetPath);
            }

            if (type == typeof(TextAsset))
            {
                return await GameEntry.Resource.LoadAssetAsync<TextAsset>(assetPath);
            }

            if (type == typeof(Font))
            {
                return await GameEntry.Resource.LoadAssetAsync<Font>(assetPath);
            }

            return await GameEntry.Resource.LoadAssetAsync<UnityEngine.Object>(assetPath);
        }

        internal sealed class PackageState
        {
            public readonly string Name;
            public readonly List<UnityEngine.Object> LoadedAssets = new List<UnityEngine.Object>();
            public TextAsset Descriptor;
            public UIPackage Package;
            public UniTaskCompletionSource<UIPackage> Loading;
            public int ReferenceCount;
            public bool IsActive = true;

            public PackageState(string name)
            {
                Name = name;
            }
        }
    }
}
