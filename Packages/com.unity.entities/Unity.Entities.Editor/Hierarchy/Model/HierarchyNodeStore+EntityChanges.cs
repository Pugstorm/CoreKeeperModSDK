using System;
using System.Collections;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor;

namespace Unity.Entities.Editor
{
    partial struct HierarchyNodeStore
    {
        /// <summary>
        /// Represents a batch of changes to be integrated in to the dynamic hierarchy model.
        /// </summary>
        unsafe struct HierarchyEntityChangesBatch
        {
            /// <summary>
            /// !!IMPORTANT!! This is the order we will process elements for the batch.
            /// </summary>
            enum ChangeType
            {
                RemovedSceneTagsWithoutParents,
                RemovedParents,
                DestroyedEntities,
                CreatedEntities,
                AddedSceneTagWithoutParents,
                AddedParents,

                // Used to compute array lengths.
                Count
            }

            readonly struct Range
            {
                public readonly int Start;
                public readonly int Count;

                public Range(int start, int count)
                {
                    Start = start;
                    Count = count;
                }
            }

            readonly HierarchyEntityChanges m_Changes;
            readonly int m_BatchStart;
            readonly int m_BatchCount;

#pragma warning disable 649
            fixed int m_Offset[(int) ChangeType.Count];
            fixed int m_Length[(int) ChangeType.Count];
#pragma warning restore 649

            public NativeArray<Entity> CreatedEntities => SubArray(m_Changes.CreatedEntities, GetRange(ChangeType.CreatedEntities));
            public NativeArray<Entity> DestroyedEntities => SubArray(m_Changes.DestroyedEntities, GetRange(ChangeType.DestroyedEntities));

            public NativeArray<Entity> AddedParentEntities=> SubArray(m_Changes.AddedParentEntities, GetRange(ChangeType.AddedParents));
            public NativeArray<Entity> RemovedParentEntities=> SubArray(m_Changes.RemovedParentEntities, GetRange(ChangeType.RemovedParents));
            public NativeArray<Parent> AddedParentComponents=> SubArray(m_Changes.AddedParentComponents, GetRange(ChangeType.AddedParents));
            public NativeArray<SceneTag> AddedParentSceneTagForNullParentComponents => SubArray(m_Changes.AddedParentSceneTagForNullParentComponents, GetRange(ChangeType.AddedParents));

            public NativeArray<Entity> AddedSceneTagWithoutParentEntities=> SubArray(m_Changes.AddedSceneTagWithoutParentEntities, GetRange(ChangeType.AddedSceneTagWithoutParents));
            public NativeArray<Entity> RemovedSceneTagWithoutParentEntities=> SubArray(m_Changes.RemovedSceneTagWithoutParentEntities, GetRange(ChangeType.RemovedSceneTagsWithoutParents));
            public NativeArray<SceneTag> AddedSceneTagWithoutParentComponents => SubArray(m_Changes.AddedSceneTagWithoutParentComponents, GetRange(ChangeType.AddedSceneTagWithoutParents));

            public HierarchyEntityChangesBatch(HierarchyEntityChanges changes, int batchStart, int batchCount)
            {
                m_Changes = changes;
                m_BatchStart = batchStart;
                m_BatchCount = batchCount;

                m_Length[(int) ChangeType.RemovedSceneTagsWithoutParents] = m_Changes.RemovedSceneTagWithoutParentEntities.Length;
                m_Length[(int) ChangeType.RemovedParents] = m_Changes.RemovedParentEntities.Length;
                m_Length[(int) ChangeType.DestroyedEntities] = m_Changes.DestroyedEntities.Length;
                m_Length[(int) ChangeType.CreatedEntities] = m_Changes.CreatedEntities.Length;
                m_Length[(int) ChangeType.AddedSceneTagWithoutParents] = m_Changes.AddedSceneTagWithoutParentEntities.Length;
                m_Length[(int) ChangeType.AddedParents] = m_Changes.AddedParentEntities.Length;

                var offset = 0;
                for (var i = 0; i < (int) ChangeType.Count; i++)
                {
                    m_Offset[i] = offset;
                    offset += m_Length[i];
                }
            }

            Range GetRange(ChangeType type)
            {
                var offset = m_Offset[(int) type];
                var length = m_Length[(int) type];
                var start = math.clamp(m_BatchStart - offset, 0, length);
                var count = math.clamp(m_BatchStart - offset + m_BatchCount - start, 0, length - start);
                return new Range(start, count);
            }

