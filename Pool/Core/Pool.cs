using System;
using UnityEngine;

namespace VegetaSystem
{
    /// <summary>
    /// No-DI entry point: one lazy PoolSystem singleton. Mutually exclusive with constructing
    /// PoolSystem yourself — see the guard in its constructor.
    /// </summary>
    public static class Pool
    {
        private static PoolSystem poolSystem;
        private static ISpawner spawner;

        /// <summary>Set before first use. A property, unlike GetPoolSystem(): this getter locks in nothing and can't throw.</summary>
        public static ISpawner Spawner
        {
            get => spawner ??= new DefaultSpawner();
            set
            {
                if (poolSystem != null)
                    throw new InvalidOperationException("The pool was already created — set Spawner before first use.");
                spawner = value;
            }
        }

        /// <summary>
        /// Escape hatch for APIs that want the instance (SO_AllPoolData.ApplyTo, ...); prefer the
        /// forwards below. A method, not a property: the first call creates the singleton, locks in
        /// static mode and can throw — a debugger must not trigger that by evaluating a property.
        /// </summary>
        public static PoolSystem GetPoolSystem()
            => poolSystem ??= PoolSystem.CreateForStaticFacade(Spawner);

        // Keep in sync with PoolSystem's public API region.
        public static void Register<T>(T prefab, int initAmount) where T : Component
            => GetPoolSystem().Register(prefab, initAmount);

        public static T GetObj<T>() where T : class, IPoolable
            => GetPoolSystem().GetObj<T>();

        public static T GetObj<T>(string subKey) where T : class, ISubKeyPoolable
            => GetPoolSystem().GetObj<T>(subKey);

        public static void ReleaseObj(IPoolableBase obj, bool ignoreParentPool = false, bool worldPosStay = true)
            => GetPoolSystem().ReleaseObj(obj, ignoreParentPool, worldPosStay);

        public static void DestroyPool<T>() where T : class, IPoolable
            => GetPoolSystem().DestroyPool<T>();

        public static void DestroyPool<T>(string subKey) where T : class, ISubKeyPoolable
            => GetPoolSystem().DestroyPool<T>(subKey);

        public static void DestroyAllPools()
            => GetPoolSystem().DestroyAllPools();
    }
}
