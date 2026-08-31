using System.Collections.Generic;
using UnityEngine;

namespace VegetaSystem
{
    [CreateAssetMenu(fileName = "AllPoolData", menuName = "Pool/AllPoolData")]
    public class SO_AllPoolData : ScriptableObject
    {
        public List<SO_PoolData> configs;

        /// <summary>Adapter — registers every entry in this asset with the given PoolSystem instance.</summary>
        public void ApplyTo(PoolSystem pool)
        {
            if (configs == null) return;

            foreach (var config in configs)
            {
                if (config == null) continue; // empty slot left in the list

                if (config.Mode == PoolConfigMode.Single)
                {
                    if (config.Prefab != null)
                        pool.Register(config.Prefab, config.InitAmount);
                    continue;
                }

                if (config.PoolItems == null || config.PoolItems.Count == 0)
                {
                    Debug.LogWarning("Multiple pool config has no items assigned!");
                    continue;
                }

                foreach (var item in config.PoolItems)
                {
                    if (item.Prefab != null)
                        pool.Register(item.Prefab, item.InitAmount);
                }
            }
        }
    }
}
