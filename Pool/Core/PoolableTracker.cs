using System;
using UnityEngine;

namespace VegetaSystem
{
    /// <summary>Internal bookkeeping — attached automatically, never touched by consumer code.</summary>
    internal class PoolableTracker : MonoBehaviour
    {
        internal Component Owner;
        internal string TypeName;
        internal string SubKey;
        internal bool IsReleased = true;

        // Which instance spawned this — how this.Release() finds its pool without a global lookup.
        internal PoolSystem OwnerPool;

        internal Action<PoolableTracker> OnDestroyedExternally;

        private void OnDestroy() => OnDestroyedExternally?.Invoke(this);
    }
}
