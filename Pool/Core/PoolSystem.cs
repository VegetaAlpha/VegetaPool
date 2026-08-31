using System;
using System.Collections.Generic;
using UnityEngine;

namespace VegetaSystem
{
    internal enum PoolUsageMode
    {
        None = 0,
        StaticFacade,
        Constructed, // manual/DI; several instances are fine
    }

    public class PoolSystem
    {
        #region Entry types (private)

        private readonly struct PoolConfigEntry
        {
            public readonly Component Prefab;
            public readonly int InitAmount;

            public PoolConfigEntry(Component prefab, int initAmount)
            {
                Prefab = prefab;
                InitAmount = initAmount;
            }
        }

        private class PoolEntry
        {
            private readonly PoolSystem owner;
            private readonly Component prefab;
            private readonly Transform parent;
            private readonly string typeName;
            private readonly string subKey;
            private readonly Stack<Component> free = new();
            private readonly HashSet<Component> active = new();

            public PoolEntry(PoolSystem owner, Component prefab, Transform parent, string typeName, string subKey)
            {
                this.owner = owner;
                this.prefab = prefab;
                this.parent = parent;
                this.typeName = typeName;
                this.subKey = subKey;
            }

            private Component CreateInstance()
            {
                Component obj = owner.spawner.Spawn(prefab);
                obj.transform.SetParent(parent);

                var tracker = obj.gameObject.AddComponent<PoolableTracker>();
                tracker.Owner = obj;
                tracker.TypeName = typeName;
                tracker.SubKey = subKey;
                tracker.OwnerPool = owner;
                tracker.OnDestroyedExternally = HandleExternalDestroy;
                owner.trackersById[obj.GetInstanceID()] = tracker;

                obj.gameObject.SetActive(false);
                return obj;
            }

            public void Prewarm(int count)
            {
                for (int i = 0; i < count; i++)
                    free.Push(CreateInstance());
            }

            public Component Get()
            {
                Component obj = null;
                while (free.Count > 0)
                {
                    obj = free.Pop();
                    if (obj != null) break; // destroyed while sitting released in the pool, discard and keep popping
                }

                if (obj == null)
                    obj = CreateInstance();

                active.Add(obj);
                owner.trackersById[obj.GetInstanceID()].IsReleased = false;
                ((IPoolableBase)obj).OnGet();
                return obj;
            }

            public void Release(Component obj, bool ignoreParentPool, bool worldPosStay)
            {
                active.Remove(obj);

                if (!ignoreParentPool && parent != null)
                    obj.transform.SetParent(parent, worldPosStay);

                owner.trackersById[obj.GetInstanceID()].IsReleased = true;
                ((IPoolableBase)obj).OnRelease();
                free.Push(obj);
            }

            private void HandleExternalDestroy(PoolableTracker tracker)
            {
                active.Remove(tracker.Owner);
                owner.trackersById.Remove(tracker.Owner.GetInstanceID());
            }

            public void DestroyAll()
            {
                foreach (var obj in active)
                {
                    if (obj != null)
                        UnityEngine.Object.Destroy(obj.gameObject);
                }
                active.Clear();

                while (free.Count > 0)
                {
                    var obj = free.Pop();
                    if (obj != null)
                        UnityEngine.Object.Destroy(obj.gameObject);
                }

                if (parent != null)
                    UnityEngine.Object.Destroy(parent.gameObject);
            }
        }

        #endregion

        private readonly ISpawner spawner;

        private Transform root;
        private readonly Dictionary<string, Dictionary<string, PoolConfigEntry>> configIndex = new();
        private readonly Dictionary<string, Dictionary<string, PoolEntry>> pools = new();

        // Keyed by GetInstanceID(), shared by every PoolEntry this instance owns.
        private readonly Dictionary<int, PoolableTracker> trackersById = new();

        // Lives here and not in Pool.cs: only the constructor can see a direct `new PoolSystem()`.
        private static PoolUsageMode usageMode = PoolUsageMode.None;

        internal static PoolUsageMode UsageMode => usageMode;

        /// <summary>Pool's own singleton. The constructor claims Constructed; this corrects it.</summary>
        internal static PoolSystem CreateForStaticFacade(ISpawner spawner)
        {
            if (usageMode == PoolUsageMode.Constructed)
                throw new InvalidOperationException(
                    "PoolSystem was already constructed manually/via DI elsewhere — can't also use the static Pool facade. Pick one usage mode.");

            var pool = new PoolSystem(spawner);
            usageMode = PoolUsageMode.StaticFacade;
            return pool;
        }

        // No [Inject]: DI frameworks auto-pick the sole constructor, which keeps this class free
        // of any DI reference. The default lets `new PoolSystem()` compile for quick no-DI use.
        public PoolSystem(ISpawner spawner = null)
        {
            if (usageMode == PoolUsageMode.StaticFacade)
                throw new InvalidOperationException(
                    "The static Pool facade was already used elsewhere — can't also construct PoolSystem manually/via DI. Pick one usage mode.");

            // Always Constructed from here; CreateForStaticFacade corrects it when it's the caller.
            usageMode = PoolUsageMode.Constructed;
            this.spawner = spawner ?? new DefaultSpawner();
        }

        private void AddConfigEntry(string typeName, string subKey, Component prefab, int initAmount)
        {
            if (!configIndex.TryGetValue(typeName, out var sub))
            {
                sub = new Dictionary<string, PoolConfigEntry>();
                configIndex[typeName] = sub;
            }
            sub[subKey] = new PoolConfigEntry(prefab, initAmount);
        }

