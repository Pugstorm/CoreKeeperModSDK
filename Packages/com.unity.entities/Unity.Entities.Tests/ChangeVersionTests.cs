using NUnit.Framework;
using Unity.Collections;
using Unity.Jobs;

namespace Unity.Entities.Tests
{
    //@TODO: It s a bit annoying that the debug name of this system is longer than 64 characters and thus the name won't match when  a subclass of the test?
    // Maybe we should use a different string storage for the system debug names?
    partial class BumpChunkTypeVersionSystem : SystemBase
    {
        struct UpdateChunks : IJobParallelFor
        {
            public NativeArray<ArchetypeChunk> Chunks;
            public ComponentTypeHandle<EcsTestData> EcsTestDataTypeHandle;

            public void Execute(int chunkIndex)
            {
                var chunk = Chunks[chunkIndex];
                var ecsTestData = chunk.GetNativeArray(ref EcsTestDataTypeHandle);
                for (int i = 0; i < chunk.Count; i++)
                {
                    ecsTestData[i] = new EcsTestData {value = ecsTestData[i].value + 1};
                }
            }
        }

        EntityQuery m_Group;
        private bool m_LastAllChanged;

        protected override void OnCreate()
        {
            m_Group = GetEntityQuery(typeof(EcsTestData));
            m_LastAllChanged = false;
        }

        protected override void OnUpdate()
        {
            var chunks = m_Group.ToArchetypeChunkArray(World.UpdateAllocator.ToAllocator);
            var ecsTestDataType = GetComponentTypeHandle<EcsTestData>();
            var updateChunksJob = new UpdateChunks
            {
                Chunks = chunks,
                EcsTestDataTypeHandle = ecsTestDataType
            };
            var updateChunksJobHandle = updateChunksJob.Schedule(chunks.Length, 32, Dependency);
            updateChunksJobHandle.Complete();

            // LastSystemVersion bumped after update. Check for change
            // needs to occur inside system update.
            m_LastAllChanged = true;
            for (int i = 0; i < chunks.Length; i++)
            {
                m_LastAllChanged &= chunks[i].DidChange(ref ecsTestDataType, LastSystemVersion);
            }
        }

        public bool AllEcsTestDataChunksChanged()
        {
            return m_LastAllChanged;
        }
    }

    partial class ChangeVersionTests : ECSTestsFixture
    {
        partial class BumpVersionSystemInJob : SystemBase
        {
            JobHandle UpdateEcsTestData2()
            {
                return
                    Entities
                        .ForEach((ref EcsTestData data, ref EcsTestData2 data2) =>
                        {
                            data2 = new EcsTestData2 { value0 = 10 };
                        })
                        .Schedule(default);
            }

            protected override void OnUpdate()
            {
                UpdateEcsTestData2().Complete();
            }

            protected override void OnCreate()
            {
            }
        }

        partial class BumpVersionSystem : SystemBase
        {
            public EntityQuery m_Group;

            protected override void OnUpdate()
            {
                var data = m_Group.ToComponentDataArray<EcsTestData>(World.UpdateAllocator.ToAllocator);
                var data2 = m_Group.ToComponentDataArray<EcsTestData2>(World.UpdateAllocator.ToAllocator);

                for (int i = 0; i < data.Length; ++i)
                {
                    var d2 = data2[i];
                    d2.value0 = 10;
                    data2[i] = d2;
                }

                m_Group.CopyFromComponentDataArray(data);
                m_Group.CopyFromComponentDataArray(data2);
            }

            protected override void OnCreate()
            {
                m_Group = GetEntityQuery(ComponentType.ReadWrite<EcsTestData>(),
                    ComponentType.ReadWrite<EcsTestData2>());
            }
        }



        [Test]
        public void CHG_BumpValueChangesChunkTypeVersion()
        {
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var bumpChunkTypeVersionSystem = World.CreateSystemManaged<BumpChunkTypeVersionSystem>();

            bumpChunkTypeVersionSystem.Update();
            Assert.AreEqual(true, bumpChunkTypeVersionSystem.AllEcsTestDataChunksChanged());

            bumpChunkTypeVersionSystem.Update();
            Assert.AreEqual(true, bumpChunkTypeVersionSystem.AllEcsTestDataChunksChanged());
        }

        [Test]
        public void GetLastWriterSystemName()
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            var bumpChunkTypeVersionSystem = World.CreateSystemManaged<BumpChunkTypeVersionSystem>();
            bumpChunkTypeVersionSystem.Update();

            var systemNotFound = EntityManager.EntityManagerDebug.GetLastWriterSystemName(m_Manager.GetChunk(entity), typeof(EcsTestData2));
            Assert.AreEqual("Couldn't find the system that modified the chunk.", systemNotFound);

            var componentMissing = EntityManager.EntityManagerDebug.GetLastWriterSystemName(m_Manager.GetChunk(entity), typeof(EcsTestData3));
            Assert.AreEqual($"'{typeof(EcsTestData3)}' was not present on the chunk.", componentMissing);

