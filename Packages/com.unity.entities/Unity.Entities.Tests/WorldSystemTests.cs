using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    public class WorldSystemTests : ECSTestsCommonBase
    {
        partial class EmptyTestSystem : SystemBase
        {
            protected override void OnUpdate() {}
        }

        partial struct EmptyTestISystem : ISystem
        {
        }

        static void CheckManagedSystemExists<T>(World world, T system) where T : SystemBase
        {
            Assert.AreEqual(system, world.GetExistingSystemManaged<T>());
            Assert.AreEqual(1, world.Systems.Count);
            Assert.AreEqual(system, world.Systems[0]);
            Assert.AreEqual(SystemHandle.Null, world.Unmanaged.ExecutingSystem);
        }

        static void CheckUnmanagedSystemExists<T>(World world, SystemHandle system) where T : unmanaged, ISystem
        {
            Assert.AreEqual(system, world.GetExistingSystem<T>());
            var systems = world.Unmanaged.GetAllUnmanagedSystems(Allocator.Temp);
            Assert.AreEqual(1, systems.Length);
            Assert.AreEqual(system, systems[0]);
            Assert.AreEqual(SystemHandle.Null, world.Unmanaged.ExecutingSystem);
        }
        static void CheckManagedSystemEmpty<T>(World world) where T : SystemBase
        {
            Assert.AreEqual(null, world.GetExistingSystemManaged<T>());
            Assert.AreEqual(0, world.Systems.Count);
            Assert.AreEqual(SystemHandle.Null, world.Unmanaged.ExecutingSystem);
        }

        static void CheckUnmanagedSystemEmpty<T>(World world) where T : unmanaged, ISystem
        {
            Assert.AreEqual(SystemHandle.Null, world.GetExistingSystem<T>());
            Assert.AreEqual(0, world.Unmanaged.GetAllUnmanagedSystems(Allocator.Temp).Length);
            Assert.AreEqual(SystemHandle.Null, world.Unmanaged.ExecutingSystem);
        }

        [Test]
        public void WorldVersionIsConsistentSystem()
        {
            using (var world = new World("WorldX"))
            {
                Assert.AreEqual(0, world.Version);

                var version = world.Version;
                world.GetOrCreateSystemManaged<EmptyTestSystem>();
                Assert.AreNotEqual(version, world.Version);

                version = world.Version;
                var manager = world.GetOrCreateSystemManaged<EmptyTestSystem>();
                Assert.AreEqual(version, world.Version);

                version = world.Version;
                world.DestroySystemManaged(manager);
                Assert.AreNotEqual(version, world.Version);
            }
        }

        [Test]
        public unsafe void ManagedSystemEntityExistsAndDestroyed_WithSystem()
        {
            using (var world = new World("WorldX"))
            {
                EmptyTestSystem system = world.GetOrCreateSystemManaged<EmptyTestSystem>();
                Assert.AreEqual(world.GetExistingSystemManaged<EmptyTestSystem>(), system);
                Assert.AreEqual(world.GetExistingSystem<EmptyTestSystem>(), system.SystemHandle);

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));
                using (var systemEntities = systemQuery.ToEntityArray(world.UpdateAllocator.ToAllocator))
                {
                    Assert.That((IntPtr)world.EntityManager.GetComponentData<SystemInstance>(system.SystemHandle.m_Entity).state, Is.EqualTo((IntPtr)system.CheckedState()));
                    Assert.That(systemEntities[0], Is.EqualTo(system.SystemHandle.m_Entity));
                }

                world.DestroySystemManaged(system);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public unsafe void ManagedSystemEntityExistsAndDestroyed_WithHandle()
        {
            using (var world = new World("WorldX"))
            {
                SystemHandle system = world.GetOrCreateSystem<EmptyTestSystem>();
                Assert.AreEqual(world.GetExistingSystemManaged<EmptyTestSystem>().SystemHandle, system);
                Assert.AreEqual(world.GetExistingSystem<EmptyTestSystem>(), system);

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));
                using (var systemEntities = systemQuery.ToEntityArray(world.UpdateAllocator.ToAllocator))
                {
                    Assert.That((IntPtr)world.EntityManager.GetComponentData<SystemInstance>(system.m_Entity).state,
                        Is.EqualTo((IntPtr)world.Unmanaged.ResolveSystemState(system)));
                    Assert.That(systemEntities[0], Is.EqualTo(system.m_Entity));
                }

                world.DestroySystem(system);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public unsafe void ManagedSystemEntityExistsAndDestroyed_WithType()
        {
            using (var world = new World("WorldX"))
            {
                SystemHandle system = world.GetOrCreateSystem(typeof(EmptyTestSystem));
                Assert.AreEqual(world.GetExistingSystemManaged<EmptyTestSystem>().SystemHandle, system);
                Assert.AreEqual(world.GetExistingSystem<EmptyTestSystem>(), system);

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));
                using (var systemEntities = systemQuery.ToEntityArray(world.UpdateAllocator.ToAllocator))
                {
                    Assert.That((IntPtr)world.EntityManager.GetComponentData<SystemInstance>(system.m_Entity).state,
                        Is.EqualTo((IntPtr)world.Unmanaged.ResolveSystemState(system)));
                    Assert.That(systemEntities[0], Is.EqualTo(system.m_Entity));
                }

                world.DestroySystem(system);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public unsafe void UnmanagedSystemEntityExistsAndDestroyed_WithHandle()
        {
            using (var world = new World("WorldX"))
            {
                SystemHandle system = world.GetOrCreateSystem<EmptyTestISystem>();
                Assert.AreEqual(world.GetExistingSystem<EmptyTestISystem>(), system);
                Assert.AreEqual(world.Unmanaged.GetExistingUnmanagedSystem<EmptyTestISystem>(), system);

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                using var systemEntities = systemQuery.ToEntityArray(world.UpdateAllocator.ToAllocator);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));

                Assert.That((IntPtr)world.EntityManager.GetComponentData<SystemInstance>(system.m_Entity).state,
                    Is.EqualTo((IntPtr)world.Unmanaged.ResolveSystemState(system)));
                Assert.That(systemEntities[0], Is.EqualTo(system.m_Entity));

                world.DestroySystem(system);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public unsafe void UnmanagedSystemEntityExistsAndDestroyed_WithType()
        {
            using (var world = new World("WorldX"))
            {
                SystemHandle system = world.GetOrCreateSystem(typeof(EmptyTestISystem));
                Assert.AreEqual(world.GetExistingSystem<EmptyTestISystem>(), system);
                Assert.AreEqual(world.Unmanaged.GetExistingUnmanagedSystem<EmptyTestISystem>(), system);

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                using var systemEntities = systemQuery.ToEntityArray(world.UpdateAllocator.ToAllocator);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));

                Assert.That((IntPtr)world.EntityManager.GetComponentData<SystemInstance>(system.m_Entity).state,
                    Is.EqualTo((IntPtr)world.Unmanaged.ResolveSystemState(system)));
                Assert.That(systemEntities[0], Is.EqualTo(system.m_Entity));

                world.DestroySystem(system);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public unsafe void ManagedSystemEntityDestroyAll()
        {
            using (var world = new World("WorldX"))
            {
                EmptyTestSystem system = world.GetOrCreateSystemManaged<EmptyTestSystem>();

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));

                world.DestroyAllSystemsAndLogException(out bool errorsWhileDestroyingSystems);
                Assert.IsFalse(errorsWhileDestroyingSystems);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));

                world.GetOrCreateSystemManaged<EmptyTestSystem>();
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));
            }
        }

        [Test]
        public unsafe void UnmanagedSystemEntityDestroyAll()
        {
            using (var world = new World("WorldX"))
            {
                SystemHandle system = world.GetOrCreateSystem<EmptyTestISystem>();

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));

                world.DestroyAllSystemsAndLogException(out bool errorsWhileDestroyingSystems);
                Assert.IsFalse(errorsWhileDestroyingSystems);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));

                world.GetOrCreateSystem<EmptyTestISystem>();
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(1));
            }
        }

        [Test]
        public unsafe void ManagedSystemMultipleEntitiesSameType()
        {
            using (var world = new World("WorldX"))
            {
                world.CreateSystemManaged<EmptyTestSystem>();
                world.CreateSystemManaged<EmptyTestSystem>();

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(2));

                world.DestroyAllSystemsAndLogException(out bool errorsWhileDestroyingSystems);
                Assert.IsFalse(errorsWhileDestroyingSystems);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public unsafe void UnmanagedSystemMultipleEntitiesSameType()
        {
            using (var world = new World("WorldX"))
            {
                world.CreateSystem<EmptyTestISystem>();
                world.CreateSystem<EmptyTestISystem>();

                using var systemQuery = world.EntityManager.CreateEntityQuery(typeof(SystemInstance));
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(2));

                world.DestroyAllSystemsAndLogException(out bool errorsWhileDestroyingSystems);
                Assert.IsFalse(errorsWhileDestroyingSystems);
                Assert.That(systemQuery.CalculateEntityCount(), Is.EqualTo(0));
            }
        }

        [Test]
        public void WorldVersionIsConsistentISystem()
        {
            using (var world = new World("WorldX"))
            {
                Assert.AreEqual(0, world.Version);

                var version = world.Version;
                world.GetOrCreateSystem<EmptyTestISystem>();
                Assert.AreNotEqual(version, world.Version);

                version = world.Version;
                var manager = world.GetOrCreateSystem<EmptyTestISystem>();
                Assert.AreEqual(version, world.Version);

                version = world.Version;
                world.DestroySystem(manager);
                Assert.AreNotEqual(version, world.Version);
            }
        }

        [Test]
        public void NeverAddedSystemReturnsNull()
        {
            using (var world = new World("WorldX"))
            {
                CheckUnmanagedSystemEmpty<EmptyTestISystem>(world);
                CheckManagedSystemEmpty<EmptyTestSystem>(world);
            }
        }

        [Test]
        [TestRequiresCollectionChecks]
        public void UsingDisposedWorldThrows()
        {
            var world = new World("WorldX");
            var unmanagedWorld = world.Unmanaged;
            world.Dispose();

            Assert.Throws<ObjectDisposedException>(() => world.GetExistingSystemManaged<EmptyTestSystem>());
            Assert.Throws<ObjectDisposedException>(() => unmanagedWorld.GetExistingUnmanagedSystem<EmptyTestISystem>());
        }

        partial class SystemThrowingInOnCreateIsRemovedSystem : SystemBase
        {
            protected override void OnCreate()
            {
                throw new AssertionException("");
            }

            protected override void OnDestroy()
            {
                UnityEngine.Debug.LogError("Should never be called");
            }

            protected override void OnUpdate() {UnityEngine.Debug.LogError("Should never be called"); }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void SystemThrowingInOnCreateIsRemoved()
        {
            using (var world = new World("WorldX"))
            {
                Assert.Throws<AssertionException>(() => world.GetOrCreateSystemManaged<SystemThrowingInOnCreateIsRemovedSystem>());
                // throwing during OnCreateManager does not add the manager to the behaviour manager list
                CheckManagedSystemEmpty<SystemThrowingInOnCreateIsRemovedSystem>(world);
            }
        }

        [BurstCompile]
        partial struct SystemThrowingInOnCreateIsRemovedISystem : ISystem
        {
            [BurstCompile( CompileSynchronously = true)]
            public void OnCreate(ref SystemState state)
            {
                throw new AssertionException("");
            }
            public void OnDestroy(ref SystemState state) { UnityEngine.Debug.LogError("Should never be called"); }
            public void OnUpdate(ref SystemState state) { UnityEngine.Debug.LogError("Should never be called"); }
        }

        [Test]
#if !UNITY_WEBGL
        [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void ISystemThrowingInOnCreateIsRemoved()
        {
            using (var world = new World("WorldX"))
            {
                // Flexible on what exception is thrown because burst might change it
                if (IsBurstEnabled())
                    Assert.Throws<InvalidOperationException>(() => world.CreateSystem<SystemThrowingInOnCreateIsRemovedISystem>());
                else
                    Assert.Throws<AssertionException>(() => world.CreateSystem<SystemThrowingInOnCreateIsRemovedISystem>());

                // throwing during OnCreateManager does not add the manager to the behaviour manager list
                CheckUnmanagedSystemEmpty<SystemThrowingInOnCreateIsRemovedISystem>(world);
            }
        }

        partial class SystemIsAccessibleDuringOnCreateSystem : SystemBase
        {
            protected override void OnCreate()
            {
                Assert.AreEqual(this, World.GetOrCreateSystemManaged<SystemIsAccessibleDuringOnCreateSystem>());
            }

            protected override void OnUpdate() {}
        }

        [Test]
        public void SystemIsAccessibleDuringOnCreate()
        {
            using (var world = new World("WorldX"))
            {
                var system = world.CreateSystemManaged<SystemIsAccessibleDuringOnCreateSystem>();
                CheckManagedSystemExists(world, system);
            }
        }

        partial struct SystemIsAccessibleDuringOnCreateISystem : ISystem
        {
            unsafe public void OnCreate(ref SystemState state)
            {
                var systemFind = state.World.GetExistingSystem<SystemIsAccessibleDuringOnCreateISystem>();
                var systemCreate = state.World.GetOrCreateSystem<SystemIsAccessibleDuringOnCreateISystem>();
                Assert.IsTrue(UnsafeUtility.AddressOf(ref this) == state.WorldUnmanaged.ResolveSystemState(systemFind)->m_SystemPtr);
                Assert.IsTrue(UnsafeUtility.AddressOf(ref this) == state.WorldUnmanaged.ResolveSystemState(systemCreate)->m_SystemPtr);

                Assert.AreEqual(state.SystemHandle, systemFind);
                Assert.AreEqual(state.SystemHandle, systemCreate);
            }

            }

        [Test]
        public void ISystemIsAccessibleDuringOnCreate()
        {
            using (var world = new World("WorldX"))
            {
                var res = world.CreateSystem<SystemIsAccessibleDuringOnCreateISystem>();

                CheckUnmanagedSystemExists<SystemIsAccessibleDuringOnCreateISystem>(world, res);
            }
        }

        [Test]
        public void GetAllUnmanagedSystemsOnlyReturnsOnlyUnmanaged()
        {
            using (var world = new World("WorldX"))
            {
                var resUnmanaged = world.CreateSystem<SystemIsAccessibleDuringOnCreateISystem>();
                var resManaged = world.CreateSystem<SystemIsAccessibleDuringOnCreateSystem>();

                var systems = world.Unmanaged.GetAllUnmanagedSystems(Allocator.Temp);
                Assert.AreEqual(1, systems.Length);
            }
        }

        [Test]
        public void GetAllSystemsReturnsAll()
        {
            using (var world = new World("WorldX"))
            {
                var resUnmanaged = world.CreateSystem<SystemIsAccessibleDuringOnCreateISystem>();
                var resManaged = world.CreateSystem<SystemIsAccessibleDuringOnCreateSystem>();

                var systems = world.Unmanaged.GetAllSystems(Allocator.Temp);
                Assert.AreEqual(2, systems.Length);
            }
        }

        [Test]
        public void SystemIsFindableAndRemovable()
        {
            using (var world = new World("WorldX"))
            {
                var managerM = world.GetOrCreateSystemManaged<EmptyTestSystem>();
                CheckManagedSystemExists(world, managerM);
                world.DestroySystemManaged(managerM);
                CheckManagedSystemEmpty<EmptyTestSystem>(world);
            }
        }

        [Test]
        public void ISystemIsFindableAndRemovable()
        {
            using (var world = new World("WorldX"))
            {
                var managerI = world.GetOrCreateSystem<EmptyTestISystem>();

                CheckUnmanagedSystemExists<EmptyTestISystem>(world, managerI);
                world.DestroySystem(managerI);
                CheckUnmanagedSystemEmpty<EmptyTestISystem>(world);
            }
        }

        partial class CantDestroyDuringSystemExecution : SystemBase
        {
            protected override void OnCreate()
            {
                Assert.Throws<ArgumentException>(() => World.DestroySystemManaged(this));
                Assert.Throws<ArgumentException>(() => World.Dispose());
            }

            protected override void OnDestroy()
            {
                Assert.Throws<ArgumentException>(() => World.DestroySystemManaged(this));
                Assert.Throws<ArgumentException>(() => World.Dispose());
            }

            protected override void OnUpdate()
            {
                Assert.Throws<ArgumentException>(() => World.DestroySystemManaged(this));
                Assert.Throws<ArgumentException>(() => World.Dispose());
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void CantDestroySystemDuringSystemExecutionTest()
        {
            //using (var world = new World("WorldX"))
            var world = new World("WorldX");
            {
                var system = world.CreateSystemManaged<CantDestroyDuringSystemExecution>();
                CheckManagedSystemExists(world, system);
                world.DestroySystemManaged(system);
                CheckManagedSystemEmpty<CantDestroyDuringSystemExecution>(world);

                if (world.Unmanaged.ExecutingSystem != SystemHandle.Null)
                    throw new ArgumentException("Boinboing " + world.Unmanaged.ExecutingSystem.m_WorldSeqNo);
                world.Dispose();
            }
        }

        partial struct CantDestroyDuringISystemExecution : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                var world = state.World;
                var systemHandle = state.SystemHandle;
                Assert.Throws<ArgumentException>(() => world.DestroySystem(systemHandle));
                Assert.Throws<ArgumentException>(() => world.Dispose());
            }

            public void OnDestroy(ref SystemState state)
            {
                var world = state.World;
                var systemHandle = state.SystemHandle;
                Assert.Throws<ArgumentException>(() => world.DestroySystem(systemHandle));
                Assert.Throws<ArgumentException>(() => world.Dispose());
            }

            public void OnUpdate(ref SystemState state)
            {
                var world = state.World;
                var systemHandle = state.SystemHandle;
                Assert.Throws<ArgumentException>(() => world.DestroySystem(systemHandle));
                Assert.Throws<ArgumentException>(() => world.Dispose());
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires system safety checks")]
        public void CantDestroySystemDuringISystemExecutionTest()
        {
            using (var world = new World("WorldX"))
            {
                var system = world.CreateSystem<CantDestroyDuringISystemExecution>();
                // While CantDestroyDuringISystemExecution attempts to destroy stuff, it is prevented before any state is modified... Thus the system should be fully initialized here.
                CheckUnmanagedSystemExists<CantDestroyDuringISystemExecution>(world, system);
                world.DestroySystem(system);
                CheckUnmanagedSystemEmpty<CantDestroyDuringISystemExecution>(world);
            }
        }

        partial class ThrowDuringDestroySystem : SystemBase
        {
            protected override void OnDestroy()
            {
                throw new ArgumentException();
            }

            protected override void OnUpdate() { }
        }

        [Test]
        public void ThrowDuringDestroyStillRemovesSystem()
        {
            using (var world = new World("WorldX"))
            {
                var system = world.CreateSystemManaged<ThrowDuringDestroySystem>();
                CheckManagedSystemExists(world, system);
                Assert.Throws<ArgumentException>(() => world.DestroySystemManaged(system));
                CheckManagedSystemEmpty<ThrowDuringDestroySystem>(world);
            }
        }

        partial struct ThrowDuringDestroyISystem : ISystem
        {
            public void OnDestroy(ref SystemState systemState)
            {
                throw new ArgumentException();
            }
        }

        [Test]
        public void ThrowDuringDestroyStillRemovesISystem()
        {
            using (var world = new World("WorldX"))
            {
                var system = world.CreateSystem<ThrowDuringDestroyISystem>();
                CheckUnmanagedSystemExists<ThrowDuringDestroyISystem>(world, system);
                Assert.Throws<ArgumentException>(() => world.DestroySystem(system));
                CheckUnmanagedSystemEmpty<ThrowDuringDestroyISystem>(world);
            }
        }

        public void TestCreateISystemAndLogExceptionsFailureIsolation(Type badSystem)
        {
            using (var world = new World("WorldX"))
            {
                var unmanagedTypes = new NativeList<SystemTypeIndex>(2, Allocator.Temp);
                unmanagedTypes.Add(TypeManager.GetSystemTypeIndex(badSystem));
                unmanagedTypes.Add(TypeManager.GetSystemTypeIndex<EmptyTestISystem>());

                var unmanagedSystemHandles =
                    world.GetOrCreateSystemsAndLogException(unmanagedTypes, unmanagedTypes.Length, Allocator.Temp);

                Assert.AreEqual(2, unmanagedSystemHandles.Length);
                Assert.AreEqual(SystemHandle.Null, unmanagedSystemHandles[0]);
                Assert.AreEqual(SystemHandle.Null, world.Unmanaged.GetExistingUnmanagedSystem(badSystem));

                // Other systems are unaffected
                Assert.AreEqual(typeof(EmptyTestISystem), world.Unmanaged.GetTypeOfSystem(unmanagedSystemHandles[1]));
                unmanagedSystemHandles[1].Update(world.Unmanaged);

                unmanagedSystemHandles.Dispose();
            }
        }
    }
}
