using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;
using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Mathematics;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities.Tests
{
    [TestFixture]
    partial class ArchetypeChunkArrayTest : ECSTestsFixture
    {
        public Entity CreateEntity(int value, int sharedValue)
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsIntElement));
            SetupEntity(entity, value, sharedValue);
            return entity;
        }

        public void SetupEntity(Entity entity, int value, int sharedValue)
        {
            m_Manager.SetComponentData(entity, new EcsTestData(value));
            m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(sharedValue));
            var buffer = m_Manager.GetBuffer<EcsIntElement>(entity);
            buffer.ResizeUninitialized(value);
            for (int i = 0; i < value; ++i)
            {
                buffer[i] = i;
            }
        }

        public Entity CreateEntity2(int value, int sharedValue)
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData2), typeof(EcsTestSharedComp));
            m_Manager.SetComponentData(entity, new EcsTestData2(value));
            m_Manager.SetSharedComponentManaged(entity, new EcsTestSharedComp(sharedValue));
            return entity;
        }

        public Entity CreateEnableableEntity(int value)
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2), typeof(EcsIntElementEnableable));
            m_Manager.SetComponentData(entity, new EcsTestDataEnableable(value));
            m_Manager.SetComponentData(entity, new EcsTestDataEnableable2(value));
            var buffer = m_Manager.GetBuffer<EcsIntElementEnableable>(entity);
            for (int i = 0; i < 4; ++i)
                buffer.Add(new EcsIntElementEnableable { Value = i });
            return entity;
        }

        void CreateEntities(int count)
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsIntElement));
            var entities = m_Manager.CreateEntity(archetype, count, m_Manager.World.UpdateAllocator.ToAllocator);
            for (int i = 0; i != count; i++)
                SetupEntity(entities[i], i, i % 7);
        }

        void CreateEntities2(int count)
        {
            for (int i = 0; i != count; i++)
                CreateEntity2(i, i % 7);
        }

        void CreateEnableableEntities(int count)
        {
            for (int i = 0; i != count; i++)
                CreateEnableableEntity(i);
        }

        void CreateMixedEntities(int count)
        {
            for (int i = 0; i != count; i++)
            {
                if ((i & 1) == 0)
                    CreateEntity(i, i % 7);
                else
                    CreateEntity2(-i, i % 7);
            }
        }

        struct ChangeMixedValues : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ArchetypeChunk> chunks;
            public ComponentTypeHandle<EcsTestData> ecsTestData;
            public ComponentTypeHandle<EcsTestData2> ecsTestData2;

            public void Execute(int chunkIndex)
            {
                var chunk = chunks[chunkIndex];
                var chunkCount = chunk.Count;
                var chunkEcsTestData = chunk.GetNativeArray(ref ecsTestData);
                var chunkEcsTestData2 = chunk.GetNativeArray(ref ecsTestData2);

                if (chunkEcsTestData.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        chunkEcsTestData[i] = new EcsTestData(chunkEcsTestData[i].value + 100);
                    }
                }
                else if (chunkEcsTestData2.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        chunkEcsTestData2[i] = new EcsTestData2(chunkEcsTestData2[i].value0 - 1000);
                    }
                }
            }
        }

        [Test]
        public void ACS_WriteMixed()
        {
            CreateMixedEntities(64);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAny<EcsTestData2, EcsTestData>()
                .Build(m_Manager);
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            query.Dispose();

            Assert.AreEqual(14, chunks.Length);

            var ecsTestData = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            var ecsTestData2 = m_Manager.GetComponentTypeHandle<EcsTestData2>(false);
            var changeValuesJobs = new ChangeMixedValues
            {
                chunks = chunks,
                ecsTestData = ecsTestData,
                ecsTestData2 = ecsTestData2,
            };

            var collectValuesJobHandle = changeValuesJobs.Schedule(chunks.Length, 64);
            collectValuesJobHandle.Complete();

            ulong foundValues = 0;
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                var chunkCount = chunk.Count;

                Assert.AreEqual(4, math.ceilpow2(chunkCount - 1));

                var chunkEcsTestData = chunk.GetNativeArray(ref ecsTestData);
                var chunkEcsTestData2 = chunk.GetNativeArray(ref ecsTestData2);
                if (chunkEcsTestData.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        foundValues |= (ulong)1 << (chunkEcsTestData[i].value - 100);
                    }
                }
                else if (chunkEcsTestData2.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        foundValues |= (ulong)1 << (-chunkEcsTestData2[i].value0 - 1000);
                    }
                }
            }

            foundValues++;
            Assert.AreEqual(0, foundValues);
        }

        struct ChangeMixedValuesSharedFilter : IJobParallelFor
        {
            [ReadOnly] public NativeArray<ArchetypeChunk> chunks;
            public ComponentTypeHandle<EcsTestData> ecsTestData;
            public ComponentTypeHandle<EcsTestData2> ecsTestData2;
            [ReadOnly] public SharedComponentTypeHandle<EcsTestSharedComp> ecsTestSharedData;
            public int sharedFilterIndex;

            public void Execute(int chunkIndex)
            {
                var chunk = chunks[chunkIndex];
                var chunkCount = chunk.Count;
                var chunkEcsTestData = chunk.GetNativeArray(ref ecsTestData);
                var chunkEcsTestData2 = chunk.GetNativeArray(ref ecsTestData2);
                var chunkEcsSharedDataIndex = chunk.GetSharedComponentIndex(ecsTestSharedData);

                if (chunkEcsSharedDataIndex != sharedFilterIndex)
                    return;

                if (chunkEcsTestData.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        chunkEcsTestData[i] = new EcsTestData(chunkEcsTestData[i].value + 100);
                    }
                }
                else if (chunkEcsTestData2.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        chunkEcsTestData2[i] = new EcsTestData2(chunkEcsTestData2[i].value0 - 1000);
                    }
                }
            }
        }

        [Test]
        public void ACS_WriteMixedFilterShared()
        {
            CreateMixedEntities(64);

            Assert.AreEqual(1, m_Manager.GlobalSystemVersion);

            // Only update shared value == 1
            var unique = new List<EcsTestSharedComp>(0);
            m_Manager.GetAllUniqueSharedComponentsManaged(unique);
            var sharedFilterValue = 1;
            var sharedFilterIndex = -1;
            for (int i = 0; i < unique.Count; i++)
            {
                if (unique[i].value == sharedFilterValue)
                {
                    sharedFilterIndex = i;
                    break;
                }
            }

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAny<EcsTestData2, EcsTestData>()
                .WithAll<EcsTestSharedComp>()
                .Build(m_Manager);
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            query.Dispose();

            Assert.AreEqual(14, chunks.Length);

            var ecsTestData = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            var ecsTestData2 = m_Manager.GetComponentTypeHandle<EcsTestData2>(false);
            var ecsTestSharedData = m_Manager.GetSharedComponentTypeHandle<EcsTestSharedComp>();
            var changeValuesJobs = new ChangeMixedValuesSharedFilter
            {
                chunks = chunks,
                ecsTestData = ecsTestData,
                ecsTestData2 = ecsTestData2,
                ecsTestSharedData = ecsTestSharedData,
                sharedFilterIndex = sharedFilterIndex
            };

            var collectValuesJobHandle = changeValuesJobs.Schedule(chunks.Length, 64);
            collectValuesJobHandle.Complete();

            ulong foundValues = 0;
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                var chunkCount = chunk.Count;

                Assert.AreEqual(4, math.ceilpow2(chunkCount - 1));

                var chunkEcsSharedDataIndex = chunk.GetSharedComponentIndex(ecsTestSharedData);

                var chunkEcsTestData = chunk.GetNativeArray(ref ecsTestData);
                var chunkEcsTestData2 = chunk.GetNativeArray(ref ecsTestData2);
                if (chunkEcsTestData.Length > 0)
                {
                    var chunkEcsTestDataVersion = chunk.GetChangeVersion(ref ecsTestData);

                    Assert.AreEqual(1, chunkEcsTestDataVersion);

                    for (int i = 0; i < chunkCount; i++)
                    {
                        if (chunkEcsSharedDataIndex == sharedFilterIndex)
                        {
                            foundValues |= (ulong)1 << (chunkEcsTestData[i].value - 100);
                        }
                        else
                        {
                            foundValues |= (ulong)1 << (chunkEcsTestData[i].value);
                        }
                    }
                }
                else if (chunkEcsTestData2.Length > 0)
                {
                    var chunkEcsTestData2Version = chunk.GetChangeVersion(ref ecsTestData2);

                    Assert.AreEqual(1, chunkEcsTestData2Version);

                    for (int i = 0; i < chunkCount; i++)
                    {
                        if (chunkEcsSharedDataIndex == sharedFilterIndex)
                        {
                            foundValues |= (ulong)1 << (-chunkEcsTestData2[i].value0 - 1000);
                        }
                        else
                        {
                            foundValues |= (ulong)1 << (-chunkEcsTestData2[i].value0);
                        }
                    }
                }
            }

            foundValues++;
            Assert.AreEqual(0, foundValues);
        }

        [Test]
        public void ACS_Buffers()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var intElements = m_Manager.GetBufferTypeHandle<EcsIntElement>(false);

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];
                var accessor = chunk.GetBufferAccessor(ref intElements);

                for (int k = 0; k < accessor.Length; ++k)
                {
                    var buffer = accessor[i];

                    for (int n = 0; n < buffer.Length; ++n)
                    {
                        if (buffer[n] != n)
                            Assert.Fail("buffer element does not have the expected value");
                    }
                }
            }
        }

        partial class BumpChunkBufferTypeVersionSystem : SystemBase
        {
            struct UpdateChunks : IJobParallelFor
            {
                public NativeArray<ArchetypeChunk> Chunks;
                public BufferTypeHandle<EcsIntElement> EcsIntElements;

                public void Execute(int chunkIndex)
                {
                    var chunk = Chunks[chunkIndex];
                    var ecsBufferAccessor = chunk.GetBufferAccessor(ref EcsIntElements);
                    for (int i = 0; i < ecsBufferAccessor.Length; ++i)
                    {
                        var buffer = ecsBufferAccessor[i];
                        if (buffer.Length > 0)
                        {
                            buffer[0] += 1;
                        }
                    }
                }
            }

            EntityQuery m_Group;

            protected override void OnCreate()
            {
                m_Group = GetEntityQuery(typeof(EcsIntElement));
            }

            protected override void OnUpdate()
            {
                var chunks = m_Group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                var ecsIntElements = GetBufferTypeHandle<EcsIntElement>();
                var updateChunksJob = new UpdateChunks
                {
                    Chunks = chunks,
                    EcsIntElements = ecsIntElements
                };
                var updateChunksJobHandle = updateChunksJob.Schedule(chunks.Length, 32);
                updateChunksJobHandle.Complete();
            }
        }

        [Test]
        public void ACS_BufferHas()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var intElements = m_Manager.GetBufferTypeHandle<EcsIntElement>(false);
            var missingElements = m_Manager.GetBufferTypeHandle<EcsComplexEntityRefElement>(false);

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];

                // Test Has<T>()
                bool hasIntElements = chunk.Has(ref intElements);
                Assert.IsTrue(hasIntElements, "Has(EcsIntElement) should be true");
                bool hasMissingElements = chunk.Has(ref missingElements);
                Assert.IsFalse(hasMissingElements, "Has(EcsComplexEntityRefElement) should be false");
            }
        }

        [Test]
        public void ACS_Has()
        {
            CreateEntities(128);

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData>(), ComponentType.ReadWrite<EcsIntElement>(), ComponentType.ReadWrite<EcsTestSharedComp>());
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];

                // Test Has<T>()
                Assert.IsTrue(chunk.Has<EcsTestData>(), "Has(EcsTestData) should be true");
                Assert.IsTrue(chunk.Has<EcsIntElement>(), "Has(EcsIntElement) should be true");
                Assert.IsTrue(chunk.Has<EcsTestSharedComp>(), "Has(EcsTestSharedComp) should be true");
                Assert.IsFalse(chunk.Has<EcsComplexEntityRefElement>(), "Has(EcsComplexEntityRefElement) should be false");
            }
        }

        [Test]
        public void ACS_HasChunkComponent()
        {
            var e = m_Manager.CreateEntity();
            m_Manager.AddChunkComponentData<EcsTestData2>(e);
            var chunk = m_Manager.GetChunk(e);

            // Test HasChunkComponent<T>()
            Assert.IsFalse(chunk.HasChunkComponent<EcsTestData>(), "Has(EcsTestData) should be false");
            Assert.IsTrue(chunk.HasChunkComponent<EcsTestData2>(), "Has(EcsTestData2) should be true");
        }


