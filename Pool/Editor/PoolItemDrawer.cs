using UnityEditor;
using UnityEngine;

namespace VegetaSystem.Editor
{
    [CustomPropertyDrawer(typeof(PoolItem))]
    public class PoolItemDrawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            EditorGUI.BeginProperty(position, label, property);

            var prefabProp = property.FindPropertyRelative(nameof(PoolItem.Prefab));
            var initAmountProp = property.FindPropertyRelative(nameof(PoolItem.InitAmount));

            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;
            float y = position.y;

            var prefabRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(prefabRect, prefabProp);
            PoolPrefabValidation.RejectIfInvalidMulti(prefabProp);
            y += lineHeight + spacing;

            var amountRect = new Rect(position.x, y, position.width, lineHeight);
            EditorGUI.PropertyField(amountRect, initAmountProp);

            EditorGUI.EndProperty();
        }

        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            float lineHeight = EditorGUIUtility.singleLineHeight;
            float spacing = EditorGUIUtility.standardVerticalSpacing;

            return lineHeight + spacing + lineHeight; // prefab field + amount field
        }
    }
}
