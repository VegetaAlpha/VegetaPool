using UnityEngine;

namespace VegetaSystem
{
    /// <summary>Default ISpawner — plain UnityEngine.Object.Instantiate, no DI involved.</summary>
    public class DefaultSpawner : ISpawner
    {
        public T Spawn<T>(T prefab) where T : Component => UnityEngine.Object.Instantiate(prefab);
    }
}