            static NativeArray<T> SubArray<T>(NativeList<T> list, Range range) where T : unmanaged
            {
                if (range.Count == 0) return default;
                return list.AsArray().GetSubArray(range.Start, range.Count);
            }
        }

        public struct IntegrateEntityChangesEnumerator : IEnumerator, IDisposable
        {
            enum Step
            {
                IntegrateChanges,
                UpdateVersion,
                Complete
            }

            readonly HierarchyNodeStore m_Hierarchy;
            readonly World m_World;
            readonly HierarchyEntityChanges m_Changes;
            readonly NativeParallelHashMap<SceneTag, HierarchyNodeHandle> m_SceneTagToSubSceneNodeHandle;

            Step m_Step;

            int m_TotalCount;
            int m_BatchStart;
            int m_BatchCount;

            public object Current => null;

            public void Reset() => throw new InvalidOperationException($"{nameof(IntegrateEntityChangesEnumerator)} can not be reset. Instead a new instance should be used with a new change set.");

            public float Progress => m_TotalCount > 0 ? m_BatchStart / (float) m_TotalCount : 0;

            public IntegrateEntityChangesEnumerator(HierarchyNodeStore hierarchy, World world, HierarchyEntityChanges changes, int batchSize, NativeParallelHashMap<SceneTag, HierarchyNodeHandle> sceneTagToSubSceneNodeHandle)
            {
                m_Hierarchy = hierarchy;
                m_World = world;
                m_Changes = changes;
                m_TotalCount = changes.GetChangeCount();
                m_BatchStart = 0;
                m_BatchCount = batchSize > 0 ? batchSize : m_TotalCount;
                m_SceneTagToSubSceneNodeHandle = sceneTagToSubSceneNodeHandle;
                m_Step = Step.IntegrateChanges;

#if !ENTITY_STORE_V1
                m_Hierarchy.m_Nodes.ResizeEntityCapacity(world.EntityManager.HighestEntityIndex() + 1);
#else
                m_Hierarchy.m_Nodes.ResizeEntityCapacity(world.EntityManager.EntityCapacity);
#endif
            }

            public void Dispose()
            {
                m_SceneTagToSubSceneNodeHandle.Dispose();
            }

            public bool MoveNext()
            {
                switch (m_Step)
                {
                    case Step.IntegrateChanges:
                    {
                        new IntegrateEntityChangesJob
                        {
                            SceneTagToSubSceneNodeHandle = m_SceneTagToSubSceneNodeHandle,
                            Batch = new HierarchyEntityChangesBatch(m_Changes, m_BatchStart, m_BatchCount),
                            Hierarchy = m_Hierarchy
                        }.Run();

                        m_BatchStart += m_BatchCount;

                        if (m_BatchStart >= m_TotalCount)
                            m_Step = Step.UpdateVersion;

                        return true;
                    }

                    case Step.UpdateVersion:
                    {
                        m_Hierarchy.UpdateChangeVersion(HierarchyNodeHandle.Root);
                        m_Step = Step.Complete;
                        return false;
                    }
                    case Step.Complete:
                    {
                        return false;
                    }
                    default:
                        throw new ArgumentOutOfRangeException();
                }
            }
        }


        // For perf tests only
        public void IntegrateEntityChanges(World world, HierarchyEntityChanges changes, NativeParallelHashMap<SceneTag, HierarchyNodeHandle> sceneTagToSubSceneNodeHandle)
        {
            var enumerator = CreateIntegrateEntityChangesEnumerator(world, changes, changes.GetChangeCount(), sceneTagToSubSceneNodeHandle);
            while (enumerator.MoveNext()) { }
        }

        /// <summary>
        /// Creates an enumerator which will integrate the given entity changes over several ticks.
        /// </summary>
        /// <param name="world">The world being operated on.</param>
        /// <param name="changes">The entity changes to apply.</param>
        /// <param name="batchSize">The number of changes to integrate per tick.</param>
        /// <returns>An enumerator which can be ticked.</returns>
        public IntegrateEntityChangesEnumerator CreateIntegrateEntityChangesEnumerator(World world, HierarchyEntityChanges changes, int batchSize, NativeParallelHashMap<SceneTag, HierarchyNodeHandle> sceneTagToSubSceneNodeHandle)
        {
            return new IntegrateEntityChangesEnumerator(this, world, changes, batchSize, sceneTagToSubSceneNodeHandle);
        }