        private Transform GetOrCreateRoot()
        {
            if (root == null)
            {
                var go = new GameObject("[PoolSystem]");
                UnityEngine.Object.DontDestroyOnLoad(go);
                root = go.transform;
            }
            return root;
        }

        private PoolEntry GetOrCreatePoolEntry(string typeName, string subKey)
        {
            if (pools.TryGetValue(typeName, out var subDict) && subDict.TryGetValue(subKey, out var existing))
                return existing;

            if (!configIndex.TryGetValue(typeName, out var configSub) || !configSub.TryGetValue(subKey, out var config))
            {
                Debug.LogWarning($"Pool config not found: {typeName}{(string.IsNullOrEmpty(subKey) ? "" : "/" + subKey)}");
                return null;
            }

            string parentName = string.IsNullOrEmpty(subKey) ? $"{typeName}_C" : $"{typeName}_{subKey}_C";
            GameObject parentGO = new GameObject(parentName);
            parentGO.transform.SetParent(GetOrCreateRoot());
            parentGO.transform.position = Vector3.zero;

            var entry = new PoolEntry(this, config.Prefab, parentGO.transform, typeName, subKey);
            if (config.InitAmount > 0)
                entry.Prewarm(config.InitAmount);

            if (!pools.TryGetValue(typeName, out subDict))
            {
                subDict = new Dictionary<string, PoolEntry>();
                pools[typeName] = subDict;
            }
            subDict[subKey] = entry;

            return entry;
        }

        private T GetFromPool<T>(string typeName, string subKey) where T : class
        {
            var entry = GetOrCreatePoolEntry(typeName, subKey);
            if (entry == null)
                return null;

            var obj = entry.Get();
            if (obj is T typedObj)
                return typedObj;

            Debug.LogWarning($"Wrong type from pool {typeName}{subKey}. Expected {typeof(T)}, got {obj.GetType()}");
            entry.Release(obj, ignoreParentPool: false, worldPosStay: true);
            return null;
        }

        private void HandleRelease(Component obj, bool ignoreParentPool, bool worldPosStay)
        {
            try
            {
                if (obj == null) return;

                if (!trackersById.TryGetValue(obj.GetInstanceID(), out var tracker))
                {
                    Debug.LogWarning($"{obj.name} has no PoolableTracker — was it fetched from this PoolSystem's GetObj<T>()?");
                    return;
                }

                if (tracker.IsReleased)
                {
                    Debug.Log($"Object with name {obj.name} is already release");
                    return;
                }

                if (pools.TryGetValue(tracker.TypeName, out var subDict) && subDict.TryGetValue(tracker.SubKey, out var entry))
                {
                    entry.Release(obj, ignoreParentPool, worldPosStay);
                }
                else
                {
                    Debug.LogWarning($"Pool {tracker.TypeName}{tracker.SubKey} not found when releasing {obj.name}");
                    UnityEngine.Object.Destroy(obj.gameObject);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"Vegeta System Err : {ex}");
            }
        }

        private void DestroyPoolByType(string typeName)
        {
            if (!pools.TryGetValue(typeName, out var subDict))
                return;

            foreach (var entry in subDict.Values)
                entry.DestroyAll();

            pools.Remove(typeName);
        }

        private void DestroyPoolBySubKey(string typeName, string subKey)
        {
            if (!pools.TryGetValue(typeName, out var subDict) || !subDict.TryGetValue(subKey, out var entry))
                return;

            entry.DestroyAll();
            subDict.Remove(subKey);

            if (subDict.Count == 0)
                pools.Remove(typeName);
        }

        private void DestroyEverything()
        {
            foreach (var subDict in pools.Values)
                foreach (var entry in subDict.Values)
                    entry.DestroyAll();
            pools.Clear();

            if (root != null)
            {
                UnityEngine.Object.Destroy(root.gameObject);
                root = null;
            }
        }

        #region API

        /// <summary>
        /// Lazily registers a prefab. Keyed by prefab.GetType() (its actual runtime type), not
        /// the compile-time T — so a subclass registers under its own name even if T is inferred
        /// as its base class.
        /// </summary>
        public void Register<T>(T prefab, int initAmount) where T : Component
        {
            if (prefab is ISubKeyPoolable sub)
            {
                AddConfigEntry(prefab.GetType().Name, sub.GetSubKeyPool(), prefab, initAmount);
                return;
            }

            if (prefab is IPoolable)
            {
                AddConfigEntry(prefab.GetType().Name, "", prefab, initAmount);
                return;
            }

            Debug.LogWarning($"{prefab.GetType().Name} implements neither IPoolable nor ISubKeyPoolable — not registered.");
        }

        public T GetObj<T>() where T : class, IPoolable => GetFromPool<T>(typeof(T).Name, "");

        public T GetObj<T>(string subKey) where T : class, ISubKeyPoolable => GetFromPool<T>(typeof(T).Name, subKey);

        public void ReleaseObj(IPoolableBase obj, bool ignoreParentPool = false, bool worldPosStay = true)
            => HandleRelease(obj as Component, ignoreParentPool, worldPosStay);

        // Destroys every subkey of T, not just the default one.
        public void DestroyPool<T>() where T : class, IPoolable => DestroyPoolByType(typeof(T).Name);

        public void DestroyPool<T>(string subKey) where T : class, ISubKeyPoolable => DestroyPoolBySubKey(typeof(T).Name, subKey);

        public void DestroyAllPools() => DestroyEverything();

        #endregion
    }
}
