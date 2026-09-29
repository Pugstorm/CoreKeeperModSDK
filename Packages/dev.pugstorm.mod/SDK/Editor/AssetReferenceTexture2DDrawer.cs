#if PUG_MOD_SDK
using UnityEditor;
using UnityEngine;
using UnityEngine.AddressableAssets;

namespace PugMod
{
	[CustomPropertyDrawer(typeof(AssetReferenceTexture2D))]
	public class AssetReferenceTexture2DDrawer : PropertyDrawer
	{
		public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
		{
			var guid = property.FindPropertyRelative("m_AssetGUID");
			var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid.stringValue));

			EditorGUI.BeginProperty(position, label, property);
			var showMixedValue = EditorGUI.showMixedValue;
			EditorGUI.showMixedValue = guid.hasMultipleDifferentValues;
			EditorGUI.BeginChangeCheck();
			texture = (Texture2D)EditorGUI.ObjectField(position, label, texture, typeof(Texture2D), false);
			if (EditorGUI.EndChangeCheck())
			{
				guid.stringValue = texture == null ? string.Empty : AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(texture));
				property.FindPropertyRelative("m_SubObjectName").stringValue = string.Empty;
				property.FindPropertyRelative("m_SubObjectType").stringValue = string.Empty;
				property.FindPropertyRelative("m_SubObjectGUID").stringValue = string.Empty;
			}
			EditorGUI.showMixedValue = showMixedValue;
			EditorGUI.EndProperty();
		}
	}
}
#endif
