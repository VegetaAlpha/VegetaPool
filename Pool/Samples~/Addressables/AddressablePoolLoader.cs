using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace VegetaSystem
{
    /// <summary>
    /// No dedicated ISpawner needed for Addressables — the async part is only "resolve the prefab
    /// reference by key" (this method). Once resolved, registering/spawning stays fully synchronous
    /// through PoolSystem.Register + the usual ISpawner (DefaultSpawner/VContainerSpawner/...).
    /// Requires com.unity.addressables installed.
    /// </summary>
    public static class AddressablePoolLoader
    {
        public static async Task RegisterAsync<T>(this PoolSystem pool, string addressableKey, int initAmount) where T : Component
        {
            var handle = Addressables.LoadAssetAsync<GameObject>(addressableKey);
            var prefab = await handle.Task;
            pool.Register(prefab.GetComponent<T>(), initAmount);
        }
    }
}