#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public void ACS_ManagedComponentHas()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestManagedComponent>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var strComponents = m_Manager.GetComponentTypeHandle<EcsTestManagedComponent>(false);
            var missingComponents = m_Manager.GetComponentTypeHandle<EcsTestData>(false);

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];

                // Test Has<T>()
                bool hasstrComponents = chunk.Has(ref strComponents);
                Assert.IsTrue(hasstrComponents, "Has(EcsTestManagedComponent) should be true");
                bool hasMissingElements = chunk.Has(ref missingComponents);
                Assert.IsFalse(hasMissingElements, "Has(EcsComplexEntityRefElement) should be false");
            }
        }

#endif

        [Test]
        public void ACS_BufferVersions()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var intElements = m_Manager.GetBufferTypeHandle<EcsIntElement>(false);
            uint[] chunkBufferVersions = new uint[chunks.Length];

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];

                // Test DidChange() before modifications
                chunkBufferVersions[i] = chunk.GetChangeVersion(ref intElements);
                bool beforeDidChange = chunk.DidChange(ref intElements, chunkBufferVersions[i]);
                Assert.IsFalse(beforeDidChange, "DidChange() is true before modifications");
                uint beforeVersion = chunk.GetChangeVersion(ref intElements);
                Assert.AreEqual(chunkBufferVersions[i], beforeVersion, "version mismatch before modifications");
            }

            // Run system to bump chunk versions
            var bumpChunkBufferTypeVersionSystem = World.CreateSystemManaged<BumpChunkBufferTypeVersionSystem>();
            bumpChunkBufferTypeVersionSystem.Update();

            // Check versions after modifications
            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];

                uint afterVersion = chunk.GetChangeVersion(ref intElements);
                Assert.AreNotEqual(chunkBufferVersions[i], afterVersion, "version match after modifications");
                bool afterDidAddChange = chunk.DidChange(ref intElements, chunkBufferVersions[i]);
                Assert.IsTrue(afterDidAddChange, "DidChange() is false after modifications");
            }
        }

        [Test]
        public void ACS_BuffersRO()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();
            var intElements = m_Manager.GetBufferTypeHandle<EcsIntElement>(true);

            var chunk = chunks[0];
            var accessor = chunk.GetBufferAccessor(ref intElements);
            var buffer = accessor[0];

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.Throws<InvalidOperationException>(() => buffer.Add(12));
#endif
        }

        [Test]
        public void ACS_ChunkArchetypeTypesMatch()
        {
            var entityTypes = new ComponentType[] {typeof(EcsTestData), typeof(EcsTestSharedComp), typeof(EcsIntElement)};

            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(entityTypes);
            using (var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator))
            {
                foreach (var chunk in chunks)
                {
                    var archetype = chunk.Archetype;
                    var chunkTypes = archetype.GetComponentTypes().ToArray();
                    foreach (var type in entityTypes)
                    {
                        Assert.Contains(type, chunkTypes);
                    }

                    Assert.AreEqual(entityTypes.Length, entityTypes.Length);
                }
            }

            group.Dispose();
        }

        [MaximumChunkCapacity(3)]
        struct Max3Capacity : IComponentData {}

        [Test]
        public void MaximumChunkCapacityIsRespected()
        {
            for (int i = 0; i != 4; i++)
                m_Manager.CreateEntity(typeof(Max3Capacity));

            var group = m_Manager.CreateEntityQuery(typeof(Max3Capacity));
            using (var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator))
            {
                Assert.AreEqual(2, chunks.Length);
                Assert.AreEqual(3, chunks[0].Capacity);

                Assert.AreEqual(3, chunks[0].Count);
                Assert.AreEqual(1, chunks[1].Count);
            }

            group.Dispose();
        }

        [Test]
        public void ACS_DynamicComponentDataArrayReinterpret()
        {
            CreateMixedEntities(64);

            var query = new EntityQueryBuilder(Allocator.Temp)
                .WithAnyRW<EcsTestData2, EcsTestData>()
                .Build(m_Manager);
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            query.Dispose();

            Assert.AreEqual(14, chunks.Length);

            var ecsTestData = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            var ecsTestData2 = m_Manager.GetComponentTypeHandle<EcsTestData2>(false);
            var changeValuesJobs = new ChangeMixedValues
            {
                chunks = chunks,
                ecsTestData = ecsTestData,
                ecsTestData2 = ecsTestData2,
            };

            var collectValuesJobHandle = changeValuesJobs.Schedule(chunks.Length, 64);
            collectValuesJobHandle.Complete();

            var ecsTestDataDynamic = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData)));
            var ecsTestDataDynamic2 = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData2)));

            ulong foundValues = 0;
            for (int chunkIndex = 0; chunkIndex < chunks.Length; chunkIndex++)
            {
                var chunk = chunks[chunkIndex];
                var chunkCount = chunk.Count;

                Assert.AreEqual(4, math.ceilpow2(chunkCount - 1));

                var chunkEcsTestData = chunk.GetDynamicComponentDataArrayReinterpret<int>(ref ecsTestDataDynamic, UnsafeUtility.SizeOf<int>());
                var chunkEcsTestData2 = chunk.GetDynamicComponentDataArrayReinterpret<int2>(ref ecsTestDataDynamic2, UnsafeUtility.SizeOf<int2>());
                if (chunkEcsTestData.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        foundValues |= (ulong)1 << (chunkEcsTestData[i] - 100);
                    }
                }
                else if (chunkEcsTestData2.Length > 0)
                {
                    for (int i = 0; i < chunkCount; i++)
                    {
                        Assert.AreEqual(chunkEcsTestData2[i].x, chunkEcsTestData2[i].y);
                        foundValues |= (ulong)1 << (-chunkEcsTestData2[i].x - 1000);
                    }
                }
            }

            foundValues++;
            Assert.AreEqual(0, foundValues);
        }


        partial class UntypedBufferSystemBumpVersion : SystemBase
        {
            struct UpdateChunks : IJobParallelFor
            {
                public NativeArray<ArchetypeChunk> Chunks;
                public DynamicComponentTypeHandle ElementHandle;

                public void Execute(int chunkIndex)
                {
                    var chunk = Chunks[chunkIndex];
                    var ecsBufferAccessor = chunk.GetUntypedBufferAccessor(ref ElementHandle);
                    for (int i = 0; i < ecsBufferAccessor.Length; ++i)
                    {
                        unsafe
                        {
                            var buffer = ecsBufferAccessor.GetUnsafeReadOnlyPtrAndLength(i, out var len);
                            for(int e=0;e<len;++e)
                            {
                                ((int*) buffer)[e] = 0xBADF00D;
                            }
                        }
                    }
                }
            }
            protected override void OnUpdate()
            {
                var query = GetEntityQuery(typeof(EcsIntElement));
                var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                var updateChunksJob = new UpdateChunks
                {
                    Chunks = chunks,
                    ElementHandle = GetDynamicComponentTypeHandle(ComponentType.ReadWrite(typeof(EcsIntElement)))
                };
                var updateChunksJobHandle = updateChunksJob.Schedule(chunks.Length, 32);
                updateChunksJobHandle.Complete();
            }
        }

        [Test]
        public void ACS_UntypedBuffers()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var roComponentType = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsIntElement)));
            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];
                var accessor = chunk.GetUntypedBufferAccessor(ref roComponentType);
                Assert.IsTrue(accessor.Length != 0);
                Assert.IsTrue(accessor.ElementSize == UnsafeUtility.SizeOf<EcsIntElement>());
                for (int k = 0; k < accessor.Length; ++k)
                {
                    unsafe
                    {
                        var bufferPtr = (byte*) accessor.GetUnsafeReadOnlyPtrAndLength(k, out var length);
                        Assert.IsTrue(bufferPtr != null);
                        //Verify all accessors return the same values
                        Assert.IsTrue(bufferPtr == accessor.GetUnsafeReadOnlyPtr(k));
                        Assert.AreEqual(length, accessor.GetBufferLength(k));

                        //Check value contents by either case to int or EcsIntElement. Is also an example of proper api use
                        var bufferData = (int*) bufferPtr;
                        for (int n = 0; n < length; ++n)
                            Assert.AreEqual(n, bufferData[n], "buffer element does not have the expected value");

                        var elementSize = UnsafeUtility.SizeOf<EcsIntElement>();
                        for (int n = 0; n < length; ++n)
                        {
                            var ecsElement = UnsafeUtility.AsRef<EcsIntElement>((void*) bufferPtr);
                            Assert.AreEqual(n, ecsElement.Value, "buffer element does not have the expected value");
                            bufferPtr += elementSize;
                        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
                        //This should throw, since is a readonly handle
                        Assert.Throws<InvalidOperationException>(() =>
                        {
                            accessor.ResizeUninitialized(k, length + 1);
                        });
#endif
                    }
                }

                // Test resize
                var rwComponentType = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(typeof(EcsIntElement)));
                accessor = chunk.GetUntypedBufferAccessor(ref rwComponentType);
                for (int k = 0; k < accessor.Length; ++k)
                {
                    unsafe
                    {
                        //Increase the buffer size.
                        var oldLength = accessor.GetBufferLength(k);
                        var newLength = oldLength + 16;
                        accessor.ResizeUninitialized(k, newLength);
                        var bufferPtr = (byte*) accessor.GetUnsafePtrAndLength(k, out var length);

                        Assert.IsTrue(bufferPtr != null);
                        Assert.AreEqual(newLength, length);
                        Assert.GreaterOrEqual(accessor.GetBufferCapacity(k), newLength);
                        //Check content in range 0 - oldLen are the same
                        var bufferData = (int*) bufferPtr;
                        for (int n = 0; n < oldLength; ++n)
                            Assert.AreEqual(n, bufferData[n], "buffer element does not have the expected value");

                        //Shrink the buffer size. Check capacity is still the same
                        var oldCapacity = accessor.GetBufferCapacity(k);
                        accessor.ResizeUninitialized(k, 10);
                        Assert.IsTrue(accessor.GetUnsafePtr(k) != null);
                        Assert.AreEqual(10, accessor.GetBufferLength(k));
                        Assert.AreEqual(oldCapacity, accessor.GetBufferCapacity(k));
                        //Check content in range 0 - 10 are still the same
                        var toCheck = oldLength < 10 ? oldLength : 10;
                        bufferData = (int*) bufferPtr;
                        for (int n = 0; n < toCheck; ++n)
                            Assert.AreEqual(n, bufferData[n], "buffer element does not have the expected value");
                    }
                }
            }
        }

        [Test]
        public void ACS_UntypedBuffersBumpVersion()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(Allocator.Temp);
            group.Dispose();

            var versions = new NativeArray<uint>(chunks.Length, Allocator.Temp);
            var roComponentType = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsIntElement)));
            for (int i = 0; i < chunks.Length; ++i)
                versions[i] = chunks[i].GetChangeVersion(ref roComponentType);

            var system = World.CreateSystemManaged<UntypedBufferSystemBumpVersion>();
            system.Update();

            for (int i = 0; i < chunks.Length; ++i)
            {
                var newVersion = chunks[i].GetChangeVersion(ref roComponentType);
                Assert.AreNotEqual(versions[i], newVersion);
                bool afterDidAddChange = chunks[i].DidChange(ref roComponentType, versions[i]);
                Assert.IsTrue(afterDidAddChange, "DidChange() is false after modifications");
            }
        }

        struct UntypedBufferResizeJob : IJobChunk
        {
            public DynamicComponentTypeHandle typeHandle;
            public int size;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                var accessor = chunk.GetUntypedBufferAccessor(ref typeHandle);
                for (int i = 0; i < chunk.Count; ++i)
                {
                    //Resize and invalidate the secondary version.
                    accessor.ResizeUninitialized(i, size);
                }
            }
        }

        [Test]
        public void ACS_UntypedBuffersInvalidationWorks()
        {
            var entity = m_Manager.CreateEntity();
            var bufferRW = m_Manager.AddBuffer<EcsIntElement>(entity);
            bufferRW.Length = 1;

            var bufferLookup = m_Manager.GetBufferLookup<EcsIntElement>(true);
            var bufferRO = bufferLookup[entity];
            //grab also an alias
            var array = bufferRO.AsNativeArray();
            var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var typeHandle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite(typeof(EcsIntElement)));
            //Resize buffer with a job (on the main thread)
            new UntypedBufferResizeJob {
                typeHandle = typeHandle,
                size = 5
            }.Run(query);
            //old buffer references are still valid here. So no throw or errors
            Assert.AreEqual(5, bufferRO.Length);
            Assert.AreEqual(5, bufferRW.Length);
            Assert.DoesNotThrow(() =>
            {
                for (int i = 0; i < bufferRO.Length; ++i)  {
                    var o = bufferRO[i];
                }
            });

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            //But arrays should be invalidated
            Assert.Throws<ObjectDisposedException>(() =>
            {
                var el = array[0];
            });
