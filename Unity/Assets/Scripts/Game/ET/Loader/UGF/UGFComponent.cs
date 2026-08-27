using System.Threading;
using Cysharp.Threading.Tasks;
using Game;
using UnityEngine;
using UnityGameFramework.Extension;

namespace ET
{
    [EntitySystemOf(typeof(UGFComponent))]
    [FriendOf(typeof(UGFComponent))]
    public static partial class UGFComponentSystem
    {
        [EntitySystem]
        private static void Awake(this UGFComponent self)
        {
        }
    }

    [ComponentOf(typeof(Scene))]
    public sealed class UGFComponent : Entity, IAwake
    {
        public async UniTask<T> LoadAssetAsync<T>(string assetName) where T : UnityEngine.Object
        {
            T asset = await GameEntry.Resource.LoadAssetAsync<T>(assetName);
            return asset;
        }

        public void UnloadAsset(UnityEngine.Object asset)
        {
            GameEntry.Resource.UnloadAsset(asset);
        }

        public async UniTask LoadSceneAsync(string sceneAssetName)
        {
            await GameEntry.Scene.LoadSceneAsync(sceneAssetName);
        }

        public async UniTask LoadSceneAsync(int scentTypeId)
        {
            await GameEntry.Scene.LoadSceneAsync(scentTypeId);
        }

        public bool SceneIsLoaded(string sceneAssetName)
        {
            return GameEntry.Scene.SceneIsLoaded(sceneAssetName);
        }

        public bool SceneIsLoading(string sceneAssetName)
        {
            return GameEntry.Scene.SceneIsLoading(sceneAssetName);
        }

        public bool SceneIsLoaded(int scentTypeId)
        {
            return GameEntry.Scene.SceneIsLoaded(scentTypeId);
        }

        public bool SceneIsLoading(int scentTypeId)
        {
            return GameEntry.Scene.SceneIsLoading(scentTypeId);
        }

        public async UniTask UnloadSceneAsync(string sceneAssetName)
        {
            await GameEntry.Scene.UnloadSceneAsync(sceneAssetName);
        }

        public async UniTask UnloadAllScenesAsync()
        {
            ListComponent<string> loadingSceneAssetNames = ListComponent<string>.Create();
            ListComponent<UniTask> unloadTasks = ListComponent<UniTask>.Create();
            GameEntry.Scene.GetLoadingSceneAssetNames(loadingSceneAssetNames);
            foreach (var loadingSceneAssetName in loadingSceneAssetNames)
            {
                unloadTasks.Add(GameEntry.Scene.UnloadSceneAsync(loadingSceneAssetName));
            }
            loadingSceneAssetNames.Clear();
            GameEntry.Scene.GetLoadedSceneAssetNames(loadingSceneAssetNames);
            foreach (var loadingSceneAssetName in loadingSceneAssetNames)
            {
                unloadTasks.Add(GameEntry.Scene.UnloadSceneAsync(loadingSceneAssetName));
            }
            loadingSceneAssetNames.Dispose();
            await UniTask.WhenAll(unloadTasks);
            unloadTasks.Dispose();
        }

        public async UniTask<Transform> ShowEntityAsync(int entityTypeId, CancellationToken cancellationToken = default)
        {
            UnityGameFramework.Runtime.Entity ugfEntity = await GameEntry.Entity.ShowEntityAsync<ETMonoUGFEntity>(entityTypeId, cancellationToken: cancellationToken);
            return ugfEntity.Logic.CachedTransform;
        }

        public async UniTask<Transform> ShowEntityAsync(string entityAssetName, string entityGroupName, CancellationToken cancellationToken = default, int priority = 0)
        {
            UnityGameFramework.Runtime.Entity ugfEntity = await GameEntry.Entity.ShowEntityAsync(GameEntry.Entity.GenerateSerialId(), typeof(ETMonoUGFEntity), entityAssetName, entityGroupName, priority, cancellationToken: cancellationToken);
            return ugfEntity.Logic.CachedTransform;
        }
    }
}
