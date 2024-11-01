using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Jobs;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    class EntityTransactionTests : ECSTestsFixture
    {
        EntityQuery m_Query;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            m_Query = m_Manager.CreateEntityQuery(typeof(EcsTestData));

            // Archetypes can't be created on a job
            m_Manager.CreateArchetype(typeof(EcsTestData));
        }

        struct CreateEntityAddToListJob : IJob
        {
            public ExclusiveEntityTransaction entities;
            public NativeList<Entity> createdEntities;

            public void Execute()
            {
                var entity = entities.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
                entities.SetComponentData(entity, new EcsTestData(42));
                Assert.AreEqual(42, entities.GetComponentData<EcsTestData>(entity).value);

                createdEntities.Add(entity);
            }
        }

        struct CreateEntityJob : IJob
        {
            public ExclusiveEntityTransaction entities;

            public void Execute()
            {
                var entity = entities.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
                entities.SetComponentData(entity, new EcsTestData(42));
                Assert.AreEqual(42, entities.GetComponentData<EcsTestData>(entity).value);
            }
        }

        [Test]
        public void CreateEntitiesChainedJob()
        {
            var job = new CreateEntityAddToListJob();
            job.entities = m_Manager.BeginExclusiveEntityTransaction();
            job.createdEntities = new NativeList<Entity>(0, World.UpdateAllocator.ToAllocator);

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);

            m_Manager.EndExclusiveEntityTransaction();

            var data = m_Query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(2, m_Query.CalculateEntityCount());
            Assert.AreEqual(42, data[0].value);
            Assert.AreEqual(42, data[1].value);

            Assert.IsTrue(m_Manager.Exists(job.createdEntities[0]));
            Assert.IsTrue(m_Manager.Exists(job.createdEntities[1]));
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CommitAfterNotRegisteredTransactionJobLogsError()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginExclusiveEntityTransaction();

            var jobHandle = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);

            Assert.Throws<InvalidOperationException>(() => m_Manager.EndExclusiveEntityTransaction());

            jobHandle.Complete();

            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity transaction safety checks")]
        public void EntityManagerAccessDuringTransactionThrows()
        {
            var job = new CreateEntityAddToListJob();
            job.entities = m_Manager.BeginExclusiveEntityTransaction();

            Assert.Throws<InvalidOperationException>(() => { m_Manager.CreateEntity(typeof(EcsTestData)); });
            Assert.Throws<InvalidOperationException>(() => { m_Manager.Exists(new Entity()); });
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        public void AccessExistingEntityFromTransactionWorks()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));

            var transaction = m_Manager.BeginExclusiveEntityTransaction();
            Assert.AreEqual(42, transaction.GetComponentData<EcsTestData>(entity).value);
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void MissingJobCreationDependency()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginExclusiveEntityTransaction();

            var jobHandle = job.Schedule();
            Assert.Throws<InvalidOperationException>(() => { job.Schedule(); });

            jobHandle.Complete();
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void CreationJobAndMainThreadNotAllowedInParallel()
        {
            var job = new CreateEntityJob();
            job.entities = m_Manager.BeginExclusiveEntityTransaction();

            var jobHandle = job.Schedule();

            Assert.Throws<InvalidOperationException>(() => { job.entities.CreateEntity(typeof(EcsTestData)); });

            jobHandle.Complete();
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        public void CreatingEntitiesBeyondCapacityInTransactionWorks()
        {
            var arch = m_Manager.CreateArchetype(typeof(EcsTestData));

            var transaction = m_Manager.BeginExclusiveEntityTransaction();
            var entities = new NativeArray<Entity>(1000, Allocator.Persistent);
            transaction.CreateEntity(arch, entities);
            entities.Dispose();
            m_Manager.EndExclusiveEntityTransaction();
        }

        struct DynamicBufferElement : IBufferElementData
        {
            public int Value;
        }

        struct DynamicBufferJob : IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public Entity OldEntity;
            public NativeArray<Entity> NewEntity;

            public void Execute()
            {
                NewEntity[0] = Transaction.CreateEntity(typeof(DynamicBufferElement));
                var newBuffer = Transaction.GetBuffer<DynamicBufferElement>(NewEntity[0]);

                var oldBuffer = Transaction.GetBuffer<DynamicBufferElement>(OldEntity);
                var oldArray = new NativeArray<DynamicBufferElement>(oldBuffer.Length, Allocator.Temp);
                oldBuffer.AsNativeArray().CopyTo(oldArray);

                foreach (var element in oldArray)
                {
                    newBuffer.Add(new DynamicBufferElement {Value = element.Value * 2});
                }

                oldArray.Dispose();
            }
        }

        [Test]
        public void DynamicBuffer([Values] bool mainThread)
        {
            var entity = m_Manager.CreateEntity(typeof(DynamicBufferElement));
            var buffer = m_Manager.GetBuffer<DynamicBufferElement>(entity);

            buffer.Add(new DynamicBufferElement {Value = 123});
            buffer.Add(new DynamicBufferElement {Value = 234});
            buffer.Add(new DynamicBufferElement {Value = 345});

            var newEntity = CollectionHelper.CreateNativeArray<Entity, RewindableAllocator>(1, ref World.UpdateAllocator);

            var job = new DynamicBufferJob();
            job.NewEntity = newEntity;
            job.Transaction = m_Manager.BeginExclusiveEntityTransaction();
            job.OldEntity = entity;

            if (mainThread)
            {
                job.Run();
            }
            else
            {
                job.Schedule().Complete();
            }

            m_Manager.EndExclusiveEntityTransaction();

            Assert.AreNotEqual(entity, job.NewEntity[0]);

            var newBuffer = m_Manager.GetBuffer<DynamicBufferElement>(job.NewEntity[0]);

            Assert.AreEqual(3, newBuffer.Length);

            Assert.AreEqual(123 * 2, newBuffer[0].Value);
            Assert.AreEqual(234 * 2, newBuffer[1].Value);
            Assert.AreEqual(345 * 2, newBuffer[2].Value);
        }

        struct SyncIJobChunk : IJobChunk
        {
            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
            }
        }

        struct SyncMiddleJob : IJob
        {
            public ExclusiveEntityTransaction Txn;

            public void Execute()
            {
            }
        }

        struct SyncEntityMgrJob : IJob
        {
            public EntityManager TheManager;

            public void Execute()
            {
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void TransactionSync1()
        {
            var top = new SyncIJobChunk {}.Schedule(m_Manager.UniversalQuery, default);
            Assert.Throws<InvalidOperationException>(() =>
            {
                // Cant run exclusive transaction while ijob chunk is running
                var exclusive = m_Manager.BeginExclusiveEntityTransaction();
                var middle = new SyncMiddleJob { Txn = exclusive }.Schedule(top);
            });
            top.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks("Requires Job Safety System")]
        public void TransactionSync2()
        {
            var exclusive = m_Manager.BeginExclusiveEntityTransaction();
            var middle = new SyncMiddleJob { Txn = exclusive }.Schedule();
            Assert.Throws<InvalidOperationException>(() =>
            {
                // job wasn't registered & thus couldn't be synced
                m_Manager.EndExclusiveEntityTransaction();
                new SyncIJobChunk {}.ScheduleParallel(m_Manager.UniversalQuery, default).Complete();
            });
            middle.Complete();
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity transaction safety checks")]
        public void TransactionSync3()
        {
            var exclusive = m_Manager.BeginExclusiveEntityTransaction();
            Assert.Throws<InvalidOperationException>(() =>
            {
                // Cant run ijob chunk while in transaction
                new SyncIJobChunk {}.ScheduleParallel(m_Manager.UniversalQuery, default);
            });
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [Ignore("Need additional safety handle features to be able to do this")]
        public void TransactionSync4()
        {
            var top = new SyncIJobChunk {}.Schedule(m_Manager.UniversalQuery, default);
            Assert.Throws<InvalidOperationException>(() =>
            {
                // Cant run exclusive transaction while ijob chunk is running
                new SyncEntityMgrJob { TheManager = m_Manager }.Schedule().Complete();
            });
            top.Complete();
        }

        [Test]
        [Ignore("Need additional safety handle features to be able to do this")]
        public void TransactionSync5()
        {
            var q = m_Manager.UniversalQuery;
            var j = new SyncEntityMgrJob { TheManager = m_Manager }.Schedule();
            Assert.Throws<InvalidOperationException>(() =>
            {
                // Can't schedule job while entity manager belongs to job
                new SyncIJobChunk {}.ScheduleParallel(q, default).Complete();
            });
            j.Complete();
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void BufferLookup_AcquiredBeforeTransaction_Throws()
        {
            var c = m_Manager.CreateEntity();
            var linkedEntityGroup = m_Manager.GetBufferLookup<LinkedEntityGroup>();
            m_Manager.BeginExclusiveEntityTransaction();
            Assert.Throws<ObjectDisposedException>(() => linkedEntityGroup.HasBuffer(c));
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity transaction safety checks")]
        public void BufferLookup_AcquiredDuringTransaction_Throws()
        {
            var c = m_Manager.CreateEntity();
            m_Manager.BeginExclusiveEntityTransaction();
            Assert.Throws<InvalidOperationException>(() => m_Manager.GetBufferLookup<LinkedEntityGroup>());
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        public void BufferLookup_AcquiredFromTransaction_Throws()
        {
            var c = m_Manager.CreateEntity();
            var transaction = m_Manager.BeginExclusiveEntityTransaction();
            var linkedEntityGroup = transaction.EntityManager.GetBufferLookup<LinkedEntityGroup>();
            Assert.DoesNotThrow(() => linkedEntityGroup.HasBuffer(c));
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity transaction safety checks")]
        public void CanAccessEntitiesAfterTransaction()
        {
            m_Manager.BeginExclusiveEntityTransaction();
            Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity());
            m_Manager.EndExclusiveEntityTransaction();

            Assert.DoesNotThrow(() => m_Manager.CreateEntity());
        }

        [Test]
        public void CanBeginExclusiveEntityTransactionWorks()
        {
            Assert.IsTrue(m_Manager.CanBeginExclusiveEntityTransaction());
            m_Manager.BeginExclusiveEntityTransaction();
            Assert.IsFalse(m_Manager.CanBeginExclusiveEntityTransaction());
            m_Manager.EndExclusiveEntityTransaction();
            Assert.IsTrue(m_Manager.CanBeginExclusiveEntityTransaction());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity transaction safety checks")]
        public void CanChainTransactions()
        {
            m_Manager.BeginExclusiveEntityTransaction();
            Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity());
            m_Manager.EndExclusiveEntityTransaction();

            Assert.DoesNotThrow(() => m_Manager.CreateEntity());

            m_Manager.BeginExclusiveEntityTransaction();
            Assert.Throws<InvalidOperationException>(() => m_Manager.CreateEntity());
            m_Manager.EndExclusiveEntityTransaction();

            Assert.DoesNotThrow(() => m_Manager.CreateEntity());
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity transaction safety checks")]
        public void NestedTransactionThrows()
        {
            m_Manager.BeginExclusiveEntityTransaction();
            Assert.Throws<InvalidOperationException>(() => m_Manager.BeginExclusiveEntityTransaction());
            m_Manager.EndExclusiveEntityTransaction();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity transaction safety checks")]
        public void Transaction_IsInvalid_AfterItHasEnded()
        {
            var transaction = m_Manager.BeginExclusiveEntityTransaction();
            m_Manager.EndExclusiveEntityTransaction();
            Assert.Throws<InvalidOperationException>(() => transaction.CreateEntity());
        }

        struct CreateArchetypeJob: IJob
        {
            public ComponentType ComponentType1;
            public ComponentType ComponentType2;
            public ExclusiveEntityTransaction Transaction;
            public NativeReference<EntityArchetype> OutArchetype;

            public void Execute()
            {
                OutArchetype.Value = Transaction.CreateArchetype(ComponentType1, ComponentType2);
            }
        }

        [Test]
        public void CreateArchetypeInJob()
        {

            var job = new CreateArchetypeJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                ComponentType1 = typeof(EcsTestData),
                ComponentType2 = typeof(EcsTestData2),
                OutArchetype = new NativeReference<EntityArchetype>(World.UpdateAllocator.ToAllocator)
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);

            m_Manager.EndExclusiveEntityTransaction();

            Assert.DoesNotThrow(() => { m_Manager.CreateEntity(job.OutArchetype.Value);});
        }

        struct AddComponentJob: IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public Entity SrcEntity;
            public ComponentType ComponentType;

            public void Execute()
            {
                Transaction.AddComponent(SrcEntity, ComponentType);
            }
        }

        [Test]
        public void AddComponentInJob()
        {
            var entity = m_Manager.CreateEntity();
            var componentType = typeof(EcsTestData);
            Assert.IsFalse(m_Manager.HasComponent(entity,componentType));

            var job = new AddComponentJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                SrcEntity = entity,
                ComponentType = componentType,
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            Assert.IsTrue(m_Manager.HasComponent(entity,componentType));
        }

        struct AddMissingComponent: IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public NativeArray<Entity> SrcEntities;
            public ComponentType ComponentType;
            public void Execute()
            {
                for (int i = 0; i < SrcEntities.Length; i++)
                {
                    if (!Transaction.HasComponent(SrcEntities[i],ComponentType))
                        Transaction.AddComponent(SrcEntities[i],ComponentType);
                }
            }
        }

        [Test]
        public void HasComponentInJob_Works()
        {
            int numEntities = 100;
            var archetype = m_Manager.CreateArchetype();
            var entities = m_Manager.CreateEntity(archetype,numEntities,World.UpdateAllocator.ToAllocator);

            //only add in every other component to the entity array
            for (int i = 0; i < numEntities; i += 2)
                m_Manager.AddComponent<EcsTestData>(entities[i]);

            var job = new AddMissingComponent()
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                SrcEntities = entities,
                ComponentType = typeof(EcsTestData)
            };

            var jobHandle = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            m_Manager.EndExclusiveEntityTransaction();

            //now every entity should have the component
            for(int i = 0; i < numEntities; i++)
                Assert.IsTrue(m_Manager.HasComponent(entities[i],typeof(EcsTestData)));
        }

        struct AddMissingEntitiesJob: IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public NativeArray<Entity> SrcEntities;
            public void Execute()
            {
                for (int i = 0; i < SrcEntities.Length; i++)
                {
                    if (!Transaction.Exists(SrcEntities[i]))
                        SrcEntities[i] = Transaction.CreateEntity();
                }
            }
        }

        [Test]
        public void ExistsInJob_Works()
        {
            int numEntities = 100;
            var entities = CollectionHelper.CreateNativeArray<Entity>(numEntities,World.UpdateAllocator.ToAllocator);

            //only add in every other entity to the allocated array
            for (int i = 0; i < numEntities; i += 2)
                entities[i] = m_Manager.CreateEntity();

            var job = new AddMissingEntitiesJob()
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                SrcEntities = entities,
            };

            var jobHandle = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            m_Manager.EndExclusiveEntityTransaction();

            Debug.Log(m_Manager.GetAllEntities().Length);
            //now every entity should be added
            for(int i = 0; i < numEntities; i++)
                Assert.IsTrue(m_Manager.Exists(entities[i]));
        }

        struct RemoveComponentJob: IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public Entity SrcEntity;
            public ComponentType ComponentType;

            public void Execute()
            {
                Transaction.RemoveComponent(SrcEntity, ComponentType);
            }
        }

        [Test]
        public void RemoveComponentInJob()
        {
            var componentType = typeof(EcsTestData);
            var entity = m_Manager.CreateEntity(componentType);
            Assert.IsTrue(m_Manager.HasComponent(entity,componentType));

            var job = new RemoveComponentJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                SrcEntity = entity,
                ComponentType = componentType,
            };

            //Cannot be called on worker thread yet
            var jobHandle = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            m_Manager.EndExclusiveEntityTransaction();

            Assert.IsFalse(m_Manager.HasComponent(entity,componentType));
        }

        struct CreateArchetypeUnsafeJob: IJob
        {
            public NativeArray<ComponentType> ComponentTypes;
            public ExclusiveEntityTransaction Transaction;
            public NativeReference<EntityArchetype> OutEntityArchetype;
            public unsafe void Execute()
            {
                OutEntityArchetype.Value = Transaction.CreateArchetype((ComponentType*)ComponentTypes.GetUnsafePtr(), ComponentTypes.Length);
            }
        }

        [Test]
        public void CreateArchetypeInJob_Unsafe_Works()
        {



            ComponentType[] componentTypes = {typeof(EcsTestData),typeof(EcsTestData2)};

            NativeReference<EntityArchetype> entityArchetype =
                new NativeReference<EntityArchetype>(World.UpdateAllocator.ToAllocator);
            var job = new CreateArchetypeUnsafeJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                ComponentTypes = CollectionHelper.CreateNativeArray(componentTypes,World.UpdateAllocator.ToAllocator),
                OutEntityArchetype = entityArchetype
            };

            var jobHandle = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            m_Manager.EndExclusiveEntityTransaction();

            Assert.DoesNotThrow(() => { m_Manager.CreateEntity(job.OutEntityArchetype.Value);});

        }

        struct DestroyEntityJob: IJob
        {
            public NativeArray<Entity> EntitiesArray;
            public NativeSlice<Entity> EntitiesSlice;
            public Entity EntitySingle;
            public ExclusiveEntityTransaction Transaction;

            public void Execute()
            {
                Transaction.DestroyEntity(EntitySingle);
                Transaction.DestroyEntity(EntitiesArray);
                Transaction.DestroyEntity(EntitiesSlice);
            }
        }

        [Test]
        public void DestroyEntityInJob_Works()
        {
            int numEntities = 100;
            int sliceBegin = 25;
            int sliceEnd = 75;

            EntityArchetype archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entitySingle = m_Manager.CreateEntity(archetype);
            var entitiesAllDestroyed = m_Manager.CreateEntity(archetype, numEntities,World.UpdateAllocator.ToAllocator);
            var entitiesSliceDestroyed = m_Manager.CreateEntity(archetype, numEntities,World.UpdateAllocator.ToAllocator);
            var slice = new NativeSlice<Entity>(entitiesSliceDestroyed, sliceBegin, sliceEnd - sliceBegin);


            var job = new DestroyEntityJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                EntitySingle = entitySingle,
                EntitiesArray = entitiesAllDestroyed,
                EntitiesSlice = slice
            };

            var jobHandle = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            m_Manager.EndExclusiveEntityTransaction();

            Assert.IsFalse(m_Manager.Exists(entitySingle));

            for (int i = 0; i < numEntities; i++)
            {
                Assert.IsFalse(m_Manager.Exists(entitiesAllDestroyed[i]));
                bool isInDeletedRange =  i >= sliceBegin  && i < sliceEnd;
                Assert.AreEqual(!isInDeletedRange,m_Manager.Exists(entitiesSliceDestroyed[i]));
            }
        }

        struct CreateEntity_EntityArchetypeJob: IJob
        {
            public NativeArray<Entity> OutEntities;
            public Entity OutEntitySingle;
            public ExclusiveEntityTransaction Transaction;

            public void Execute()
            {
                var archetype = Transaction.CreateArchetype(typeof(EcsTestData));
                Transaction.CreateEntity(archetype, OutEntities);
                OutEntitySingle = Transaction.CreateEntity(archetype);
            }
        }

        [Test]
        public void CreateEntityInJob_EntityArchetype()
        {
            int numEntities = 3;

            var job = new CreateEntity_EntityArchetypeJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                OutEntities = CollectionHelper.CreateNativeArray<Entity>(numEntities,
                World.UpdateAllocator.ToAllocator),
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            var numTestEntities = m_Query.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator).Length;

            //+1 to account for the single Output Entity created, to test each codepath
            Assert.AreEqual(numEntities + 1,numTestEntities);
            for (int i = 0; i < numEntities; i++)
            {
                var entity = job.OutEntities[i];
                Assert.IsTrue(m_Manager.Exists(entity));
            }

        }

        struct InstantiateSingleEntityJob: IJob
        {
            public NativeReference<Entity> DstEntitySingle;
            public NativeArray<Entity> DstEntities;
            public Entity SrcEntity;
            public ExclusiveEntityTransaction Transaction;

            public void Execute()
            {
                DstEntitySingle.Value = Transaction.Instantiate(SrcEntity);
                Transaction.Instantiate(SrcEntity,DstEntities);
            }
        }

        [Test]
        public void InstantiateEntityInJob_Single()
        {
            int value = 42;
            int numEntities = 3;

            var srcEntity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(srcEntity,new EcsTestData
            {
                value = value
            });

            var job = new InstantiateSingleEntityJob
            {
                SrcEntity = srcEntity,
                DstEntities = CollectionHelper.CreateNativeArray<Entity>(numEntities,World.UpdateAllocator.ToAllocator),
                DstEntitySingle = new NativeReference<Entity>(World.UpdateAllocator.ToAllocator),
                Transaction = m_Manager.BeginExclusiveEntityTransaction()
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            Assert.IsTrue(m_Manager.Exists(job.DstEntitySingle.Value));
            Assert.AreEqual(value,m_Manager.GetComponentData<EcsTestData>(job.DstEntitySingle.Value).value);

            for (int i = 0; i < numEntities; i++)
            {
                var entity = job.DstEntities[i];
                Assert.IsTrue(m_Manager.Exists(entity));
                Assert.AreEqual(value,m_Manager.GetComponentData<EcsTestData>(entity).value);
            }


        }

        [BurstCompile]
        struct BatchAddSharedComponentsJob: IJob
        {
            public NativeArray<ArchetypeChunk> SrcChunks;
            public ExclusiveEntityTransaction Transaction;
            public int SharedValue;
            public void Execute()
            {
                Transaction.AddSharedComponent(SrcChunks,new EcsTestSharedComp
                {
                    value = SharedValue
                });

            }
        }

        [Test]
        public void BatchSharedComponentOperationInJob()
        {
            int value = 42;
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            //small array of entities of the same archetype, should all be in the same chunk
            var entities = m_Manager.CreateEntity(archetype,archetype.ChunkCapacity,World.UpdateAllocator.ToAllocator);

            //grab chunk representing all entities
            var testChunk = m_Manager.GetChunk(entities[0]);

            var chunks =
                CollectionHelper.CreateNativeArray(new[] { testChunk },
                    World.UpdateAllocator.ToAllocator);

            var job = new BatchAddSharedComponentsJob()
            {
                SrcChunks = chunks,
                SharedValue = value,
                Transaction = m_Manager.BeginExclusiveEntityTransaction()
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();


            for (int i = 0; i < archetype.ChunkCapacity; i++)
            {
                Assert.AreEqual(value,m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entities[i]).value);
            }

        }

        struct DoubleSharedComponentValue: IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public Entity Entity;
            public void Execute()
            {
                var value = Transaction.GetSharedComponentManaged<EcsTestSharedComp>(Entity).value;
                Transaction.SetSharedComponent(Entity,new EcsTestSharedComp
                {
                    value = value * 2
                });
            }
        }

        [Test]
        public void SingleSharedComponentInJob()
        {
            int value = 24;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entity = m_Manager.CreateEntity(archetype);

            m_Manager.AddSharedComponent(entity, new EcsTestSharedComp
            {
                value = value
            });

            var job = new DoubleSharedComponentValue()
            {
                Entity = entity,
                Transaction = m_Manager.BeginExclusiveEntityTransaction()
            };

            var jobHandle = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            jobHandle.Complete();

            m_Manager.EndExclusiveEntityTransaction();

            Assert.AreEqual(value * 2,m_Manager.GetSharedComponent<EcsTestSharedComp>(entity).value);

        }

        struct SwapComponentsJob: IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public NativeReference<ArchetypeChunk> Chunk;
            public int IndexA;
            public int IndexB;
            public void Execute()
            {
                Transaction.SwapComponents(Chunk.Value,IndexA,Chunk.Value,IndexB);
            }
        }

        [Test]
        public void SwapComponentsInJob()
        {
            int valueA = 24;
            int valueB = 42;

            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData));
            var entityA = m_Manager.CreateEntity(archetype);
            var entityB = m_Manager.CreateEntity(archetype);
            m_Manager.SetComponentData(entityA, new EcsTestData
            {
                value = valueA
            });
            m_Manager.SetComponentData(entityB, new EcsTestData
            {
                value = valueB
            });

            //entity A and B should be in the same chunk, size of 2
            var chunk = m_Manager.GetChunk(entityA);
            Assert.AreEqual(chunk.Count,2);
            Assert.AreEqual(chunk.Archetype,archetype);


            var job = new SwapComponentsJob()
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                Chunk = new NativeReference<ArchetypeChunk>(chunk,World.UpdateAllocator.ToAllocator),
                IndexA = 0,
                IndexB = 1,
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            Assert.AreEqual(valueB,m_Manager.GetComponentData<EcsTestData>(entityA).value);
            Assert.AreEqual(valueA,m_Manager.GetComponentData<EcsTestData>(entityB).value);


        }

        [BurstCompile]
        struct AddSharedComponentDataJob: IJob
        {
            public EcsTestSharedComp SharedComp;
            public ExclusiveEntityTransaction Transaction;
            public Entity Entity;
            public void Execute()
            {
                Assert.IsTrue(Transaction.AddSharedComponent(Entity,SharedComp));
            }
        }

        [Test]
        public void AddSharedComponentDataInJob()
        {
            int value = 42;

            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            var job = new AddSharedComponentDataJob()
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                Entity = entity,
                SharedComp = new EcsTestSharedComp
                {
                    value = value
                }
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            Assert.AreEqual(value,m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity).value);
        }

        struct AddSharedComponentDataToQueryJob: IJob
        {
            public EcsTestSharedComp SharedComp;
            public ExclusiveEntityTransaction Transaction;
            public void Execute()
            {
                var comp = SharedComp;
                var t = Transaction;
                Assert.DoesNotThrow(() => t.EntityManager.AddSharedComponentManaged(t.EntityManager.UniversalQuery, comp));
            }
        }

        [Test]
        public void AddSharedComponentDataToQueryInJob()
        {
            int value = 42;

            var entity1 = m_Manager.CreateEntity(typeof(EcsTestData));
            var entity2 = m_Manager.CreateEntity(typeof(EcsTestData));

            var job = new AddSharedComponentDataToQueryJob()
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                SharedComp = new EcsTestSharedComp
                {
                    value = value
                }
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            Assert.AreEqual(value,m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity1).value);
            Assert.AreEqual(value,m_Manager.GetSharedComponentManaged<EcsTestSharedComp>(entity2).value);
        }

        struct AddMissingBuffersJob: IJob
        {
            [ReadOnly]
            public NativeArray<Entity> Entities;
            public EcsTestSharedComp SharedComp;
            public ExclusiveEntityTransaction Transaction;
            public void Execute()
            {
                for (int i = 0; i < Entities.Length; i++)
                {
                    if (!Transaction.HasBuffer<EcsIntElement>(Entities[i]))
                    {
                        var buffer = Transaction.AddBuffer<EcsIntElement>(Entities[i]);
                        buffer.Add(i);
                    }
                }
            }
        }

        [Test]
        public void AddHasBufferInJob()
        {
            var archetype = m_Manager.CreateArchetype();
            var entities = m_Manager.CreateEntity(archetype,10,World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < entities.Length; i += 2)
            {
                var buffer = m_Manager.AddBuffer<EcsIntElement>(entities[i]);
                buffer.Add(i);
            }

            var job = new AddMissingBuffersJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                Entities = entities,
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            for (int i = 0; i < entities.Length; i++)
            {
                var buffer = m_Manager.GetBuffer<EcsIntElement>(entities[i]);
                Assert.AreEqual(1, buffer.Length);
                Assert.AreEqual(i, buffer[0].Value);
            }
        }
        struct EnabledComponentsJobGeneric: IJob
        {
            [ReadOnly]
            public NativeArray<Entity> Entities;
            public ExclusiveEntityTransaction Transaction;
            public ComponentType EnabledType;

            public void Execute()
            {
                for (int i = 0; i < Entities.Length; i++)
                {
                    if (!Transaction.IsComponentEnabled(Entities[i],EnabledType))
                    {
                        Transaction.SetComponentEnabled(Entities[i],EnabledType, true);
                    }
                }
            }
        }

        [Test]
        public void EnabledComponentsInJobGeneric()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            var entityCount = 10;
            var entities = m_Manager.CreateEntity(archetype,entityCount,World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < entities.Length; i += 2)
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i],false);
            }

            //sanity check, make sure half the components are disabled
            int count = 0;
            for (int i = 0; i < entities.Length; i++)
            {
                if (m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]))
                    count++;
            }

            Assert.AreEqual(entityCount / 2, count);


            var job = new EnabledComponentsJobGeneric
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                Entities = entities,
                EnabledType = typeof(EcsTestDataEnableable)
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            for (int i = 0; i < entities.Length; i++)
            {
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]));
            }
        }

        struct EnabledComponentsJob: IJob
        {
            public NativeArray<Entity> Entities;
            public ExclusiveEntityTransaction Transaction;
            public void Execute()
            {
                for (int i = 0; i < Entities.Length; i++)
                {
                    if (!Transaction.IsComponentEnabled<EcsTestDataEnableable>(Entities[i]))
                    {
                        Transaction.SetComponentEnabled<EcsTestDataEnableable>(Entities[i],true);
                    }
                }
            }
        }

        [Test]
        public void EnabledComponentsInJob()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestDataEnableable));
            var entityCount = 10;
            var entities = m_Manager.CreateEntity(archetype,entityCount,World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < entities.Length; i += 2)
            {
                m_Manager.SetComponentEnabled<EcsTestDataEnableable>(entities[i],false);
            }

            //sanity check, make sure half the components are disabled
            int count = 0;
            for (int i = 0; i < entities.Length; i ++)
            {
                if (m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]))
                    count++;
            }

            Assert.AreEqual(entityCount / 2, count);


            var job = new EnabledComponentsJob
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                Entities = entities,
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            for (int i = 0; i < entities.Length; i++)
            {
               Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]));
            }
        }