            var name = EntityManager.EntityManagerDebug.GetLastWriterSystemName(m_Manager.GetChunk(entity), typeof(EcsTestData));
            Assert.AreEqual(TypeManager.GetSystemName(typeof(BumpChunkTypeVersionSystem)), name);
        }
        [Test]
        public void CHG_SystemVersionZeroWhenNotRun()
        {
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var system = World.CreateSystemManaged<BumpVersionSystem>();
            Assert.AreEqual(0, system.LastSystemVersion);
            system.Update();
            Assert.AreNotEqual(0, system.LastSystemVersion);
        }

        partial class ChunkDidChangeManagedSystem : SystemBase
        {
            ComponentTypeHandle<EcsTestData> m_ComponentTypeHandle;
            EntityQuery m_Query;

            public bool DidChange { get; private set; }

            protected override void OnCreate()
            {
                m_ComponentTypeHandle = GetComponentTypeHandle<EcsTestData>();
                m_Query = GetEntityQuery(typeof(EcsTestData));
            }

            protected override void OnUpdate()
            {
                using (var chunks = m_Query.ToArchetypeChunkArray(Allocator.Temp))
                {
                    Assert.That(chunks.Length, Is.EqualTo(1));
                    DidChange = chunks[0].DidChange(ref m_ComponentTypeHandle, LastSystemVersion);
                }
            }
        }

        [Test]
        public void Chunk_DidChange_ManagedSystemDetectsChange()
        {
            var system = World.GetOrCreateSystemManaged<ChunkDidChangeManagedSystem>();
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            system.Update();
            Assert.That(system.DidChange, Is.True);

            system.Update();
            Assert.That(system.DidChange, Is.False);

            system.Update();
            Assert.That(system.DidChange, Is.False);

            m_Manager.SetComponentData(entity, new EcsTestData { value = 1 });

            system.Update();
            Assert.That(system.DidChange, Is.True);

            system.Update();
            Assert.That(system.DidChange, Is.False);

            system.Update();
            Assert.That(system.DidChange, Is.False);
        }

        partial struct ChunkDidChangeUnmanagedSystem : ISystem
        {
            ComponentTypeHandle<EcsTestData> m_ComponentTypeHandle;
            EntityQuery m_Query;

            public bool DidChange { get; private set; }

            public void OnCreate(ref SystemState state)
            {
                m_ComponentTypeHandle = state.GetComponentTypeHandle<EcsTestData>();
                m_Query = state.GetEntityQuery(typeof(EcsTestData));
            }

            public void OnUpdate(ref SystemState state)
            {
                using (var chunks = m_Query.ToArchetypeChunkArray(Allocator.Temp))
                {
                    Assert.That(chunks.Length, Is.EqualTo(1));
                    DidChange = chunks[0].DidChange(ref m_ComponentTypeHandle, state.LastSystemVersion);
                }
            }
        }

        [Test]
        public unsafe void Chunk_DidChange_UnmanagedSystemDetectsChange()
        {
            var system = World.GetOrCreateSystem<ChunkDidChangeUnmanagedSystem>();
            ref var systemRef = ref World.Unmanaged.GetUnsafeSystemRef<ChunkDidChangeUnmanagedSystem>(system);
            var entity = m_Manager.CreateEntity(typeof(EcsTestData));

            system.Update(World.Unmanaged);
            Assert.That(systemRef.DidChange, Is.True);

            system.Update(World.Unmanaged);
            Assert.That(systemRef.DidChange, Is.False);

            system.Update(World.Unmanaged);
            Assert.That(systemRef.DidChange, Is.False);

            m_Manager.SetComponentData(entity, new EcsTestData { value = 1 });

            system.Update(World.Unmanaged);
            Assert.That(systemRef.DidChange, Is.True);

            system.Update(World.Unmanaged);
            Assert.That(systemRef.DidChange, Is.False);

            system.Update(World.Unmanaged);
            Assert.That(systemRef.DidChange, Is.False);
        }

        partial class DidChangeTestSystem : SystemBase
        {
            protected override void OnUpdate()
            {
                var bfe = GetBufferLookup<EcsIntElement>(true);
                var componentLookup = GetComponentLookup<EcsTestData>(true);
                uint lastSysVersion = LastSystemVersion;
                Entities
                    .WithAll<EcsTestData, EcsIntElement>()
                    .ForEach((Entity e, ref EcsTestData2 changed) =>
                    {
                        changed.value0 = componentLookup.DidChange(e, lastSysVersion) ? 1 : 0;
                        changed.value1 = bfe.DidChange(e, lastSysVersion) ? 1 : 0;
                    }).Run();
            }
        }

        partial class ChangeEntitiesWithTag : SystemBase
        {
            protected override void OnUpdate()
            {
                Entities
                    .WithAll<EcsTestTag>()
                    .ForEach((Entity e, ref EcsTestData testData, ref DynamicBuffer<EcsIntElement> buf) =>
                    {
                        testData.value += 10;
                        buf.Add(new EcsIntElement{Value=17});
                    }).Run();
            }
        }

