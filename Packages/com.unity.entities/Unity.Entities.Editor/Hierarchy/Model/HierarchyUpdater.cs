using System;
using System.Collections;
using System.IO;
using Unity.Collections;
using Unity.Editor.Bridge;
using UnityEditor.SceneManagement;

namespace Unity.Entities.Editor
{
    class HierarchyUpdater : IDisposable, IEnumerator
    {
        public enum UpdateStep
        {
            Start,
            GetEntityChanges,
            GetGameObjectChanges,
            GetPrefabStageChanges,
            IntegrateEntityChanges,
            IntegrateGameObjectChanges,
            IntegratePrefabStageChanges,
            ExportImmutable,
            End
        }

        readonly SubSceneMap m_SubSceneMap;
        readonly HierarchyNodeStore m_HierarchyNodeStore;
        readonly HierarchyNodeImmutableStore m_HierarchyNodeImmutableStore;
        readonly HierarchyNameStore m_HierarchyNameStore;
        readonly HierarchyNodes m_HierarchyNodes;

        readonly Allocator m_Allocator;

        World m_World;

        HierarchyEntityChangeTracker.OperationModeType m_HierarchyEntityChangeTrackerOperationMode;

        SubSceneChangeTracker m_SubSceneChangeTracker;
        HierarchyEntityChangeTracker m_HierarchyEntityChangeTracker;
        HierarchyGameObjectChangeTracker m_HierarchyGameObjectChangeTracker;
        HierarchyPrefabStageChangeTracker m_HierarchyPrefabStageChangeTracker;

        UpdateStep m_Step;

        uint m_Version;

        /// <summary>
        /// Re-usable data structure to store results from the <see cref="SubSceneMap"/>.
        /// </summary>
        readonly SubSceneChangeTracker.SubSceneMapChanges m_SubSceneChanges;

        /// <summary>
        /// Re-usable data structure to store results from the <see cref="HierarchyEntityChangeTracker"/>.
        /// </summary>
        readonly HierarchyEntityChanges m_HierarchyEntityChanges;

        /// <summary>
        /// Re-usable data structure to store results from the <see cref="HierarchyGameObjectChangeTracker"/>.
        /// </summary>
        readonly HierarchyGameObjectChanges m_HierarchyGameObjectChanges;

        /// <summary>
        /// Re-usable data structure to store results from the <see cref="HierarchyGameObjectChangeTracker"/>.
        /// </summary>
        readonly HierarchyPrefabStageChanges m_HierarchyPrefabStageChanges;

        /// <summary>
        /// A nested 'Enumerator' used to handle the implementation details of integrating entity changes over several ticks.
        /// </summary>
        HierarchyNodeStore.IntegrateEntityChangesEnumerator m_IntegrateEntityChangesEnumerator;

        /// <summary>
        /// A nested 'Enumerator' used to handle the implementation details of integrating gameobject changes over several ticks.
        /// </summary>
        HierarchyNodeStore.IntegrateGameObjectChangesEnumerator m_IntegrateGameObjectChangesEnumerator;

        /// <summary>
        /// A nested 'Enumerator' used to handle the implementation details of building the immutable data set over several ticks.
        /// </summary>
        HierarchyNodeStore.ExportImmutableEnumerator m_ExportImmutableEnumerator;

        /// <summary>
        /// A shared state object used to drive the <see cref="m_ExportImmutableEnumerator"/>.
        /// </summary>
        HierarchyNodeStore.ExportImmutableState m_ExportImmutableState;

        /// <summary>
        /// The current prefab stage (can be null).
        /// </summary>
        PrefabStage m_PrefabStage;

        public int EntityChangeIntegrationBatchSize { get; set; } = 1000;
        public int GameObjectChangeIntegrationBatchSize { get; set; } = 100;
        public int ExportImmutableBatchSize { get; set; } = 1000;

        public UpdateStep Step => m_Step;

        /// <summary>
        /// Returns the updater version. This is the total number of full update cycles that have been completed.
        /// </summary>
        public uint Version => m_Version;

