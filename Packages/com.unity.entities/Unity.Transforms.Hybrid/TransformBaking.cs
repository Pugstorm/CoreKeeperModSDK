using System;
using Unity.Assertions;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Profiling;
using Unity.Transforms;
using UnityEngine;

[RequireMatchingQueriesForUpdate]
[UpdateInGroup(typeof(TransformBakingSystemGroup))]
[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
[BurstCompile]
internal partial struct TransformBakingSystem : ISystem
{
    EntityQuery               _Query;
    BakedRuntimeTransformsJob _Job;

    ProfilerMarker            _Playback;

    [BurstCompile]
    struct BakedRuntimeTransformsJob : IJobChunk
    {
        [ReadOnly]
        public ComponentTypeHandle<TransformAuthoring> Transform;
        public EntityTypeHandle                        Entities;

        public ComponentTypeHandle<LocalToWorld>       LocalToWorld;
        public ComponentTypeHandle<LocalTransform>      LocalTransform;
        public ComponentTypeHandle<PostTransformMatrix>  PostTransformMatrix;
        public ComponentTypeHandle<Parent>             Parent;

        public EntityQueryMask                         Static;
        public EntityQueryMask                         HasTransform;
        public EntityQueryMask                         HasPostTransformMatrix;
        public EntityQueryMask                         HasParent;
        public EntityQueryMask                         HasLocalToWorld;

        public EntityCommandBuffer.ParallelWriter      Commands;
        public uint                                    ChangeVersion;

        public static bool HasFlag(RuntimeTransformComponentFlags category, RuntimeTransformComponentFlags field)
        {
            return (((uint) category & (uint) field) != 0);
        }

        public RuntimeTransformComponentFlags GetChunkCategory(ArchetypeChunk batchInChunk)
        {
            RuntimeTransformComponentFlags chunkCategory = RuntimeTransformComponentFlags.None;
            if (HasTransform.MatchesIgnoreFilter(batchInChunk))
                chunkCategory |= RuntimeTransformComponentFlags.LocalTransform;

            if (HasPostTransformMatrix.MatchesIgnoreFilter(batchInChunk))
                chunkCategory |= RuntimeTransformComponentFlags.PostTransformMatrix;

            if (HasParent.MatchesIgnoreFilter(batchInChunk))
                chunkCategory |= RuntimeTransformComponentFlags.RequestParent;

            if (HasLocalToWorld.MatchesIgnoreFilter(batchInChunk))
                chunkCategory |= RuntimeTransformComponentFlags.LocalToWorld;

            return chunkCategory;
        }

        public RuntimeTransformComponentFlags GetEntityCategory(TransformAuthoring transformAuthoring, bool isStatic)
        {
            if (transformAuthoring.RuntimeTransformUsage == RuntimeTransformComponentFlags.None)
                return RuntimeTransformComponentFlags.None;

            if (isStatic)
                return RuntimeTransformComponentFlags.LocalToWorld;

            var transformUsage = transformAuthoring.RuntimeTransformUsage;

            /*
            // We add the PostTransformMatrix if needed
            RuntimeTransformComponentFlags flags = transformAuthoring.RuntimeTransformUsage;
            if (!IsUniformScale(transformAuthoring.LocalScale))
            {
                flags |= RuntimeTransformComponentFlags.PostTransformMatrix;
            }*/

            /*
            // If the parent is null, make sure we don't add the parent
            if (transformAuthoring.RuntimeParent != Entity.Null)
            {
                flags &= ~RuntimeTransformComponentFlags.RequestParent;
            }*/

            return transformUsage;
        }

        const float k_Tolerance = 0.001f;

        [BurstCompile]
        static bool IsUniformScale(in float3 scale)
        {
            return math.abs(scale.x - scale.y) <= k_Tolerance &&
                   math.abs(scale.x - scale.z) <= k_Tolerance &&
                   math.abs(scale.y - scale.z) <= k_Tolerance &&
                   math.sign(scale.x) == math.sign(scale.y) &&
                   math.sign(scale.x) == math.sign(scale.z);
        }

        static float3 CalculateLossyScale(float4x4 matrix, quaternion rotation)
        {
            float4x4 m4x4 = matrix;
            float3x3 invR = new float3x3(math.conjugate(rotation));
            float3x3 gsm = new float3x3 { c0 = m4x4.c0.xyz, c1 = m4x4.c1.xyz, c2 = m4x4.c2.xyz };
            float3x3 scale = math.mul(invR, gsm);
            float3 globalScale = new float3(scale.c0.x, scale.c1.y, scale.c2.z);
            return globalScale;
        }

        static void CalculateLocalTransform(in TransformAuthoring transform, bool requestedPostTransformMatrix, out LocalTransform localTransform, out float3 scale, out bool needsNonUniformScale)
        {
            float3 position;
            quaternion rotation;

            // Get Local or World space transform data
            bool hasParent = transform.RuntimeParent != Entity.Null;
            if (hasParent || transform.AuthoringParent == Entity.Null)
            {
                position = transform.LocalPosition;
                rotation = transform.LocalRotation;
                scale = transform.LocalScale;
            }
            else
            {
                position = transform.Position;
                rotation = transform.Rotation;
                scale = CalculateLossyScale(transform.LocalToWorld, transform.Rotation);
            }

            // Detect non uniform scale
            float uniformScale;
            needsNonUniformScale = requestedPostTransformMatrix || !IsUniformScale(scale);
            if (needsNonUniformScale)
            {
                uniformScale = 1f;
            }
            else
            {
                uniformScale = scale.x;
                scale = new float3(1f, 1f, 1f);
            }

            // Calculate the transform
            localTransform = Unity.Transforms.LocalTransform.FromPositionRotationScale(position, rotation, uniformScale);
        }

        [BurstCompile]
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            Assert.IsFalse(useEnabledMask);
            var transforms = chunk.GetNativeArray(ref Transform);
            var entities = chunk.GetNativeArray(Entities);

            //if (batchInChunk.DidOrderChange(ChangeVersion))
            //     Debug.Log($"Order: {batchInChunk.Archetype.ToString()}");
            //if (batchInChunk.DidChange(Transform, ChangeVersion))
            //    Debug.Log($"AuthoringChange: {batchInChunk.Archetype.ToString()}");

            var chunkCategory = GetChunkCategory(chunk);
            var isStatic = Static.MatchesIgnoreFilter(chunk);

            var localToWorlds = chunk.GetNativeArray(ref LocalToWorld);
            var LocalTransforms = chunk.GetNativeArray(ref LocalTransform);
            var postTransformMatrices = chunk.GetNativeArray(ref PostTransformMatrix);
            var parents = chunk.GetNativeArray(ref Parent);

            for (int i = 0; i != transforms.Length; i++)
            {
                // We check if the component was already there by checking the chunk,
                // then we check the desired components in the entity. Based on that we add, remove or just update
                // the components in the entity

                var requestedMode = GetEntityCategory(transforms[i], isStatic);

                // ManualOverride is different than None. With None, all the components previously added will be removed.
                // With ManualOverride this system will not add anything to the entity
                // TODO: Incremental will not work when moving the entity from other configuration to ManualOverride, because this system will not remove the previously requested components
                if (HasFlag(transforms[i].RuntimeTransformUsage, RuntimeTransformComponentFlags.ManualOverride))
                    continue;

                var entity = entities[i];

                // LocalToWorld
                var chunkHasFlagLocalToWorld = HasFlag(chunkCategory, RuntimeTransformComponentFlags.LocalToWorld);
                var requestedHasFlagLocalToWorld = HasFlag(requestedMode, RuntimeTransformComponentFlags.LocalToWorld);
                if (chunkHasFlagLocalToWorld && requestedHasFlagLocalToWorld)
                {
                    localToWorlds[i] = new LocalToWorld { Value = transforms[i].LocalToWorld };
                }
                else if (requestedHasFlagLocalToWorld)
                {
                    Commands.AddComponent(unfilteredChunkIndex, entity, new LocalToWorld { Value = transforms[i].LocalToWorld });
                }
                else if (chunkHasFlagLocalToWorld)
                {
                    Commands.RemoveComponent<LocalToWorld>(unfilteredChunkIndex, entity);
                }

                // Transform
                var chunkHasLocalTransform = HasFlag(chunkCategory, RuntimeTransformComponentFlags.LocalTransform);
                var chunkHasPostTransformMatrix = HasFlag(chunkCategory, RuntimeTransformComponentFlags.PostTransformMatrix);
                var requestedHasLocalTransform = HasFlag(requestedMode, RuntimeTransformComponentFlags.LocalTransform);
                var requestedHasPostTransformMatrix = HasFlag(requestedMode, RuntimeTransformComponentFlags.PostTransformMatrix);

                // Calculate LocalTransform and non uniform scale if any of them are needed
                float3 scale = default;
                LocalTransform localTransform = default;
                if (requestedHasLocalTransform || requestedHasPostTransformMatrix)
                {
                    CalculateLocalTransform(transforms[i], requestedHasPostTransformMatrix, out localTransform, out scale, out bool needsNonUniformScale);
                    // We will need PostTransformMatrix if the scale is non uniform, even if we didn't requested
                    requestedHasPostTransformMatrix |= needsNonUniformScale;
                }

                if (chunkHasLocalTransform && requestedHasLocalTransform)
                {
                    LocalTransforms[i] = localTransform;
                }
                else if (requestedHasLocalTransform)
                {
                    Commands.AddComponent(unfilteredChunkIndex, entity, localTransform);
                }
                else if (chunkHasLocalTransform)
                {
                    Commands.RemoveComponent<LocalTransform>(unfilteredChunkIndex, entity);
                }

                if (chunkHasPostTransformMatrix && requestedHasPostTransformMatrix)
                {
                    postTransformMatrices[i] = new PostTransformMatrix { Value = float4x4.Scale(scale) };
                }
                else if (requestedHasPostTransformMatrix)
                {
                    Commands.AddComponent(unfilteredChunkIndex, entity, new PostTransformMatrix { Value = float4x4.Scale(scale) });
                }
                else if (chunkHasPostTransformMatrix)
                {
                    Commands.RemoveComponent<PostTransformMatrix>(unfilteredChunkIndex, entity);
                }

                // Parent
                var chunkHasFlagParent = HasFlag(chunkCategory, RuntimeTransformComponentFlags.RequestParent);
                var requestedHasFlagParent = HasFlag(requestedMode, RuntimeTransformComponentFlags.RequestParent);
                if (chunkHasFlagParent && requestedHasFlagParent)
                {
                    parents[i] = new Parent() { Value = transforms[i].RuntimeParent };
                }
                else if (requestedHasFlagParent)
                {
                    Commands.AddComponent(unfilteredChunkIndex, entity, new Parent { Value = transforms[i].RuntimeParent });
                }
                else if (chunkHasFlagParent)
                {
                    Commands.RemoveComponent<Parent>(unfilteredChunkIndex, entity);
                }
            }
        }
    }

    [BurstCompile]
    public void OnCreate(ref SystemState state)
    {
        _Playback = new ProfilerMarker("EntityCommandBuffer.Playback");
        _Query = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<TransformAuthoring>()
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
            .Build(ref state);

        // The BakedRuntimeTransformsJob only processes chunks with Transform authoring changes as well as structural changes that may have added / removed the Static tag component
        _Query.AddChangedVersionFilter(ComponentType.ReadOnly<TransformAuthoring>());
        _Query.AddOrderVersionFilter();

        _Job.Static = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Static>()
            // Intentionally leaving static prefabs out of this query, so we don't strip the hierarchy or transform components based on this
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities)
            .Build(ref state).GetEntityQueryMask();

        _Job.HasTransform = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalTransform>()
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
            .Build(ref state).GetEntityQueryMask();

        _Job.HasPostTransformMatrix = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<PostTransformMatrix>()
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
            .Build(ref state).GetEntityQueryMask();

        _Job.HasParent = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<Parent>()
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
            .Build(ref state).GetEntityQueryMask();

        _Job.HasLocalToWorld = new EntityQueryBuilder(Allocator.Temp)
            .WithAll<LocalToWorld>()
            .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
            .Build(ref state).GetEntityQueryMask();
    }

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var buf = new EntityCommandBuffer(Allocator.TempJob);
        _Job.Transform = SystemAPI.GetComponentTypeHandle<TransformAuthoring>(true);
        _Job.Entities = SystemAPI.GetEntityTypeHandle();
        _Job.LocalToWorld = SystemAPI.GetComponentTypeHandle<LocalToWorld>();
        _Job.Parent = SystemAPI.GetComponentTypeHandle<Parent>();
        _Job.LocalTransform = SystemAPI.GetComponentTypeHandle<LocalTransform>();
        _Job.PostTransformMatrix = SystemAPI.GetComponentTypeHandle<PostTransformMatrix>();
        _Job.ChangeVersion = state.LastSystemVersion;
        _Job.Commands = buf.AsParallelWriter();
        state.Dependency = _Job.ScheduleParallelByRef(_Query, state.Dependency);

        state.CompleteDependency();

        using (_Playback.Auto())
        {
            buf.Playback(state.EntityManager);
        }

        buf.Dispose();
    }
}