#endif

            //Let's now run the job using schedule and check that is not possible to invalidate the buffer from the main
            //thread if they are assigned to to a job
            var jobHandle = new UntypedBufferResizeJob {
                typeHandle = typeHandle,
                size = 10
            }.Schedule(query, default);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.Throws<InvalidOperationException>(() => { bufferRW.Add(new EcsIntElement()); });
            Assert.Throws<InvalidOperationException>(() => { bufferRW.Length = 2; });
#endif
            jobHandle.Complete();
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        public void ACS_UntypeBufferAccessorIncorrectUse()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var bufferType = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsIntElement)));
            var componentType = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData)));

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];
                //Fail: cannot use GetDynamicComponentDataArrayReinterpret with IBufferElementData types
                Assert.Throws<ArgumentException>(() =>
                {
                    chunk.GetDynamicComponentDataArrayReinterpret<EcsIntElement>(ref bufferType, UnsafeUtility.SizeOf<EcsIntElement>());
                });

                //Fail: cannot use GetUntypedBufferAccessor with IComponentData types
                Assert.Throws<ArgumentException>(() =>
                {
                    chunk.GetUntypedBufferAccessor(ref componentType);
                });
            }
        }

        [Test]
        public void ACS_DynamicTypeHas()
        {
            CreateEntities(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsIntElement>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var ecsTestData = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData)));
            var missingElements = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData3)));

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];

                // Test Has(DynamicComponentTypeHandle)
                bool hasEcsTestData = chunk.Has(ref ecsTestData);
                Assert.IsTrue(hasEcsTestData, "Has(EcsTestData) should be true");
                bool hasMissingElements = chunk.Has(ref missingElements);
                Assert.IsFalse(hasMissingElements, "Has(EcsTestData3) should be false");
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        public void ACS_DynamicComponentDataArrayReinterpretIncorrectUse()
        {
            CreateEntities2(128);

            var group = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestData2>());
            var chunks = group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            group.Dispose();

            var ecsTestData = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData2)));

            for (int i = 0; i < chunks.Length; ++i)
            {
                var chunk = chunks[i];

                // Fail: not expected size
                Assert.Throws<InvalidOperationException>(() =>
                {
                    var chunkEcsTestData = chunk.GetDynamicComponentDataArrayReinterpret<int>(ref ecsTestData, UnsafeUtility.SizeOf<int>());
                });

                // If (Count * sizeof(int2)) % sizeof(int3) == 0 -> the test fail because the types can be aliased in that case.
                if ((chunk.Count * UnsafeUtility.SizeOf<int2>() % UnsafeUtility.SizeOf<int3>()) != 0)
                {
                    // Fail: not dividable by size
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        var chunkEcsTestData = chunk.GetDynamicComponentDataArrayReinterpret<int3>(ref ecsTestData, UnsafeUtility.SizeOf<int2>());
                    });
                }
                else
                {
                    Assert.DoesNotThrow(()=>
                    {
                        var chunkEcsTestData = chunk.GetDynamicComponentDataArrayReinterpret<int3>(ref ecsTestData, UnsafeUtility.SizeOf<int2>());
                    });
                }

            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        public void ACS_SetComponentEnabled_NonEnableableDynamicComponent_Throws()
        {
            var e = m_Manager.CreateEntity(typeof(EcsTestData));
            var chunk = m_Manager.GetChunk(e);
            var typeHandle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite<EcsTestData>());
            Assert.That(() => chunk.SetComponentEnabled(ref typeHandle, 0, false),
                Throws.ArgumentException.With.Message.Contains($"must implement {nameof(IEnableableComponent)}"));
        }

        [Test]
        public void ACS_SetComponentEnabledForAll_Works()
        {
            int entityCount = 120;
            CreateEnableableEntities(entityCount);

            var entityTypeHandle = m_Manager.GetEntityTypeHandle();
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var dynamicTypeHandle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite<EcsTestDataEnableable2>());
            var bufferTypeHandle = m_Manager.GetBufferTypeHandle<EcsIntElementEnableable>(false);

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>());
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, chunks.Length, "This test assumes all entities fit in a single chunk");
            var chunk = chunks[0];

            var entities = chunk.GetNativeArray(entityTypeHandle);
            chunk.SetComponentEnabledForAll(ref typeHandle, false);
            chunk.SetComponentEnabledForAll(ref dynamicTypeHandle, false);
            chunk.SetComponentEnabledForAll(ref bufferTypeHandle, false);
            for (int i = 0; i < entities.Length; ++i)
            {
                Assert.IsFalse(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]));
                Assert.IsFalse(m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(entities[i]));
                Assert.IsFalse(m_Manager.IsComponentEnabled<EcsIntElementEnableable>(entities[i]));
            }
            chunk.SetComponentEnabledForAll(ref typeHandle, true);
            chunk.SetComponentEnabledForAll(ref dynamicTypeHandle, true);
            chunk.SetComponentEnabledForAll(ref bufferTypeHandle, true);
            for (int i = 0; i < entities.Length; ++i)
            {
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]));
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(entities[i]));
                Assert.IsTrue(m_Manager.IsComponentEnabled<EcsIntElementEnableable>(entities[i]));
            }
        }

        [Test]
        public void ACS_IsComponentEnabled_SetComponentEnabled_Works()
        {
            int entityCount = 120;
            CreateEnableableEntities(entityCount);

            var entityTypeHandle = m_Manager.GetEntityTypeHandle();
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var dynamicTypeHandle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite<EcsTestDataEnableable2>());
            var bufferTypeHandle = m_Manager.GetBufferTypeHandle<EcsIntElementEnableable>(false);

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>());
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, chunks.Length, "This test assumes all entities fit in a single chunk");
            var chunk = chunks[0];

            var entities = chunk.GetNativeArray(entityTypeHandle);
            for (int i = 0; i < entities.Length; ++i)
            {
                bool enabled1 = (i % 2 == 0);
                bool enabled2 = (i % 3 == 0);
                bool enabled3 = (i % 5 == 0);
                chunk.SetComponentEnabled(ref typeHandle, i, enabled1);
                chunk.SetComponentEnabled(ref dynamicTypeHandle, i, enabled2);
                chunk.SetComponentEnabled(ref bufferTypeHandle, i, enabled3);
            }
            for (int i = 0; i < entities.Length; ++i)
            {
                bool expectedEnabled1 = (i % 2 == 0);
                bool expectedEnabled2 = (i % 3 == 0);
                bool expectedEnabled3 = (i % 5 == 0);
                Assert.AreEqual(expectedEnabled1, m_Manager.IsComponentEnabled<EcsTestDataEnableable>(entities[i]),
                    $"Entity {i} type 1 mismatch (expected {expectedEnabled1}, manager says {!expectedEnabled1}");
                Assert.AreEqual(expectedEnabled1, chunk.IsComponentEnabled(ref typeHandle, i),
                    $"Entity {i} type 1 mismatch (expected {expectedEnabled1}, chunk says {!expectedEnabled1}");
                Assert.AreEqual(expectedEnabled2, m_Manager.IsComponentEnabled<EcsTestDataEnableable2>(entities[i]),
                    $"Entity {i} type 2 mismatch (expected {expectedEnabled2}, manager says {!expectedEnabled2}");
                Assert.AreEqual(expectedEnabled2, chunk.IsComponentEnabled(ref dynamicTypeHandle, i),
                    $"Entity {i} type 2 mismatch (expected {expectedEnabled2}, chunk says {!expectedEnabled2}");
                Assert.AreEqual(expectedEnabled3, m_Manager.IsComponentEnabled<EcsIntElementEnableable>(entities[i]),
                    $"Entity {i} type 3 mismatch (expected {expectedEnabled3}, manager says {!expectedEnabled3}");
                Assert.AreEqual(expectedEnabled3, chunk.IsComponentEnabled(ref bufferTypeHandle, i),
                    $"Entity {i} type 3 mismatch (expected {expectedEnabled3}, chunk says {!expectedEnabled3}");
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires collections checks / debug checks")]
        public void ACS_IsComponentEnabled_SetComponentEnabled_OutOfRangeIndex_Throws()
        {
            int entityCount = 120;
            CreateEnableableEntities(entityCount);

            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(false);
            var dynamicTypeHandle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite<EcsTestDataEnableable2>());
            var bufferTypeHandle = m_Manager.GetBufferTypeHandle<EcsIntElementEnableable>(false);

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>());
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, chunks.Length, "This test assumes all entities fit in a single chunk");
            var chunk = chunks[0];

            Assert.Throws<ArgumentException>(() => chunk.IsComponentEnabled(ref typeHandle, -1));
            Assert.Throws<ArgumentException>(() => chunk.IsComponentEnabled(ref typeHandle, 128));
            Assert.Throws<ArgumentException>(() => chunk.IsComponentEnabled(ref dynamicTypeHandle, -1));
            Assert.Throws<ArgumentException>(() => chunk.IsComponentEnabled(ref dynamicTypeHandle, 128));
            Assert.Throws<ArgumentException>(() => chunk.IsComponentEnabled(ref bufferTypeHandle, -1));
            Assert.Throws<ArgumentException>(() => chunk.IsComponentEnabled(ref bufferTypeHandle, 128));

            // checking invalid entities inside the chunk is valid; it just returns false.
            Assert.IsFalse(chunk.IsComponentEnabled(ref typeHandle, entityCount));
            Assert.IsFalse(chunk.IsComponentEnabled(ref dynamicTypeHandle, entityCount));
            Assert.IsFalse(chunk.IsComponentEnabled(ref bufferTypeHandle, entityCount));

            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref typeHandle, -1, false));
            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref typeHandle, 128, false));
            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref dynamicTypeHandle, -1, false));
            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref dynamicTypeHandle, 128, false));
            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref bufferTypeHandle, -1, false));
            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref bufferTypeHandle, 128, false));

            // setting invalid entities inside the chunk is also valid, but a terrible idea.
            Assert.DoesNotThrow(() => chunk.SetComponentEnabled(ref typeHandle, entityCount, false));
            Assert.DoesNotThrow(() => chunk.SetComponentEnabled(ref dynamicTypeHandle, entityCount, false));
            Assert.DoesNotThrow(() => chunk.SetComponentEnabled(ref bufferTypeHandle, entityCount, false));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires safety handle checks")]
        public void ACS_SetComponentEnabled_ReadOnlyTypeHandle_Throws()
        {
            int entityCount = 120;
            CreateEnableableEntities(entityCount);

            var typeHandleRO = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            var dynamicTypeHandleRO = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly<EcsTestDataEnableable2>());
            var bufferTypeHandleRO = m_Manager.GetBufferTypeHandle<EcsIntElementEnableable>(true);

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>());
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, chunks.Length, "This test assumes all entities fit in a single chunk");
            var chunk = chunks[0];

            Assert.Throws<InvalidOperationException>(() => chunk.SetComponentEnabled(ref typeHandleRO, 0, false));
            Assert.Throws<InvalidOperationException>(() => chunk.SetComponentEnabled(ref dynamicTypeHandleRO, 0, false));
            Assert.Throws<InvalidOperationException>(() => chunk.SetComponentEnabled(ref bufferTypeHandleRO, 0, false));
        }

        [Test]
        public void ACS_IsComponentEnabled_ComponentNotPresent_ReturnsFalse()
        {
            int entityCount = 120;
            CreateEnableableEntities(entityCount);

            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable3>(false);
            var dynamicTypeHandle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite<EcsTestDataEnableable3>());
            var bufferTypeHandle = m_Manager.GetBufferTypeHandle<EcsIntElementEnableable3>(false);

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>());
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, chunks.Length, "This test assumes all entities fit in a single chunk");
            var chunk = chunks[0];

            Assert.IsFalse(chunk.IsComponentEnabled(ref typeHandle, 0));
            Assert.IsFalse(chunk.IsComponentEnabled(ref bufferTypeHandle, 0));
            Assert.IsFalse(chunk.IsComponentEnabled(ref dynamicTypeHandle, 0));
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires collections checks / debug checks")]
        public void ACS_SetComponentEnabled_ComponentNotPresent_Throws()
        {
            int entityCount = 120;
            CreateEnableableEntities(entityCount);

            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable3>(false);
            var dynamicTypeHandle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadWrite<EcsTestDataEnableable3>());
            var bufferTypeHandle = m_Manager.GetBufferTypeHandle<EcsIntElementEnableable3>(false);

            using var query = m_Manager.CreateEntityQuery(ComponentType.ReadWrite<EcsTestDataEnableable>());
            var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            Assert.AreEqual(1, chunks.Length, "This test assumes all entities fit in a single chunk");
            var chunk = chunks[0];

            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref typeHandle, 0, false));
            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref dynamicTypeHandle, 0, false));
            Assert.Throws<ArgumentException>(() => chunk.SetComponentEnabled(ref bufferTypeHandle, 0, false));
        }

        [Test]
        public unsafe void ArchetypeChunk_GetComponentDataPtr_MatchesNativeArrayPtr()
        {
            CreateEntities(1000);
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
                using var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                foreach(var archetypeChunk in chunks)
                {
                    var roArrayPtr = archetypeChunk.GetNativeArray(ref typeHandle).GetUnsafePtr();

                    var roRawPtr = archetypeChunk.GetComponentDataPtrRO(ref typeHandle);
                    Assert.AreEqual((ulong)roArrayPtr, (ulong)roRawPtr);
                    var rwRawPtr = archetypeChunk.GetComponentDataPtrRW(ref typeHandle);
                    Assert.AreEqual((ulong)roArrayPtr, (ulong)rwRawPtr);
                }
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires data validation checks")]
        public unsafe void ArchetypeChunk_GetComponentDataPtrRW_ThrowsOnReadOnlyHandle()
        {
            CreateEntities(1000);
            using(var query = m_Manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData>(true);
                using var chunks = query.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
                foreach(var archetypeChunk in chunks)
                {
                    Assert.Throws<InvalidOperationException>(() =>
                    {
                        archetypeChunk.GetComponentDataPtrRW(ref typeHandle);
                    });
                }
            }
        }

        partial class TypeHandleUpdateDummySystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var ent = EntityManager.CreateEntity(typeof(EcsTestData));
                EntityManager.SetComponentData(ent, new EcsTestData {value = 17});
            }
        }
        partial class TypeHandleUpdateSystem : SystemBase
        {
            private ComponentTypeHandle<EcsTestData> typeHandle1;

            protected override void OnCreate()
            {
                typeHandle1 = GetComponentTypeHandle<EcsTestData>();
            }

            protected override void OnUpdate()
            {
                var typeHandle2 = GetComponentTypeHandle<EcsTestData>();
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(typeHandle2.GlobalSystemVersion, typeHandle1.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreNotEqual(typeHandle2.m_Safety, typeHandle1.m_Safety);
#endif
                // After updating the cached handle, these values (and the handles as a whole) should match
                typeHandle1.Update(this);
                Assert.AreEqual(typeHandle2.GlobalSystemVersion, typeHandle1.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(typeHandle2.m_Safety, typeHandle1.m_Safety);
#endif
            }
        }

        [Test]
        public void ComponentTypeHandle_SystemBase_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<TypeHandleUpdateDummySystem>();
            var sys = World.CreateSystemManaged<TypeHandleUpdateSystem>();
            World.Update();
            World.Update();
            World.Update();
        }

        partial struct TypeHandleUpdateSystemUnmanaged : ISystem
        {
            private ComponentTypeHandle<EcsTestData> typeHandle1;

            public void OnCreate(ref SystemState state)
            {
                typeHandle1 = state.GetComponentTypeHandle<EcsTestData>();
            }

            public void OnUpdate(ref SystemState state)
            {
                var typeHandle2 = state.GetComponentTypeHandle<EcsTestData>();
                // A cached handle is not guaranteed to match a newly-created handle if other systems have run in the interim.
                Assert.AreNotEqual(typeHandle2.GlobalSystemVersion, typeHandle1.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreNotEqual(typeHandle2.m_Safety, typeHandle1.m_Safety);
#endif
                // After updating the cached handle, these values (and the handles as a whole) should match
                typeHandle1.Update(ref state);
                Assert.AreEqual(typeHandle2.GlobalSystemVersion, typeHandle1.GlobalSystemVersion);
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                Assert.AreEqual(typeHandle2.m_Safety, typeHandle1.m_Safety);
#endif
            }
        }

        [Test]
        public void ComponentTypeHandle_ISystem_UpdateWorks()
        {
            var dummy = World.CreateSystemManaged<TypeHandleUpdateDummySystem>();
            var sys = World.CreateSystem<TypeHandleUpdateSystemUnmanaged>();
            World.Update();
            World.Update();
            World.Update();
        }

        struct ComponentTypeHandleContainerJob : IJob
        {
            public ComponentTypeHandle<EcsTestContainerData> handle;

            public void Execute() { }
        }

        struct ComponentTypeHandleJob : IJob
        {
            public ComponentTypeHandle<EcsTestData> handle;

            public void Execute() { }
        }

        [Test]
        public unsafe void ComponentTypeHandle_Component_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData { value = 17 });

            var handle = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.GetComponentDataPtrRO(ref handle)[0].value, 17);
        }

        [Test]
        public unsafe void ComponentTypeHandle_ComponentWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestContainerData));
            var component = new EcsTestContainerData();
            component.Create();
            m_Manager.SetComponentData(entity, component);

            var handle = m_Manager.GetComponentTypeHandle<EcsTestContainerData>(false);
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.GetComponentDataPtrRO(ref handle)[0], component);
            Assert.AreEqual(chunk.GetComponentDataPtrRO(ref handle)[0].data[1], component.data[1]);

            component.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void ComponentTypeHandle_ComponentWithContainerInJob_Throws()
        {
            var job = new ComponentTypeHandleContainerJob();
            job.handle = m_Manager.GetComponentTypeHandle<EcsTestContainerData>(false);
            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void ComponentTypeHandle_InJob_Works()
        {
            var job = new ComponentTypeHandleJob();
            job.handle = m_Manager.GetComponentTypeHandle<EcsTestData>(false);
            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void ComponentTypeHandle_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var chunkComponentType = m_Manager.GetComponentTypeHandle<EcsTestData>(true);

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates ComponentTypeHandle

            var chunk = m_Manager.GetChunk(entity);
            Assert.That(() => { chunk.GetChunkComponentData(ref chunkComponentType); },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains(
                        "ComponentTypeHandle<Unity.Entities.Tests.EcsTestData> which has been invalidated by a structural change."));
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void ComponentTypeHandle_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var chunkComponentType = m_Manager.GetComponentTypeHandle<EcsTestData>(false);

            var changeValuesJobs = new ComponentTypeHandleJob
            {
                handle = chunkComponentType,
            };

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates ComponentTypeHandle

            Assert.That(() => { changeValuesJobs.Run(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains(
                        "ComponentTypeHandle<Unity.Entities.Tests.EcsTestData> ComponentTypeHandleJob.handle which has been invalidated by a structural change."));
        }

        struct DynamicComponentTypeHandleJob : IJob
        {
            public DynamicComponentTypeHandle handle;
            public void Execute() { }
        }

        [Test]
        public void DynamicComponentTypeHandle_Component_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData { value = 17 });

            var handle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData)));
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.GetDynamicComponentDataArrayReinterpret<EcsTestData>(ref handle, UnsafeUtility.SizeOf<EcsTestData>())[0].value, 17);
        }

        [Test]
        public void DynamicComponentTypeHandle_ComponentWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestContainerData));
            var component = new EcsTestContainerData();
            component.Create();
            m_Manager.SetComponentData(entity, component);

            var handle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestContainerData)));
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.GetDynamicComponentDataArrayReinterpret<EcsTestContainerData>(ref handle, UnsafeUtility.SizeOf<EcsTestContainerData>())[0], component);
            Assert.AreEqual(chunk.GetDynamicComponentDataArrayReinterpret<EcsTestContainerData>(ref handle, UnsafeUtility.SizeOf<EcsTestContainerData>())[0].data[1], component.data[1]);

            component.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void DynamicComponentTypeHandle_ComponentWithContainerInJob_Throws()
        {
            var job = new DynamicComponentTypeHandleJob();
            job.handle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestContainerData)));
            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void DynamicComponentTypeHandle_InJob_Works()
        {
            var job = new DynamicComponentTypeHandleJob();
            job.handle = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData)));
            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void DynamicComponentTypeHandle_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var chunkComponentType = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData)));

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates DynamicComponentTypeHandle

            var chunk = m_Manager.GetChunk(entity);
            Assert.That(() => { chunk.GetDynamicComponentDataArrayReinterpret<int>(ref chunkComponentType, UnsafeUtility.SizeOf<int>()); },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains("Unity.Entities.DynamicComponentTypeHandle which has been invalidated by a structural change"));
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void DynamicComponentTypeHandle_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var chunkComponentType = m_Manager.GetDynamicComponentTypeHandle(ComponentType.ReadOnly(typeof(EcsTestData)));

            var changeValuesJobs = new DynamicComponentTypeHandleJob
            {
                handle = chunkComponentType,
            };

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates DynamicComponentTypeHandle

            Assert.That(() => { changeValuesJobs.Run(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains("Unity.Entities.DynamicComponentTypeHandle DynamicComponentTypeHandleJob.handle which has been invalidated by a structural change."));
        }

        struct DynamicSharedComponentTypeHandleJob : IJob
        {
            public DynamicSharedComponentTypeHandle handle;
            public void Execute() { }
        }

        [Test]
        public unsafe void DynamicSharedComponentTypeHandle_Component_Works()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(17));
            var handle = m_Manager.GetDynamicSharedComponentTypeHandle(typeof(EcsTestSharedComp));
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(((EcsTestSharedComp*)chunk.GetDynamicSharedComponentDataAddress(ref handle))->value, 17);
        }

        [Test]
        public unsafe void DynamicSharedComponentTypeHandle_ComponentWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity();
            var component = new EcsTestContainerSharedComp();
            component.Create();
            m_Manager.AddSharedComponentManaged(entity, component);

            var handle = m_Manager.GetDynamicSharedComponentTypeHandle(typeof(EcsTestContainerSharedComp));
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual((*(EcsTestContainerSharedComp*)chunk.GetDynamicSharedComponentDataAddress(ref handle)), component);
            Assert.AreEqual(((EcsTestContainerSharedComp*)chunk.GetDynamicSharedComponentDataAddress(ref handle))->data[1], component.data[1]);

            component.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void DynamicSharedComponentTypeHandle_ComponentWithContainerInJob_Throws()
        {
            var job = new DynamicSharedComponentTypeHandleJob();
            job.handle = m_Manager.GetDynamicSharedComponentTypeHandle(typeof(EcsTestContainerSharedComp));
            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void DynamicSharedComponentTypeHandle_InJob_Works()
        {
            var job = new DynamicSharedComponentTypeHandleJob();
            job.handle = m_Manager.GetDynamicSharedComponentTypeHandle(typeof(EcsTestSharedComp));
            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void DynamicSharedComponentTypeHandle_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(17));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var ecsTestData = m_Manager.GetDynamicSharedComponentTypeHandle(typeof(EcsTestSharedComp));

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates DynamicSharedComponentTypeHandle

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // No main-thread code currently references SharedComponentTypeHandle.m_Safety, so we have to manually verify that it's been invalidated
            Assert.That(() => { AtomicSafetyHandle.CheckReadAndThrow(ecsTestData.m_Safety); },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains(
                        "DynamicSharedComponentTypeHandle which has been invalidated by a structural change."));
#endif
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void DynamicSharedComponentTypeHandle_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(17));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var ecsTestData = m_Manager.GetDynamicSharedComponentTypeHandle(typeof(EcsTestSharedComp));

            var changeValuesJobs = new DynamicSharedComponentTypeHandleJob
            {
                handle = ecsTestData,
            };

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates SharedComponentTypeHandle

            Assert.That(() => { changeValuesJobs.Run(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains(
                        "DynamicSharedComponentTypeHandle DynamicSharedComponentTypeHandleJob.handle which has been invalidated by a structural change."));
        }

        struct BufferTypeHandleContainerJob : IJob
        {
            public BufferTypeHandle<EcsTestContainerElement> handle;

            public void Execute() { }
        }

        struct BufferTypeHandleJob : IJob
        {
            public BufferTypeHandle<EcsIntElement> handle;

            public void Execute() { }
        }

        [Test]
        public void BufferTypeHandle_ElementWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity();
            var element = new EcsTestContainerElement();
            element.Create();
            m_Manager.AddBuffer<EcsTestContainerElement>(entity);
            m_Manager.GetBuffer<EcsTestContainerElement>(entity).Add(element);

            var handle = m_Manager.GetBufferTypeHandle<EcsTestContainerElement>(false);
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.GetBufferAccessor(ref handle)[0][0], element);
            Assert.AreEqual(chunk.GetBufferAccessor(ref handle)[0][0].data[1], element.data[1]);

            element.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void BufferTypeHandle_ElementWithContainerInJob_Throws()
        {
            var job = new BufferTypeHandleContainerJob();
            var entity = m_Manager.CreateEntity();
            var element = new EcsTestContainerElement();
            m_Manager.AddBuffer<EcsTestContainerElement>(entity);
            m_Manager.GetBuffer<EcsTestContainerElement>(entity).Add(element);

            job.handle = m_Manager.GetBufferTypeHandle<EcsTestContainerElement>(false);

            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void BufferTypeHandle_InJob_Works()
        {
            var job = new BufferTypeHandleJob();
            var entity = m_Manager.CreateEntity();
            var element = new EcsIntElement();
            m_Manager.AddBuffer<EcsIntElement>(entity);
            m_Manager.GetBuffer<EcsIntElement>(entity).Add(element);

            job.handle = m_Manager.GetBufferTypeHandle<EcsIntElement>(false);

            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void BufferTypeHandle_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var ecsTestData = m_Manager.GetBufferTypeHandle<EcsIntElement>(false);

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates BufferTypeHandle

            var chunk = m_Manager.GetChunk(entity);
            Assert.That(() => { chunk.GetBufferAccessor(ref ecsTestData); },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains(
                        "BufferTypeHandle<Unity.Entities.Tests.EcsIntElement> which has been invalidated by a structural change."));
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void BufferTypeHandle_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsIntElement));
            var ecsTestData = m_Manager.GetBufferTypeHandle<EcsIntElement>(false);

            var changeValuesJobs = new BufferTypeHandleJob
            {
                handle = ecsTestData,
            };

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates BufferTypeHandle

            Assert.That(() => { changeValuesJobs.Run(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains(
                        "BufferTypeHandle<Unity.Entities.Tests.EcsIntElement> BufferTypeHandleJob.handle which has been invalidated by a structural change."));
        }

        struct SharedComponentTypeHandleJob : IJob
        {
            public SharedComponentTypeHandle<EcsTestSharedComp> handle;

            public void Execute() { }
        }

        struct SharedComponentTypeHandleContainerJob : IJob
        {
            public SharedComponentTypeHandle<EcsTestContainerSharedComp> handle;

            public void Execute() { }
        }

        [Test]
        public unsafe void SharedComponentTypeHandle_Component_Works()
        {
            var entity = m_Manager.CreateEntity();
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(17));
            var handle = m_Manager.GetSharedComponentTypeHandle<EcsTestSharedComp>();
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.GetSharedComponent(handle).value, 17);
        }

        [Test]
        public unsafe void SharedComponentTypeHandle_ComponentWithContainer_Works()
        {
            var entity = m_Manager.CreateEntity();
            var component = new EcsTestContainerSharedComp();
            component.Create();
            m_Manager.AddSharedComponentManaged(entity, component);

            var handle = m_Manager.GetSharedComponentTypeHandle<EcsTestContainerSharedComp>();
            var chunk = m_Manager.GetChunk(entity);

            Assert.AreEqual(chunk.GetSharedComponent(handle), component);
            Assert.AreEqual(chunk.GetSharedComponent(handle).data[1], component.data[1]);

            component.Destroy();
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void SharedComponentTypeHandle_ComponentWithContainerInJob_Throws()
        {
            var job = new SharedComponentTypeHandleContainerJob();
            job.handle = m_Manager.GetSharedComponentTypeHandle<EcsTestContainerSharedComp>();
            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void SharedComponentTypeHandle_InJob_Works()
        {
            var job = new SharedComponentTypeHandleJob();
            job.handle = m_Manager.GetSharedComponentTypeHandle<EcsTestSharedComp>();
            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void SharedComponentTypeHandle_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(17));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var ecsTestData = m_Manager.GetSharedComponentTypeHandle<EcsTestSharedComp>();

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates SharedComponentTypeHandle

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // No main-thread code currently references SharedComponentTypeHandle.m_Safety, so we have to manually verify that it's been invalidated
            Assert.That(() => { AtomicSafetyHandle.CheckReadAndThrow(ecsTestData.m_Safety); },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains(
                        "SharedComponentTypeHandle<Unity.Entities.Tests.EcsTestSharedComp> which has been invalidated by a structural change."));
#endif
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void SharedComponentTypeHandle_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.AddSharedComponentManaged(entity, new EcsTestSharedComp(17));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var ecsTestData = m_Manager.GetSharedComponentTypeHandle<EcsTestSharedComp>();

            var changeValuesJobs = new SharedComponentTypeHandleJob
            {
                handle = ecsTestData,
            };

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates SharedComponentTypeHandle

            Assert.That(() => { changeValuesJobs.Run(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains(
                        "SharedComponentTypeHandle<Unity.Entities.Tests.EcsTestSharedComp> SharedComponentTypeHandleJob.handle which has been invalidated by a structural change."));
        }

        struct BufferAccessorContainerJob : IJob
        {
            public BufferAccessor<EcsTestContainerElement> accessor;

            public void Execute() { }
        }

        struct BufferAccessorJob : IJob
        {
            public BufferAccessor<EcsIntElement> accessor;

            public void Execute() { }
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on jobs debugger")]
        public void BufferAccessor_ElementWithContainerInJob_Throws()
        {
            var job = new BufferAccessorContainerJob();
            var entity = m_Manager.CreateEntity();
            var element = new EcsTestContainerElement();
            m_Manager.AddBuffer<EcsTestContainerElement>(entity);
            m_Manager.GetBuffer<EcsTestContainerElement>(entity).Add(element);

            var chunk = m_Manager.GetChunk(entity);
            var handle = m_Manager.GetBufferTypeHandle<EcsTestContainerElement>(false);

            job.accessor = chunk.GetBufferAccessor(ref handle);

            var e = Assert.Throws<InvalidOperationException>(() => job.Schedule());
            Assert.IsTrue(e.Message.Contains("Nested native containers are illegal in jobs"));
        }

        [Test]
        public void BufferAccessor_InJob_Works()
        {
            var job = new BufferAccessorJob();
            var entity = m_Manager.CreateEntity();
            var element = new EcsIntElement();
            m_Manager.AddBuffer<EcsIntElement>(entity);
            m_Manager.GetBuffer<EcsIntElement>(entity).Add(element);

            var chunk = m_Manager.GetChunk(entity);
            var handle = m_Manager.GetBufferTypeHandle<EcsIntElement>(false);

            job.accessor = chunk.GetBufferAccessor(ref handle);

            Assert.DoesNotThrow(() => job.Schedule().Complete());
        }

        struct UseEntityTypeHandle : IJob
        {
            public EntityTypeHandle ecsTestData;
            public void Execute()
            {
            }
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void EntityTypeHandle_UseAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var chunkEntityType = m_Manager.GetEntityTypeHandle();

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates EntityTypeHandle

            var chunk = m_Manager.GetChunk(entity);
            Assert.That(() => { chunk.GetNativeArray(chunkEntityType); },
                Throws.Exception.TypeOf<ObjectDisposedException>()
                    .With.Message.Contains(
                        "Unity.Entities.EntityTypeHandle which has been invalidated by a structural change."));
        }

        [Test]
        [TestRequiresCollectionChecks("Relies on static safety id system")]
        public void EntityTypeHandle_UseFromJobAfterStructuralChange_ThrowsCustomErrorMessage()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(entity, new EcsTestData(42));
            var chunkEntityType = m_Manager.GetEntityTypeHandle();

            var changeValuesJobs = new UseEntityTypeHandle
            {
                ecsTestData = chunkEntityType,
            };

            m_Manager.AddComponent<EcsTestData2>(entity); // invalidates EntityTypeHandle

            Assert.That(() => { changeValuesJobs.Run(); },
                Throws.Exception.TypeOf<InvalidOperationException>()
                    .With.Message.Contains(
                        "Unity.Entities.EntityTypeHandle UseEntityTypeHandle.ecsTestData which has been invalidated by a structural change."));
        }
    }
}
