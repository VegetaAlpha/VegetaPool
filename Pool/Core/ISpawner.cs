using UnityEngine;

namespace VegetaSystem
{
    /// <summary>The only thing PoolSystem knows about instantiation. Swap DI frameworks here, not in PoolSystem.</summary>
    public interface ISpawner
    {
        T Spawn<T>(T prefab) where T : Component;
    }
}
