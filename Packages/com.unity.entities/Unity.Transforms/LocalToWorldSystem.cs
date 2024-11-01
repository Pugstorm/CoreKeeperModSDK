using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Assertions;
using Unity.Burst.CompilerServices;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;

namespace Unity.Transforms
{
    /// <summary>
    /// This system computes a <see cref="LocalToWorld"/> matrix for each entity
    /// </summary>
    /// <remarks>
    /// Entity transformation hierarchies are created using the <see cref="Parent"/> and <see cref="LocalTransform"/>
    /// components, and maintained by the <see cref="ParentSystem"/>.
    ///
    /// For root-level / world-space entities with no <see cref="Parent"/>, the <see cref="LocalToWorld"/> can be
    /// computed directly from the entity's <see cref="LocalTransform"/>.
    ///
    /// For child entities, each unique hierarchy is traversed recursively, computing each child's <see cref="LocalToWorld"/>
    /// by composing its <see cref="LocalTransform"/> with its parent's world-space transform.
    /// </remarks>
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(TransformSystemGroup))]
    [UpdateAfter(typeof(ParentSystem))]
    public partial struct LocalToWorldSystem : ISystem
    {
        // Compute the LocalToWorld of all root-level entities
        [BurstCompile]
        unsafe struct ComputeRootLocalToWorldJob : IJobChunk
        {
            [ReadOnly] public ComponentTypeHandle<LocalTransform> LocalTransformTypeHandleRO;
            [ReadOnly] public ComponentTypeHandle<PostTransformMatrix> PostTransformMatrixTypeHandleRO;
            public ComponentTypeHandle<LocalToWorld> LocalToWorldTypeHandleRW;
            public uint LastSystemVersion;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);

                LocalTransform* chunkLocalTransforms = (LocalTransform*)chunk.GetRequiredComponentDataPtrRO(ref LocalTransformTypeHandleRO);
                if (chunk.DidChange(ref LocalTransformTypeHandleRO, LastSystemVersion) ||
                    chunk.DidChange(ref PostTransformMatrixTypeHandleRO, LastSystemVersion))
                {
                    LocalToWorld* chunkLocalToWorlds = (LocalToWorld*)chunk.GetRequiredComponentDataPtrRW(ref LocalToWorldTypeHandleRW);
                    PostTransformMatrix* chunkPostTransformMatrices = (PostTransformMatrix*)chunk.GetComponentDataPtrRO(ref PostTransformMatrixTypeHandleRO);
                    if (chunkPostTransformMatrices != null)
                    {
                        for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                        {
                            chunkLocalToWorlds[i].Value = math.mul(chunkLocalTransforms[i].ToMatrix(),
                                chunkPostTransformMatrices[i].Value);
                        }
                    }
                    else
                    {
                        for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; ++i)
                        {
                            chunkLocalToWorlds[i].Value = chunkLocalTransforms[i].ToMatrix();
                        }
                    }
                }
            }
        }

        [BurstCompile]
        unsafe struct ComputeChildLocalToWorldJob : IJobChunk
        {
            [ReadOnly] public EntityQueryMask LocalToWorldWriteGroupMask;

            [ReadOnly] public BufferTypeHandle<Child> ChildTypeHandleRO;
            [ReadOnly] public BufferLookup<Child> ChildLookupRO;
            public ComponentTypeHandle<LocalToWorld> LocalToWorldTypeHandleRW;

            [ReadOnly] public ComponentLookup<LocalTransform> LocalTransformLookupRO;
            [ReadOnly] public ComponentLookup<PostTransformMatrix> PostTransformMatrixLookupRO;
            [NativeDisableContainerSafetyRestriction] public ComponentLookup<LocalToWorld> LocalToWorldLookupRW;
            public uint LastSystemVersion;

            void ChildLocalToWorldFromTransformMatrix(in float4x4 parentLocalToWorld, Entity childEntity, bool updateChildrenTransform)
            {
                updateChildrenTransform = updateChildrenTransform
                                          || PostTransformMatrixLookupRO.DidChange(childEntity, LastSystemVersion)
                                          || LocalTransformLookupRO.DidChange(childEntity, LastSystemVersion);

                float4x4 localToWorld;
                bool hasChildBuffer = ChildLookupRO.TryGetBuffer(childEntity, out DynamicBuffer<Child> children);

                if (updateChildrenTransform && LocalToWorldWriteGroupMask.MatchesIgnoreFilter(childEntity))
                {
                    // this entity (or its ancestors) has a dirty transform, AND this system is responsible for updating it LocalToWorld.
                    var localTransform = LocalTransformLookupRO[childEntity];
                    localToWorld = math.mul(parentLocalToWorld, localTransform.ToMatrix());
                    if (PostTransformMatrixLookupRO.HasComponent(childEntity))
                    {
                        localToWorld = math.mul(localToWorld, PostTransformMatrixLookupRO[childEntity].Value);
                    }
                    LocalToWorldLookupRW[childEntity] = new LocalToWorld{Value = localToWorld};
                    if (hasChildBuffer)
                    {
                        for (int i = 0, childCount = children.Length; i < childCount; i++)
                        {
                            ChildLocalToWorldFromTransformMatrix(localToWorld, children[i].Value, updateChildrenTransform);
                        }
                    }
                }
                else
                {
                    // either ancestors are not dirty, or we didn't match the write group. We still need to recurse
                    // to any children (if any), which may themselves have dirty local transforms.
                    if (hasChildBuffer)
                    {
                        localToWorld = LocalToWorldLookupRW[childEntity].Value;
                        updateChildrenTransform = LocalToWorldLookupRW.DidChange(childEntity, LastSystemVersion);
                        for (int i = 0, childCount = children.Length; i < childCount; i++)
                        {
                            ChildLocalToWorldFromTransformMatrix(localToWorld, children[i].Value, updateChildrenTransform);
                        }
                    }
                }
            }

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);

                bool updateChildrenTransform = chunk.DidChange(ref ChildTypeHandleRO, LastSystemVersion);
                BufferAccessor<Child> chunkChildBuffers = chunk.GetBufferAccessor(ref ChildTypeHandleRO);
                updateChildrenTransform = updateChildrenTransform || chunk.DidChange(ref LocalToWorldTypeHandleRW, LastSystemVersion);
                LocalToWorld* chunkLocalToWorlds = (LocalToWorld*)chunk.GetRequiredComponentDataPtrRO(ref LocalToWorldTypeHandleRW);
                for (int i = 0, chunkEntityCount = chunk.Count; i < chunkEntityCount; i++)
                {
                    var localToWorld = chunkLocalToWorlds[i].Value;
                    var children = chunkChildBuffers[i];
                    for (int j = 0, childCount = children.Length; j < childCount; j++)
                    {
                        ChildLocalToWorldFromTransformMatrix(localToWorld, children[j].Value, updateChildrenTransform);
                    }
                }
            }
        }

        EntityQuery _rootsQuery;
        EntityQuery _parentsQuery;
        EntityQueryMask _localToWorldWriteGroupMask;

        ComponentTypeHandle<LocalTransform> _localTransformTypeHandleRO;
        ComponentTypeHandle<PostTransformMatrix> _postTransformMatrixTypeHandleRO;
        ComponentTypeHandle<LocalTransform> _localTransformTypeHandleRW;
        ComponentTypeHandle<LocalToWorld> _localToWorldTypeHandleRW;

        BufferTypeHandle<Child> _childTypeHandleRO;
        BufferLookup<Child> _childLookupRO;

        ComponentLookup<LocalTransform> _localTransformLookupRO;
        ComponentLookup<PostTransformMatrix> _postTransformMatrixLookupRO;
        ComponentLookup<LocalToWorld> _localToWorldLookupRW;

        /// <inheritdoc cref="ISystem.OnCreate"/>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LocalTransform>()
                .WithAllRW<LocalToWorld>()
                .WithNone<Parent>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup);
            _rootsQuery = state.GetEntityQuery(builder);

            builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LocalTransform, Child>()
                .WithAllRW<LocalToWorld>()
                .WithNone<Parent>();
            _parentsQuery = state.GetEntityQuery(builder);

            builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<LocalTransform, Parent>()
                .WithAllRW<LocalToWorld>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup);
            _localToWorldWriteGroupMask = state.GetEntityQuery(builder).GetEntityQueryMask();

            _localTransformTypeHandleRO = state.GetComponentTypeHandle<LocalTransform>(true);
            _postTransformMatrixTypeHandleRO = state.GetComponentTypeHandle<PostTransformMatrix>(true);
            _localTransformTypeHandleRW = state.GetComponentTypeHandle<LocalTransform>(false);
            _localToWorldTypeHandleRW = state.GetComponentTypeHandle<LocalToWorld>(false);

            _childTypeHandleRO = state.GetBufferTypeHandle<Child>(true);
            _childLookupRO = state.GetBufferLookup<Child>(true);

            _localTransformLookupRO = state.GetComponentLookup<LocalTransform>(true);
            _postTransformMatrixLookupRO = state.GetComponentLookup<PostTransformMatrix>(true);
            _localToWorldLookupRW = state.GetComponentLookup<LocalToWorld>(false);
        }

        /// <inheritdoc cref="ISystem.OnDestroy"/>
        /// <inheritdoc cref="ISystem.OnUpdate"/>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            _localTransformTypeHandleRO.Update(ref state);
            _postTransformMatrixTypeHandleRO.Update(ref state);
            _localTransformTypeHandleRW.Update(ref state);
            _localToWorldTypeHandleRW.Update(ref state);

            _childTypeHandleRO.Update(ref state);
            _childLookupRO.Update(ref state);

            _localTransformLookupRO.Update(ref state);
            _postTransformMatrixLookupRO.Update(ref state);
            _localToWorldLookupRW.Update(ref state);

            // Compute LocalToWorld for all root-level entities
            var rootJob = new ComputeRootLocalToWorldJob
            {
                LocalTransformTypeHandleRO = _localTransformTypeHandleRO,
                PostTransformMatrixTypeHandleRO = _postTransformMatrixTypeHandleRO,
                LocalToWorldTypeHandleRW = _localToWorldTypeHandleRW,
                LastSystemVersion = state.LastSystemVersion,
            };
            state.Dependency = rootJob.ScheduleParallelByRef(_rootsQuery, state.Dependency);

            // Compute LocalToWorld for all child entities
            var childJob = new ComputeChildLocalToWorldJob
            {
                LocalToWorldWriteGroupMask = _localToWorldWriteGroupMask,
                ChildTypeHandleRO = _childTypeHandleRO,
                ChildLookupRO = _childLookupRO,
                LocalToWorldTypeHandleRW = _localToWorldTypeHandleRW,
                LocalTransformLookupRO = _localTransformLookupRO,
                PostTransformMatrixLookupRO = _postTransformMatrixLookupRO,
                LocalToWorldLookupRW = _localToWorldLookupRW,
                LastSystemVersion = state.LastSystemVersion,
            };
            state.Dependency = childJob.ScheduleParallelByRef(_parentsQuery, state.Dependency);
        }
    }
}
