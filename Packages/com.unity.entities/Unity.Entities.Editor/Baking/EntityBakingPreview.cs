using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Editor.Legacy;
using Unity.Mathematics;
using Unity.Serialization.Editor;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Editor
{
    [CustomPreview(typeof(GameObject))]
    class EntityBakingPreview : ObjectPreview
    {
        const string k_SharedStateKey = nameof(EntityBakingPreview) + "." + nameof(SharedState);

        const int k_MaxAdditionalEntitiesDisplay = 250;
        const int k_MaxPreviewableGameObjectsCount = 100;

        static readonly GUIContent k_PreviewTitle = EditorGUIUtility.TrTextContent("Entity Baking Preview");

        static readonly ComponentTypeNameComparer s_ComponentTypeNameComparer = new ComponentTypeNameComparer();
        static readonly WorldListChangeTracker s_WorldListChangeTracker = new WorldListChangeTracker();

        // internal for tests
        internal static class Worlds
        {
            const WorldFlags MustMatchMask = WorldFlags.Live | WorldFlags.Conversion;
            const WorldFlags MustNotMatchMask = WorldFlags.Streaming | WorldFlags.Shadow;

            static readonly WorldDisplayNameCache WorldDisplayNameCache = new WorldDisplayNameCache(FilterWorld);
            static readonly List<World> s_FilteredWorlds = new List<World>();
            static string[] s_WorldNames = Array.Empty<string>();

            static bool FilterWorld(World w) => (w.Flags & MustMatchMask) != 0 && (w.Flags & MustNotMatchMask) == 0;

            static void Update()
            {
                if (!s_WorldListChangeTracker.HasChanged())
                    return;

                s_FilteredWorlds.Clear();

                foreach (var world in World.All)
                {
                    if (FilterWorld(world))
                        s_FilteredWorlds.Add(world);
                }

                WorldDisplayNameCache.RebuildCache();
                s_WorldNames = s_FilteredWorlds.Select(WorldDisplayNameCache.GetWorldDisplayName).ToArray();
            }

            public static List<World> FilteredWorlds
            {
                get
                {
                    Update();
                    return s_FilteredWorlds;
                }
            }

            public static string[] FilteredWorldNames
            {
                get
                {
                    Update();
                    return s_WorldNames;
                }
            }
        }

        static class Strings
        {
            public static readonly string Entity = L10n.Tr("Entity");
            public static readonly string Entities = L10n.Tr("Entities");
            public static readonly string LiveConversionDisabled = L10n.Tr("Live Conversion is disabled. Enable it in the DOTS file menu to see entity baking preview.");
            public static readonly string PreviewForUnbakedGameObjects = L10n.Tr("Entity baking can only be previewed for GameObjects baked by a SubScene.");
            public static readonly string MultiSelectionWarning = L10n.Tr("Components that are only on some of the baked entities are not shown.");
            public static readonly string WillBeBakedAtRuntime = L10n.Tr("This game object will be baked at runtime.");
            public static readonly string PrimaryEntityDestroyed = L10n.Tr("The primary entity has been destroyed during baking.");
            public static readonly string OneOrMorePrimaryEntitiesDestroyed = L10n.Tr("One or more primary entities have been destroyed during baking.");
            public static readonly string TooManyAdditionalEntities = L10n.Tr("Too many additional entities to display.");
            public static readonly string TooManyGameObjectsToPreview = L10n.Tr($"For performance reasons, baking preview is disabled when inspecting more than {k_MaxPreviewableGameObjectsCount} GameObjects.");
        }

        static class Styles
        {
            public static readonly GUIStyle EntityConversionWarningMessage = EditorStyleUSSBridge.FromUSS(".EntityConversionWarningMessage");
            public static readonly GUIStyle EntityConversionCommonComponentMessage = EditorStyleUSSBridge.FromUSS(".EntityConversionCommonComponentMessage");
            public static readonly GUIStyle EntityConversionComponentTag = EditorStyleUSSBridge.FromUSS(".EntityConversionComponentTag");
            public static readonly GUIStyle AdditionalEntityTag = EditorStyleUSSBridge.FromUSS(".AdditionalEntityTag");
            public static readonly GUIStyle SelectedAdditionalEntityTag = EditorStyleUSSBridge.FromUSS(".SelectedAdditionalEntityTag");
            public static readonly GUIStyle AdditionalEntityIconTag = EditorStyleUSSBridge.FromUSS(".AdditionalEntityIconTag");
            public static readonly GUIStyle EntityConversionComponentArea = EditorStyleUSSBridge.FromUSS(".EntityConversionComponentArea");
            public static readonly GUIStyle AdditionalEntityToggle = EditorStyleUSSBridge.FromUSS(".AdditionalEntityToggle");
        }

        /// <summary>
        /// Helper container to store session state data per <see cref="GameObject"/>.
        /// </summary>
        class State
        {
            /// <summary>
            /// This field controls the toggle/foldout for additional entities. This is independent of the additional entity index.
            /// </summary>
            public bool ShowAdditionalEntities;

            /// <summary>
            /// The selected index for additional entities. This value is preserved even when toggling <see cref="ShowAdditionalEntities"/>
            /// </summary>
            public int AdditionalEntityIndex;

            /// <summary>
            /// The set of currently selected component type indices. These are the components that have fields drawn in the preview window.
            /// </summary>
            public readonly List<TypeIndex> SelectedComponentTypes = new List<TypeIndex>();
        }

        /// <summary>
        /// Helper container to store session state data for the all instances of <see cref="EntityBakingPreview"/>.
        /// </summary>
        class SharedState
        {
            /// <summary>
            /// The currently selected <see cref="World"/> in the drop-down.
            /// </summary>
            public int SelectedWorldIndex;
        }

        /// <summary>
        /// State data per <see cref="GameObject"/>. This data is persisted between domain reloads.
        /// </summary>
        State m_State;

        /// <summary>
        /// State data for all instances of <see cref="EntityBakingPreview"/>. This data is persisted between domain reloads.
        /// </summary>
        SharedState m_SharedState;

        /// <summary>
        /// Helper structure to drawn runtime component data.
        /// </summary>
        RuntimeComponentsDrawer m_RuntimeComponentsDrawer;

        /// <summary>
        /// Helper class to detect changes to entities that derive from a given set of gameObjects.
        /// </summary>
        GameObjectBakingChangeTracker m_ChangeTracker;

        /// <summary>
        /// This is used to keep the current targets of the preview. This should be used instead of `ObjectPreview.target`
        /// and `ObjectPreview.m_Targets` because we need to override the default behaviour of multi-selection. This is
        /// achieved by "forcing" single selection on the preview <see cref="Initialize"/>.
        /// </summary>
        readonly List<GameObject> m_GameObjectTargets = new List<GameObject>();

        Rect m_PreviewRect;
        Vector2 m_ScrollPosition = Vector2.zero;
        Vector2 m_ScrollHeaderPosition = Vector2.zero;
        int m_LastSelectedComponentIdx;

        bool m_ReadyForFirstLayout = true;

        // Caches for expensive operations that survive through an IMGUI rendering cycle (User Events + Layout Events (1..N) + Repaint Event).
        readonly List<EntityBakingData> m_CachedBakingData = new List<EntityBakingData>();
        readonly List<EntityContainer> m_InspectorTargets = new List<EntityContainer>();
        readonly List<ComponentType> m_CommonComponentTypes = new List<ComponentType>();

        /// <summary>
        /// Gets the currently selected <see cref="World"/> in the preview window for the current session.
        /// </summary>
        /// <returns>The currently selected <see cref="World"/>.</returns>
        public static World GetCurrentlySelectedWorld()
        {
            var state = SessionState<SharedState>.GetOrCreate(k_SharedStateKey);

            if (Worlds.FilteredWorlds.Count == 0)
            {
                state.SelectedWorldIndex = -1;
                return null;
            }

            state.SelectedWorldIndex = state.SelectedWorldIndex >= 0 && state.SelectedWorldIndex < Worlds.FilteredWorlds.Count ? state.SelectedWorldIndex : 0;
            return Worlds.FilteredWorlds[state.SelectedWorldIndex];
        }

        /// <summary>
        /// Called when the preview gets created.
        /// </summary>
        /// <param name="targets">The selected <see cref="UnityEngine.Object"/> to preview.</param>
        public override void Initialize(Object[] targets)
        {
            m_GameObjectTargets.Clear();
            m_GameObjectTargets.AddRange(targets.OfType<GameObject>());

            var mainTarget = m_GameObjectTargets.First();
            var instanceId = mainTarget.GetInstanceID();

            m_State = SessionState<State>.GetOrCreate($"{nameof(EntityBakingPreview)}.{nameof(State)}.{instanceId}");
            m_SharedState = SessionState<SharedState>.GetOrCreate(k_SharedStateKey);
            m_RuntimeComponentsDrawer = new RuntimeComponentsDrawer();
            m_RuntimeComponentsDrawer.OnDeselectComponent += typeIndex => m_State.SelectedComponentTypes.Remove(typeIndex);
            m_ChangeTracker = new GameObjectBakingChangeTracker();
            m_Targets = new Object[] { mainTarget };
            m_LastSelectedComponentIdx = -1;

            EditorApplication.update += Update;
        }

        /// <summary>
        /// Called to determine if the targeted <see cref="UnityEngine.Object"/> can be previewed in its current state.
        /// </summary>
        public override bool HasPreviewGUI()
        {
            return m_GameObjectTargets.Any(GameObjectBakingEditorUtility.IsBaked);
        }

        /// <summary>
        /// Called to get the content label of the preview header.
        /// </summary>
        public override GUIContent GetPreviewTitle()
        {
            return k_PreviewTitle;
        }

        /// <summary>
        /// Called to implement custom controls in the preview header.
        /// </summary>
        public override void OnPreviewSettings()
        {
            m_SharedState.SelectedWorldIndex = EditorGUILayout.Popup(GUIContent.none, m_SharedState.SelectedWorldIndex, Worlds.FilteredWorldNames);
        }

        /// <summary>
        /// Called to implement custom controls in the preview window.
        /// </summary>
        public override void OnPreviewGUI(Rect r, GUIStyle background)
        {
            var firstLayout = false;
            if (Event.current.type == EventType.Layout)
            {
                firstLayout = m_ReadyForFirstLayout;
                m_ReadyForFirstLayout = false;
            }
            else if (Event.current.type == EventType.Repaint)
            {
                m_PreviewRect = r;

                // The next Layout event after a Repaint event will be the first Layout event of the cycle.
                m_ReadyForFirstLayout = true;
            }

            using (new GUILayout.AreaScope(m_PreviewRect, string.Empty, Styles.EntityConversionComponentArea))
            using (new EditorGUILayout.VerticalScope())
            {
                if (m_GameObjectTargets.Count > k_MaxPreviewableGameObjectsCount)
                {
                    GUILayout.Label(EditorGUIUtilityBridge.TempContent(Strings.TooManyGameObjectsToPreview), Styles.EntityConversionWarningMessage);
                    return;
                }

                if (!LiveConversionConfigHelper.LiveConversionEnabledInEditMode)
                {
                    GUILayout.Label(EditorGUIUtilityBridge.TempContent(Strings.LiveConversionDisabled), Styles.EntityConversionWarningMessage);
                    return;
                }

                if (firstLayout)
                    EntityBakingEditorUtility.GetBakingData(m_GameObjectTargets, GetCurrentlySelectedWorld(), m_CachedBakingData);

                if (m_GameObjectTargets.Count != m_CachedBakingData.Count)
                {

                    GUILayout.Label(EditorGUIUtilityBridge.TempContent(Strings.PreviewForUnbakedGameObjects), Styles.EntityConversionWarningMessage);
                    return;
                }

                if (!ValidateGameObjectBakingData(m_CachedBakingData, out var errorMessage))
                {
                    GUILayout.Label(EditorGUIUtilityBridge.TempContent(errorMessage), Styles.EntityConversionWarningMessage);
                    return;
                }

                var primary = m_CachedBakingData[0];
                var isSingleSelection = m_CachedBakingData.Count == 1;
                var additionalEntitiesCount = primary.AdditionalEntities.Length - 1;
                var hasAdditionalEntities = isSingleSelection && additionalEntitiesCount > 0;

                var entityName = isSingleSelection
                    ? $"{primary.EntityManager.GetName(primary.PrimaryEntity)} (Entity)"
                    : $"{EditorGUIBridge.mixedValueContent.text}({m_CachedBakingData.Count} entities)";

                if (m_State.AdditionalEntityIndex > additionalEntitiesCount)
                    m_State.AdditionalEntityIndex = additionalEntitiesCount;

                if (hasAdditionalEntities)
                {
                    m_State.ShowAdditionalEntities = EditorGUILayout.Foldout(m_State.ShowAdditionalEntities,
                        EditorGUIUtilityBridge.TempContent($"{entityName} + {additionalEntitiesCount} {(additionalEntitiesCount > 1 ? Strings.Entities : Strings.Entity)}", EditorIcons.Entity),
                        new GUIStyle(EditorStyles.foldout)
                        {
                            fixedWidth = 650.0f,
                            fontStyle = FontStyle.Bold
                        });

                    GUILayout.Space(4);

                    if (m_State.ShowAdditionalEntities)
                    {
                        if (additionalEntitiesCount > k_MaxAdditionalEntitiesDisplay)
                        {
                            var labelRect = GUILayoutUtility.GetRect(EditorGUIUtility.labelWidth, EditorGUIUtility.singleLineHeight);
                            labelRect.xMin += (EditorGUI.indentLevel + 1) * 15; // 15 = EditorGUI.kIndentPerLevel (private const)
                            GUI.Label(labelRect, EditorGUIUtilityBridge.TempContent(Strings.TooManyAdditionalEntities));
                        }
                        else
                        {
                            using var scroll = new EditorGUILayout.ScrollViewScope(m_ScrollHeaderPosition, GUILayout.ExpandWidth(true), GUILayout.Height(56));
                            m_ScrollHeaderPosition = scroll.scrollPosition;

                            var additionalEntityLabel = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 3f);
                            additionalEntityLabel.height -= 3.0f;
                            additionalEntityLabel.x = 4.0f;

                            for (var i = 1; i < primary.AdditionalEntities.Length; ++i)
                            {
                                var entity = primary.AdditionalEntities[i];

                                if (ShowAdditionalEntityToggle(ref additionalEntityLabel, i == m_State.AdditionalEntityIndex, primary.EntityManager, entity))
                                {
                                    m_State.AdditionalEntityIndex = i;
                                }
                                else if (i == m_State.AdditionalEntityIndex)
                                {
                                    m_State.AdditionalEntityIndex = -1;
                                }
                            }
                        }

                        GUILayout.Space(8);
                    }
                }
                else
                {
                    EditorGUILayout.LabelField(EditorGUIUtilityBridge.TempContent(entityName, EditorIcons.Entity), EditorStyles.boldLabel);
                    GUILayout.Space(4);
                }

                if (firstLayout)
                {
                    GetInspectorTargets(m_CachedBakingData, m_State, m_InspectorTargets);
                    GetCommonComponentTypes(m_InspectorTargets, m_CommonComponentTypes);
                }

                using (var scroll = new EditorGUILayout.ScrollViewScope(m_ScrollPosition))
                {
                    m_ScrollPosition = scroll.scrollPosition;

                    var labelRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 3f);
                    labelRect.x = 4f;
                    labelRect.height = EditorGUIUtility.singleLineHeight;

                    for (var i = 0; i < m_CommonComponentTypes.Count; i++)
                    {
                        var componentType = m_CommonComponentTypes[i];
                        var isSelected = m_State.SelectedComponentTypes.Contains(componentType.TypeIndex);
                        var isSelectedInUi = ShowComponentTypeToggle(ref labelRect, isSelected, componentType);

                        HandleComponentSelection(m_CommonComponentTypes, componentType, i, isSelected, isSelectedInUi);
                    }

                    GUILayout.Space(6);

                    m_RuntimeComponentsDrawer.SetTargets(m_InspectorTargets);
                    m_RuntimeComponentsDrawer.SetComponentTypes(m_State.SelectedComponentTypes);

                    if (m_State.SelectedComponentTypes.Count > 0 && m_CommonComponentTypes.Count(type => m_State.SelectedComponentTypes.Contains(type.TypeIndex)) > 0)
                    {
                        using (new WideModeScope(338))
                        {
                            m_RuntimeComponentsDrawer.OnGUI();
                        }
                    }

                    GUILayout.Space(4);

                    if (!isSingleSelection)
                    {
                        GUILayout.Label(EditorGUIUtilityBridge.TempContent(Strings.MultiSelectionWarning), Styles.EntityConversionCommonComponentMessage);
                    }
                }
            }
        }

        void HandleComponentSelection(List<ComponentType> componentTypes, ComponentType componentType, int indexInOrderedComponentList, bool isSelected, bool isSelectedInUi)
        {
            var isMultiSelectionEnabled = EditorGUI.actionKey;
            var isWideSelectionEnabled = Event.current.shift;

            if (isSelected && !isSelectedInUi)
            {
                if (isWideSelectionEnabled && m_LastSelectedComponentIdx != -1)
                {
                    m_State.SelectedComponentTypes.Clear();
                    SelectRange(componentTypes, m_LastSelectedComponentIdx, indexInOrderedComponentList);
                }
                else if (m_State.SelectedComponentTypes.Count > 1 && !isMultiSelectionEnabled)
                {
                    m_State.SelectedComponentTypes.Clear();
                    m_State.SelectedComponentTypes.Add(componentType.TypeIndex);
                    m_LastSelectedComponentIdx = indexInOrderedComponentList;
                }
                else
                {
                    m_State.SelectedComponentTypes.Remove(componentType.TypeIndex);
                }
            }
            else if (!isSelected && isSelectedInUi)
            {
                if (!isMultiSelectionEnabled)
                    m_State.SelectedComponentTypes.Clear();

                if (isWideSelectionEnabled && m_LastSelectedComponentIdx != -1 && m_LastSelectedComponentIdx != indexInOrderedComponentList)
                {
                    SelectRange(componentTypes, m_LastSelectedComponentIdx, indexInOrderedComponentList);
                }
                else
                {
                    m_State.SelectedComponentTypes.Add(componentType.TypeIndex);
                    m_LastSelectedComponentIdx = indexInOrderedComponentList;
                }
            }
        }

        void SelectRange(List<ComponentType> commonComponentTypes, int boundA, int boundB)
        {
            for (var i = math.min(boundA, boundB); i <= math.max(boundA, boundB); i++)
            {
                m_State.SelectedComponentTypes.Add(commonComponentTypes[i].TypeIndex);
            }
        }

        /// <summary>
        /// Called by <see cref="EditorApplication.update"/> ~100 per second.
        /// </summary>
        void Update()
        {
            var inspector = InspectorWindowBridge.GetPreviewOwner(this, out var isSelected);

            if (inspector == null)
            {
                // Our owner no longer exists.
                // ReSharper disable once DelegateSubtraction
                EditorApplication.update -= Update;
                return;
            }

            if (isSelected && m_ChangeTracker.DidChange(GetCurrentlySelectedWorld(), m_GameObjectTargets, m_State.SelectedComponentTypes))
                inspector.Repaint();
        }

        static bool ValidateGameObjectBakingData(IReadOnlyList<EntityBakingData> bakingDataEntries, out string message)
        {
            if (bakingDataEntries.Count == 0)
            {
                message = Strings.WillBeBakedAtRuntime;
                return false;
            }

            for (var i = 0; i < bakingDataEntries.Count; i++)
            {
                var entityManager = bakingDataEntries[i].EntityManager;
                var primaryEntity = bakingDataEntries[i].PrimaryEntity;

                if (!entityManager.SafeExists(primaryEntity))
                {
                    message = bakingDataEntries.Count == 0
                        ? Strings.PrimaryEntityDestroyed
                        : Strings.OneOrMorePrimaryEntitiesDestroyed;

                    return false;
                }
            }

            message = string.Empty;
            return true;
        }

        /// <summary>
        /// Given a set of <see cref="EntityBakingData"/> and the current preview state, this method will return a set of <see cref="EntityContainer"/> which should be inspected.
        /// </summary>
        /// <param name="bakingDataEntries">The set of currently selected objects.</param>
        /// <param name="state">The current <see cref="EntityBakingPreview"/> state.</param>
        /// <param name="result">The <see cref="EntityContainer"/> instances which should be inspected.</param>
        static void GetInspectorTargets(IReadOnlyList<EntityBakingData> bakingDataEntries, State state, List<EntityContainer> result)
        {
            result.Clear();
            var root = bakingDataEntries[0];

            // @TODO (UX) Figure out how we want to show additional entities during multi-selection.
            if (bakingDataEntries.Count == 1 && state.ShowAdditionalEntities && state.AdditionalEntityIndex != -1)
            {
                result.Add(new EntityContainer(root.EntityManager, root.AdditionalEntities[state.AdditionalEntityIndex]));
                return;
            }

            foreach (var data in bakingDataEntries)
            {
                result.Add(new EntityContainer(data.EntityManager, data.PrimaryEntity));
            }
        }

        /// <summary>
        /// Given a set of <see cref="EntityContainer"/> this method will return the set of <see cref="ComponentType"/> that are common to all of them.
        /// </summary>
        void GetCommonComponentTypes(IReadOnlyList<EntityContainer> targets, List<ComponentType> result)
        {
            result.Clear();

            if (targets.Count == 0)
                return;

            if (targets.Count == 1)
            {
                // Fast path for single target.
                using (var componentTypes = targets[0].EntityManager.GetComponentTypes(targets[0].Entity))
                {
                    componentTypes.Sort(s_ComponentTypeNameComparer);

                    foreach (var componentType in componentTypes)
                    {
                        result.Add(componentType);
                    }
                }
            }
            else
            {
                // Slow path for multi target using the intersection.
                using (var intersection = Pooling.GetList<ComponentType>())
                using (var hash = Pooling.GetHashSet<ComponentType>())
                {
                    for (var i = 0; i < targets.Count; i++)
                    {
                        using (var componentTypes = targets[i].EntityManager.GetComponentTypes(targets[i].Entity))
                        {
                            if (i == 0)
                            {
                                foreach (var type in componentTypes)
                                {
                                    intersection.List.Add(type);
                                    hash.Set.Add(type);
                                }
                            }
                            else
                            {
                                foreach (var type in hash.Set)
                                {
                                    if (!componentTypes.Contains(type))
                                    {
                                        intersection.List.Remove(type);
                                    }
                                }
                            }
                        }
                    }

                    foreach (var type in intersection.List.OrderBy(e => e, s_ComponentTypeNameComparer))
                        result.Add(type);
                }
            }
        }

        static bool ShowAdditionalEntityToggle(ref Rect labelRect, bool isSelected, EntityManager entityManager, Entity entity)
        {
            const int kIconWidth = 20;
            var name = entityManager.GetName(entity);
            var labelWidth = Styles.AdditionalEntityTag.CalcSize((new GUIContent(name))).x;
            var totalWidth = kIconWidth + labelWidth;
            var maxWidth = EditorGUIUtility.currentViewWidth - 8.0f;
            if (labelRect.x + totalWidth > maxWidth)
            {
                labelRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 3f);
                labelRect.height = EditorGUIUtility.singleLineHeight;
                labelRect.x = 4.0f;
            }

            // Draw toggle here to cover icon and label area, so that icon is also clickable.
            labelRect.width = totalWidth;
            isSelected = GUI.Toggle(labelRect, isSelected, "", Styles.AdditionalEntityToggle);

            // Draw icon
            labelRect.width = kIconWidth;
            GUI.Label(labelRect, EditorIcons.Entity, Styles.AdditionalEntityIconTag);
            labelRect.x += labelRect.width;
            labelRect.width = labelWidth;

            // Draw label
            GUI.Label(labelRect, name, isSelected ? Styles.SelectedAdditionalEntityTag : Styles.AdditionalEntityTag);

            labelRect.x += labelRect.width + 6f;
            return isSelected;
        }

        static bool ShowComponentTypeToggle(ref Rect labelRect, bool state, ComponentType type)
        {
            var maxWidth = EditorGUIUtility.currentViewWidth - 8.0f;
            var width = Styles.EntityConversionComponentTag.CalcSize((new GUIContent(type.ToString()))).x;

            if (labelRect.x + width > maxWidth)
            {
                labelRect = GUILayoutUtility.GetRect(0, EditorGUIUtility.singleLineHeight + 3f);
                labelRect.height = EditorGUIUtility.singleLineHeight;
                labelRect.x = 4f;
            }

            labelRect.width = width;
            state = GUI.Toggle(labelRect, state, type.ToString(), Styles.EntityConversionComponentTag);
            labelRect.x += labelRect.width + 4f;
            return state;
        }

        struct WideModeScope : IDisposable
        {
            readonly bool m_Previous;

            public WideModeScope(int viewWidth)
            {
                m_Previous = EditorGUIUtility.wideMode;
                EditorGUIUtility.wideMode = EditorGUIUtility.currentViewWidth > viewWidth;
            }

            public void Dispose()
            {
                EditorGUIUtility.wideMode = m_Previous;
            }
        }

        class GameObjectBakingChangeTracker
        {
            World m_LastWorld;
            EntityBakingData m_LastBakingData;
            ArchetypeChunk m_LastChunk;
            uint m_LastGlobalSystemVersion;

            public bool DidChange(World world, IEnumerable<GameObject> targets, IEnumerable<TypeIndex> selectedComponentTypes)
            {
                if (null == world)
                    return false;

                var result = DidChangeInternal(world, targets, selectedComponentTypes);
                m_LastGlobalSystemVersion = world.EntityManager.GlobalSystemVersion;
                return result;
            }

            unsafe bool DidChangeInternal(World world, IEnumerable<GameObject> targets, IEnumerable<TypeIndex> selectedComponentTypes)
            {
                if (world != m_LastWorld)
                {
                    m_LastWorld = world;
                    return true;
                }

                if (m_LastBakingData == EntityBakingData.Null)
                {
                    // @TODO Handle multiple targets correctly.
                    m_LastBakingData = EntityBakingEditorUtility.GetBakingData(targets.FirstOrDefault(), world);

                    if (m_LastBakingData == EntityBakingData.Null)
                        return false;

                    m_LastChunk = world.EntityManager.GetChunk(m_LastBakingData.PrimaryEntity);
                    return true;

                }

                var chunk = world.EntityManager.GetChunk(m_LastBakingData.PrimaryEntity);

                if (ChunkIndex.Null == chunk.m_Chunk || chunk != m_LastChunk)
                {
                    m_LastChunk = chunk;
                    return true;
                }

                var ecs = world.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore;
                var archetype = ecs->GetArchetype(m_LastChunk.m_Chunk);
                foreach (var typeIndex in selectedComponentTypes)
                {
                    var typeIndexInArchetype = ChunkDataUtility.GetIndexInTypeArray(archetype, typeIndex);
                    if (typeIndexInArchetype == -1) continue;
                    var typeChangeVersion = archetype->Chunks.GetChangeVersion(typeIndexInArchetype, m_LastChunk.m_Chunk.ListIndex);

                    if (ChangeVersionUtility.DidChange(typeChangeVersion, m_LastGlobalSystemVersion))
                    {
                        m_LastGlobalSystemVersion = world.EntityManager.GlobalSystemVersion;

                        return true;
                    }
                }

                return false;
            }
        }

        class ComponentTypeNameComparer : IComparer<ComponentType>
        {
            readonly Dictionary<TypeIndex, string> m_ComponentNameByTypeIndex = new Dictionary<TypeIndex, string>();

            public int Compare(ComponentType x, ComponentType y)
            {
                var xName = GetTypeName(x);
                var yName = GetTypeName(y);

                return string.Compare(xName, yName, StringComparison.OrdinalIgnoreCase);
            }

            string GetTypeName(ComponentType componentType)
            {
                if (!m_ComponentNameByTypeIndex.TryGetValue(componentType.TypeIndex, out var typeName))
                {
                    typeName = componentType.GetManagedType().Name;
                    m_ComponentNameByTypeIndex.Add(componentType.TypeIndex, typeName);
                }

                return typeName;
            }
        }
    }
}
