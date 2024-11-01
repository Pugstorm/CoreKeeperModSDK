#if ENABLE_PROFILER && UNITY_EDITOR
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Profiling;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;
using UnityEngine.Profiling;
using static Unity.Entities.EntitiesProfiler;
using static Unity.Entities.StructuralChangesProfiler;

namespace Unity.Entities.Tests
{
    [TestFixture]
    unsafe class StructuralChangesProfilerTests : ECSTestsFixture
    {
        static readonly string s_DataFilePath = Path.Combine(Application.temporaryCachePath, "profilerdata");
        static readonly string s_RawDataFilePath = s_DataFilePath + ".raw";
        static bool s_LastCategoryEnabled;
        static Entity s_Entity = Entity.Null;
        static NativeArray<Entity> s_Entities;
        static ComponentType s_ComponentType;
        static ComponentTypeSet s_ComponentTypeSet;
        static ComponentType s_QueryComponentType;

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

        partial class CreateEntityManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntity(EntityManager);
        }

        partial struct CreateEntityUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntity(state.EntityManager);
        }

        partial class CreateEntityWithArchetypeManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntityWithArchetype(EntityManager);
        }

        partial struct CreateEntityWithArchetypeUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntityWithArchetype(state.EntityManager);
        }

        partial class CreateEntityWithComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntityWithComponentTypeSet(EntityManager);
        }

        partial struct CreateEntityWithComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntityWithComponentTypeSet(state.EntityManager);
        }

        partial class CreateEntityWithArchetypeAndEntityCountManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntityWithArchetypeAndEntityCount(EntityManager);
        }

        partial struct CreateEntityWithArchetypeAndEntityCountUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntityWithArchetypeAndEntityCount(state.EntityManager);
        }

        partial class CreateEntityWithArchetypeAndEntityCountAndAllocatorManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntityWithArchetypeAndEntityCountAndAllocator(EntityManager);
        }

        partial struct CreateEntityWithArchetypeAndEntityCountAndAllocatorUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntityWithArchetypeAndEntityCountAndAllocator(state.EntityManager);
        }

        partial class CreateEntityWithArchetypeAndEntityArrayManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntityWithArchetypeAndEntityArray(EntityManager);
        }

        partial struct CreateEntityWithArchetypeAndEntityArrayUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntityWithArchetypeAndEntityArray(state.EntityManager);
        }

        partial class CreateEntityFromECBManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntityFromECB(EntityManager);
        }

        partial struct CreateEntityFromECBUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntityFromECB(state.EntityManager);
        }

        partial class CreateEntityFromECBWithArchetypeManagedSystem : SystemBase
        {
            protected override void OnUpdate() => CreateEntityFromECBWithArchetype(EntityManager);
        }

        partial struct CreateEntityFromECBWithArchetypeUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => CreateEntityFromECBWithArchetype(state.EntityManager);
        }

        partial class DestroyEntityManagedSystem : SystemBase
        {
            protected override void OnUpdate() => DestroyEntity(EntityManager);
        }

        partial struct DestroyEntityUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => DestroyEntity(state.EntityManager);
        }

        partial class DestroyEntityArrayManagedSystem : SystemBase
        {
            protected override void OnUpdate() => DestroyEntityArray(EntityManager);
        }

        partial struct DestroyEntityArrayUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => DestroyEntityArray(state.EntityManager);
        }

        partial class DestroyEntityArraySliceManagedSystem : SystemBase
        {
            protected override void OnUpdate() => DestroyEntityArraySlice(EntityManager);
        }

        partial struct DestroyEntityArraySliceUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => DestroyEntityArraySlice(state.EntityManager);
        }

        partial class DestroyEntityWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => DestroyEntityWithQuery(EntityManager);
        }

        partial struct DestroyEntityWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => DestroyEntityWithQuery(state.EntityManager);
        }

        partial class DestroyEntityFromECBManagedSystem : SystemBase
        {
            protected override void OnUpdate() => DestroyEntityFromECB(EntityManager);
        }

        partial struct DestroyEntityFromECBUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => DestroyEntityFromECB(state.EntityManager);
        }

        partial class DestroyEntityFromECBWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => DestroyEntityFromECBWithQuery(EntityManager);
        }

        partial struct DestroyEntityFromECBWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => DestroyEntityFromECBWithQuery(state.EntityManager);
        }

        partial class AddComponentManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponent(EntityManager, s_ComponentType);
        }

        partial struct AddComponentUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponent(state.EntityManager, s_ComponentType);
        }

        partial class AddComponentWithComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponentWithComponentTypeSet(EntityManager, s_ComponentTypeSet);
        }

        partial struct AddComponentWithComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponentWithComponentTypeSet(state.EntityManager, s_ComponentTypeSet);
        }

        partial class AddComponentWithEntityArrayManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponentWithEntityArray(EntityManager, s_ComponentType);
        }

        partial struct AddComponentWithEntityArrayUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponentWithEntityArray(state.EntityManager, s_ComponentType);
        }

        partial class AddComponentWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponentWithQuery(EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial struct AddComponentWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponentWithQuery(state.EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial class AddComponentFromECBManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponentFromECB(EntityManager, s_ComponentType);
        }

        partial struct AddComponentFromECBUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponentFromECB(state.EntityManager, s_ComponentType);
        }

        partial class AddComponentFromECBWithComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponentFromECBWithComponentTypeSet(EntityManager, s_ComponentTypeSet);
        }

        partial struct AddComponentFromECBWithComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponentFromECBWithComponentTypeSet(state.EntityManager, s_ComponentTypeSet);
        }

        partial class AddComponentFromECBWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponentFromECBWithQuery(EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial struct AddComponentFromECBWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponentFromECBWithQuery(state.EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial class AddComponentFromECBWithQueryAndComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => AddComponentFromECBWithQueryAndComponentTypeSet(EntityManager, s_QueryComponentType, s_ComponentTypeSet);
        }

        partial struct AddComponentFromECBWithQueryAndComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => AddComponentFromECBWithQueryAndComponentTypeSet(state.EntityManager, s_QueryComponentType, s_ComponentTypeSet);
        }

        partial class RemoveComponentManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponent(EntityManager, s_ComponentType);
        }

        partial struct RemoveComponentUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponent(state.EntityManager, s_ComponentType);
        }

        partial class RemoveComponentWithComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponentWithComponentTypeSet(EntityManager, s_ComponentTypeSet);
        }

        partial struct RemoveComponentWithComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponentWithComponentTypeSet(state.EntityManager, s_ComponentTypeSet);
        }

        partial class RemoveComponentWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponentWithQuery(EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial struct RemoveComponentWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponentWithQuery(state.EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial class RemoveComponentWithQueryAndComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponentWithQueryAndComponentTypeSet(EntityManager, s_QueryComponentType, s_ComponentTypeSet);
        }

        partial struct RemoveComponentWithQueryAndComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponentWithQueryAndComponentTypeSet(state.EntityManager, s_QueryComponentType, s_ComponentTypeSet);
        }

        partial class RemoveComponentFromECBManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponentFromECB(EntityManager, s_ComponentType);
        }

        partial struct RemoveComponentFromECBUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponentFromECB(state.EntityManager, s_ComponentType);
        }

        partial class RemoveComponentFromECBWithComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponentFromECBWithComponentTypeSet(EntityManager, s_ComponentTypeSet);
        }

        partial struct RemoveComponentFromECBWithComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponentFromECBWithComponentTypeSet(state.EntityManager, s_ComponentTypeSet);
        }

        partial class RemoveComponentFromECBWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponentFromECBWithQuery(EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial struct RemoveComponentFromECBWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponentFromECBWithQuery(state.EntityManager, s_QueryComponentType, s_ComponentType);
        }

        partial class RemoveComponentFromECBWithQueryAndComponentTypeSetManagedSystem : SystemBase
        {
            protected override void OnUpdate() => RemoveComponentFromECBWithQueryAndComponentTypeSet(EntityManager, s_QueryComponentType, s_ComponentTypeSet);
        }

        partial struct RemoveComponentFromECBWithQueryAndComponentTypeSetUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => RemoveComponentFromECBWithQueryAndComponentTypeSet(state.EntityManager, s_QueryComponentType, s_ComponentTypeSet);
        }

        partial class SetSharedComponentManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponent(EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial struct SetSharedComponentUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponent(state.EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial class SetSharedComponentWithEntityArrayManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentWithEntityArray(EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial struct SetSharedComponentWithEntityArrayUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentWithEntityArray(state.EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial class SetSharedComponentWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentWithQuery(EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial struct SetSharedComponentWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentWithQuery(state.EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial class SetSharedComponentFromECBManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentFromECB(EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial struct SetSharedComponentFromECBUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentFromECB(state.EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial class SetSharedComponentFromECBWithEntityArrayManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentFromECBWithEntityArray(EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial struct SetSharedComponentFromECBWithEntityArrayUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentFromECBWithEntityArray(state.EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial class SetSharedComponentFromECBWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentFromECBWithQuery(EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial struct SetSharedComponentFromECBWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentFromECBWithQuery(state.EntityManager, new EcsTestSharedComp { value = 42 });
        }

        partial class SetSharedComponentManagedManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentManaged(EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial struct SetSharedComponentManagedUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentManaged(state.EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial class SetSharedComponentManagedWithEntityArrayManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentManagedWithEntityArray(EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial struct SetSharedComponentManagedWithEntityArrayUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentManagedWithEntityArray(state.EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial class SetSharedComponentManagedWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentManagedWithQuery(EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial struct SetSharedComponentManagedWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentManagedWithQuery(state.EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial class SetSharedComponentManagedFromECBManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentManagedFromECB(EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial struct SetSharedComponentManagedFromECBUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentManagedFromECB(state.EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial class SetSharedComponentManagedFromECBWithEntityArrayManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentManagedFromECBWithEntityArray(EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial struct SetSharedComponentManagedFromECBWithEntityArrayUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentManagedFromECBWithEntityArray(state.EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial class SetSharedComponentManagedFromECBWithQueryManagedSystem : SystemBase
        {
            protected override void OnUpdate() => SetSharedComponentManagedFromECBWithQuery(EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        partial struct SetSharedComponentManagedFromECBWithQueryUnmanagedSystem : ISystem
        {
            public void OnUpdate(ref SystemState state) => SetSharedComponentManagedFromECBWithQuery(state.EntityManager, new EcsTestSharedCompManaged { value = "hello" });
        }

        RawFrameDataView GenerateFrameMetaData(Action generateFunc)
        {
            EntitiesProfiler.Shutdown();
            EntitiesProfiler.Initialize();

            using (var scope = new ProfilerEnableScope(s_DataFilePath, StructuralChangesProfiler.Category))
            {
                generateFunc();
            }

            var loaded = ProfilerDriver.LoadProfile(s_RawDataFilePath, false);
            Assert.IsTrue(loaded);
            Assert.AreNotEqual(-1, ProfilerDriver.lastFrameIndex);

            return ProfilerDriver.GetRawFrameDataView(0, 0);
        }

        void VerifyFrameMetaData(StructuralChangeType structuralChangeType, Action action)
        {
            using (var frame = GenerateFrameMetaData(() =>
            {
                action();
                EntitiesProfiler.Update();
            }))
            {
                var structuralChangeData = GetStructuralChangeData(frame, structuralChangeType);
                var worldData = GetWorldData(frame, structuralChangeData);
                var systemData = GetSystemData(frame, structuralChangeData);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(systemData.Name, Is.Empty);
            }
        }

        void VerifyFrameMetaData<T>(StructuralChangeType structuralChangeType, Action generateFunc)
        {
            using (var frame = GenerateFrameMetaData(generateFunc))
            {
                var structuralChangeData = GetStructuralChangeData(frame, structuralChangeType);
                var worldData = GetWorldData(frame, structuralChangeData);
                var systemData = GetSystemData(frame, structuralChangeData);
                Assert.That(worldData.Name, Is.EqualTo(World.Name));
                Assert.That(systemData.Name, Is.EqualTo(TypeManager.GetSystemName(typeof(T))));
            }
        }

        void VerifyFrameMetaDataManagedSystem<T>(StructuralChangeType structuralChangeType) where T : SystemBase
        {
            VerifyFrameMetaData<T>(structuralChangeType, () =>
            {
                var system = World.GetOrCreateSystemManaged<T>();
                system.Update();
                EntitiesProfiler.Update();
                World.DestroySystemManaged(system);
            });
        }

        void VerifyFrameMetaDataUnmanagedSystem<T>(StructuralChangeType structuralChangeType) where T : unmanaged, ISystem
        {
            VerifyFrameMetaData<T>(structuralChangeType, () =>
            {
                var system = World.GetOrCreateSystem<T>();
                system.Update(World.Unmanaged);
                EntitiesProfiler.Update();
                World.DestroySystem(system);
            });
        }

        static void CreateEntity(EntityManager manager)
        {
            manager.CreateEntity();
        }

        static void CreateEntityWithArchetype(EntityManager manager)
        {
            manager.CreateEntity(manager.CreateArchetype(typeof(EcsTestData)));
        }

        static void CreateEntityWithComponentTypeSet(EntityManager manager)
        {
            manager.CreateEntity(typeof(EcsTestData));
        }

        static void CreateEntityWithArchetypeAndEntityCount(EntityManager manager)
        {
            manager.CreateEntity(manager.CreateArchetype(typeof(EcsTestData)), 10);
        }

        static void CreateEntityWithArchetypeAndEntityCountAndAllocator(EntityManager manager)
        {
            using var entities = manager.CreateEntity(manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
        }

        static void CreateEntityWithArchetypeAndEntityArray(EntityManager manager)
        {
            using (var entities = new NativeArray<Entity>(10, Allocator.Temp))
            {
                manager.CreateEntity(manager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            }
        }

        static void CreateEntityFromECB(EntityManager manager)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.CreateEntity();
                ecb.Playback(manager);
            }
        }

        static void CreateEntityFromECBWithArchetype(EntityManager manager)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.CreateEntity(manager.CreateArchetype(typeof(EcsTestData)));
                ecb.Playback(manager);
            }
        }

        static void DestroyEntity(EntityManager manager)
        {
            manager.DestroyEntity(s_Entity);
        }

        static void DestroyEntityArray(EntityManager manager)
        {
            manager.DestroyEntity(s_Entities);
        }

        static void DestroyEntityArraySlice(EntityManager manager)
        {
            manager.DestroyEntity(s_Entities.Slice(0, 1));
        }

        static void DestroyEntityWithQuery(EntityManager manager)
        {
            manager.DestroyEntity(manager.CreateEntityQuery(typeof(EcsTestData)));
        }

        static void DestroyEntityFromECB(EntityManager manager)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.DestroyEntity(s_Entity);
                ecb.Playback(manager);
            }
        }

        static void DestroyEntityFromECBWithQuery(EntityManager manager)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            using (var query = manager.CreateEntityQuery(typeof(EcsTestData)))
            {
                ecb.DestroyEntity(query, EntityQueryCaptureMode.AtPlayback);
                ecb.Playback(manager);
            }
        }

        static void AddComponent(EntityManager manager, ComponentType type)
        {
            manager.AddComponent(s_Entity, type);
        }

        static void AddComponentWithComponentTypeSet(EntityManager manager, in ComponentTypeSet types)
        {
            manager.AddComponent(s_Entity, types);
        }

        static void AddComponentWithEntityArray(EntityManager manager, ComponentType type)
        {
            manager.AddComponent(s_Entities, type);
        }

        static void AddComponentWithQuery(EntityManager manager, ComponentType queryType, ComponentType type)
        {
            using (var query = manager.CreateEntityQuery(queryType))
            {
                manager.AddComponent(query, type);
            }
        }

        static void AddComponentFromECB(EntityManager manager, ComponentType type)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.AddComponent(s_Entity, type);
                ecb.Playback(manager);
            }
        }

        static void AddComponentFromECBWithComponentTypeSet(EntityManager manager, in ComponentTypeSet types)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.AddComponent(s_Entity, types);
                ecb.Playback(manager);
            }
        }

        static void AddComponentFromECBWithQuery(EntityManager manager, ComponentType queryType, ComponentType type)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            using (var query = manager.CreateEntityQuery(queryType))
            {
                ecb.AddComponent(query, type, EntityQueryCaptureMode.AtPlayback);
                ecb.Playback(manager);
            }
        }

        static void AddComponentFromECBWithQueryAndComponentTypeSet(EntityManager manager, ComponentType queryType, in ComponentTypeSet types)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            using (var query = manager.CreateEntityQuery(queryType))
            {
                ecb.AddComponent(query, types, EntityQueryCaptureMode.AtPlayback);
                ecb.Playback(manager);
            }
        }

        static void RemoveComponent(EntityManager manager, ComponentType type)
        {
            manager.RemoveComponent(s_Entity, type);
        }

        static void RemoveComponentWithComponentTypeSet(EntityManager manager, in ComponentTypeSet types)
        {
            manager.RemoveComponent(s_Entity, types);
        }

        static void RemoveComponentWithQuery(EntityManager manager, ComponentType queryType, ComponentType type)
        {
            using (var query = manager.CreateEntityQuery(queryType))
            {
                manager.RemoveComponent(query, type);
            }
        }

        static void RemoveComponentWithQueryAndComponentTypeSet(EntityManager manager, ComponentType queryType, in ComponentTypeSet types)
        {
            using (var query = manager.CreateEntityQuery(queryType))
            {
                manager.RemoveComponent(query, types);
            }
        }

        static void RemoveComponentFromECB(EntityManager manager, ComponentType type)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.RemoveComponent(s_Entity, type);
                ecb.Playback(manager);
            }
        }

        static void RemoveComponentFromECBWithComponentTypeSet(EntityManager manager, in ComponentTypeSet types)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.RemoveComponent(s_Entity, types);
                ecb.Playback(manager);
            }
        }

        static void RemoveComponentFromECBWithQuery(EntityManager manager, ComponentType queryType, ComponentType type)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            using (var query = manager.CreateEntityQuery(queryType))
            {
                ecb.RemoveComponent(query, type, EntityQueryCaptureMode.AtPlayback);
                ecb.Playback(manager);
            }
        }

        static void RemoveComponentFromECBWithQueryAndComponentTypeSet(EntityManager manager, ComponentType queryType, in ComponentTypeSet types)
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            using (var query = manager.CreateEntityQuery(queryType))
            {
                ecb.RemoveComponent(query, types, EntityQueryCaptureMode.AtPlayback);
                ecb.Playback(manager);
            }
        }

        static void SetSharedComponent<T>(EntityManager manager, T data) where T : unmanaged, ISharedComponentData
        {
            manager.SetSharedComponent(s_Entity, data);
        }

        static void SetSharedComponentWithEntityArray<T>(EntityManager manager, T data) where T : unmanaged, ISharedComponentData
        {
            manager.SetSharedComponent(s_Entities, data);
        }

        static void SetSharedComponentWithQuery<T>(EntityManager manager, T data) where T : unmanaged, ISharedComponentData
        {
            using (var query = manager.CreateEntityQuery(typeof(T)))
            {
                manager.SetSharedComponent(query, data);
            }
        }

        static void SetSharedComponentFromECB<T>(EntityManager manager, T data) where T : unmanaged, ISharedComponentData
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.SetSharedComponent(s_Entity, data);
                ecb.Playback(manager);
            }
        }

        static void SetSharedComponentFromECBWithEntityArray<T>(EntityManager manager, T data) where T : unmanaged, ISharedComponentData
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.SetSharedComponent(s_Entities, data);
                ecb.Playback(manager);
            }
        }

        static void SetSharedComponentFromECBWithQuery<T>(EntityManager manager, T data) where T : unmanaged, ISharedComponentData
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            using (var query = manager.CreateEntityQuery(typeof(T)))
            {
                ecb.SetSharedComponent(query, data, EntityQueryCaptureMode.AtPlayback);
                ecb.Playback(manager);
            }
        }

        static void SetSharedComponentManaged<T>(EntityManager manager, T data) where T : struct, ISharedComponentData
        {
            manager.SetSharedComponentManaged(s_Entity, data);
        }

        static void SetSharedComponentManagedWithEntityArray<T>(EntityManager manager, T data) where T : struct, ISharedComponentData
        {
            manager.SetSharedComponentManaged(s_Entities, data);
        }

        static void SetSharedComponentManagedWithQuery<T>(EntityManager manager, T data) where T : struct, ISharedComponentData
        {
            using (var query = manager.CreateEntityQuery(typeof(T)))
            {
                manager.SetSharedComponentManaged(query, data);
            }
        }

        static void SetSharedComponentManagedFromECB<T>(EntityManager manager, T data) where T : struct, ISharedComponentData
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.SetSharedComponentManaged(s_Entity, data);
                ecb.Playback(manager);
            }
        }

        static void SetSharedComponentManagedFromECBWithEntityArray<T>(EntityManager manager, T data) where T : struct, ISharedComponentData
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            {
                ecb.SetSharedComponentManaged(s_Entities, data);
                ecb.Playback(manager);
            }
        }

        static void SetSharedComponentManagedFromECBWithQuery<T>(EntityManager manager, T data) where T : struct, ISharedComponentData
        {
            using (var ecb = new EntityCommandBuffer(Allocator.Temp))
            using (var query = manager.CreateEntityQuery(typeof(T)))
            {
                ecb.SetSharedComponentManaged(query, data, EntityQueryCaptureMode.AtPlayback);
                ecb.Playback(manager);
            }
        }

        static WorldData GetWorldData(RawFrameDataView frame, StructuralChangeData structuralChangeData) =>
            GetSessionMetaData<WorldData>(frame, EntitiesProfiler.Guid, (int)DataTag.WorldData).First(x => x.SequenceNumber == structuralChangeData.WorldSequenceNumber);

        static SystemData GetSystemData(RawFrameDataView frame, StructuralChangeData structuralChangeData) =>
            GetSessionMetaData<SystemData>(frame, EntitiesProfiler.Guid, (int)DataTag.SystemData).FirstOrDefault(x => x.System == structuralChangeData.ExecutingSystem);

        static StructuralChangeData GetStructuralChangeData(RawFrameDataView frame, StructuralChangeType type) =>
            GetFrameMetaData<StructuralChangeData>(frame, StructuralChangesProfiler.Guid, 0).Last(x => x.Type == type);

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
            s_LastCategoryEnabled = Profiler.IsCategoryEnabled(StructuralChangesProfiler.Category);
            Profiler.SetCategoryEnabled(StructuralChangesProfiler.Category, true);
        }

        [OneTimeTearDown]
        public void OneTimeTearDown()
        {
            Profiler.SetCategoryEnabled(StructuralChangesProfiler.Category, s_LastCategoryEnabled);
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            s_Entity = Entity.Null;
            if (s_Entities.IsCreated)
                s_Entities.Dispose();
            s_ComponentType = default;
            s_ComponentTypeSet = default;
            s_QueryComponentType = default;
        }

        [Test]
        public void Uninitialized_DoesNotThrow()
        {
            StructuralChangesProfiler.Shutdown();
            using var recorder = new StructuralChangesProfiler.Recorder(Allocator.Persistent);
            Assert.DoesNotThrow(() =>
            {
                recorder.Begin(StructuralChangeType.CreateEntity, default);
                recorder.End();
            });
            Assert.DoesNotThrow(() =>
            {
                recorder.Begin(StructuralChangeType.DestroyEntity, default);
                recorder.End();
            });
            Assert.DoesNotThrow(() =>
            {
                recorder.Begin(StructuralChangeType.AddComponent, default);
                recorder.End();
            });
            Assert.DoesNotThrow(() =>
            {
                recorder.Begin(StructuralChangeType.RemoveComponent, default);
                recorder.End();
            });
            Assert.DoesNotThrow(() => StructuralChangesProfiler.Flush());
        }

        [Test]
        public void CreateEntity()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntity(World.EntityManager));
        }

        [Test]
        public void CreateEntity_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntity_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetype()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntityWithArchetype(World.EntityManager));
        }

        [Test]
        public void CreateEntityWithArchetype_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityWithArchetypeManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetype_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityWithArchetypeUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithComponentTypeSet()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntityWithComponentTypeSet(World.EntityManager));
        }

        [Test]
        public void CreateEntityWithComponentTypeSet_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityWithComponentTypeSetManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithComponentTypeSet_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityWithComponentTypeSetUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityCount()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntityWithArchetypeAndEntityCount(World.EntityManager));
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityCount_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityWithArchetypeAndEntityCountManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityCount_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityWithArchetypeAndEntityCountUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityCountAndAllocator()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntityWithArchetypeAndEntityCountAndAllocator(World.EntityManager));
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityCountAndAllocator_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityWithArchetypeAndEntityCountAndAllocatorManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityCountAndAllocator_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityWithArchetypeAndEntityCountAndAllocatorUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityArray()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntityWithArchetypeAndEntityArray(World.EntityManager));
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityArray_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityWithArchetypeAndEntityArrayManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityWithArchetypeAndEntityArray_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityWithArchetypeAndEntityArrayUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityFromECB()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntityFromECB(World.EntityManager));
        }

        [Test]
        public void CreateEntityFromECB_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityFromECBManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityFromECB_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityFromECBUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityFromECBWithArchetype()
        {
            VerifyFrameMetaData(StructuralChangeType.CreateEntity, () => CreateEntityFromECBWithArchetype(World.EntityManager));
        }

        [Test]
        public void CreateEntityFromECBWithArchetype_ManagedSystem()
        {
            VerifyFrameMetaDataManagedSystem<CreateEntityFromECBWithArchetypeManagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void CreateEntityFromECBWithArchetype_UnmanagedSystem()
        {
            VerifyFrameMetaDataUnmanagedSystem<CreateEntityFromECBWithArchetypeUnmanagedSystem>(StructuralChangeType.CreateEntity);
        }

        [Test]
        public void DestroyEntity()
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaData(StructuralChangeType.DestroyEntity, () => DestroyEntity(World.EntityManager));
        }

        [Test]
        public void DestroyEntity_ManagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataManagedSystem<DestroyEntityManagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntity_UnmanagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataUnmanagedSystem<DestroyEntityUnmanagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityArray()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            VerifyFrameMetaData(StructuralChangeType.DestroyEntity, () => DestroyEntityArray(World.EntityManager));
        }

        [Test]
        public void DestroyEntityArray_ManagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            VerifyFrameMetaDataManagedSystem<DestroyEntityArrayManagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityArray_UnmanagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            VerifyFrameMetaDataUnmanagedSystem<DestroyEntityArrayUnmanagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityArraySlice()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            VerifyFrameMetaData(StructuralChangeType.DestroyEntity, () => DestroyEntityArraySlice(World.EntityManager));
        }

        [Test]
        public void DestroyEntityArraySlice_ManagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            VerifyFrameMetaDataManagedSystem<DestroyEntityArraySliceManagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityArraySlice_UnmanagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10, Allocator.Temp);
            VerifyFrameMetaDataUnmanagedSystem<DestroyEntityArraySliceUnmanagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityWithQuery()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10);
            VerifyFrameMetaData(StructuralChangeType.DestroyEntity, () => DestroyEntityWithQuery(World.EntityManager));
        }

        [Test]
        public void DestroyEntityWithQuery_ManagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10);
            VerifyFrameMetaDataManagedSystem<DestroyEntityWithQueryManagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityWithQuery_UnmanagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10);
            VerifyFrameMetaDataUnmanagedSystem<DestroyEntityWithQueryUnmanagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityFromECB()
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaData(StructuralChangeType.DestroyEntity, () => DestroyEntityFromECB(World.EntityManager));
        }

        [Test]
        public void DestroyEntityFromECB_ManagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataManagedSystem<DestroyEntityFromECBManagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityFromECB_UnmanagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataUnmanagedSystem<DestroyEntityFromECBUnmanagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityFromECBWithQuery()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10);
            VerifyFrameMetaData(StructuralChangeType.DestroyEntity, () => DestroyEntityFromECBWithQuery(World.EntityManager));
        }

        [Test]
        public void DestroyEntityFromECBWithQuery_ManagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10);
            VerifyFrameMetaDataManagedSystem<DestroyEntityFromECBWithQueryManagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [Test]
        public void DestroyEntityFromECBWithQuery_UnmanagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestData)), 10);
            VerifyFrameMetaDataUnmanagedSystem<DestroyEntityFromECBWithQueryUnmanagedSystem>(StructuralChangeType.DestroyEntity);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponent(Type type)
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponent(World.EntityManager, type));
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponent_ManagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataManagedSystem<AddComponentManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponent_UnmanagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataUnmanagedSystem<AddComponentUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentWithComponentTypeSet(Type type1, Type type2)
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponentWithComponentTypeSet(World.EntityManager, new ComponentTypeSet(type1, type2)));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentWithComponentTypeSet_ManagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataManagedSystem<AddComponentWithComponentTypeSetManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentWithComponentTypeSet_UnmanagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataUnmanagedSystem<AddComponentWithComponentTypeSetUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponentWithEntityArray(Type type)
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(), 10, Allocator.Temp);
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponentWithEntityArray(World.EntityManager, type));
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponentWithEntityArray_ManagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(), 10, Allocator.Temp);
            VerifyFrameMetaDataManagedSystem<AddComponentWithEntityArrayManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponentWithEntityArray_UnmanagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(), 10, Allocator.Temp);
            VerifyFrameMetaDataUnmanagedSystem<AddComponentWithEntityArrayUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentWithQuery(Type type1, Type type2)
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponentWithQuery(World.EntityManager, type1, type2));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentWithQuery_ManagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaDataManagedSystem<AddComponentWithQueryManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentWithQuery_UnmanagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaDataUnmanagedSystem<AddComponentWithQueryUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponentFromECB(Type type)
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponentFromECB(World.EntityManager, type));
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponentFromECB_ManagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataManagedSystem<AddComponentFromECBManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void AddComponentFromECB_UnmanagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataUnmanagedSystem<AddComponentFromECBUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentFromECBWithComponentTypeSet(Type type1, Type type2)
        {
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponentFromECBWithComponentTypeSet(World.EntityManager, new ComponentTypeSet(type1, type2)));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentFromECBWithComponentTypeSet_ManagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataManagedSystem<AddComponentFromECBWithComponentTypeSetManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentFromECBWithComponentTypeSet_UnmanagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity();
            VerifyFrameMetaDataUnmanagedSystem<AddComponentFromECBWithComponentTypeSetUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentFromECBWithQuery(Type type1, Type type2)
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponentFromECBWithQuery(World.EntityManager, type1, type2));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentFromECBWithQuery_ManagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaDataManagedSystem<AddComponentFromECBWithQueryManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void AddComponentFromECBWithQuery_UnmanagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaDataUnmanagedSystem<AddComponentFromECBWithQueryUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void AddComponentFromECBWithQueryAndComponentTypeSet(Type type1, Type type2, Type type3)
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaData(StructuralChangeType.AddComponent, () => AddComponentFromECBWithQueryAndComponentTypeSet(World.EntityManager, type1, new ComponentTypeSet(type2, type3)));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void AddComponentFromECBWithQueryAndComponentTypeSet_ManagedSystem(Type type1, Type type2, Type type3)
        {
            s_QueryComponentType = type1;
            s_ComponentTypeSet = new ComponentTypeSet(type2, type3);
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaDataManagedSystem<AddComponentFromECBWithQueryAndComponentTypeSetManagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void AddComponentFromECBWithQueryAndComponentTypeSet_UnmanagedSystem(Type type1, Type type2, Type type3)
        {
            s_QueryComponentType = type1;
            s_ComponentTypeSet = new ComponentTypeSet(type2, type3);
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1), 10);
            VerifyFrameMetaDataUnmanagedSystem<AddComponentFromECBWithQueryAndComponentTypeSetUnmanagedSystem>(StructuralChangeType.AddComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void RemoveComponent(Type type)
        {
            s_Entity = World.EntityManager.CreateEntity(type);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponent(World.EntityManager, type));
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void RemoveComponent_ManagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity(type);
            VerifyFrameMetaDataManagedSystem<RemoveComponentManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void RemoveComponent_UnmanagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity(type);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentWithComponentTypeSet(Type type1, Type type2)
        {
            s_Entity = World.EntityManager.CreateEntity(type1, type2);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponentWithComponentTypeSet(World.EntityManager, new ComponentTypeSet(type1, type2)));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentWithComponentTypeSet_ManagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity(type1, type2);
            VerifyFrameMetaDataManagedSystem<RemoveComponentWithComponentTypeSetManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentWithComponentTypeSet_UnmanagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity(type1, type2);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentWithComponentTypeSetUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentWithQuery(Type type1, Type type2)
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2), 10);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponentWithQuery(World.EntityManager, type1, type2));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentWithQuery_ManagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2), 10);
            VerifyFrameMetaDataManagedSystem<RemoveComponentWithQueryManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentWithQuery_UnmanagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2), 10);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentWithQueryUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void RemoveComponentWithQueryAndComponentTypeSet(Type type1, Type type2, Type type3)
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2, type3), 10);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponentWithQueryAndComponentTypeSet(World.EntityManager, type1, new ComponentTypeSet(type2, type3)));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void RemoveComponentWithQueryAndComponentTypeSet_ManagedSystem(Type type1, Type type2, Type type3)
        {
            s_QueryComponentType = type1;
            s_ComponentTypeSet = new ComponentTypeSet(type2, type3);
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2, type3), 10);
            VerifyFrameMetaDataManagedSystem<RemoveComponentWithQueryAndComponentTypeSetManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void RemoveComponentWithQueryAndComponentTypeSet_UnmanagedSystem(Type type1, Type type2, Type type3)
        {
            s_QueryComponentType = type1;
            s_ComponentTypeSet = new ComponentTypeSet(type2, type3);
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2, type3), 10);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentWithQueryAndComponentTypeSetUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void RemoveComponentFromECB(Type type)
        {
            s_Entity = World.EntityManager.CreateEntity(type);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponentFromECB(World.EntityManager, type));
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void RemoveComponentFromECB_ManagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity(type);
            VerifyFrameMetaDataManagedSystem<RemoveComponentFromECBManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData))]
        [TestCase(typeof(EcsTestSharedComp))]
        [TestCase(typeof(EcsTestSharedCompManaged))]
        [TestCase(typeof(EcsIntElement))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent))]