        [Test]
        public void ComponentLookup_DidChange_DetectsChanges()
        {
            var archetype = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsIntElement), typeof(EcsTestData2));
            int entityCount = 10;
            var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Temp);
            for(int i=0; i<entityCount; ++i)
            {
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestTag>(entities[i]);
            }

            var detectChangesSys = World.CreateSystemManaged<DidChangeTestSystem>();
            var changeEntitiesWithTagSys = World.CreateSystemManaged<ChangeEntitiesWithTag>();

            // First update: all elements "changed"
            detectChangesSys.Update();
            foreach(var ent in entities)
                Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestData2>(ent).value0);

            // Second update: no changes
            detectChangesSys.Update();
            foreach(var ent in entities)
                Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(ent).value0);

            // Modify only entities with the EcsTestTag.
            changeEntitiesWithTagSys.Update();

            // Third update: has EcsTestTag -> non-zero EcsTestData2.value0
            detectChangesSys.Update();
            foreach (var ent in entities)
            {
                bool hasTag = m_Manager.HasComponent<EcsTestTag>(ent);
                Assert.AreEqual(hasTag, m_Manager.GetComponentData<EcsTestData2>(ent).value0 != 0);
            }

            entities.Dispose();
        }

        [Test]
        public void BufferLookup_DidChange_DetectsChanges()
        {
            var archetype =
                m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsIntElement), typeof(EcsTestData2));
            int entityCount = 10;
            var entities = m_Manager.CreateEntity(archetype, entityCount, Allocator.Temp);
            for(int i=0; i<entityCount; ++i)
            {
                if (i % 2 == 0)
                    m_Manager.AddComponent<EcsTestTag>(entities[i]);
            }

            var detectChangesSys = World.CreateSystemManaged<DidChangeTestSystem>();
            var changeEntitiesWithTagSys = World.CreateSystemManaged<ChangeEntitiesWithTag>();

            // First update: all elements "changed"
            detectChangesSys.Update();
            foreach(var ent in entities)
                Assert.AreEqual(1, m_Manager.GetComponentData<EcsTestData2>(ent).value1);

            // Second update: no changes
            detectChangesSys.Update();
            foreach(var ent in entities)
                Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(ent).value1);

            // Modify only entities with the EcsTestTag.
            changeEntitiesWithTagSys.Update();

            // Third update: has EcsTestTag -> non-zero EcsTestData2.value1
            detectChangesSys.Update();
            foreach (var ent in entities)
            {
                bool hasTag = m_Manager.HasComponent<EcsTestTag>(ent);
                Assert.AreEqual(hasTag, m_Manager.GetComponentData<EcsTestData2>(ent).value1 != 0);
            }

            entities.Dispose();
        }

        [Test]
        public void CHG_SystemVersionJob()
        {
            m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            var system = World.CreateSystemManaged<BumpVersionSystemInJob>();
            Assert.AreEqual(0, system.LastSystemVersion);
            system.Update();
            Assert.AreNotEqual(0, system.LastSystemVersion);
        }

        [Test]
        public void AddComponent_BumpsChangeVersion()
        {
            m_Manager.Debug.SetGlobalSystemVersion(17);
            var e1 = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.Debug.SetGlobalSystemVersion(23);
            var e2 = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));
            uint srcVersion = 23;
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestData2>(true);
            m_Manager.Debug.SetGlobalSystemVersion(37);
            Assert.IsFalse(m_Manager.GetChunk(e1).DidChange(ref typeHandle, srcVersion));
            Assert.IsFalse(m_Manager.GetChunk(e2).DidChange(ref typeHandle, srcVersion));
            m_Manager.AddComponent<EcsTestData2>(e1);
            Assert.IsTrue(m_Manager.GetChunk(e1).DidChange(ref typeHandle, srcVersion));
            Assert.IsTrue(m_Manager.GetChunk(e2).DidChange(ref typeHandle, srcVersion));
        }

        [Test]
        public void EnableComponent_BumpsChangeVersion()
        {
            m_Manager.Debug.SetGlobalSystemVersion(17);
            var e = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            uint srcVersion = 17;
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            m_Manager.Debug.SetGlobalSystemVersion(23);
            Assert.IsFalse(m_Manager.GetChunk(e).DidChange(ref typeHandle, srcVersion));
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(e, false);
            Assert.IsTrue(m_Manager.GetChunk(e).DidChange(ref typeHandle, srcVersion));
        }

        [Test]
        public void EnableComponent_Query_BumpsChangeVersion()
        {
            m_Manager.Debug.SetGlobalSystemVersion(17);
            var e = m_Manager.CreateEntity(typeof(EcsTestDataEnableable));
            uint srcVersion = 17;
            var typeHandle = m_Manager.GetComponentTypeHandle<EcsTestDataEnableable>(true);
            m_Manager.Debug.SetGlobalSystemVersion(23);
            Assert.IsFalse(m_Manager.GetChunk(e).DidChange(ref typeHandle, srcVersion));
            using var query = new EntityQueryBuilder(Allocator.Temp)
                .WithPresent<EcsTestDataEnableable>()
                .Build(m_Manager);
            m_Manager.SetComponentEnabled<EcsTestDataEnableable>(query, false);
            Assert.IsTrue(m_Manager.GetChunk(e).DidChange(ref typeHandle, srcVersion));
        }
    }
}
