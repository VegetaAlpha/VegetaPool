using UnityEngine;

namespace VegetaSystem
{
    public static class PoolableExtensions
    {
        /// <summary>
        /// Routes through the object's PoolableTracker, so it finds the right pool even with
        /// several alive. Costs a GetComponent — prefer pool.ReleaseObj(obj) where you hold `pool`.
        /// </summary>
        public static void Release(this IPoolableBase obj, bool ignoreParentPool = false, bool worldPosStay = true)
        {
            if (obj is not Component component || component == null) return;

            var tracker = component.GetComponent<PoolableTracker>();
            if (tracker == null || tracker.OwnerPool == null)
            {
                Debug.LogWarning($"{component.name} was never obtained from a PoolSystem — can't self-release.");
                return;
            }

            tracker.OwnerPool.ReleaseObj(obj, ignoreParentPool, worldPosStay);
        }
    }
}
