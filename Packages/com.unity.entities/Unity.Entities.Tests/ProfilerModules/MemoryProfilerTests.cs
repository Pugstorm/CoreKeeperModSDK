#if ENABLE_PROFILER && UNITY_EDITOR
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Profiling;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using UnityEngine.TestTools;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.MemoryProfiler;

namespace Unity.Entities.Tests
{
    [TestFixture]
    unsafe class MemoryProfilerTests : ECSTestsFixture
    {
        static readonly string s_DataFilePath = Path.Combine(Application.temporaryCachePath, "profilerdata");
        static readonly string s_RawDataFilePath = s_DataFilePath + ".raw";
        static bool s_LastCategoryEnabled;

        class ProfilerEnableScope : IDisposable
        {
            readonly bool m_Enabled;
            readonly bool m_EnableAllocationCallstacks;
            readonly bool m_EnableBinaryLog;
            readonly string m_LogFile;
            readonly ProfilerCategory m_Category;
            readonly bool m_CategoryEnabled;

            public ProfilerEnableScope(string dataFilePath, ProfilerCategory category)
            {
                m_Enabled = Profiler.enabled;
                m_EnableAllocationCallstacks = Profiler.enableAllocationCallstacks;
                m_EnableBinaryLog = Profiler.enableBinaryLog;
                m_LogFile = Profiler.logFile;
                m_Category = category;
                m_CategoryEnabled = Profiler.IsCategoryEnabled(category);

                Profiler.logFile = dataFilePath;
                Profiler.enableBinaryLog = true;
                Profiler.enableAllocationCallstacks = false;
                Profiler.enabled = true;
                Profiler.SetCategoryEnabled(category, true);
            }

            public void Dispose()
            {
                Profiler.enabled = m_Enabled;
                Profiler.enableAllocationCallstacks = m_EnableAllocationCallstacks;
                Profiler.enableBinaryLog = m_EnableBinaryLog;
                Profiler.logFile = m_LogFile;
                Profiler.SetCategoryEnabled(m_Category, m_CategoryEnabled);
            }
        }

        public override void Setup()
        {
            base.Setup();
            DefaultWorldInitialization.AddSystemsToRootLevelSystemGroups(World,
                DefaultWorldInitialization.GetAllSystems(WorldSystemFilterFlags.Default));
        }

        RawFrameDataView GenerateFrameMetaData(Action onUpdate = null)
        {
            EntitiesProfiler.Shutdown();
            EntitiesProfiler.Initialize();

            using (var scope = new ProfilerEnableScope(s_DataFilePath, MemoryProfiler.Category))
            {
                EntitiesProfiler.Update();
                onUpdate?.Invoke();
                World.Update();
                EntitiesProfiler.Update();
            }

            var loaded = ProfilerDriver.LoadProfile(s_RawDataFilePath, false);
            Assert.IsTrue(loaded);
            Assert.AreNotEqual(-1, ProfilerDriver.lastFrameIndex);

            return ProfilerDriver.GetRawFrameDataView(0, 0);
        }

        static IReadOnlyDictionary<ulong, WorldData> GetWorldsData(RawFrameDataView frame) =>
            GetSessionMetaData<WorldData>(frame, EntitiesProfiler.Guid, (int)DataTag.WorldData).Distinct().ToDictionary(x => x.SequenceNumber, x => x);

        static IReadOnlyDictionary<ulong, ArchetypeData> GetArchetypesData(RawFrameDataView frame) =>
            GetSessionMetaData<ArchetypeData>(frame, EntitiesProfiler.Guid, (int)DataTag.ArchetypeData).Distinct().ToDictionary(x => x.StableHash, x => x);

        static IEnumerable<ArchetypeMemoryData> GetArchetypeMemoryData(RawFrameDataView frame) =>
            GetFrameMetaData<ArchetypeMemoryData>(frame, MemoryProfiler.Guid, 0);

        static IEnumerable<T> GetSessionMetaData<T>(RawFrameDataView frame, Guid guid, int tag) where T : unmanaged
        {
            var metaDataCount = frame.GetSessionMetaDataCount(guid, tag);
            for (var metaDataIter = 0; metaDataIter < metaDataCount; ++metaDataIter)
            {
                var metaDataArray = frame.GetSessionMetaData<T>(guid, tag, metaDataIter);
                for (var i = 0; i < metaDataArray.Length; ++i)
                    yield return metaDataArray[i];
            }
        }

        static IEnumerable<T> GetFrameMetaData<T>(RawFrameDataView frame, Guid guid, int tag) where T : unmanaged
        {
            var metaDataCount = frame.GetFrameMetaDataCount(guid, tag);
            for (var metaDataIter = 0; metaDataIter < metaDataCount; ++metaDataIter)
            {
                var metaDataArray = frame.GetFrameMetaData<T>(guid, tag, metaDataIter);
                for (var i = 0; i < metaDataArray.Length; ++i)
                    yield return metaDataArray[i];
            }
        }

