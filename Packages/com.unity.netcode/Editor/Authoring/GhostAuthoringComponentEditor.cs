using System;
using System.Collections.Generic;
using Unity.Entities.Conversion;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Unity.NetCode.Editor
{
    [CustomEditor(typeof(GhostAuthoringComponent))]
    internal class GhostAuthoringComponentEditor : UnityEditor.Editor
    {
        SerializedProperty DefaultGhostMode;
        SerializedProperty SupportedGhostModes;
        SerializedProperty OptimizationMode;
        SerializedProperty HasOwner;
        SerializedProperty SupportAutoCommandTarget;
        SerializedProperty TrackInterpolationDelay;
        SerializedProperty GhostGroup;
        SerializedProperty UsePreSerialization;
        SerializedProperty Importance;



        internal static Color brokenColor = new Color(1f, 0.56f, 0.54f);
        internal static Color brokenColorUIToolkit = new Color(0.35f, 0.19f, 0.19f);
        internal static Color brokenColorUIToolkitText = new Color(0.9f, 0.64f, 0.61f);

        /// <summary>Aligned with NetCode for GameObjects.</summary>
        public static Color netcodeColor => new Color(0.91f, 0.55f, 0.86f, 1f);

        void OnEnable()
        {
            DefaultGhostMode = serializedObject.FindProperty("DefaultGhostMode");
            SupportedGhostModes = serializedObject.FindProperty("SupportedGhostModes");
            OptimizationMode = serializedObject.FindProperty("OptimizationMode");
            HasOwner = serializedObject.FindProperty("HasOwner");
            SupportAutoCommandTarget = serializedObject.FindProperty("SupportAutoCommandTarget");
            TrackInterpolationDelay = serializedObject.FindProperty("TrackInterpolationDelay");
            GhostGroup = serializedObject.FindProperty("GhostGroup");
            UsePreSerialization = serializedObject.FindProperty("UsePreSerialization");
            Importance = serializedObject.FindProperty("Importance");
        }

        public override void OnInspectorGUI()
        {
            var authoringComponent = (GhostAuthoringComponent)target;
            var go = authoringComponent.gameObject;
            var isPrefabEditable = IsPrefabEditable(go);
            GUI.enabled = isPrefabEditable;

            var currentPrefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var isViewingPrefab = PrefabUtility.IsPartOfPrefabAsset(go) || PrefabUtility.IsPartOfPrefabInstance(go) || (currentPrefabStage != null && currentPrefabStage.IsPartOfPrefabContents(go));
            if (isPrefabEditable)
            {
                if (authoringComponent.transform != authoringComponent.transform.root)
                {
                    EditorGUILayout.HelpBox("The `GhostAuthoringComponent` must only be added to the root GameObject of a prefab. This is invalid, please remove or correct this authoring.", MessageType.Error);
                    GUI.enabled = false;
                }

                if (!isViewingPrefab)
                {
                    EditorGUILayout.HelpBox($"'{authoringComponent}' is not a recognised Prefab, so the `GhostAuthoringComponent` is not valid. Please ensure that this GameObject is an unmodified Prefab instance mapped to a known project asset.", MessageType.Error);
                }
            }


            var originalColor = GUI.color;

            GUI.color = originalColor;
            EditorGUILayout.PropertyField(Importance);
            EditorGUILayout.PropertyField(SupportedGhostModes);

            var self = (GhostAuthoringComponent) target;
            var isOwnerPredictedError = DefaultGhostMode.enumValueIndex == (int) GhostMode.OwnerPredicted && !self.HasOwner;

            if (SupportedGhostModes.intValue == (int) GhostModeMask.All)
            {
                EditorGUILayout.PropertyField(DefaultGhostMode);

                // Selecting OwnerPredicted on a ghost without a GhostOwner will cause an exception during conversion - display an error for that case in the inspector
                if (isOwnerPredictedError)
                {
                    EditorGUILayout.HelpBox("Setting `Default Ghost Mode` to `Owner Predicted` is not valid unless the Ghost also supports being Owned by a player (via the `Ghost Owner Component`). Please resolve it one of the following ways.", MessageType.Error);
                    GUI.color = brokenColor;
                    if (GUILayout.Button("Enable Ownership via 'Has Owner'?")) HasOwner.boolValue = true;
                    if (GUILayout.Button("Set to `GhostMode.Interpolated`?")) DefaultGhostMode.enumValueIndex = (int) GhostMode.Interpolated;
                    if (GUILayout.Button("Set to `GhostMode.Predicted`?")) DefaultGhostMode.enumValueIndex = (int) GhostMode.Predicted;
                    GUI.color = originalColor;
                }
            }

            EditorGUILayout.PropertyField(OptimizationMode);
            EditorGUILayout.PropertyField(HasOwner);

            if (self.HasOwner)
            {
                EditorGUILayout.PropertyField(SupportAutoCommandTarget);
                EditorGUILayout.PropertyField(TrackInterpolationDelay);
            }
            EditorGUILayout.PropertyField(GhostGroup);
            EditorGUILayout.PropertyField(UsePreSerialization);

            if (serializedObject.ApplyModifiedProperties())
            {
                GhostAuthoringInspectionComponent.forceBake = true;
                var allComponentOverridesForGhost = GhostAuthoringInspectionComponent.CollectAllComponentOverridesInInspectionComponents(authoringComponent, false);
                GhostComponentAnalytics.BufferConfigurationData(authoringComponent, allComponentOverridesForGhost.Count);
            }

            if (isViewingPrefab && !go.GetComponent<GhostAuthoringInspectionComponent>())
            {
                EditorGUILayout.HelpBox("To modify this ghost's per-entity component meta-data, add a `Ghost Authoring Inspection Component` (a MonoBehaviour) to the relevant authoring GameObject. Inspecting children is supported by adding the Inspection component to the relevant child.", MessageType.Info);
            }
        }

        // TODO - Add guard against nested Ghost prefabs as they're invalid (although a non-ghost prefab containing ghost nested prefabs is valid AFAIK).
        /// <summary>
        /// <para>Lots of valid and invalid ways to view a prefab. These API calls check to ensure we're either:</para>
        /// <para>- IN the prefabs own scene (thus it's editable).</para>
        /// <para>- Selecting the prefab in the PROJECT.</para>
        /// <para>- NOT selecting this prefab in a SCENE.</para>
        /// </summary>
        /// <remarks>Note that it is valid to add this Inspection onto a nested-prefab!</remarks>
        internal static bool IsPrefabEditable(GameObject go)
        {
            if (PrefabUtility.IsPartOfImmutablePrefab(go))
                return false;
            if (PrefabUtility.IsPartOfPrefabAsset(go))
                return true;
            return !PrefabUtility.IsPartOfPrefabInstance(go);
        }
    }
}
