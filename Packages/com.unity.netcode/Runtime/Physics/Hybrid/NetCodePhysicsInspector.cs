#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Unity.NetCode.Editor
{
    [CustomEditor(typeof(NetCodePhysicsConfig))]
    public sealed class NetCodePhysicsInspector : UnityEditor.Editor
    {
        private SerializedProperty EnableLagCompensation;
        private SerializedProperty ServerHistorySize;
        private SerializedProperty ClientHistorySize;
        private SerializedProperty ClientNonGhostWorldIndex;

        private void OnEnable()
        {
            EnableLagCompensation = serializedObject.FindProperty("EnableLagCompensation");
            ServerHistorySize = serializedObject.FindProperty("ServerHistorySize");
            ClientHistorySize = serializedObject.FindProperty("ClientHistorySize");
            ClientNonGhostWorldIndex = serializedObject.FindProperty("ClientNonGhostWorldIndex");
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.PropertyField(serializedObject.FindProperty("m_Script"), true);
            EditorGUILayout.PropertyField(EnableLagCompensation, new GUIContent("Lag Compensation"));
            if (EnableLagCompensation.boolValue)
            {
                EditorGUI.indentLevel += 1;
                EditorGUILayout.PropertyField(ServerHistorySize);
                EditorGUILayout.PropertyField(ClientHistorySize);
                EditorGUI.indentLevel -= 1;
            }

            EditorGUILayout.PropertyField(ClientNonGhostWorldIndex);

            if (serializedObject.hasModifiedProperties)
                serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