        [OneTimeSetUp]
        public void OneTimeSetup()
        {
            s_LastCategoryEnabled = Profiler.IsCategoryEnabled(MemoryProfiler.Category);
            Profiler.SetCategoryEnabled(MemoryProfiler.Category, true);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Profiler.SetCategoryEnabled(MemoryProfiler.Category, s_LastCategoryEnabled);
        }

        [Test]
        public void Uninitialized_DoesNotThrow()
        {
            MemoryProfiler.Shutdown();
            Assert.DoesNotThrow(() => MemoryProfiler.Update());
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4))]
#if !UNITY_WEBGL
        [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
        public void ArchetypeOnly(params Type[] types)
        {
            var archetype = default(EntityArchetype);
            var componentTypes = types.Select(t => new ComponentType(t)).ToArray();
            using (var frame = GenerateFrameMetaData(() => { archetype = m_Manager.CreateArchetype(componentTypes); }))
            {
                var worldsData = GetWorldsData(frame);
                var archetypesData = GetArchetypesData(frame);
                var archetypeMemoryData = GetArchetypeMemoryData(frame).First(x => x.StableHash == archetype.StableHash);

                Assert.That(worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData), Is.True);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(worldData.SequenceNumber, Is.EqualTo(World.SequenceNumber));

                Assert.That(archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData), Is.True);
                Assert.That(archetypeData.StableHash, Is.EqualTo(archetype.StableHash));
                Assert.That(archetypeData.ChunkCapacity, Is.EqualTo(archetype.ChunkCapacity));
                Assert.That(archetypeData.InstanceSize, Is.EqualTo(archetype.Archetype->InstanceSize));

                Assert.That(archetypeData.ComponentTypes.Length, Is.EqualTo(archetype.Archetype->TypesCount));
                for (var i = 0; i < archetypeData.ComponentTypes.Length; ++i)
                {
                    var typeIndex = archetype.Archetype->Types[i].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var componentTypeData = archetypeData.ComponentTypes[i];
                    Assert.That(componentTypeData.StableTypeHash, Is.EqualTo(typeInfo.StableTypeHash));
                    Assert.AreEqual(componentTypeData.Flags.HasFlag(ComponentTypeFlags.ChunkComponent), TypeManager.IsChunkComponent(typeIndex) ? true : false);
                }

                var allocatedBytes = archetype.ChunkCount * Chunk.kChunkSize;
                var unusedEntityCount = (archetype.ChunkCount * archetype.ChunkCapacity) - archetype.Archetype->EntityCount;
                var unusedBytes = unusedEntityCount * archetype.Archetype->InstanceSize;

                Assert.That(archetypeMemoryData.CalculateAllocatedBytes(), Is.EqualTo(allocatedBytes));
                Assert.That(archetypeMemoryData.CalculateUnusedBytes(archetypeData), Is.EqualTo(unusedBytes));
                Assert.That(archetypeMemoryData.EntityCount, Is.EqualTo(archetype.Archetype->EntityCount));
                Assert.That(archetypeMemoryData.CalculateUnusedEntityCount(archetypeData), Is.EqualTo(unusedEntityCount));
                Assert.That(archetypeMemoryData.ChunkCount, Is.EqualTo(archetype.ChunkCount));
                Assert.That(archetypeMemoryData.SegmentCount, Is.EqualTo(0));
            }
        }

        [TestCase(10, typeof(EcsTestData))]
        [TestCase(100, typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(1000, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(10000, typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData4))]
#if !UNITY_WEBGL
        [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
        public void ArchetypeWithEntities(int entityCount, params Type[] types)
        {
            var archetype = default(EntityArchetype);
            var componentTypes = types.Select(t => new ComponentType(t)).ToArray();
            using (var frame = GenerateFrameMetaData(() =>
            {
                archetype = m_Manager.CreateArchetype(componentTypes);
                m_Manager.CreateEntity(archetype, entityCount);
            }))
            {
                var worldsData = GetWorldsData(frame);
                var archetypesData = GetArchetypesData(frame);
                var archetypeMemoryData = GetArchetypeMemoryData(frame).First(x => x.StableHash == archetype.StableHash);

                Assert.That(worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData), Is.True);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(worldData.SequenceNumber, Is.EqualTo(World.SequenceNumber));

                Assert.That(archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData), Is.True);
                Assert.That(archetypeData.StableHash, Is.EqualTo(archetype.StableHash));
                Assert.That(archetypeData.ChunkCapacity, Is.EqualTo(archetype.ChunkCapacity));
                Assert.That(archetypeData.InstanceSize, Is.EqualTo(archetype.Archetype->InstanceSize));

                Assert.That(archetypeData.ComponentTypes.Length, Is.EqualTo(archetype.Archetype->TypesCount));
                for (var i = 0; i < archetypeData.ComponentTypes.Length; ++i)
                {
                    var typeIndex = archetype.Archetype->Types[i].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var componentTypeData = archetypeData.ComponentTypes[i];
                    Assert.That(componentTypeData.StableTypeHash, Is.EqualTo(typeInfo.StableTypeHash));
                    Assert.AreEqual(componentTypeData.Flags.HasFlag(ComponentTypeFlags.ChunkComponent), TypeManager.IsChunkComponent(typeIndex) ? true : false);
                }

                var allocatedBytes = archetype.ChunkCount * Chunk.kChunkSize;
                var unusedEntityCount = (archetype.ChunkCount * archetype.ChunkCapacity) - archetype.Archetype->EntityCount;
                var unusedBytes = unusedEntityCount * archetype.Archetype->InstanceSize;

                Assert.That(archetypeMemoryData.CalculateAllocatedBytes(), Is.EqualTo(allocatedBytes));
                Assert.That(archetypeMemoryData.CalculateUnusedBytes(archetypeData), Is.EqualTo(unusedBytes));
                Assert.That(archetypeMemoryData.EntityCount, Is.EqualTo(archetype.Archetype->EntityCount));
                Assert.That(archetypeMemoryData.CalculateUnusedEntityCount(archetypeData), Is.EqualTo(unusedEntityCount));
                Assert.That(archetypeMemoryData.ChunkCount, Is.EqualTo(archetype.ChunkCount));
                Assert.That(archetypeMemoryData.SegmentCount, Is.EqualTo(0));
            }
        }

        [Test]
        public void ArchetypeWithSharedComponentValues()
        {
            var archetype = default(EntityArchetype);
            using (var frame = GenerateFrameMetaData(() =>
            {
                archetype = m_Manager.CreateArchetype(typeof(EcsTestSharedComp));

                var entity1 = m_Manager.CreateEntity(archetype);
                m_Manager.SetSharedComponentManaged(entity1, new EcsTestSharedComp { value = 42 });

                var entity2 = m_Manager.CreateEntity(archetype);
                m_Manager.SetSharedComponentManaged(entity2, new EcsTestSharedComp { value = 24 });
            }))
            {
                var worldsData = GetWorldsData(frame);
                var archetypesData = GetArchetypesData(frame);
                var archetypeMemoryData = GetArchetypeMemoryData(frame).First(x => x.StableHash == archetype.StableHash);

                Assert.That(worldsData.TryGetValue(archetypeMemoryData.WorldSequenceNumber, out var worldData), Is.True);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(worldData.SequenceNumber, Is.EqualTo(World.SequenceNumber));

                Assert.That(archetypesData.TryGetValue(archetypeMemoryData.StableHash, out var archetypeData), Is.True);
                Assert.That(archetypeData.StableHash, Is.EqualTo(archetype.StableHash));
                Assert.That(archetypeData.ChunkCapacity, Is.EqualTo(archetype.ChunkCapacity));
                Assert.That(archetypeData.InstanceSize, Is.EqualTo(archetype.Archetype->InstanceSize));

                Assert.That(archetypeData.ComponentTypes.Length, Is.EqualTo(archetype.Archetype->TypesCount));
                for (var i = 0; i < archetypeData.ComponentTypes.Length; ++i)
                {
                    var typeIndex = archetype.Archetype->Types[i].TypeIndex;
                    var typeInfo = TypeManager.GetTypeInfo(typeIndex);
                    var componentTypeData = archetypeData.ComponentTypes[i];
                    Assert.That(componentTypeData.StableTypeHash, Is.EqualTo(typeInfo.StableTypeHash));
                    Assert.AreEqual(componentTypeData.Flags.HasFlag(ComponentTypeFlags.ChunkComponent), TypeManager.IsChunkComponent(typeIndex) ? true : false);
                }

                var allocatedBytes = archetype.ChunkCount * Chunk.kChunkSize;
                var unusedEntityCount = (archetype.ChunkCount * archetype.ChunkCapacity) - archetype.Archetype->EntityCount;
                var unusedBytes = unusedEntityCount * archetype.Archetype->InstanceSize;

                Assert.That(archetypeMemoryData.CalculateAllocatedBytes(), Is.EqualTo(allocatedBytes));
                Assert.That(archetypeMemoryData.CalculateUnusedBytes(archetypeData), Is.EqualTo(unusedBytes));
                Assert.That(archetypeMemoryData.EntityCount, Is.EqualTo(archetype.Archetype->EntityCount));
                Assert.That(archetypeMemoryData.CalculateUnusedEntityCount(archetypeData), Is.EqualTo(unusedEntityCount));
                Assert.That(archetypeMemoryData.ChunkCount, Is.EqualTo(archetype.ChunkCount));

                // Disabled for now
                //Assert.That(archetypeMemoryData.SegmentCount, Is.EqualTo(2));
            }
        }
    }
}
#endif
