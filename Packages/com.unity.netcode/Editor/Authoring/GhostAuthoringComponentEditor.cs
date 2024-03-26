using System;
using System.Collections.Generic;
using Unity.Entities.Conversion;
using UnityEditor;
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

        internal static EntityPrefabComponentsPreview prefabPreview { get; private set; }
        internal static Dictionary<GameObject, BakedGameObjectResult> bakedGameObjectResultsMap { get; private set; }
        internal static GhostAuthoringComponent bakedGhostAuthoringComponent { get; private set; }

        internal static Color brokenColor = new Color(1f, 0.56f, 0.54f);
        internal static Color brokenColorUIToolkit = new Color(0.35f, 0.19f, 0.19f);
        internal static Color brokenColorUIToolkitText = new Color(0.9f, 0.64f, 0.61f);

        internal static bool hasBakedNetCodePrefab => bakedGhostAuthoringComponent != null;
        internal static bool bakingSucceeded => hasBakedNetCodePrefab && bakedGameObjectResultsMap != default;

        /// <summary>Aligned with NetCode for GameObjects.</summary>
        public static Color netcodeColor => EditorGUIUtility.isProSkin ? new Color(0.91f, 0.55f, 0.86f) : new Color(0.8f, 0.14f, 0.5f);

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

            var isViewingPrefab = IsViewingPrefab(go, out var isViewingInstance);
            if (isViewingPrefab)
            {
                if (!isViewingInstance)
                {
                    if (authoringComponent.transform != authoringComponent.transform.root)
                    {
                        EditorGUILayout.HelpBox("The `GhostAuthoringComponent` must only be added to the root GameObject of a prefab. Cannot continue setup.", MessageType.Error);
                        return;
                    }
                }
            }
            else
            {
                var prefabInstanceStatus = PrefabUtility.GetPrefabInstanceStatus(go);
                if (prefabInstanceStatus == PrefabInstanceStatus.NotAPrefab || prefabInstanceStatus == PrefabInstanceStatus.MissingAsset)
                    EditorGUILayout.HelpBox($"'{authoringComponent}' is not a recognised Prefab, so the `GhostAuthoringComponent` is not valid. Please ensure that this GameObject is an unmodified Prefab instance mapped to a known project asset.", MessageType.Error);
            }

            GUI.enabled = isViewingPrefab;

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

            if (isViewingPrefab && !isViewingInstance && !go.GetComponent<GhostAuthoringInspectionComponent>())
            {
                EditorGUILayout.HelpBox("To modify this ghost's per-entity component meta-data, add a `Ghost Authoring Inspection Component` (a MonoBehaviour) to the relevant authoring GameObject.", MessageType.Info);
            }
        }

        // TODO - Add guard against nested Ghost prefabs as they're invalid (although a non-ghost prefab containing ghost nested prefabs is valid AFAIK).
        /// <summary>
        /// <para>Lots of valid and invalid ways to view a prefab. These API calls check to ensure we're either:</para>
        /// <para>- IN the prefabs own scene (thus it's a prefab).</para>
        /// <para>- Selecting the prefab in the project.</para>
        /// <para>Thus, it's invalid to select a prefab instance in some other scene or sub-scene (or some other prefabs scene).</para>
        /// </summary>
        internal static bool IsViewingPrefab(GameObject go, out bool isViewingInstance)
        {
            var isInPrefabScene = go.scene.IsValid();
            isViewingInstance = PrefabUtility.IsPartOfPrefabInstance(go) || isInPrefabScene;
            return !PrefabUtility.IsPartOfNonAssetPrefabInstance(go) && (isInPrefabScene || PrefabUtility.IsPartOfPrefabAsset(go));
        }

        internal static bool TryGetEntitiesAssociatedWithAuthoringGameObject(GhostAuthoringInspectionComponent ghostAuthoringInspection, out BakedGameObjectResult result)
        {
            result = default;
            if (bakedGhostAuthoringComponent == null || bakedGhostAuthoringComponent.gameObject != ghostAuthoringInspection.transform.root.gameObject)
            {
                BakeNetCodePrefab(ghostAuthoringInspection.GetComponent<GhostAuthoringComponent>() ?? ghostAuthoringInspection.transform.root.GetComponent<GhostAuthoringComponent>());
            }
            return bakedGameObjectResultsMap != null && bakedGameObjectResultsMap.TryGetValue(ghostAuthoringInspection.gameObject, out result);
        }

        public static void BakeNetCodePrefab(GhostAuthoringComponent ghostAuthoring)
        {
            if (bakedGhostAuthoringComponent != ghostAuthoring || GhostAuthoringInspectionComponent.forceBake)
            {
                // These allow interop with GhostAuthoringInspectionComponentEditor.
                bakedGhostAuthoringComponent = ghostAuthoring;
                prefabPreview = new EntityPrefabComponentsPreview();
                bakedGameObjectResultsMap = new Dictionary<GameObject, BakedGameObjectResult>(4);
                try
                {
                    prefabPreview.BakeEntireNetcodePrefab(ghostAuthoring, bakedGameObjectResultsMap);
                }
                catch
                {
                    prefabPreview = default;
                    bakedGameObjectResultsMap = default;
                    throw;
                }
            }
        }
    }
}
