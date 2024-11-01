#if !UNITY_DISABLE_MANAGED_COMPONENTS
using Unity.Burst;
using Unity.Collections;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.Jobs;

namespace Unity.Entities
{
    struct CompanionGameObjectUpdateTransformCleanup : ICleanupComponentData
    {
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateAfter(typeof(TransformSystemGroup))]
    [BurstCompile]
    public partial struct CompanionGameObjectUpdateTransformSystem : ISystem
    {
        static readonly string s_ProfilerMarkerAddNewString = "AddNew";
        static readonly string s_ProfilerMarkerRemoveString = "Remove";
        static readonly string s_ProfilerMarkerUpdateString = "Update";
        static readonly ProfilerMarker s_ProfilerMarkerAddNew = new(s_ProfilerMarkerAddNewString);
        static readonly ProfilerMarker s_ProfilerMarkerRemove = new(s_ProfilerMarkerRemoveString);
        static readonly ProfilerMarker s_ProfilerMarkerUpdate = new(s_ProfilerMarkerUpdateString);

        struct IndexAndInstance
        {
            public int transformAccessArrayIndex;
            public int instanceID;
        }

        TransformAccessArray m_TransformAccessArray;
        NativeList<Entity> m_Entities;
        NativeHashMap<Entity, IndexAndInstance> m_EntitiesMap;

        EntityQuery m_CreatedQuery;
        EntityQuery m_DestroyedQuery;

        ComponentLookup<LocalToWorld> m_LocalToWorldLookup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            m_TransformAccessArray = new TransformAccessArray(64);
            m_Entities = new NativeList<Entity>(64, Allocator.Persistent);
            m_EntitiesMap = new NativeHashMap<Entity, IndexAndInstance>(64, Allocator.Persistent);
            m_CreatedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CompanionLink,CompanionLinkTransform,LocalToWorld>()
                .WithNone<CompanionGameObjectUpdateTransformCleanup>()
                .Build(ref state);
            m_DestroyedQuery = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CompanionGameObjectUpdateTransformCleanup>()
                .WithNone<CompanionLink>()
                .Build(ref state);

            m_LocalToWorldLookup = state.GetComponentLookup<LocalToWorld>();
        }

        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_TransformAccessArray.Dispose();
            m_Entities.Dispose();
            m_EntitiesMap.Dispose();
        }

        struct RemoveDestroyedEntitiesArgs
        {
            public EntityQuery DestroyedQuery;
            public NativeList<Entity> Entities;
            public NativeHashMap<Entity, IndexAndInstance> EntitiesMap;
            public TransformAccessArray TransformAccessArray;
            public EntityManager EntityManager;
        }

        [BurstCompile]
        static void RemoveDestroyedEntities(ref RemoveDestroyedEntitiesArgs args)
        {
            var entities = args.DestroyedQuery.ToEntityArray(Allocator.Temp);
            for (int i = 0; i < entities.Length; i++)
            {
                var entity = entities[i];
                // This check is necessary because the code for adding entities is conditional and in edge-cases where
                // objects are quickly created-and-destroyed, we might not have the entity in the map.
                if (args.EntitiesMap.TryGetValue(entity, out var indexAndInstance))
                {
                    var index = indexAndInstance.transformAccessArrayIndex;
                    args.TransformAccessArray.RemoveAtSwapBack(index);
                    args.Entities.RemoveAtSwapBack(index);
                    args.EntitiesMap.Remove(entity);
                    if (index < args.Entities.Length)
                    {
                        var fixup = args.EntitiesMap[args.Entities[index]];
                        fixup.transformAccessArrayIndex = index;
                        args.EntitiesMap[args.Entities[index]] = fixup;
                    }
                }
            }
            entities.Dispose();
            args.EntityManager.RemoveComponent<CompanionGameObjectUpdateTransformCleanup>(args.DestroyedQuery);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using (s_ProfilerMarkerAddNew.Auto())
            {
                if (!m_CreatedQuery.IsEmpty)
                {
                    var entities = m_CreatedQuery.ToEntityArray(Allocator.Temp);
                    for (int i = 0; i < entities.Length; i++)
                    {
                        var entity = entities[i];
                        var link = state.EntityManager.GetComponentData<CompanionLinkTransform>(entity);

                        // It is possible that an object is created and immediately destroyed, and then this shouldn't run.
                        if (link.CompanionTransform.IsValid())
                        {
                            IndexAndInstance indexAndInstance = default;
                            indexAndInstance.transformAccessArrayIndex = m_Entities.Length;
                            indexAndInstance.instanceID = link.CompanionTransform.Id.instanceId;
                            m_EntitiesMap.Add(entity, indexAndInstance);
                            m_TransformAccessArray.Add(link.CompanionTransform.Id.instanceId);
                            m_Entities.Add(entity);
                        }
                    }

                    entities.Dispose();
                    state.EntityManager.AddComponent<CompanionGameObjectUpdateTransformCleanup>(m_CreatedQuery);
                }
            }

            using (s_ProfilerMarkerRemove.Auto())
            {
                if (!m_DestroyedQuery.IsEmpty)
                {
                    var args = new RemoveDestroyedEntitiesArgs
                    {
                        Entities = m_Entities,
                        DestroyedQuery = m_DestroyedQuery,
                        EntitiesMap = m_EntitiesMap,
                        EntityManager = state.EntityManager,
                        TransformAccessArray = m_TransformAccessArray
                    };
                    RemoveDestroyedEntities(ref args);
                }
            }

            using (s_ProfilerMarkerUpdate.Auto())
            {
                foreach (var (link, entity) in SystemAPI.Query<CompanionLinkTransform>().WithChangeFilter<CompanionLink>().WithEntityAccess())
                {
                    var cached = m_EntitiesMap[entity];
                    var currentID = link.CompanionTransform.Id.instanceId;
                    if (cached.instanceID != currentID)
                    {
                        // We avoid the need to update the indices and reorder the entities array by adding
                        // the new transform first, and removing the old one after with a RemoveAtSwapBack.
                        // Example, replacing B with X in ABCD:
                        // 1. ABCD + X = ABCDX
                        // 2. ABCDX - B = AXCD
                        // -> the transform is updated, but the index remains unchanged
                        m_TransformAccessArray.Add(link.CompanionTransform.Id.instanceId);
                        m_TransformAccessArray.RemoveAtSwapBack(cached.transformAccessArrayIndex);
                        cached.instanceID = currentID;
                        m_EntitiesMap[entity] = cached;
                    }
                }
            }

            m_LocalToWorldLookup.Update(ref state);
            state.Dependency = new CopyTransformJob
            {
                localToWorld = m_LocalToWorldLookup,
                entities = m_Entities
            }.Schedule(m_TransformAccessArray, state.Dependency);
        }

        [BurstCompile]
        struct CopyTransformJob : IJobParallelForTransform
        {
            [NativeDisableParallelForRestriction] public ComponentLookup<LocalToWorld> localToWorld;
            [ReadOnly] public NativeList<Entity> entities;

            public unsafe void Execute(int index, TransformAccess transform)
            {
                var ltw = localToWorld[entities[index]];
                transform.localPosition = ltw.Position;

                // We need to use the safe version as the vectors will not be normalized if there is some scale
                transform.localRotation = quaternion.LookRotationSafe(ltw.Forward, ltw.Up);

                var mat = *(UnityEngine.Matrix4x4*) &ltw;
                transform.localScale = mat.lossyScale;
            }
        }
    }
}
#endif
