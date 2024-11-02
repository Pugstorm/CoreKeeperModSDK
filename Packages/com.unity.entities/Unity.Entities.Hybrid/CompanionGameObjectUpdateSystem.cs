#if !UNITY_DISABLE_MANAGED_COMPONENTS

/*
 * Hybrid Components are classic Unity components that are added to an entity via AddComponentObject.
 * We call Companion GameObject the GameObject owned by an entity in order to host those Hybrid Components.
 * Companion GameObjects should be considered implementation details and are not intended to be directly accessed by users.
 * An entity can also have Hybrid Components owned externally (this is used during conversion), but this is not what the system below is about.
 * When an entity owns a Companion GameObject, the entity also has a managed CompanionLink, which contains a reference to that GameObject.
 * Companion GameObjects are in world space, their transform is updated from their entities, never the other way around.
 * Getting to the Companion GameObject from an Entity is done through the managed CompanionLink.
 * Going the other way around, from the Companion GameObject to the Entity, isn't possible nor advised.
 */

using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities
{

    // Needs to be a cleanup component because instantiation will always create disabled GameObjects
    struct CompanionGameObjectActiveCleanup : ICleanupComponentData
    {
    }

#if UNITY_EDITOR
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    partial struct CompanionGameObjectLiveBakingInitSystem : ISystem
    {
        private EntityQuery toInitialize;

        public void OnCreate(ref SystemState state)
        {
            toInitialize = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CompanionLink>()
                .WithNone<CompanionReference>()
                .WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)
                .Build(ref state);
            state.RequireForUpdate(toInitialize);
        }

        public void OnUpdate(ref SystemState state)
        {
            // First time initialize only in Editor
            var ecb = new EntityCommandBuffer(Allocator.Temp);
            foreach(var (link, entity) in SystemAPI.Query<RefRO<CompanionLink>>()
                        .WithNone<CompanionReference>()
                        .WithEntityAccess()
                        .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab))
            {
                ecb.AddComponent(entity, new CompanionReference { Companion = link.ValueRO.Companion });
            }
            ecb.Playback(state.EntityManager);
        }
    }
#endif

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [BurstCompile]
    partial struct CompanionGameObjectUpdateSystem : ISystem
    {
        private EntityQuery companionChanged;
        private EntityQuery toActivate;
        private EntityQuery toDeactivate;
        private EntityQuery toCleanup;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            companionChanged = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CompanionLink>()
                .Build(ref state);
            companionChanged.SetChangedVersionFilter(ComponentType.ReadWrite<CompanionLink>());

            toActivate = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CompanionLink>()
                .WithNone<CompanionGameObjectActiveCleanup, Disabled>()
                .Build(ref state);
            toDeactivate = new EntityQueryBuilder(Allocator.Temp)
                .WithAny<Disabled, Prefab>()
                .WithAll<CompanionGameObjectActiveCleanup, CompanionLink>()
                .WithOptions(EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab)
                .Build(ref state);
            toCleanup = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<CompanionGameObjectActiveCleanup>()
                .WithNone<CompanionLink>()
                .Build(ref state);
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            // Activate
            if (!toActivate.IsEmpty)
            {
                using var companionLinksToActivate = toActivate.ToComponentDataArray<CompanionLink>(Allocator.Temp).Reinterpret<int>();
                GameObject.SetGameObjectsActive(companionLinksToActivate, true);
                state.EntityManager.AddComponent<CompanionGameObjectActiveCleanup>(toActivate);
            }

            // Deactivate
            if (!toDeactivate.IsEmpty)
            {
                using var companionLinksToDeactivate = toDeactivate.ToComponentDataArray<CompanionLink>(Allocator.Temp).Reinterpret<int>();
                GameObject.SetGameObjectsActive(companionLinksToDeactivate, false);
                state.EntityManager.RemoveComponent<CompanionGameObjectActiveCleanup>(toDeactivate);
            }

            state.EntityManager.RemoveComponent<CompanionGameObjectActiveCleanup>(toCleanup);
        }
    }
}
#endif
