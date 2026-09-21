#if PUG_MOD_SDK && USE_PUG_OTHER
using System;
using System.Collections.Generic;
using Pug.Sprite;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ModSDKEntityPreviewRestorer
{
    private const string PreviewName = "EntityPreviewInScene";
    private const string MaterialDirectory = "Packages/dev.pugstorm.mod/Assets/Materials/SpriteObject/";
    private const HideFlags PreviewFlags = HideFlags.HideAndDontSave | HideFlags.NotEditable;
    private const double UpdateInterval = 0.2;

    private sealed class Preview
    {
        public Component authoring;
        public GameObject root;
        public GameObject prefab;
        public Sprite icon;
        public int variation;
        public bool initialized;
        public bool created;
    }

    private static readonly Dictionary<int, Preview> Previews = new Dictionary<int, Preview>();
    private static readonly List<int> RemovedPreviews = new List<int>();
    private static readonly Dictionary<string, Material> Materials = new Dictionary<string, Material>();
    private static GUIStyle _labelStyle;
    private static double _nextUpdate;

    static ModSDKEntityPreviewRestorer()
    {
        EditorApplication.update += UpdatePreviews;
        AssemblyReloadEvents.beforeAssemblyReload += ClearPreviews;
        EditorApplication.quitting += ClearPreviews;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.projectChanged += RefreshPreviews;
        Undo.undoRedoPerformed += RefreshPreviews;
        EditorSceneManager.sceneSaving += OnSceneSaving;
        EditorSceneManager.sceneClosing += OnSceneClosing;
        PrefabStage.prefabStageClosing += OnPrefabStageClosing;
    }

    private static void RefreshPreviews()
    {
        ClearPreviews();
        Materials.Clear();
        _nextUpdate = 0;
        SceneView.RepaintAll();
    }

    [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
    private static void DrawPreview_EntityMonoBehaviourData(EntityMonoBehaviourData authoring, GizmoType gizmoType)
    {
        if (!CanPreview(authoring) || authoring.objectInfo == null)
            return;

        DrawPreview(authoring, authoring.objectInfo.prefabTileSize,
            authoring.objectInfo.prefabCornerOffset, gizmoType);
    }

    [DrawGizmo(GizmoType.InSelectionHierarchy | GizmoType.NotInSelectionHierarchy)]
    private static void DrawPreview_ObjectAuthoring(ObjectAuthoring authoring, GizmoType gizmoType)
    {
        if (!CanPreview(authoring))
            return;

        // Some imported prefabs contain both authoring types; use only one preview.
        if (authoring.TryGetComponent<EntityMonoBehaviourData>(out var legacy) && legacy.objectInfo != null)
            return;

        var placeable = authoring.GetComponent<PlaceableObjectAuthoring>();
        DrawPreview(authoring, placeable != null ? placeable.prefabTileSize : Vector2.one,
            placeable != null ? placeable.prefabCornerOffset : Vector2.zero, gizmoType);
    }

    private static bool CanPreview(Component authoring)
    {
        if (authoring == null || EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorUtility.IsPersistent(authoring) || !authoring.gameObject.scene.IsValid() ||
            !authoring.gameObject.scene.isLoaded || !authoring.gameObject.activeInHierarchy)
            return false;

        for (var current = authoring.transform; current != null; current = current.parent)
        {
            if (current.name == PreviewName && (current.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
                return false;
        }

        return true;
    }

    private static void DrawPreview(Component authoring, Vector2 tileSize, Vector2 cornerOffset, GizmoType gizmoType)
    {
        int id = authoring.GetInstanceID();
        if (!Previews.TryGetValue(id, out var preview))
        {
            preview = new Preview { authoring = authoring };
            Previews.Add(id, preview);
        }

        bool selected = (gizmoType & (GizmoType.Selected | GizmoType.InSelectionHierarchy)) != 0;
        var oldMatrix = Gizmos.matrix;
        var oldColor = Gizmos.color;
        try
        {
            if (selected || PrefabStageUtility.GetCurrentPrefabStage() != null)
            {
                Gizmos.matrix = Matrix4x4.TRS(authoring.transform.position, authoring.transform.rotation, Vector3.one);
                var size = new Vector3(tileSize.x, 0.01f, tileSize.y);
                var offset = new Vector3(cornerOffset.x, 0.05f, cornerOffset.y);
                Gizmos.color = Color.green;
                Gizmos.DrawWireCube(offset + (size - new Vector3(1, 0, 1)) / 2f, size);
                Gizmos.color = Color.red;
                Gizmos.DrawWireCube(offset + Vector3.up * 0.05f, new Vector3(1, 0.01f, 1));
            }

            if (preview.initialized && preview.root == null)
            {
                Gizmos.matrix = Matrix4x4.identity;
                Gizmos.color = Color.yellow;
                Gizmos.DrawSphere(authoring.transform.position + Vector3.up * 0.5f, 0.2f);
                if (selected)
                {
                    _labelStyle ??= new GUIStyle
                    {
                        alignment = TextAnchor.MiddleCenter,
                        fontStyle = FontStyle.Bold,
                        fontSize = 14,
                        normal = { textColor = Color.white }
                    };
                    Handles.Label(authoring.transform.position + Vector3.up, authoring.name, _labelStyle);
                }
            }
        }
        finally
        {
            Gizmos.matrix = oldMatrix;
            Gizmos.color = oldColor;
        }
    }

    private static void UpdatePreviews()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling ||
            EditorApplication.isUpdating || EditorApplication.timeSinceStartup < _nextUpdate)
            return;

        _nextUpdate = EditorApplication.timeSinceStartup + UpdateInterval;
        RemovedPreviews.Clear();
        bool changed = false;
        foreach (var pair in Previews)
        {
            var preview = pair.Value;
            if (!CanPreview(preview.authoring))
            {
                DestroyPreview(preview);
                RemovedPreviews.Add(pair.Key);
                changed = true;
                continue;
            }

            ResolvePreview(preview.authoring, out var prefab, out var icon, out int variation);
            if (preview.initialized && preview.prefab == prefab && preview.icon == icon &&
                preview.variation == variation && (preview.root != null || !preview.created))
            {
                continue;
            }

            DestroyPreview(preview);
            RemoveStalePreviews(preview.authoring.transform);
            preview.prefab = prefab;
            preview.icon = icon;
            preview.variation = variation;
            preview.initialized = true;
            preview.created = false;
            CreatePreview(preview);
            preview.created = preview.root != null;
            changed = true;
        }

        foreach (int id in RemovedPreviews)
        {
            Previews.Remove(id);
        }

        if (changed)
        {
            SceneView.RepaintAll();
        }
    }

    private static void ResolvePreview(Component authoring, out GameObject prefab, out Sprite icon, out int variation)
    {
        prefab = null;
        icon = null;
        variation = 0;
        if (authoring is EntityMonoBehaviourData legacy)
        {
            var info = legacy.objectInfo;
            if (info == null)
                return;

            prefab = legacy.optionalPreviewPrefab;
            if (prefab == null && info.prefabInfo != null)
            {
                var graphical = info.prefabInfo.GetGraphical();
                if (graphical != null)
                    prefab = graphical;
            }
            icon = info.icon;
            variation = info.variation;
        }
        else if (authoring is ObjectAuthoring modular)
        {
            prefab = modular.graphicalRef.TryGet(out var graphical)
                ? graphical.prefab
                : modular.graphicalPrefab;
            var inventory = modular.GetComponent<InventoryItemAuthoring>();
            icon = inventory != null ? inventory.icon : null;
            variation = modular.variation;
        }
    }

    private static void CreatePreview(Preview preview)
    {
        if (preview.prefab == null && preview.icon == null)
        {
            return;
        }

        var root = new GameObject(PreviewName) { hideFlags = PreviewFlags };
        root.SetActive(false);
        SceneManager.MoveGameObjectToScene(root, preview.authoring.gameObject.scene);
        preview.root = root;
        bool completed = false;

        try
        {
            bool hasVisuals = false;

            if (preview.prefab != null)
            {
                var graphics = Object.Instantiate(preview.prefab, root.transform, false);
                graphics.SetActive(true);
                PrepareGraphics(graphics);
                root.transform.SetParent(preview.authoring.transform, false);

                ObjectInfo info = null;
                foreach (var component in graphics.GetComponents<MonoBehaviour>())
                {
                    if (component is IEntityMonoBehaviourDataPreview updater)
                    {
                        info ??= preview.authoring is EntityMonoBehaviourData legacy
                            ? legacy.objectInfo
                            : ((ObjectAuthoring)preview.authoring).ObjectInfo;
                        updater.UpdateGraphicsFromObjectInfo(info);
                    }
                }

                PrepareGraphics(graphics);
                hasVisuals = HasVisuals(graphics.transform, true);

                if (!hasVisuals)
                {
                    Object.DestroyImmediate(graphics);
                }
            }

            if (!hasVisuals && preview.icon != null)
            {
                var iconObject = new GameObject("Icon");
                iconObject.transform.SetParent(root.transform, false);
                iconObject.transform.localPosition = new Vector3(0, 0.05f, 0);
                iconObject.transform.localRotation = Quaternion.Euler(90, 0, 0);
                var renderer = iconObject.AddComponent<SpriteRenderer>();
                renderer.sprite = preview.icon;
                renderer.sortingOrder = 5;
                hasVisuals = true;
            }

            if (hasVisuals)
            {
                SetPreviewFlags(root.transform);
                root.transform.SetParent(preview.authoring.transform, false);
                root.SetActive(true);
            }
            else
            {
                DestroyPreview(preview);
            }
            completed = true;
        }
        finally
        {
            if (!completed)
            {
                DestroyPreview(preview);
            }
        }
    }

    private static void PrepareGraphics(GameObject graphics)
    {
        foreach (var component in graphics.GetComponentsInChildren<Component>(true))
        {
            if (component == null)
            {
                continue;
            }

            if (component is SpriteObject spriteObject)
            {
                spriteObject.material = GetPreviewMaterial(spriteObject.material);
                if (spriteObject.enabled && spriteObject.asset != null &&
                    spriteObject.TryGetComponent<SpriteRenderer>(out var redundantSprite))
                    redundantSprite.enabled = false;
            }
            else if (component is Behaviour behaviour)
            {
                behaviour.enabled = false;
            }
            else if (component is Collider collider)
            {
                collider.enabled = false;
            }
            else if (component is Collider2D collider2D)
            {
                collider2D.enabled = false;
            }
            else if (component is Rigidbody body)
            {
                body.detectCollisions = false;
                body.isKinematic = true;
            }
            else if (component is Rigidbody2D body2D)
            {
                body2D.simulated = false;
            }
            else if (component is ParticleSystem particles)
            {
                var main = particles.main;
                main.playOnAwake = false;
                particles.Stop(false, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            else if (component is ParticleSystemRenderer || component is TrailRenderer || component is LineRenderer)
            {
                ((Renderer)component).enabled = false;
            }
        }
    }

    private static Material GetPreviewMaterial(Material original)
    {
        if (original != null && AssetDatabase.GetAssetPath(original).StartsWith(MaterialDirectory, StringComparison.Ordinal))
            return original;

        string kind = "Lit";
        if (original != null)
        {
            if (original.name.IndexOf("Shadow", StringComparison.OrdinalIgnoreCase) >= 0)
                kind = "Shadow";
            else if (original.name.IndexOf("Indirect", StringComparison.OrdinalIgnoreCase) >= 0)
                kind = "IndirectLight";
            else if (original.name.IndexOf("Unlit", StringComparison.OrdinalIgnoreCase) >= 0)
                kind = "Unlit";
        }

        if (!Materials.TryGetValue(kind, out var material))
        {
            string path = MaterialDirectory + "UGC SpriteObject " + kind + ".mat";
            material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
                throw new InvalidOperationException($"Missing entity preview material: {path}");
            Materials.Add(kind, material);
        }
        return material;
    }

    private static bool HasVisuals(Transform transform, bool parentActive)
    {
        bool active = parentActive && transform.gameObject.activeSelf;
        if (!active)
            return false;

        if (transform.TryGetComponent<SpriteObject>(out var spriteObject) &&
            spriteObject.enabled && spriteObject.asset != null && spriteObject.material != null)
            return true;
        if (transform.TryGetComponent<SpriteRenderer>(out var sprite) && sprite.enabled && sprite.sprite != null)
            return true;
        if (transform.TryGetComponent<MeshRenderer>(out var mesh) && mesh.enabled &&
            transform.TryGetComponent<MeshFilter>(out var filter) && filter.sharedMesh != null)
            return true;
        if (transform.TryGetComponent<SkinnedMeshRenderer>(out var skinned) && skinned.enabled && skinned.sharedMesh != null)
            return true;

        foreach (Transform child in transform)
        {
            if (HasVisuals(child, active))
            {
                return true;
            }
        }
        return false;
    }

    private static void SetPreviewFlags(Transform transform)
    {
        transform.gameObject.hideFlags = PreviewFlags;

        foreach (Transform child in transform)
        {
            SetPreviewFlags(child);
        }
    }

    private static void RemoveStalePreviews(Transform authoring)
    {
        // Only remove transient instances left by duplication or the previous script.
        // Never open/save the prefab asset to delete an in-memory preview.
        for (int i = authoring.childCount - 1; i >= 0; i--)
        {
            var child = authoring.GetChild(i);

            if (child.name == PreviewName && (child.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0)
            {
                Object.DestroyImmediate(child.gameObject);
            }
        }
    }

    private static void DestroyPreview(Preview preview)
    {
        if (preview.root != null)
        {
            Object.DestroyImmediate(preview.root);
        }

        preview.root = null;
    }

    private static void ClearPreviews()
    {
        foreach (var preview in Previews.Values)
        {
            DestroyPreview(preview);
        }

        Previews.Clear();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        ClearPreviews();

        if (state == PlayModeStateChange.EnteredEditMode)
        {
            SceneView.RepaintAll();
        }
    }

    private static void OnSceneSaving(Scene scene, string path) => ClearPreviews();
    private static void OnSceneClosing(Scene scene, bool removingScene) => ClearPreviews();
    private static void OnPrefabStageClosing(PrefabStage stage) => ClearPreviews();
}
#endif
