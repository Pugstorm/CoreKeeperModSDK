using System;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Doc.CodeSamples.Tests
{
    #region singleton-type-example

    public struct Singlet : IComponentData
    {
        public int Value;
    }
    #endregion
    //Types used in examples below
    public struct Melee : IComponentData {}
    public struct Ranger : IComponentData {}
    public struct Player : IComponentData {}
    public struct ObjectPosition : IComponentData { public float3 Value; }
    public struct ObjectVelocity : IComponentData { public float3 Value; }
    public struct ObjectUniformScale : IComponentData { public float Value; }
    public struct ObjectNonUniformScale : IComponentData { public float3 Value; }
    public struct ObjectCompositeScale : IComponentData { public float3 Value; }
    public struct ObjectRotation : IComponentData { public quaternion Value; }
    public struct ObjectRotationSpeed : IComponentData { public float RadiansPerSecond; }
    public struct Displacement : IComponentData { public float3 Value; }
    public struct Friction : IComponentData { public float Value; }

    [RequireMatchingQueriesForUpdate]
    public partial class EntityQueryExamples : SystemBase
    {
        void queryFromList()
        {
            #region query-from-list

            EntityQuery query = GetEntityQuery(typeof(ObjectRotation),
                ComponentType.ReadOnly<ObjectRotationSpeed>());
            #endregion
        }

        void queryFromDescription()
        {
            {
                #region query-from-description

                EntityQueryDesc description = new EntityQueryDesc
                {
                    None = new ComponentType[]
                    {
                        typeof(Static)
                    },
                    All = new ComponentType[]
                    {
                        typeof(ObjectRotation),
                        ComponentType.ReadOnly<ObjectRotationSpeed>()
                    }
                };
                EntityQuery query = GetEntityQuery(description);

                #endregion
            }

            {
                #region query-from-builder

                EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<ObjectRotation>()
                    .WithAll<ObjectRotationSpeed>()
                    .WithNone<Static>()
                    .Build(this);

                #endregion
            }
        }

        protected override void OnCreate()
        {
            {
                #region query-description

                EntityQueryDesc description = new EntityQueryDesc
                {
                    Any = new ComponentType[] { typeof(Melee), typeof(Ranger) },
                    None = new ComponentType[] { typeof(Player) },
                    All = new ComponentType[] { typeof(ObjectPosition), typeof(ObjectRotation) }
                };

                #endregion
                var query = GetEntityQuery(description);
            }
            {
                #region query-builder-chained-withall

                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<ObjectPosition, ObjectVelocity>()
                    .WithAll<ObjectRotation, ObjectRotationSpeed>()
                    .Build(this);

                #endregion
            }
            {
                #region query-builder-chained-withallrw

                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<Friction>()
                    .WithAllRW<ObjectPosition, ObjectVelocity>()
                    .WithAllRW<ObjectRotation, ObjectRotationSpeed>()
                    .Build(this);

                #endregion
            }
            {
                #region query-builder-chained-withany

                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<ObjectPosition>()
                    .WithAny<ObjectUniformScale, ObjectNonUniformScale>()
                    .WithAny<ObjectCompositeScale>()
                    .Build(this);

                #endregion
            }
            {
                #region query-builder-chained-withanyrw

                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<ObjectPosition>()
                    .WithAnyRW<ObjectUniformScale, ObjectNonUniformScale>()
                    .WithAnyRW<ObjectCompositeScale>()
                    .Build(this);

                #endregion
            }
            {
                #region query-builder

                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<ObjectPosition, ObjectRotation>()
                    .WithAny<Melee, Ranger>()
                    .WithNone<Player>()
                    .Build(this);

                #endregion

                Entity entity = Entity.Null;

                #region entity-query-mask

                var mask = query.GetEntityQueryMask();
                bool doesArchetypeMatch = mask.MatchesIgnoreFilter(entity);

                #endregion
            }
            {
                #region query-builder-chunk-component-all

                var entityWithPlayerComponent = EntityManager.CreateEntity();
                EntityManager.AddComponent<Player>(entityWithPlayerComponent);

                var entityWithPlayerChunkComponent = EntityManager.CreateEntity();
                EntityManager.AddComponent(entityWithPlayerChunkComponent, ComponentType.ChunkComponent<Player>());

                // This query will only match entityWithPlayerComponent
                var playerQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<Player>()
                    .Build(this);

                // This query will only match entityWithPlayerChunkComponent
                var chunkPlayerQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllChunkComponent<Player>()
                    .Build(this);

                #endregion
            }
            {
                #region query-builder-chunk-component-any

                var entityWithPlayerComponent = EntityManager.CreateEntity();
                EntityManager.AddComponent<Player>(entityWithPlayerComponent);

                var entityWithPlayerChunkComponent = EntityManager.CreateEntity();
                EntityManager.AddComponent(entityWithPlayerChunkComponent, ComponentType.ChunkComponent<Player>());

                // This query will match both entityWithPlayerComponent and entityWithPlayerChunkComponent
                var playerQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithAny<Player>()
                    .WithAnyChunkComponent<Player>()
                    .Build(this);

                #endregion
            }
            {
                #region query-builder-chunk-component-none

                var entityWithPlayerComponent = EntityManager.CreateEntity();
                EntityManager.AddComponent<Player>(entityWithPlayerComponent);

                var entityWithPlayerChunkComponent = EntityManager.CreateEntity();
                EntityManager.AddComponent(entityWithPlayerChunkComponent, ComponentType.ChunkComponent<Player>());

                // This query will only match entityWithPlayerChunkComponent, excluding entityWithPlayerComponent
                var noPlayerQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithNone<Player>()
                    .Build(this);

                // This query will only match entityWithPlayerComponent, excluding entityWithPlayerChunkComponent
                var noChunkPlayerQuery = new EntityQueryBuilder(Allocator.Temp)
                    .WithNoneChunkComponent<Player>()
                    .Build(this);

                #endregion
            }
        }

        protected override void OnUpdate()
        {
            var queryForSingleton = GetEntityQuery(typeof(Singlet));
            var entityManager = EntityManager;
            #region create-singleton

            Entity singletonEntity = entityManager.CreateEntity(typeof(Singlet));
            entityManager.SetComponentData(singletonEntity, new Singlet { Value = 1 });

            #endregion


            #region set-singleton

            queryForSingleton.SetSingleton<Singlet>(new Singlet {Value = 1});
            #endregion
        }

        void ManualExamples1()
        {
            {
                #region define-query
                EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<ObjectRotation>()
                    .WithAll<ObjectRotationSpeed>()
                    .Build(this);
                #endregion
            }
            {
                #region query-desc
                EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<ObjectRotation>()
                    .WithAll<ObjectRotationSpeed>()
                    .WithNone<Static>()
                    .Build(this);
                #endregion
            }
            {
                #region query-builder-manual

                EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<ObjectRotation>()
                    .WithAll<ObjectRotationSpeed>()
                    .WithNone<Static>()
                    .Build(this);

                #endregion
            }
            {
                #region combine-query
                EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAllRW<ObjectRotation>()
                    // Start a new query description
                    .AddAdditionalQuery()
                    .WithAllRW<ObjectRotationSpeed>()
                    .Build(this);
                #endregion
            }

            {
                #region combine-query-builder

                EntityQuery query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<Parent>().WithNone<Child>()
                    .AddAdditionalQuery()
                    .WithAll<Child>().WithNone<Parent>()
                    .Build(this);

                #endregion
            }
        }
    }
    #region query-writegroup

    public struct CharacterComponent : IComponentData { }

    [WriteGroup(typeof(CharacterComponent))]
    public struct LuigiComponent : IComponentData { }

    [WriteGroup(typeof(CharacterComponent))]
    public struct MarioComponent : IComponentData { }

    [RequireMatchingQueriesForUpdate]
    public partial class ECSSystem : SystemBase
    {
        protected override void OnCreate()
        {
            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<CharacterComponent>()
                .WithAll<MarioComponent>()
                .WithOptions(EntityQueryOptions.FilterWriteGroup)
                .Build(this);
        }

        protected override void OnUpdate()
        {
            throw new NotImplementedException();
        }
    }
    #endregion

    #region get-query

    [RequireMatchingQueriesForUpdate]
    public partial class RotationSpeedSys : SystemBase
    {
        private EntityQuery query;

        protected override void OnUpdate()
        {
            float deltaTime = SystemAPI.Time.DeltaTime;

            Entities
                .WithStoreEntityQueryInField(ref query)
                .ForEach(
                (ref ObjectRotation rotation, in ObjectRotationSpeed speed) => {
                    rotation.Value
                        = math.mul(
                            math.normalize(rotation.Value),
                                quaternion.AxisAngle(math.up(),
                                    speed.RadiansPerSecond * deltaTime)
                         );
                })
                .Schedule();
        }
    }
    #endregion

    #region get-query-ijobchunk

    [RequireMatchingQueriesForUpdate]
    public partial class RotationSystem : SystemBase
    {
        private EntityQuery query;

        protected override void OnCreate()
        {
            query = GetEntityQuery(typeof(ObjectRotation),
                   ComponentType.ReadOnly<ObjectRotationSpeed>());
        }

        protected override void OnUpdate()
        {
            throw new NotImplementedException();
        }
    }
    #endregion
    #region shared-component-filter

    struct SharedGrouping : ISharedComponentData
    {
        public int Group;
    }

    [RequireMatchingQueriesForUpdate]
    partial class ImpulseSystem : SystemBase
    {
        EntityQuery query;

        protected override void OnCreate()
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<ObjectPosition>()
                .WithAll<Displacement, SharedGrouping>()
                .Build(this);
        }

        protected override void OnUpdate()
        {
            // By default (without a filter), count all entities that have the required components.
            query.ResetFilter();
            int unfilteredCount = query.CalculateEntityCount();
            // With a filter, only entities in chunks that have SharedGrouping=1 will be counted.
            query.SetSharedComponentFilter(new SharedGrouping { Group = 1 });
            int filteredCount = query.CalculateEntityCount();
            // Many query methods include a variant that ignores any active filters. These variants are generally
            // more efficient, and should be used when conservative upper-bound results are acceptable.
            int ignoreFilterCount = query.CalculateEntityCountWithoutFiltering();
        }
    }

    #endregion
    [RequireMatchingQueriesForUpdate]
    partial class UpdateSystem : SystemBase
    {
        #region change-filter

        EntityQuery query;

        protected override void OnCreate()
        {
            query = new EntityQueryBuilder(Allocator.Temp)
                .WithAllRW<LocalToWorld>()
                .WithAll<ObjectPosition>()
                .Build(this);
            query.SetChangedVersionFilter(typeof(ObjectPosition));

        }
        #endregion

        protected override void OnUpdate()
        {
            throw new NotImplementedException();
        }
    }
}