#if ENTITY_STORE_V1
        struct IncreaseCapacityJob: IJob
        {
            public ExclusiveEntityTransaction Transaction;
            public int Count;

            public void Execute()
            {
                Transaction.AllocateConsecutiveEntitiesForLoading(Count);
            }
        }

        [Test]
        [Ignore("Need more context on a practical for this function. Being addressed in DOTS-6013")]
        public unsafe void AllocateConsecutiveEntitiesForLoadingInJob_Works()
        {
            int currentCapacity = m_Manager.GetCheckedEntityDataAccess()->EntityComponentStore->EntitiesCapacity;
            Debug.Log(currentCapacity);
            int newCapacity = currentCapacity * 2;


            var job = new IncreaseCapacityJob()
            {
                Transaction = m_Manager.BeginExclusiveEntityTransaction(),
                Count = newCapacity
            };

            m_Manager.ExclusiveEntityTransactionDependency = job.Schedule(m_Manager.ExclusiveEntityTransactionDependency);
            m_Manager.EndExclusiveEntityTransaction();

            //+2 because the EntityComponentStore has one slot allocated for Entity.Null, and one to signify the end of the various structures
            Assert.AreEqual(newCapacity + 2,m_Manager.GetCheckedEntityDataAccess()->EntityComponentStore->EntitiesCapacity);
        }