#endif
        public void RemoveComponentFromECB_UnmanagedSystem(Type type)
        {
            s_ComponentType = type;
            s_Entity = World.EntityManager.CreateEntity(type);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentFromECBUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentFromECBWithComponentTypeSet(Type type1, Type type2)
        {
            s_Entity = World.EntityManager.CreateEntity(type1, type2);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponentFromECBWithComponentTypeSet(World.EntityManager, new ComponentTypeSet(type1, type2)));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentFromECBWithComponentTypeSet_ManagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity(type1, type2);
            VerifyFrameMetaDataManagedSystem<RemoveComponentFromECBWithComponentTypeSetManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentFromECBWithComponentTypeSet_UnmanagedSystem(Type type1, Type type2)
        {
            s_ComponentTypeSet = new ComponentTypeSet(type1, type2);
            s_Entity = World.EntityManager.CreateEntity(type1, type2);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentFromECBWithComponentTypeSetUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentFromECBWithQuery(Type type1, Type type2)
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2), 10);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponentFromECBWithQuery(World.EntityManager, type1, type2));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentFromECBWithQuery_ManagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2), 10);
            VerifyFrameMetaDataManagedSystem<RemoveComponentFromECBWithQueryManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2))]
#endif
        public void RemoveComponentFromECBWithQuery_UnmanagedSystem(Type type1, Type type2)
        {
            s_QueryComponentType = type1;
            s_ComponentType = type2;
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2), 10);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentFromECBWithQueryUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void RemoveComponentFromECBWithQueryAndComponentTypeSet(Type type1, Type type2, Type type3)
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2, type3), 10);
            VerifyFrameMetaData(StructuralChangeType.RemoveComponent, () => RemoveComponentFromECBWithQueryAndComponentTypeSet(World.EntityManager, type1, new ComponentTypeSet(type2, type3)));
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void RemoveComponentFromECBWithQueryAndComponentTypeSet_ManagedSystem(Type type1, Type type2, Type type3)
        {
            s_QueryComponentType = type1;
            s_ComponentTypeSet = new ComponentTypeSet(type2, type3);
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2, type3), 10);
            VerifyFrameMetaDataManagedSystem<RemoveComponentFromECBWithQueryAndComponentTypeSetManagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [TestCase(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3))]
        [TestCase(typeof(EcsTestSharedComp), typeof(EcsTestSharedComp2), typeof(EcsTestSharedComp3))]
        [TestCase(typeof(EcsTestSharedCompManaged), typeof(EcsTestSharedCompManaged2), typeof(EcsTestSharedCompManaged3))]
        [TestCase(typeof(EcsIntElement), typeof(EcsIntElement2), typeof(EcsIntElement3))]
