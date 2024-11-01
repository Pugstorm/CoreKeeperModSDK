using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;
using Unity.Entities.Tests.InAnotherAssembly;

[assembly: RegisterGenericSystemType(typeof(ISystemTests.MyGenericISystem<float>))]
[assembly: RegisterGenericComponentType(typeof(ISystemTests.MySentinelGenericComponent<float>))]
[assembly: RegisterGenericSystemType(typeof(MyGenericISystem_FromAnotherAssembly<float>))]
[assembly: RegisterGenericComponentType(typeof(MySentinelGenericComponent_FromAnotherAssembly<float>))]

[assembly: RegisterGenericSystemType(typeof(MyGenericISystem_FromAnotherAssembly<TypeManagerTests.TestType1>))]
[assembly: RegisterGenericComponentType(typeof(MySentinelGenericComponent_FromAnotherAssembly<TypeManagerTests.TestType1>))]

[assembly: RegisterGenericSystemType(typeof(MyGenericNestingType<float>.MyNestedGenericISystem))] 

[assembly: RegisterGenericComponentType(typeof(MySentinelGenericComponent_FromAnotherAssembly<MyGenericNestingType<float>.MyNestedGenericISystem>))]


namespace Unity.Entities.Tests
{
    [BurstCompile]
    public partial class ISystemTests : ECSTestsFixture
    {
        public struct MySentinelGenericComponent<T> : IComponentData
        {
        }
        
        [BurstCompile]
        public partial struct MyGenericISystem<T> : ISystem   
        {
            [BurstCompile]
            public void OnCreate(ref SystemState state)
            {
                var types = new NativeArray<ComponentType>(1, Allocator.Temp);
                types[0] = ComponentType.ReadWrite<MySentinelGenericComponent<T>>();
                
                state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(types));
            }
        } 
        
        struct MySystemData2 : IComponentData
        {
            public int UpdateCount;
            public int Dummy;
        }