#endif

        partial class ScheduleJobDuringExclusiveEntityTransactionTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                try
                {
                    var job = new EmptyJob();
                    EntityQuery query = GetEntityQuery(typeof(EcsTestData));
                    EntityManager.BeginExclusiveEntityTransaction();
                    job.Schedule(query, new JobHandle()).Complete();
                }
                finally
                {
                    EntityManager.EndExclusiveEntityTransaction();
                }
            }
        }

        [Test]
        public void ScheduleJobDuringExclusiveEntityTransactionTest_Throws()
        {
            var system = World.GetOrCreateSystemManaged<ScheduleJobDuringExclusiveEntityTransactionTestSystem>();
            var exception = Assert.Throws<InvalidOperationException>(() => system.Update());
            Assert.AreEqual(exception.Message, "You can't schedule an IJobChunk while an exclusive transaction is active");
        }

        partial class RunByRefDuringExclusiveEntityTransactionTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                try
                {
                    var job = new EmptyJob();
                    EntityQuery query = GetEntityQuery(typeof(EcsTestData));
                    EntityManager.BeginExclusiveEntityTransaction();
                    Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, query);
                }
                finally
                {
                    EntityManager.EndExclusiveEntityTransaction();
                }
            }
        }

        [Test]
        public void RunByRefDuringExclusiveEntityTransactionTest_Throws()
        {
            var system = World.GetOrCreateSystemManaged<RunByRefDuringExclusiveEntityTransactionTestSystem>();
            var exception = Assert.Throws<InvalidOperationException>(() => system.Update());
            Assert.AreEqual(exception.Message, "You can't schedule an IJobChunk while an exclusive transaction is active");
        }
    }
}