#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [TestCase(typeof(EcsTestManagedComponent), typeof(EcsTestManagedComponent2), typeof(EcsTestManagedComponent3))]
#endif
        public void RemoveComponentFromECBWithQueryAndComponentTypeSet_UnmanagedSystem(Type type1, Type type2, Type type3)
        {
            s_QueryComponentType = type1;
            s_ComponentTypeSet = new ComponentTypeSet(type2, type3);
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(type1, type2, type3), 10);
            VerifyFrameMetaDataUnmanagedSystem<RemoveComponentFromECBWithQueryAndComponentTypeSetUnmanagedSystem>(StructuralChangeType.RemoveComponent);
        }

        [Test]
        public void SetSharedComponent()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedComp));
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponent(World.EntityManager, new EcsTestSharedComp { value = 42 }));
        }

        [Test]
        public void SetSharedComponent_ManagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedComp));
            VerifyFrameMetaDataManagedSystem<SetSharedComponentManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponent_UnmanagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedComp));
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentWithEntityArray()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10, Allocator.Temp);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentWithEntityArray(World.EntityManager, new EcsTestSharedComp { value = 42 }));
        }

        [Test]
        public void SetSharedComponentWithEntityArray_ManagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10, Allocator.Temp);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentWithEntityArrayManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentWithEntityArray_UnmanagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10, Allocator.Temp);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentWithEntityArrayUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentWithQuery()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentWithQuery(World.EntityManager, new EcsTestSharedComp { value = 42 }));
        }

        [Test]
        public void SetSharedComponentWithQuery_ManagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentWithQueryManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentWithQuery_UnmanagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentWithQueryUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentFromECB()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedComp));
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentFromECB(World.EntityManager, new EcsTestSharedComp { value = 42 }));
        }

        [Test]
        public void SetSharedComponentFromECB_ManagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedComp));
            VerifyFrameMetaDataManagedSystem<SetSharedComponentFromECBManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentFromECB_UnmanagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedComp));
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentFromECBUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentFromECBWithEntityArray()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10, Allocator.Temp);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentFromECBWithEntityArray(World.EntityManager, new EcsTestSharedComp { value = 42 }));
        }

        [Test]
        public void SetSharedComponentFromECBWithEntityArray_ManagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10, Allocator.Temp);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentFromECBWithEntityArrayManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentFromECBWithEntityArray_UnmanagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10, Allocator.Temp);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentFromECBWithEntityArrayUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentFromECBWithQuery()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentFromECBWithQuery(World.EntityManager, new EcsTestSharedComp { value = 42 }));
        }

        [Test]
        public void SetSharedComponentFromECBWithQuery_ManagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentFromECBWithQueryManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentFromECBWithQuery_UnmanagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedComp)), 10);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentFromECBWithQueryUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManaged()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedCompManaged));
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentManaged(World.EntityManager, new EcsTestSharedCompManaged { value = "hello" }));
        }

        [Test]
        public void SetSharedComponentManaged_ManagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedCompManaged));
            VerifyFrameMetaDataManagedSystem<SetSharedComponentManagedManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManaged_UnmanagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedCompManaged));
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentManagedUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedWithEntityArray()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10, Allocator.Temp);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentManagedWithEntityArray(World.EntityManager, new EcsTestSharedCompManaged { value = "hello" }));
        }

        [Test]
        public void SetSharedComponentManagedWithEntityArray_ManagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10, Allocator.Temp);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentManagedWithEntityArrayManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedWithEntityArray_UnmanagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10, Allocator.Temp);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentManagedWithEntityArrayUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedWithQuery()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentManagedWithQuery(World.EntityManager, new EcsTestSharedCompManaged { value = "hello" }));
        }

        [Test]
        public void SetSharedComponentManagedWithQuery_ManagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentManagedWithQueryManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedWithQuery_UnmanagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentManagedWithQueryUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedFromECB()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedCompManaged));
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentManagedFromECB(World.EntityManager, new EcsTestSharedCompManaged { value = "hello" }));
        }

        [Test]
        public void SetSharedComponentManagedFromECB_ManagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedCompManaged));
            VerifyFrameMetaDataManagedSystem<SetSharedComponentManagedFromECBManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedFromECB_UnmanagedSystem()
        {
            s_Entity = World.EntityManager.CreateEntity(typeof(EcsTestSharedCompManaged));
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentManagedFromECBUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedFromECBWithEntityArray()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10, Allocator.Temp);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentManagedFromECBWithEntityArray(World.EntityManager, new EcsTestSharedCompManaged { value = "hello" }));
        }

        [Test]
        public void SetSharedComponentManagedFromECBWithEntityArray_ManagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10, Allocator.Temp);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentManagedFromECBWithEntityArrayManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedFromECBWithEntityArray_UnmanagedSystem()
        {
            s_Entities = World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10, Allocator.Temp);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentManagedFromECBWithEntityArrayUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedFromECBWithQuery()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10);
            VerifyFrameMetaData(StructuralChangeType.SetSharedComponent, () => SetSharedComponentManagedFromECBWithQuery(World.EntityManager, new EcsTestSharedCompManaged { value = "hello" }));
        }

        [Test]
        public void SetSharedComponentManagedFromECBWithQuery_ManagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10);
            VerifyFrameMetaDataManagedSystem<SetSharedComponentManagedFromECBWithQueryManagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        [Test]
        public void SetSharedComponentManagedFromECBWithQuery_UnmanagedSystem()
        {
            World.EntityManager.CreateEntity(World.EntityManager.CreateArchetype(typeof(EcsTestSharedCompManaged)), 10);
            VerifyFrameMetaDataUnmanagedSystem<SetSharedComponentManagedFromECBWithQueryUnmanagedSystem>(StructuralChangeType.SetSharedComponent);
        }

        partial struct FakeDeserializeJob : IJob
        {
            [ReadOnly] public int EntityCount;
            public ExclusiveEntityTransaction Transaction;

            public void Execute()
            {
                for (var i = 0; i < EntityCount; ++i)
                {
                    var entity = Transaction.CreateEntity(typeof(EcsTestData));
                    Transaction.AddComponent(entity, typeof(EcsTestData2));
                    Transaction.RemoveComponent(entity, typeof(EcsTestData));
                }
            }
        }

        [Test]
        public void CanRecord_DuringExclusiveEntityTransaction()
        {
            const int k_WorldCount = 10;
            const int k_EntityCount = 10000;
            using (var scope = new ProfilerEnableScope(s_DataFilePath, StructuralChangesProfiler.Category))
            {
                // Create worlds
                var worlds = new World[k_WorldCount];
                for (var i = 0; i < k_WorldCount; ++i)
                    worlds[i] = new World($"Test World {i + 1}");

                // Schedule jobs
                var jobs = new JobHandle[k_WorldCount];
                for (var i = 0; i < k_WorldCount; ++i)
                {
                    jobs[i] = new FakeDeserializeJob
                    {
                        EntityCount = k_EntityCount,
                        Transaction = worlds[i].EntityManager.BeginExclusiveEntityTransaction()
                    }.Schedule();
                }

                // Wait for jobs to complete
                for (var i = 0; i < k_WorldCount; ++i)
                {
                    jobs[i].Complete();
                    worlds[i].EntityManager.EndExclusiveEntityTransaction();
                }

                // Cleanup
                for (var i = 0; i < k_WorldCount; ++i)
                {
                    worlds[i].Dispose();
                    worlds[i] = null;
                }
            }
        }
    }
}
#endif
