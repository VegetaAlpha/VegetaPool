using System;
using UnityEditor;
using UnityEngine;

namespace VegetaSystem.Editor
{
    internal static class PoolPrefabValidation
    {
        public const string SingleErrorMessage =
            "Must implement IPoolable, not ISubKeyPoolable — a multi-variant type belongs in Multiple mode instead.";

        public const string MultiErrorMessage =
            "Must implement ISubKeyPoolable — a single-pool IPoolable type belongs in Single mode instead.";

        public static bool IsValidSingle(UnityEngine.Object prefab)
            => prefab == null || (prefab is IPoolable && !(prefab is ISubKeyPoolable));

        public static bool IsValidMulti(UnityEngine.Object prefab)
            => prefab == null || prefab is ISubKeyPoolable;

        /// <summary>Call right after drawing the field — reverts + logs if the value doesn't match this mode.</summary>
        public static void RejectIfInvalidSingle(SerializedProperty prefabProp)
            => RejectIfInvalid(prefabProp, IsValidSingle, SingleErrorMessage);

        public static void RejectIfInvalidMulti(SerializedProperty prefabProp)
            => RejectIfInvalid(prefabProp, IsValidMulti, MultiErrorMessage);

        private static void RejectIfInvalid(SerializedProperty prefabProp, Func<UnityEngine.Object, bool> isValid, string errorMessage)
        {
            var value = prefabProp.objectReferenceValue;
            if (isValid(value)) return;

            // No ApplyModifiedProperties()/SetDirty() here: runs mid-draw inside a list
            // PropertyDrawer, and applying now corrupts that iteration.
            Debug.LogError($"{value.name}: {errorMessage} Reference cleared.");
            prefabProp.objectReferenceValue = null;
            GUI.changed = true;
        }
    }
}
