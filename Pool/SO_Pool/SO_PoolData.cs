using System;
using System.Collections.Generic;
using UnityEngine;

namespace VegetaSystem
{
    public enum PoolConfigMode
    {
        Single = 0,
        Multiple = 1, // several prefabs from one asset, typically ISubKeyPoolable variants
    }

    [CreateAssetMenu(fileName = "PoolData", menuName = "Pool/PoolData")]
    public class SO_PoolData : ScriptableObject
    {
        [Tooltip("Single = one prefab below. Multiple = the Pool Items list instead.")]
        public PoolConfigMode Mode = PoolConfigMode.Single;

        // Both sets stay serialized so toggling Mode doesn't discard what you already filled in.
        public MonoBehaviour Prefab;
        public int InitAmount;

        public List<PoolItem> PoolItems = new();
    }

    [Serializable]
    public class PoolItem
    {
        public MonoBehaviour Prefab;
        public int InitAmount;
    }
}
