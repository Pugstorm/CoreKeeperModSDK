using Unity.Physics.Authoring;
using UnityEditor;
using UnityEngine;

namespace Unity.Physics.Editor
{
    [CustomEditor(typeof(PhysicsStepAuthoring))]
    [CanEditMultipleObjects]
    class PhysicsStepAuthoringEditor : BaseEditor
    {
        static class Content
        {
            public static readonly GUIContent SolverStabilizationLabelUnityPhysics = EditorGUIUtility.TrTextContent("Enable Contact Solver Stabilization Heuristic",
                "Specifies whether the contact solver stabilization heuristic should be applied. Enabling this will result in better overall stability of bodies and piles, " +
                "but may result in behavior artifacts.");
            public static readonly GUIContent SolverStabilizationLabelHavokPhysics = EditorGUIUtility.TrTextContent("Enable Contact Solver Stabilization Heuristic",
                "Havok Physics already has stable contact solving algorithms due to the ability to cache states, so it doesn't need any additional solver stabilization heuristics.");
        }

#pragma warning disable 649
        [AutoPopulate] SerializedProperty m_SimulationType;
        [AutoPopulate] SerializedProperty m_Gravity;
        [AutoPopulate] SerializedProperty m_SolverIterationCount;
        [AutoPopulate] SerializedProperty m_EnableSolverStabilizationHeuristic;
        [AutoPopulate] SerializedProperty m_MultiThreaded;
        [AutoPopulate] SerializedProperty m_SynchronizeCollisionWorld;
#pragma warning restore 649

        public override void OnInspectorGUI()
        {
            serializedObject.Update();

            EditorGUI.BeginChangeCheck();

            EditorGUILayout.PropertyField(m_SimulationType);

            using (new EditorGUI.DisabledScope(m_SimulationType.intValue == (int)SimulationType.NoPhysics))
            {
                EditorGUILayout.PropertyField(m_Gravity);

                EditorGUILayout.PropertyField(m_SolverIterationCount);

                EditorGUILayout.PropertyField(m_MultiThreaded);

                EditorGUILayout.PropertyField(m_SynchronizeCollisionWorld);

#if HAVOK_PHYSICS_EXISTS
                bool havokPhysics = m_SimulationType.intValue == (int)SimulationType.HavokPhysics;
                using (new EditorGUI.DisabledScope(havokPhysics))
                {
                    bool enableStabilization = m_EnableSolverStabilizationHeuristic.boolValue;

                    // Temporarily invalidate
                    if (havokPhysics)
                        m_EnableSolverStabilizationHeuristic.boolValue = false;

                    EditorGUILayout.PropertyField(m_EnableSolverStabilizationHeuristic,
                        havokPhysics ? Content.SolverStabilizationLabelHavokPhysics : Content.SolverStabilizationLabelUnityPhysics);

                    // Revert back
                    if (havokPhysics)
                        m_EnableSolverStabilizationHeuristic.boolValue = enableStabilization;
                }
#else
                EditorGUILayout.PropertyField(m_EnableSolverStabilizationHeuristic, Content.SolverStabilizationLabelUnityPhysics);
#endif
            }

            if (EditorGUI.EndChangeCheck())
                serializedObject.ApplyModifiedProperties();
        }
    }
}
