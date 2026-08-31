using UnityEditor;

namespace VegetaSystem.Editor
{
    /// <summary>Draws only the fields belonging to the selected mode.</summary>
    [CustomEditor(typeof(SO_PoolData))]
    public class SO_PoolDataEditor : UnityEditor.Editor
    {
        private SerializedProperty modeProp;
        private SerializedProperty poolItemsProp;
        private SerializedProperty prefabProp;
        private SerializedProperty initAmountProp;

        private void OnEnable()
        {
            modeProp = serializedObject.FindProperty(nameof(SO_PoolData.Mode));
            poolItemsProp = serializedObject.FindProperty(nameof(SO_PoolData.PoolItems));
            prefabProp = serializedObject.FindProperty(nameof(SO_PoolData.Prefab));
            initAmountProp = serializedObject.FindProperty(nameof(SO_PoolData.InitAmount));
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUILayout.PropertyField(modeProp);
            EditorGUILayout.Space(6);

            // No gaps in PoolConfigMode, so enumValueIndex == the enum value.
            if ((PoolConfigMode)modeProp.enumValueIndex == PoolConfigMode.Single)
            {
                EditorGUILayout.LabelField("Single Prefab", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(prefabProp);
                PoolPrefabValidation.RejectIfInvalidSingle(prefabProp);
                EditorGUILayout.PropertyField(initAmountProp);
            }
            else
            {
                EditorGUILayout.LabelField("Multiple Prefabs", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(poolItemsProp, true);
            }

            serializedObject.ApplyModifiedProperties();
        }
    }
}