        private partial struct MyUnmanagedSystem2 : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<MySystemData2>(state.SystemHandle);
            }

            public void OnUpdate(ref SystemState state)
            {
                state.EntityManager.GetComponentDataRW<MySystemData2>(state.SystemHandle).ValueRW.UpdateCount++;
            }
        }

        struct PtrData : IComponentData
        {
            public unsafe int* Ptr;
            public int createdFlag;
        }
        private unsafe partial struct MyUnmanagedSystemWithStartStop : ISystem, ISystemStartStop
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle,
                    new PtrData
                    {
                        createdFlag = 16,
                        Ptr = null
                    });
            }

            public void OnDestroy(ref SystemState state)
            {
                ref var Ptr = ref state.EntityManager.GetComponentDataRW<PtrData>(state.SystemHandle).ValueRW;
                *Ptr.Ptr |= 1 | Ptr.createdFlag;
            }

            public void OnStartRunning(ref SystemState state)
            {
                ref var Ptr = ref state.EntityManager.GetComponentDataRW<PtrData>(state.SystemHandle).ValueRW;
                *Ptr.Ptr |= 2;
            }

            public void OnStopRunning(ref SystemState state)
            {
                ref var Ptr = ref state.EntityManager.GetComponentDataRW<PtrData>(state.SystemHandle).ValueRW;
                *Ptr.Ptr |= 4;
            }

            public void OnUpdate(ref SystemState state)
            {
                ref var Ptr = ref state.EntityManager.GetComponentDataRW<PtrData>(state.SystemHandle).ValueRW;
                *Ptr.Ptr |= 8;
            }
        }

        [BurstCompile]
        private partial struct MyUnmanagedSystem2WithBurst : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponent<MySystemData2>(state.SystemHandle);
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                state.EntityManager.GetComponentDataRW<MySystemData2>(state.SystemHandle).ValueRW.UpdateCount++;
            }
        }

        [Test]
        public void UnmanagedSystemLifetime()
        {
            SystemHandle sysHandle = SystemHandle.Null;
            Assert.Throws<InvalidOperationException>(() => World.Unmanaged.ResolveSystemStateRef(sysHandle));

            using (var tempWorld = new World("Temp"))
            {
                Assert.Throws<InvalidOperationException>(() => World.Unmanaged.ResolveSystemStateRef(sysHandle));
                var sys = tempWorld.CreateSystem<MyUnmanagedSystem2>();
                sysHandle = sys;
                Assert.IsTrue(tempWorld.Unmanaged.IsSystemValid(sysHandle));
                Assert.IsFalse(World.Unmanaged.IsSystemValid(sysHandle));
                Assert.DoesNotThrow(() => tempWorld.Unmanaged.ResolveSystemStateRef(sysHandle));
                Assert.Throws<InvalidOperationException>(() => sysHandle.Update(World.Unmanaged));
            }

            Assert.IsFalse(World.Unmanaged.IsSystemValid(sysHandle));
            Assert.Throws<InvalidOperationException>(() => World.Unmanaged.ResolveSystemStateRef(sysHandle));
            Assert.Throws<InvalidOperationException>(() => sysHandle.Update(World.Unmanaged));
        }

        [Test]
        public void UnmanagedSystemLookup()
        {
            var s1 = World.CreateSystem<MyUnmanagedSystem2>();
            var s2 = World.CreateSystem<MyUnmanagedSystem2>();

            World.EntityManager.GetComponentDataRW<MySystemData2>(s1).ValueRW.Dummy = 19;
            World.EntityManager.GetComponentDataRW<MySystemData2>(s2).ValueRW.Dummy = -19;

            // We don't know which one we'll get currently, but the point is there will be two.
            var sAny = World.GetExistingSystem<MyUnmanagedSystem2>();
            Assert.AreEqual(19, Math.Abs(World.EntityManager.GetComponentData<MySystemData2>(sAny).Dummy));

            World.DestroySystem(s1);

            s2 = World.GetExistingSystem<MyUnmanagedSystem2>();
            Assert.AreEqual(-19, World.EntityManager.GetComponentData<MySystemData2>(s2).Dummy);

            World.DestroySystem(s2);

            Assert.AreEqual(SystemHandle.Null, World.GetExistingSystem<MyUnmanagedSystem2>());
        }

        [Test]
        public unsafe void UnmanagedSystemStartStop()
        {
            int bits = 0;

            using (var w = new World("Fisk"))
            {
                var group = w.GetOrCreateSystemManaged<SimulationSystemGroup>();

                var s1 = w.CreateSystem<MyUnmanagedSystemWithStartStop>();
                ref var Ptr = ref w.EntityManager.GetComponentDataRW<PtrData>(s1).ValueRW;
                Ptr.Ptr = &bits;

                group.AddSystemToUpdateList(s1);
                w.Update();

                group.RemoveSystemFromUpdateList(s1);
                w.DestroySystem(s1);
                w.Update();
            }

            Assert.AreEqual(31, bits);
        }

        [Test]
        public unsafe void RegistryCallManagedToManaged()
        {
            var sysRef = World.CreateSystem<MyUnmanagedSystem2>();
            var statePtr = World.Unmanaged.ResolveSystemState(sysRef);
            SystemBaseRegistry.CallOnUpdate(statePtr);
            ref var UpdateData = ref World.EntityManager.GetComponentDataRW<MySystemData2>(sysRef).ValueRW;
            Assert.AreEqual(1, UpdateData.UpdateCount);
        }

        [Test]
        public unsafe void RegistryCallManagedToBurst()
        {
            var sysId = World.CreateSystem<MyUnmanagedSystem2WithBurst>();
            var statePtr = World.Unmanaged.ResolveSystemState(sysId);
            SystemBaseRegistry.CallOnUpdate(statePtr);
            ref var UpdateData = ref World.EntityManager.GetComponentDataRW<MySystemData2>(sysId).ValueRW;
            Assert.AreEqual(1, UpdateData.UpdateCount);
        }

        [Test]
        public void RegistryCallBurstToManaged()
        {
            var sysRef = World.CreateSystem<MyUnmanagedSystem2>();
            sysRef.Update(World.Unmanaged);
            ref var UpdateData = ref World.EntityManager.GetComponentDataRW<MySystemData2>(sysRef).ValueRW;
            Assert.AreEqual(1, UpdateData.UpdateCount);
        }

        [Test]
        public void CheckForBursting()
        {
            if (IsBurstEnabled()) // Test is checking if the system update is bursted (that only works if Burst is on)
                World.CreateSystem<SystemWithBurstAssertedUpdate>().Update(World.Unmanaged);
        }

        private partial struct SystemWithBurstAssertedUpdate : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                IsNonBurst(out var notBursted);
                Assert.IsTrue(notBursted);
            }

            [BurstCompile(CompileSynchronously = true)]
            public void OnDestroy(ref SystemState state)
            {
                IsNonBurst(out var notBursted);
                Assert.IsFalse(notBursted);
            }
            [BurstCompile(CompileSynchronously = true)]
            public void OnUpdate(ref SystemState state)
            {
                IsNonBurst(out var notBursted);
                Assert.IsFalse(notBursted);
            }

            [BurstDiscard]
            void IsNonBurst(out bool value) => value = true;
        }

        [Test]
        public void RegistryCallBurstToBurst()
        {
            var sysRef = World.CreateSystem<MyUnmanagedSystem2WithBurst>();
            sysRef.Update(World.Unmanaged);
            ref var UpdateData = ref World.EntityManager.GetComponentDataRW<MySystemData2>(sysRef).ValueRW;
            Assert.AreEqual(1, UpdateData.UpdateCount);
        }

        private partial class SnoopGroup : ComponentSystemGroup
        {
        }

        [BurstCompile]
        private partial struct SnoopSystem : ISystem, ISystemStartStop
        {
            [Flags]
            public enum CallFlags
            {
                None = 0,
                OnCreate = 1 << 0,
                OnUpdate = 1 << 1,
                OnDestroy = 1 << 2,
                OnStartRunning = 1 << 3,
                OnStopRunning = 1 << 4,
            }
            internal static CallFlags Flags = CallFlags.None;

            public void OnCreate(ref SystemState state)
            {
                Flags |= CallFlags.OnCreate;
            }

            public void OnUpdate(ref SystemState state)
            {
                Flags |= CallFlags.OnUpdate;
            }

            public void OnDestroy(ref SystemState state)
            {
                Flags |= CallFlags.OnDestroy;
            }

            public void OnStartRunning(ref SystemState state)
            {
                Flags |= CallFlags.OnStartRunning;
            }

            public void OnStopRunning(ref SystemState state)
            {
                Flags |= CallFlags.OnStopRunning;
            }

            public static void AssertCallsWereMade(CallFlags flags)
            {
                if (flags != (Flags & flags))
                {
                    Assert.Fail($"Expected {flags} but have {Flags & flags}");
                }
            }

            public static void AssertCallsWereNotMade(CallFlags flags)
            {
                if (0 != (Flags & flags))
                {
                    Assert.Fail($"Expected nothing, but have {Flags & flags}");
                }
            }
        }

        [Test]
        public void UnmanagedSystemUpdate()
        {
            SnoopSystem.Flags = 0;

            using (var world = new World("Temp"))
            {
                var group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

                var sysRef = world.CreateSystem<SnoopSystem>();

                group.AddSystemToUpdateList(sysRef);

                SnoopSystem.AssertCallsWereMade(SnoopSystem.CallFlags.OnCreate);

                world.Update();

                SnoopSystem.AssertCallsWereMade(SnoopSystem.CallFlags.OnUpdate | SnoopSystem.CallFlags.OnStartRunning);
            }

            SnoopSystem.AssertCallsWereMade(SnoopSystem.CallFlags.OnDestroy | SnoopSystem.CallFlags.OnStopRunning);
        }

        [Test]
        public void GenericUnmanagedSystem_Works()
        {
            using (var world = new World("Temp"))
            {
                var group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

                var sysRefFloat = world.CreateSystem<MyGenericISystem<float>>();

                group.AddSystemToUpdateList(sysRefFloat);

                Assert.That(world.EntityManager
                                .CreateEntityQuery(ComponentType.ReadWrite<MySentinelGenericComponent<float>>())
                                .CalculateEntityCount() ==
                            1);
                
                var sysRefAnotherAssemblyFloat = world.CreateSystem<MyGenericISystem_FromAnotherAssembly<float>>();

                group.AddSystemToUpdateList(sysRefAnotherAssemblyFloat);

                Assert.That(world.EntityManager
                                .CreateEntityQuery(ComponentType.ReadWrite<MySentinelGenericComponent_FromAnotherAssembly<float>>())
                                .CalculateEntityCount() ==
                            1);

                var sysRefAnotherAssemblyTestType1 = world.CreateSystem<MyGenericISystem_FromAnotherAssembly<TypeManagerTests.TestType1>>();

                group.AddSystemToUpdateList(sysRefFloat);

                Assert.That(world.EntityManager
                                .CreateEntityQuery(ComponentType.ReadWrite<MySentinelGenericComponent_FromAnotherAssembly<TypeManagerTests.TestType1>>())
                                .CalculateEntityCount() ==
                            1);

                var sysRefNested = world.CreateSystem<MyGenericNestingType<float>.MyNestedGenericISystem>();

                group.AddSystemToUpdateList(sysRefFloat);

                Assert.That(world.EntityManager
                                .CreateEntityQuery(ComponentType.ReadWrite<MySentinelGenericComponent_FromAnotherAssembly<MyGenericNestingType<float>.MyNestedGenericISystem>>())
                                .CalculateEntityCount() ==
                            1);
            }
        }

        [Test]
        public unsafe void UnmanagedSystemNoDuplicateStopSystem()
        {
            SnoopSystem.Flags = 0;

            using (var world = new World("Temp"))
            {
                var group = world.GetOrCreateSystemManaged<SimulationSystemGroup>();

                var sysRef = world.CreateSystem<SnoopSystem>();

                @group.AddSystemToUpdateList(sysRef);

                world.Update();

                SnoopSystem.AssertCallsWereMade(SnoopSystem.CallFlags.OnUpdate);

                world.Unmanaged.ResolveSystemState(sysRef)->Enabled = false;

                SnoopSystem.Flags = SnoopSystem.CallFlags.None;
                world.Update();

                SnoopSystem.AssertCallsWereNotMade(SnoopSystem.CallFlags.OnUpdate);
                SnoopSystem.AssertCallsWereMade(SnoopSystem.CallFlags.OnStopRunning);

                SnoopSystem.Flags = SnoopSystem.CallFlags.None;
            }

            SnoopSystem.AssertCallsWereMade(SnoopSystem.CallFlags.OnDestroy);
            SnoopSystem.AssertCallsWereNotMade(SnoopSystem.CallFlags.OnStopRunning);
        }

        private partial struct JobWritesComponentSystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.CreateEntity(ComponentType.ReadWrite<EcsTestData>());
            }
            public void OnDestroy(ref SystemState state) { }

            public void OnUpdate(ref SystemState state)
            {
                new TestJob().Schedule();
            }
            partial struct TestJob : IJobEntity
            {
                void Execute(ref EcsTestData normalComponent)
                {
                    normalComponent.value = 1;
                }
            }

        }

        [UpdateAfter(typeof(JobWritesComponentSystem))]
        partial struct ReadComponentSystem : ISystem, ISystemStartStop
        {
            public void OnCreate(ref SystemState state) { }
            public void OnDestroy(ref SystemState state) { }
            public void OnUpdate(ref SystemState state) { }
            public void OnStartRunning(ref SystemState state)
            {
                // For dependencies to complete properly, BeforeOnUpdate needs to have been called.
                state.CompleteDependency();
                SystemAPI.SetSingleton<EcsTestData>(default);
            }
            public void OnStopRunning(ref SystemState state) { }
        }
        
        [Test]
        public void ISystem_SingletonInOnStartRunning()
        {
            var sysA = World.CreateSystem<JobWritesComponentSystem>();
            var sysB = World.CreateSystem<ReadComponentSystem>();
        
            sysA.Update(World.Unmanaged);
            sysB.Update(World.Unmanaged);
        
            World.DestroySystem(sysB);
            World.DestroySystem(sysA);
        }
    }
}
