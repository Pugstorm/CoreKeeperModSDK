using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Properties;
using Unity.Scenes;
using Unity.Scenes.Editor;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    /// <summary>
    /// The high level configuration parameters for the hierarchy.
    /// </summary>
    [GeneratePropertyBag]
    class HierarchyConfiguration
    {
        /// <summary>
        /// Gets or sets the hierarchy update mode.
        /// </summary>
        [CreateProperty] public Hierarchy.UpdateModeType UpdateMode = Hierarchy.UpdateModeType.Asynchronous;

        /// <summary>
        /// The minimum number of milliseconds between update ticks. This is used to throttle the refresh rate to a reasonable value.
        /// </summary>
        [CreateProperty] public int MinimumMillisecondsBetweenHierarchyUpdateCycles = 16;

        /// <summary>
        /// The maximum number of milliseconds to spend on an update tick. This is used to drive time-slicing.
        /// </summary>
        /// <remarks>
        /// This is only used if <see cref="UpdateMode"/> is set to 'Asynchronous'.
        /// </remarks>
        [CreateProperty] public int MaximumMillisecondsPerEditorUpdate = 16;

        /// <summary>
        /// The internal batch size to use when integrating entity changes. A higher value means less overhead but larger chunks of work done per tick which can exceed the <see cref="MaximumMillisecondsPerEditorUpdate"/>.
        /// </summary>
        /// <remarks>
        /// This is only used if <see cref="UpdateMode"/> is set to 'Asynchronous'.
        /// </remarks>
        [CreateProperty] public int EntityChangeIntegrationBatchSize = 100000;

        /// <summary>
        /// The internal batch size to use when integrating gameobject changes. A higher value means less overhead but larger chunks of work done per tick which can exceed the <see cref="MaximumMillisecondsPerEditorUpdate"/>.
        /// </summary>
        /// <remarks>
        /// This is only used if <see cref="UpdateMode"/> is set to 'Asynchronous'.
        /// </remarks>
        [CreateProperty] public int GameObjectChangeIntegrationBatchSize = 100;

        /// <summary>
        /// The internal batch size to use when baking out the immutable hierarchy. A higher value means less overhead but larger chunks of work done per tick which can exceed the <see cref="MaximumMillisecondsPerEditorUpdate"/>.
        /// </summary>
        /// <remarks>
        /// This is only used if <see cref="UpdateMode"/> is set to 'Asynchronous'.
        /// </remarks>
        [CreateProperty] public int ExportImmutableBatchSize = 100000;

        /// <summary>
        /// A flag to disable considering unnamed nodes when searching. This will accelerate performance.
        /// </summary>
        [CreateProperty] public bool ExcludeUnnamedNodesForSearch = false;

        [CreateProperty] public bool AdvancedSearch = true;
    }

    /// <summary>
    /// The internal serializable state used by the hierarchy. This object should be set by a user when re-constructing the hierarchy.
    /// </summary>
    [GeneratePropertyBag]
    class HierarchyState
    {
        [CreateProperty] public Dictionary<string, HierarchyNodesState> Nodes = new Dictionary<string, HierarchyNodesState>();

        public HierarchyNodesState GetHierarchyNodesSerializableState(string world)
        {
            if (Nodes.TryGetValue(world, out var state))
                return state;

            state = new HierarchyNodesState();
            Nodes[world] = state;
            return state;
        }
    }

    /// <summary>
    /// A set of statistics exported by the hierarchy. This can be used to adapt the update and time-slicing behaviours dynamically.
    /// </summary>
    class HierarchyStats
    {
        const int k_RollingAveragePeriod = 20;

        float m_AverageUpdateCount;
        float m_AverageUpdateCountVariance;

        float m_AverageUpdateSkipCount;
        float m_AverageUpdateSkipCountVariance;

        float m_AverageUpdateTime;
        float m_AverageUpdateTimeVariance;

        /// <summary>
        /// The current running counter for update ticks.
        /// </summary>
        [CreateProperty, UsedImplicitly] public int UpdateCount { get; private set; }

        /// <summary>
        /// The current running counter for skipped update ticks.
        /// </summary>
        [CreateProperty, UsedImplicitly] public int UpdateSkipCount { get; private set; }

        /// <summary>
        /// The current running timer for the update.
        /// </summary>
        [CreateProperty, UsedImplicitly] public long UpdateTime { get; private set; }

        /// <summary>
        /// The number of ticks the last hierarchy update cycle took.
        /// </summary>
        [CreateProperty, UsedImplicitly] public int LastUpdateCount { get; private set; } = -1;

        /// <summary>
        /// The number of ticks the last update cycle skipped.
        /// </summary>
        [CreateProperty, UsedImplicitly] public int LastUpdateSkipCount { get; private set; } = -1;

        /// <summary>
        /// The current running timer for the update.
        /// </summary>
        [CreateProperty, UsedImplicitly] public long LastUpdateTime { get; private set; } = -1;

        /// <summary>
        /// The average number of ticks to complete a hierarchy update cycle. This is an exponential moving average using a period of 20.
        /// </summary>
        [CreateProperty, UsedImplicitly] public float AverageUpdateCount => m_AverageUpdateCount;

        /// <summary>
        /// The average number of ticks to skipped per hierarchy update cycle. This is an exponential moving average using a period of 20.
        /// </summary>
        [CreateProperty, UsedImplicitly] public float AverageUpdateSkipCount => m_AverageUpdateSkipCount;

        /// <summary>
        /// The average number of milliseconds to complete a hierarchy update cycle. This is an exponential moving average using a period of 20.
        /// </summary>
        [CreateProperty, UsedImplicitly] public float AverageUpdateTime => m_AverageUpdateTime;

        internal void IncrementUpdateCounter()
        {
            UpdateCount++;
        }

        internal void IncrementUpdateSkipCounter()
        {
            UpdateSkipCount++;
        }

        internal void IncrementUpdateTime(long elapsedMilliseconds)
        {
            UpdateTime += elapsedMilliseconds;
        }

        internal void FinishUpdateCounter()
        {
            LastUpdateCount = UpdateCount;
            LastUpdateTime = UpdateTime;

            ExponentialMovingAverage.Add(ref m_AverageUpdateCount, UpdateCount, ref m_AverageUpdateCountVariance, k_RollingAveragePeriod);
            ExponentialMovingAverage.Add(ref m_AverageUpdateTime, UpdateTime, ref m_AverageUpdateTimeVariance, k_RollingAveragePeriod);

            UpdateCount = 0;
            UpdateTime = 0;
        }

        internal void FinishSkippedUpdateCounter()
        {
            LastUpdateSkipCount = UpdateSkipCount;

            ExponentialMovingAverage.Add(ref m_AverageUpdateSkipCount, UpdateSkipCount, ref m_AverageUpdateSkipCountVariance, k_RollingAveragePeriod);

            UpdateSkipCount = 0;
        }
    }

    /// <summary>
    /// The <see cref="Hierarchy"/> data model stores a parent-child based tree which can be efficiently updated and queried.
    /// </summary>
    class Hierarchy : IDisposable
    {
        public enum OperationModeType
        {
            /// <summary>
            /// Standard operation mode for the hierarchy.
            /// </summary>
            Normal,

            /// <summary>
            /// Places the hierarchy in to a self debugging mode.
            /// </summary>
            Debug
        }

        /// <summary>
        /// The internal hierarchy update mode.
        /// </summary>
        public enum UpdateModeType
        {
            /// <summary>
            /// The hierarchy will perform all work each update.
            /// </summary>
            Synchronous,

            /// <summary>
            /// The hierarchy will time-slice it's update over several frames. <seealso cref="AsynchronousOptions"/> to adjust how the time-slicing behaves.
            /// </summary>
            Asynchronous
        }

        /// <summary>
        /// The allocator used to construct this instance.
        /// </summary>
        readonly Allocator m_Allocator;

        /// <summary>
        /// The world this hierarchy is tracking.
        /// </summary>
        World m_World;

        /// <summary>
        /// The <see cref="HierarchyNodeStore"/> is used to store and mutate nodes in the hierarchy. It is optimized for incremental updates.
        /// </summary>
        readonly HierarchyNodeStore m_HierarchyNodeStore;

        /// <summary>
        /// The <see cref="HierarchyNodeStore.Immutable"/> is used to store a baked out linear set of nodes. It is optimized for enumeration and data access.
        /// </summary>
        readonly HierarchyNodeImmutableStore m_HierarchyNodeImmutableStore;

        /// <summary>
        /// The <see cref="HierarchyNodes"/> provides an <see cref="IList"/> interface over expanded nodes in the hierarchy. This is directly used by the UI.
        /// </summary>
        readonly HierarchyNodes m_HierarchyNodes;

        /// <summary>
        /// The updater is used to process to hierarchy update over several ticks.
        /// </summary>
        readonly HierarchyUpdater m_Updater;

        /// <summary>
        /// The <see cref="HierarchyNameStore"/> is used as storage and abstraction of names.
        /// </summary>
        readonly HierarchyNameStore m_HierarchyNameStore;

        /// <summary>
        /// The filtering is used to parse and apply search filtering.
        /// </summary>
        readonly HierarchySearch m_Search;

        /// <summary>
        /// Timer used to limit the amount of full updates done within a certain time period.
        /// </summary>
        readonly Stopwatch m_UpdateThrottleTimer;

        /// <summary>
        /// Timer used to limit the amount of processing done within a single tick.
        /// </summary>
        readonly Stopwatch m_UpdateTickTimer;

        /// <summary>
        /// The hierarchy configuration. This is data which is managed externally by settings, tests or users but drives internal behaviours.
        /// </summary>
        HierarchyConfiguration m_Configuration = new HierarchyConfiguration();

        /// <summary>
        /// The serialized state of the hierarchy. This is data which is managed internally but stored externally.
        /// </summary>
        HierarchyState m_State = new HierarchyState();

        /// <summary>
        /// Tracker used to gather stats about the hierarchy update.
        /// </summary>
        HierarchyStats m_Stats = new HierarchyStats();

        /// <summary>
        /// The active hierarchy filter.
        /// </summary>
        HierarchyFilter m_Filter;

        internal Allocator Allocator => m_Allocator;
        internal HierarchySearch HierarchySearch => m_Search;

        internal readonly SubSceneMap SubSceneMap;

        /// <summary>
        /// The hierarchy configuration. This is data which is managed externally by settings, tests or users but drives internal behaviours.
        /// </summary>
        public HierarchyConfiguration Configuration
        {
            get => m_Configuration;
            set => m_Configuration = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets or sets the hierarchy update mode.
        /// </summary>
        public HierarchyState State
        {
            get => m_State;
            set
            {
                m_State = value ?? throw new ArgumentNullException(nameof(value));
                m_HierarchyNodes.SetSerializableState(null != m_World ? value.GetHierarchyNodesSerializableState(m_World.Name) : new HierarchyNodesState());
            }
        }

        /// <summary>
        /// Gets or sets the operation type. This can be used for self debugging.
        /// </summary>
        public OperationModeType OperationMode { get; set; }

        /// <summary>
        /// Returns statistic information about the hierarchy.
        /// </summary>
        public HierarchyStats Stats => m_Stats;

        /// <summary>
        /// Returns the currently active world for the hierarchy.
        /// </summary>
        public World World => m_World;

        /// <summary>
        /// Returns true if the hierarchy has filtering applied.
        /// </summary>
        public bool HasSearchFilter() => null != m_Filter;

        /// <summary>
        /// Gets the current update generation.
        /// </summary>
        public uint UpdateVersion => m_Updater.Version;

        public Hierarchy(Allocator allocator, DataMode currentMode) : this(null, allocator, currentMode)
        {
        }

        public Hierarchy(World world, Allocator allocator, DataMode currentMode)
        {
            m_Allocator = allocator;

            // Internal data storage.
            SubSceneMap = new SubSceneMap();
            m_HierarchyNodeStore = new HierarchyNodeStore(allocator);
            m_HierarchyNodeImmutableStore = new HierarchyNodeImmutableStore(allocator);
            m_HierarchyNameStore = new HierarchyNameStore(allocator);
            m_HierarchyNodes = new HierarchyNodes(allocator);

            // Internal features.
            m_Updater = new HierarchyUpdater(m_HierarchyNodeStore, m_HierarchyNodeImmutableStore, m_HierarchyNameStore, m_HierarchyNodes, SubSceneMap, allocator);
            m_Search = new HierarchySearch(m_HierarchyNameStore, allocator);

            m_UpdateThrottleTimer = new Stopwatch();
            m_UpdateTickTimer = new Stopwatch();

            SetWorld(world);
            SetDataMode(currentMode);
        }

        public void Dispose()
        {
            m_HierarchyNodeStore.Dispose();
            m_HierarchyNodeImmutableStore.Dispose();
            m_HierarchyNameStore.Dispose();
            m_HierarchyNodes.Dispose();
            m_Updater.Dispose();
            m_Search.Dispose();
            m_Filter?.Dispose();
            SubSceneMap.Dispose();
        }

        /// <summary>
        /// Applies the given search query to the hierarchy.
        /// </summary>
        /// <param name="searchString">The raw search string.</param>
        /// <param name="tokens">Optional; pre processed set of tokens by a search backend.</param>
        public void SetSearchQuery(string searchString, ICollection<string> tokens)
        {
            var filter = !string.IsNullOrEmpty(searchString)
                ? m_Search.CreateHierarchyFilter(searchString, tokens, m_Allocator)
                : null;
            SetFilter(filter);
        }

        internal void SetFilter(HierarchyFilter filter)
        {
            m_Filter?.Dispose();
            m_Filter = filter;
            m_HierarchyNodes.SetFilter(m_Filter);
        }

        public DataMode DataMode { get; private set; }
        public event Action DataModeChanged = delegate {  };

        public void SetDataMode(DataMode mode)
        {
            DataMode = mode;
            DataModeChanged();
            m_HierarchyNodes.SetDataMode(mode);
        }

        /// <summary>
        /// Gets the virtualized <see cref="HierarchyNodes"/> set.
        /// </summary>
        /// <returns>The virtualized set of nodes.</returns>
        public HierarchyNodes GetNodes()
            => m_HierarchyNodes;

        /// <summary>
        /// Gets the estimated updater progress.
        /// </summary>
        /// <remarks>
        /// This value is only an estimate. It should NOT be used for any meaningful logic.
        /// </remarks>
        /// <returns>The estimated update progress.</returns>
        public float GetEstimatedProgress()
            => m_Updater.EstimatedProgress;

        /// <summary>
        /// Sets the world this hierarchy should be showing.
        /// </summary>
        /// <param name="world"></param>
        public void SetWorld(World world)
        {
            // Clear the internal state.
            m_HierarchyNodes.Clear();

            if (m_World == world)
                return;

            m_World = world;
            m_Search.SetWorld(world);
            m_Updater.SetWorld(world);
            m_HierarchyNameStore.SetWorld(world);

            // Clear the internal state.
            SubSceneMap.Clear();
            m_HierarchyNodeStore.Clear();
            m_HierarchyNodeImmutableStore.Clear();

            m_Updater.ClearGameObjectTrackers();

            // Try to load the state of the new world.
            m_HierarchyNodes.SetSerializableState(null != m_World ? m_State.GetHierarchyNodesSerializableState(m_World.Name) : new HierarchyNodesState());
        }

        /// <summary>
        /// Returns true if the given handle is expanded in the hierarchy.
        /// </summary>
        public bool IsExpanded(HierarchyNodeHandle handle)
        {
            return m_HierarchyNodes.IsExpanded(handle);
        }

        /// <summary>
        /// Updates the hierarchy performing entity change integration and re-baking.
        /// </summary>
        public void Update(bool isVisible)
        {
            m_Updater.IsHierarchyVisible = isVisible;

            // Set the entity change tracker strategy. Note that we do NOT need to flush or handle when this value changes.
            // Instead we can always update it and the change tracker will pick it up on the next execution.
            m_Updater.SetEntityChangeTrackerMode(OperationMode == OperationModeType.Normal
                ? HierarchyEntityChangeTracker.OperationModeType.SceneReferenceAndParentComponents
                : HierarchyEntityChangeTracker.OperationModeType.Linear);

            // Setup configuration parameters.
            m_Search.ExcludeUnnamedNodes = Configuration.ExcludeUnnamedNodesForSearch;

            if (m_Updater.Step == HierarchyUpdater.UpdateStep.End && m_UpdateThrottleTimer.ElapsedMilliseconds < m_Configuration.MinimumMillisecondsBetweenHierarchyUpdateCycles)
            {
                // The updater has completed a full frame update faster than our 'TargetMinimumMillisecondsBetweenUpdates'.
                // Pad out the rest of the frames until we hit our target.
                m_Stats.IncrementUpdateSkipCounter();
                return;
            }

            var complete = false;

            m_Stats.FinishSkippedUpdateCounter();
            m_Stats.IncrementUpdateCounter();

            m_UpdateThrottleTimer.Restart();

            switch (m_Configuration.UpdateMode)
            {
                case UpdateModeType.Synchronous:
                {
                    // If we were previously running in async mode and switch to sync. We need to flush the active update operation.
                    if (m_Updater.Step != HierarchyUpdater.UpdateStep.Start && m_Updater.Step != HierarchyUpdater.UpdateStep.End)
                        m_Updater.Flush();

                    m_Updater.EntityChangeIntegrationBatchSize = 0;
                    m_Updater.GameObjectChangeIntegrationBatchSize = 0;
                    m_Updater.ExportImmutableBatchSize = 0;

                    m_UpdateTickTimer.Restart();

                    // Run the full update cycle synchronously.
                    m_Updater.Execute();

                    m_Stats.IncrementUpdateTime(m_UpdateTickTimer.ElapsedMilliseconds);
                    m_Stats.FinishUpdateCounter();

                    complete = true;
                }
                break;

                case UpdateModeType.Asynchronous:
                {
                    m_Updater.EntityChangeIntegrationBatchSize = m_Configuration.EntityChangeIntegrationBatchSize;
                    m_Updater.GameObjectChangeIntegrationBatchSize = m_Configuration.GameObjectChangeIntegrationBatchSize;
                    m_Updater.ExportImmutableBatchSize = m_Configuration.ExportImmutableBatchSize;

                    // Reset the enumerator if needed.
                    if (m_Updater.Step == HierarchyUpdater.UpdateStep.End)
                        m_Updater.Reset();

                    m_UpdateTickTimer.Restart();

                    // Tick the updater until we have exceeded a certain threshold of real time OR we have finished the update.
                    while (m_UpdateTickTimer.ElapsedMilliseconds < m_Configuration.MaximumMillisecondsPerEditorUpdate)
                    {
                        if (m_Updater.MoveNext())
                            continue;

                        complete = true;
                        break;
                    }

                    m_Stats.IncrementUpdateTime(m_UpdateTickTimer.ElapsedMilliseconds);

                    if (complete)
                        m_Stats.FinishUpdateCounter();
                }
                break;

                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (complete)
            {
                // @FIXME This should really be called every tick instead of at the end of each update cycle.
                // Refresh the virtualized node list. The work is only done if the underlying data is actually changed.
                using var subSceneStateMap = SubSceneMap.GetSubSceneStateMap();
                m_HierarchyNodes.Refresh(m_HierarchyNodeImmutableStore.GetReadBuffer(), World, subSceneStateMap);
            }
        }

        /// <summary>
        /// Returns the display name for a given <see cref="HierarchyNodeHandle"/>.
        /// </summary>
        /// <param name="handle">The handle to get the name for.</param>
        /// <returns>The name for the given handle.</returns>
        public string GetName(in HierarchyNodeHandle handle)
        {
            var str = default(FixedString64Bytes);
            m_HierarchyNameStore.GetName(handle, ref str);
            return str.ToString();
        }

        /// <summary>
        /// Set the display name into <paramref name="name"/> for a given <see cref="HierarchyNodeHandle"/>/
        /// </summary>
        /// <param name="handle">The handle to get the name for.</param>
        /// <param name="name">The name for the given handle.</param>
        public void GetName(in HierarchyNodeHandle handle, ref FixedString64Bytes name) => m_HierarchyNameStore.GetName(handle, ref name);

        /// <summary>
        /// Returns <see langword="true"/> if the given <see cref="HierarchyNodeHandle"/> is should be displayed as disabled in the hierarchy.
        /// </summary>
        /// <param name="handle">The handle to fetch the disabled state for.</param>
        /// <returns><see langword="true"/> if the given node is disabled; <see langword="false"/> otherwise.</returns>
        public bool IsDisabled(in HierarchyNodeHandle handle)
        {
            if (m_HierarchyNodeStore.GetFlags(handle).HasFlag(HierarchyNodeFlags.Disabled))
                return true;

            if (null == m_World || !m_World.IsCreated)
                return false;

            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    var entity = handle.ToEntity();

                    if (!m_World.EntityManager.Exists(entity))
                        return false;

                    if (m_World.EntityManager.HasComponent<Disabled>(entity))
                        return true;

                    var sceneEntity = GetParentSceneEntity(entity);
                    return sceneEntity != Entity.Null && SubSceneMap.GetSubSceneStateImmediate(handle, World) == SubSceneLoadedState.Closed;
                }
                case NodeKind.GameObject:
                {
                    var go = GetUnityObject(handle) as UnityEngine.GameObject;
                    if (go)
                        return !go.activeInHierarchy;

                    return false;
                }
            }

            return false;
        }

        public enum HierarchyPrefabType
        {
            None,
            PrefabRoot,
            PrefabPart,
        }

        /// <summary>
        /// Returns <see langword="true"/> if the given <see cref="HierarchyNodeHandle"/> is should be displayed as a prefab in the hierarchy.
        /// </summary>
        /// <param name="handle">The handle to fetch the prefab state for.</param>
        /// <returns><see langword="true"/> if the given node is a prefab; <see langword="false"/> otherwise.</returns>
        public HierarchyPrefabType GetPrefabType(in HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    if (null == m_World || !m_World.IsCreated)
                        return HierarchyPrefabType.None;

                    var entity = handle.ToEntity();

                    if (!m_World.EntityManager.Exists(entity))
                        return HierarchyPrefabType.None;

                    var hasPrefabComponent = m_World.EntityManager.HasComponent<Prefab>(entity);
                    var hasLinkedEntityGroupComponent = m_World.EntityManager.HasComponent<LinkedEntityGroup>(entity);

                    if (hasPrefabComponent && hasLinkedEntityGroupComponent)
                        return HierarchyPrefabType.PrefabRoot;

                    if (m_World.EntityManager.HasComponent<EntityGuid>(entity))
                    {
                        var guid = m_World.EntityManager.GetComponentData<EntityGuid>(entity);
                        var gameObject = EditorUtility.InstanceIDToObject(guid.OriginatingId) as UnityEngine.GameObject;

                        if (gameObject)
                        {
                            if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
                                return HierarchyPrefabType.PrefabRoot;

                            if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                                return HierarchyPrefabType.PrefabPart;
                        }
                    }

                    if (hasPrefabComponent)
                        return HierarchyPrefabType.PrefabPart;

                    return HierarchyPrefabType.None;
                }
                case NodeKind.GameObject:
                {
                    var gameObject = (UnityEngine.GameObject)GetUnityObject(handle);
                    if (gameObject)
                    {
                        if (PrefabUtility.IsAnyPrefabInstanceRoot(gameObject))
                            return HierarchyPrefabType.PrefabRoot;

                        if (PrefabUtility.IsPartOfAnyPrefab(gameObject))
                            return HierarchyPrefabType.PrefabPart;
                    }

                    return HierarchyPrefabType.None;
                }
            }

            return HierarchyPrefabType.None;
        }

        /// <summary>
        /// Gets the unity engine instance id for the given handle; if any.
        /// </summary>
        /// <param name="handle">The handle to get the instance id for.</param>
        /// <returns>The instance id backing this handle.</returns>
        public int GetInstanceId(in HierarchyNodeHandle handle)
        {
            switch (handle.Kind)
            {
                case NodeKind.Entity:
                {
                    var entity = handle.ToEntity();

                    if (m_World is not {IsCreated: true})
                        return 0;

                    if (!m_World.EntityManager.Exists(entity))
                        return 0;

                    if (!m_World.EntityManager.HasComponent<EntityGuid>(entity))
                        return 0;

                    return m_World.EntityManager.GetComponentData<EntityGuid>(entity).OriginatingId;
                }
                case NodeKind.GameObject:
                {
                    return handle.Index;
                }
            }

            return 0;
        }

        /// <summary>
        /// Gets the backing object for a given <see cref="HierarchyNodeHandle"/>; if any.
        /// </summary>
        /// <param name="handle">The handle to get the object for.</param>
        /// <returns>The object backing this handle.</returns>
        public UnityEngine.Object GetUnityObject(in HierarchyNodeHandle handle)
        {
            return EditorUtility.InstanceIDToObject(GetInstanceId(handle));
        }

        Entity GetParentSceneEntity(Entity e)
        {
            var entityManager = World.EntityManager;
            if (!entityManager.HasComponent<SceneTag>(e))
                return Entity.Null;

            var sceneTag = entityManager.GetSharedComponent<SceneTag>(e);

            if (entityManager.HasComponent<SceneEntityReference>(sceneTag.SceneEntity))
                return entityManager.GetComponentData<SceneEntityReference>(sceneTag.SceneEntity).SceneEntity;
            else
                return Entity.Null;
        }
    }
}