        [BurstCompile]
        struct IntegrateEntityChangesJob : IJob
        {
            public HierarchyEntityChangesBatch Batch;
            public HierarchyNodeStore Hierarchy;
            public NativeParallelHashMap<SceneTag, HierarchyNodeHandle> SceneTagToSubSceneNodeHandle;

            public void Execute()
            {
                var removedSceneTagWithoutParentEntities = Batch.RemovedSceneTagWithoutParentEntities;
                var removedParentEntities = Batch.RemovedParentEntities;
                var destroyedEntities = Batch.DestroyedEntities;
                var createdEntities = Batch.CreatedEntities;
                var addedSceneTagWithoutParentEntities = Batch.AddedSceneTagWithoutParentEntities;
                var addedSceneTagWithoutParentComponents = Batch.AddedSceneTagWithoutParentComponents;
                var addedParentEntities = Batch.AddedParentEntities;
                var addedParentComponents = Batch.AddedParentComponents;
                var addedParentSceneTagForNullParentComponents = Batch.AddedParentSceneTagForNullParentComponents;

                // When a scene is removed we recursively remove all children nodes.
                // This means some nodes for entities might not exist anymore, that's why
                // we now check for Hierarchy.Exists when parenting / removing nodes.

                for (var i = 0; i < removedSceneTagWithoutParentEntities.Length; i++)
                {
                    var entity = HierarchyNodeHandle.FromEntity(removedSceneTagWithoutParentEntities[i]);
                    if (Hierarchy.Exists(entity))
                        Hierarchy.SetParent(entity, HierarchyNodeHandle.Root);
                }

                for (var i = 0; i < removedParentEntities.Length; i++)
                {
                    var entity = HierarchyNodeHandle.FromEntity(removedParentEntities[i]);
                    if (Hierarchy.Exists(entity))
                        Hierarchy.SetParent(entity, HierarchyNodeHandle.Root);
                }

                for (var i = 0; i < destroyedEntities.Length; i++)
                {
                    var entity = HierarchyNodeHandle.FromEntity(destroyedEntities[i]);
                    if (Hierarchy.Exists(entity))
                        Hierarchy.RemoveNode(entity);
                }

                for (var i = 0; i < createdEntities.Length; i++)
                    Hierarchy.m_Nodes.ValueByEntity.SetValueDefaultUnchecked(createdEntities[i]);

                for (var i = 0; i < addedSceneTagWithoutParentEntities.Length; i++)
                {
                    if (addedSceneTagWithoutParentComponents[i].SceneEntity == Entity.Null)
                    {
                        // Something unexpected with the ECS data model. Just drop this node at the root.
                        Hierarchy.SetParent(HierarchyNodeHandle.FromEntity(addedSceneTagWithoutParentEntities[i]), HierarchyNodeHandle.Root);
                        continue;
                    }

                    var subSceneNode = SceneTagToSubSceneNodeHandle[addedSceneTagWithoutParentComponents[i]];
                    Hierarchy.SetParent(HierarchyNodeHandle.FromEntity(addedSceneTagWithoutParentEntities[i]), Hierarchy.Exists(subSceneNode) ? subSceneNode : HierarchyNodeHandle.Root);
                }

                for (var i = 0; i < addedParentEntities.Length; i++)
                {
                    if (addedParentComponents[i].Value == Entity.Null)
                    {
                        // The 'Parent.Value' is null. Try to place this entity under it's sub-scene instead.
                        if (addedParentSceneTagForNullParentComponents[i].SceneEntity == Entity.Null)
                        {
                            // The sub-scene is also missing. Drop this node at the root.
                            Hierarchy.SetParent(HierarchyNodeHandle.FromEntity(addedParentEntities[i]), HierarchyNodeHandle.Root);
                            continue;
                        }

                        var subSceneNode = SceneTagToSubSceneNodeHandle[addedParentSceneTagForNullParentComponents[i]];
                        Hierarchy.SetParent(HierarchyNodeHandle.FromEntity(addedParentEntities[i]), subSceneNode);
                        continue;
                    }

                    Hierarchy.SetParent(HierarchyNodeHandle.FromEntity(addedParentEntities[i]), HierarchyNodeHandle.FromEntity(addedParentComponents[i].Value));
                }
            }
        }
    }
}