        /// <summary>
        /// Returns the estimated progress for the updater.
        /// </summary>
        /// <remarks>
        /// This value is only an estimate. It should NOT be used for any meaningful logic.
        /// </remarks>
        public float EstimatedProgress
        {
            get
            {
                const float kEntityChangeIntegrationWeight = 1;
                const float kBuildImmutableWeight = 1;
                const float kEntityChangeProgress = kEntityChangeIntegrationWeight / (kEntityChangeIntegrationWeight + kBuildImmutableWeight);
                const float kBuildImmutableProgress = kBuildImmutableWeight / (kEntityChangeIntegrationWeight + kBuildImmutableWeight);

                switch (m_Step)
                {
                    case UpdateStep.Start:
                    case UpdateStep.GetEntityChanges:
                    case UpdateStep.GetGameObjectChanges:
                    case UpdateStep.GetPrefabStageChanges:
                    case UpdateStep.IntegrateGameObjectChanges:
                        return m_IntegrateGameObjectChangesEnumerator.Progress * kEntityChangeProgress;
                    case UpdateStep.IntegratePrefabStageChanges:
                        return 0f;
                    case UpdateStep.IntegrateEntityChanges:
                        return m_IntegrateEntityChangesEnumerator.Progress * kEntityChangeProgress;
                    case UpdateStep.ExportImmutable:
                        return m_ExportImmutableEnumerator.Progress * kBuildImmutableProgress + kEntityChangeProgress;
                    case UpdateStep.End:
                        return 1f;
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }

        public object Current => null;

        public bool IsHierarchyVisible { private get; set; }

        public HierarchyUpdater(HierarchyNodeStore hierarchyNodeStore, HierarchyNodeImmutableStore hierarchyNodeImmutableStore, HierarchyNameStore hierarchyNameStore, HierarchyNodes hierarchyNodes, SubSceneMap subSceneMap, Allocator allocator)
        {
            m_SubSceneMap = subSceneMap;
            m_HierarchyNodeStore = hierarchyNodeStore;
            m_HierarchyNodeImmutableStore = hierarchyNodeImmutableStore;
            m_HierarchyNameStore = hierarchyNameStore;
            m_HierarchyNodes = hierarchyNodes;
            m_Allocator = allocator;
            m_SubSceneChanges = new SubSceneChangeTracker.SubSceneMapChanges(128, allocator);
            m_HierarchyEntityChanges = new HierarchyEntityChanges(allocator);
            m_HierarchyGameObjectChanges = new HierarchyGameObjectChanges(allocator);
            m_HierarchyPrefabStageChanges = new HierarchyPrefabStageChanges(allocator);
            m_ExportImmutableState = new HierarchyNodeStore.ExportImmutableState(allocator);

            m_HierarchyGameObjectChangeTracker = new HierarchyGameObjectChangeTracker(m_Allocator);
            m_HierarchyPrefabStageChangeTracker = new HierarchyPrefabStageChangeTracker(m_Allocator);
            m_SubSceneChangeTracker = new SubSceneChangeTracker();

            SetState(UpdateStep.Start);
        }

        public void Dispose()
        {
            m_SubSceneChanges.Dispose();
            m_HierarchyEntityChanges.Dispose();
            m_HierarchyGameObjectChanges.Dispose();
            m_HierarchyPrefabStageChanges.Dispose();
            m_ExportImmutableState.Dispose();
            m_SubSceneChangeTracker?.Dispose();
            m_HierarchyEntityChangeTracker?.Dispose();
            m_HierarchyGameObjectChangeTracker?.Dispose();
            m_HierarchyPrefabStageChangeTracker?.Dispose();

            m_HierarchyEntityChangeTracker = null;
            m_HierarchyGameObjectChangeTracker = null;
            m_HierarchyPrefabStageChangeTracker = null;
        }

        public void SetWorld(World world)
        {
            if (world == m_World)
                return;

            m_World = world;

            m_SubSceneChangeTracker.SetWorld(m_World);

            m_HierarchyEntityChangeTracker?.Dispose();

            if (world == null)
                m_HierarchyEntityChangeTracker = null;
            else if (!TypeManager.IsInitialized)
            {
                Debug.LogError($"{nameof(TypeManager)} has not been initialized properly");
                m_HierarchyEntityChangeTracker = null;
            }
            else
                m_HierarchyEntityChangeTracker = new HierarchyEntityChangeTracker(m_World, m_Allocator) { OperationMode = m_HierarchyEntityChangeTrackerOperationMode };

            Reset();
        }

        public void ClearGameObjectTrackers()
        {
            m_HierarchyGameObjectChangeTracker.Clear();
            m_HierarchyPrefabStageChangeTracker.Clear();
        }

        public void SetEntityChangeTrackerMode(HierarchyEntityChangeTracker.OperationModeType mode)
        {
            m_HierarchyEntityChangeTrackerOperationMode = mode;

            if (null != m_HierarchyEntityChangeTracker)
                m_HierarchyEntityChangeTracker.OperationMode = mode;
        }

        /// <summary>
        /// This method is invoked when entering a new state and should be used to initialize any
        /// state specific variables. The state itself will not be run until the next frame.
        /// </summary>
        /// <param name="state">The state to switch to.</param>
        void SetState(UpdateStep state)
        {
            while (true)
            {
                m_Step = state;

                switch (state)
                {
                    case UpdateStep.Start:
                        break;

                    case UpdateStep.GetEntityChanges:
                        break;

                    case UpdateStep.GetGameObjectChanges:
                        break;

                    case UpdateStep.GetPrefabStageChanges:
                    {
                        if (!IsHierarchyVisible)
                        {
                            state = UpdateStep.IntegrateGameObjectChanges;
                            continue;
                        }

                        break;
                    }

                    case UpdateStep.IntegrateEntityChanges:
                    {
                        // We're checking if the given world still exist in unit tests scenarios world could be disposed while we are still invoking the updater.
                        if (m_HierarchyEntityChanges.HasChanges() && m_World is { IsCreated: true })
                        {
                            // Delegate the implementation to another enumerator.
                            m_IntegrateEntityChangesEnumerator = m_HierarchyNodeStore.CreateIntegrateEntityChangesEnumerator(m_World, m_HierarchyEntityChanges, EntityChangeIntegrationBatchSize, m_SubSceneMap.GetSceneTagToSubSceneHandleMap());
                        }
                        else
                        {
                            state = UpdateStep.IntegrateGameObjectChanges;
                            continue;
                        }

                        break;
                    }

                    case UpdateStep.IntegrateGameObjectChanges:
                    {
                        if (m_HierarchyGameObjectChanges.HasChanges())
                        {
                            // Delegate the implementation to another enumerator.
                            m_IntegrateGameObjectChangesEnumerator = m_HierarchyNodeStore.CreateIntegrateGameObjectChangesEnumerator(m_HierarchyGameObjectChanges, m_SubSceneMap, GameObjectChangeIntegrationBatchSize);
                        }
                        else if (!IsHierarchyVisible)
                        {
                            state = UpdateStep.End;
                            continue;
                        }
                        else
                        {
                            // No changes to integrate move on to linear packing.
                            state = UpdateStep.IntegratePrefabStageChanges;
                            continue;
                        }

                        break;
                    }

                    case UpdateStep.IntegratePrefabStageChanges:
                    {
                        if (m_HierarchyPrefabStageChanges.GameObjectChangeTrackerEvents.Length == 0)
                        {
                            state = UpdateStep.ExportImmutable;
                            continue;
                        }
                        break;
                    }

                    case UpdateStep.ExportImmutable:
                    {
                        if (IsHierarchyVisible && m_HierarchyNodeStore.GetRootChangeVersion() != m_HierarchyNodeImmutableStore.GetReadBuffer().ChangeVersion)
                        {
                            m_ExportImmutableEnumerator = m_HierarchyNodeStore.CreateBuildImmutableEnumerator(m_World, m_ExportImmutableState,m_HierarchyNodeImmutableStore.GetWriteBuffer(), m_HierarchyNodeImmutableStore.GetReadBuffer(), ExportImmutableBatchSize);
                        }
                        else
                        {
                            // The topology has not changed, skip packing and end.
                            state = UpdateStep.End;
                            continue;
                        }

                        break;
                    }

                    case UpdateStep.End:
                    {
                        unchecked
                        {
                            ++m_Version;
                        }
                        break;
                    }
                }

                break;
            }
        }

        public bool MoveNext()
        {
            switch (m_Step)
            {
                case UpdateStep.Start:
                {
                    SetState(UpdateStep.GetEntityChanges);
                    return true;
                }
                case UpdateStep.GetEntityChanges:
                {
                    if (IsHierarchyVisible && m_HierarchyEntityChangeTracker is not null)
                        m_HierarchyEntityChangeTracker.GetChanges(m_HierarchyEntityChanges);

                    m_SubSceneChangeTracker.GetChanges(m_SubSceneChanges);
                    if (m_SubSceneChanges.HasChanges())
                        m_SubSceneMap.IntegrateChanges(m_World, m_HierarchyNodeStore, m_HierarchyNameStore, m_SubSceneChanges);

                    SetState(UpdateStep.GetGameObjectChanges);
                    return true;
                }
                case UpdateStep.GetGameObjectChanges:
                {
                    m_HierarchyGameObjectChangeTracker.GetChanges(m_HierarchyGameObjectChanges, !IsHierarchyVisible);
                    SetState(UpdateStep.GetPrefabStageChanges);
                    return true;
                }
                case UpdateStep.GetPrefabStageChanges:
                {
                    m_HierarchyPrefabStageChangeTracker.GetChanges(m_HierarchyPrefabStageChanges);
                    SetState(UpdateStep.IntegrateEntityChanges);
                    return true;
                }
                case UpdateStep.IntegrateEntityChanges:
                {
                    if (m_IntegrateEntityChangesEnumerator.MoveNext())
                        return true;

                    m_IntegrateEntityChangesEnumerator.Dispose();
                    m_IntegrateEntityChangesEnumerator = default;
                    SetState(UpdateStep.IntegrateGameObjectChanges);
                    return true;
                }

                case UpdateStep.IntegrateGameObjectChanges:
                {
                    if (m_IntegrateGameObjectChangesEnumerator.MoveNext())
                        return true;

                    m_IntegrateGameObjectChangesEnumerator = default;

                    m_HierarchyNameStore?.IntegrateGameObjectChanges(m_HierarchyGameObjectChanges);
                    SetState(UpdateStep.IntegratePrefabStageChanges);
                    return true;
                }

                case UpdateStep.IntegratePrefabStageChanges:
                {
                    m_HierarchyNodeStore.IntegratePrefabStageChanges(m_HierarchyPrefabStageChanges, m_SubSceneMap);

                    // @FIXME This is a bit hackish. We don't really want to be manipulating the _future state_ of the hierarchy.
                    // i.e. `m_HierarchyNodes` represents a view over the packed set which has not even been been built yet.
                    // In practice the expanded state is simply a hashset and there is no risk in adding new elements. We should still find a better solution.
                    foreach (var change in m_HierarchyPrefabStageChanges.GameObjectChangeTrackerEvents)
                        if (change.EventType == GameObjectChangeTrackerEventType.CreatedOrChanged)
                            m_HierarchyNodes.SetExpanded(HierarchyNodeHandle.FromGameObject(change.InstanceId), true);

                    m_HierarchyNameStore?.IntegratePrefabStageChanges(m_HierarchyPrefabStageChanges);
                    SetState(UpdateStep.ExportImmutable);
                    return true;
                }

                case UpdateStep.ExportImmutable:
                {
                    if (m_ExportImmutableEnumerator.MoveNext())
                        return true;

                    m_HierarchyNodeImmutableStore.SwapBuffers();
                    m_ExportImmutableEnumerator = default;
                    SetState(UpdateStep.End);
                    return true;
                }
            }

            return false;
        }

        public void Execute()
        {
            if (m_Step != UpdateStep.Start && m_Step != UpdateStep.End)
                throw new InvalidDataException("Failed to execute the enumerator. The state must be reset before running.");

            Reset();
            Flush();
        }

        public void Reset()
        {
            SetState(UpdateStep.Start);
        }

        public void Flush()
        {
            while (MoveNext())
            {
            }
        }
    }
}
