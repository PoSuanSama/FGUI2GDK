using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using GameFramework;
using GameFramework.Event;
using GameFramework.Resource;
using UnityGameFramework.Extension;
using UnityGameFramework.Runtime;

namespace Game
{
    public sealed class ResourceContainer : IReference
    {
        public object Owner
        {
            get;
            private set;
        }

        private readonly List<UnityEngine.Object> m_Assets = new List<UnityEngine.Object>();
        private CancellationTokenSource m_CancellationTokenSource;

        /// <summary>
        /// 容器生命周期代号。每次 Create/Clear/UnloadAllAssets 递增，
        /// 在途加载回调据此判断自己是否属于已失效的那一代。
        /// 只在主线程访问，不需要 Interlocked。
        /// </summary>
        private long m_Version;

        public static ResourceContainer Create(object owner)
        {
            ResourceContainer resourceContainer = ReferencePool.Acquire<ResourceContainer>();
            resourceContainer.Owner = owner;
            resourceContainer.m_Version++;
            return resourceContainer;
        }

        public void Clear()
        {
            // 先失效代际：Cancel 内同步触发的回调已经能看到新版本号。
            m_Version++;

            // 先摘引用再 Cancel，避免 Cancel 期间重入新建的 CTS 被误清。
            CancellationTokenSource cancellation = m_CancellationTokenSource;
            m_CancellationTokenSource = null;
            if (cancellation != null)
            {
                // 必须 Cancel，否则在途加载不会结束，await 返回后仍会往已清空的列表里加资源。
                // 不 Dispose：token 已注册进 GF 的加载操作，立即 Dispose 会让注销路径抛
                // ObjectDisposedException；既有 UnloadAllAssets 同样只 Cancel 不 Dispose。
                cancellation.Cancel();
            }

            // 不卸载资产：由调用方先 UnloadAllAssets，这是既有契约。
            m_Assets.Clear();
            Owner = null;
        }

        public void LoadAsset<T>(string assetName, Action<T> onLoadSuccess, Action onLoadFailure = null, int priority = 0,
            Action<float> updateEvent = null, Action<string> dependencyAssetEvent = null) where T : UnityEngine.Object
        {
            long version = m_Version;
            void LoadAssetSuccessCallback(string _, object asset, float duration, object userData)
            {
                T assetResult = asset as T;
                if (assetResult == null)
                {
                    GameEntry.Resource.UnloadAsset(asset);
                    throw new GameFrameworkException(Utility.Text.Format("Load asset '{0}' failure load type is {1} but asset type is {2}.", assetName, asset.GetType(), typeof(T)));
                }
                //版本变化了，说明已经被销毁了，直接卸载
                if(version != m_Version)
                {
                    GameEntry.Resource.UnloadAsset(asset);
                    return;
                }
                m_Assets.Add(assetResult);
                onLoadSuccess?.Invoke(assetResult);
            }
            void LoadAssetFailureCallback(string _, LoadResourceStatus status, string errorMsg, object userData)
            {
                // 容器已清空后不再回调业务，避免访问已置空的 Owner。
                if (version != m_Version)
                {
                    return;
                }

                onLoadFailure.Invoke();
            }
            void LoadAssetUpdateCallback(string _, float progress, object userData)
            {
                if (version != m_Version)
                {
                    return;
                }

                updateEvent.Invoke(progress);
            }
            void LoadAssetDependencyAssetCallback(string _, string dependencyAssetName, int loadedCount, int totalCount, object userData)
            {
                if (version != m_Version)
                {
                    return;
                }

                dependencyAssetEvent.Invoke(dependencyAssetName);
            }

            LoadAssetCallbacks loadAssetCallbacks = new LoadAssetCallbacks(
                LoadAssetSuccessCallback,
                onLoadFailure == null ? null : LoadAssetFailureCallback,
                updateEvent == null ? null : LoadAssetUpdateCallback,
                dependencyAssetEvent == null ? null : LoadAssetDependencyAssetCallback);
            GameEntry.Resource.LoadAsset(assetName, priority, loadAssetCallbacks);
        }

        public async UniTask<T> LoadAssetAsync<T>(string assetName, int priority = 0,
            Action<float> updateEvent = null, Action<string> dependencyAssetEvent = null) where T : UnityEngine.Object
        {
            if (m_CancellationTokenSource == null)
            {
                m_CancellationTokenSource = new CancellationTokenSource();
            }

            long version = m_Version;
            T asset = await GameEntry.Resource.LoadAssetAsync<T>(assetName, priority, m_CancellationTokenSource.Token, updateEvent, dependencyAssetEvent);

            if (asset == null)
            {
                return null;
            }

            // 加载期间容器被清空：资源不再归属本容器，立即释放，避免泄漏。
            if (version != m_Version)
            {
                GameEntry.Resource.UnloadAsset(asset);
                return null;
            }

            m_Assets.Add(asset);
            return asset;
        }

        public void UnloadAsset(UnityEngine.Object asset)
        {
            m_Assets.Remove(asset);
            GameEntry.Resource.UnloadAsset(asset);
        }

        public void UnloadAllAssets()
        {
            // 先失效代际，让在途加载的回调提前退出。
            m_Version++;

            if (m_Assets.Count > 0)
            {
                foreach (var asset in m_Assets)
                {
                    GameEntry.Resource.UnloadAsset(asset);
                }
                m_Assets.Clear();
            }
            if (m_CancellationTokenSource != null)
            {
                m_CancellationTokenSource.Cancel();
                m_CancellationTokenSource = null;
            }
        }

        public void UnloadAllAssets(bool isShutdown)
        {
            // 先失效代际，让在途加载的回调提前退出。
            m_Version++;

            if (!isShutdown)
            {
                if (m_Assets.Count > 0)
                {
                    foreach (var asset in m_Assets)
                    {
                        GameEntry.Resource.UnloadAsset(asset);
                    }
                    m_Assets.Clear();
                }
            }
            if (m_CancellationTokenSource != null)
            {
                m_CancellationTokenSource.Cancel();
                m_CancellationTokenSource = null;
            }
        }
    }
}