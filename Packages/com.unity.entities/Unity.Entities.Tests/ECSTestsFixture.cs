using NUnit.Framework;
using Unity.Collections;
using Unity.Entities.CodeGeneratedJobForEach;
using Unity.Jobs.LowLevel.Unsafe;
using Assert = FastAssert;
using Unity.Burst;
using UnityEngine.LowLevel;

namespace Unity.Entities.Tests
{

    // If ENABLE_UNITY_COLLECTIONS_CHECKS is not defined we will ignore the test
    // When using this attribute, consider it to logically AND with any other TestRequiresxxxx attrubute
#if ENABLE_UNITY_COLLECTIONS_CHECKS
    internal class TestRequiresCollectionChecks : System.Attribute
    {
        public TestRequiresCollectionChecks(string msg = null) { }
    }
#else
    internal class TestRequiresCollectionChecks : IgnoreAttribute
    {
        public TestRequiresCollectionChecks(string msg = null) : base($"Test requires ENABLE_UNITY_COLLECTION_CHECKS which is not defined{(msg == null ? "." : $": {msg}")}") { }
    }
#endif

    // If ENABLE_UNITY_COLLECTIONS_CHECKS and UNITY_DOTS_DEBUG is not defined we will ignore the test
    // conversely if either of them are defined the test will be run.
    // When using this attribute, consider it to logically AND with any other TestRequiresxxxx attrubute
#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
    internal class TestRequiresDotsDebugOrCollectionChecks: System.Attribute
    {
        public TestRequiresDotsDebugOrCollectionChecks(string msg = null) { }
    }
#else
    internal class TestRequiresDotsDebugOrCollectionChecks : IgnoreAttribute
    {
        public TestRequiresDotsDebugOrCollectionChecks(string msg = null) : base($"Test requires UNITY_DOTS_DEBUG || ENABLE_UNITY_COLLECTION_CHECKS which neither are defined{(msg == null ? "." : $": {msg}")}") { }
    }
#endif

    // Ignores te test when in an il2cpp build only. Please make use of the 'msg' string
    // to tell others why this test should be ignored
#if !ENABLE_IL2CPP
    internal class IgnoreTest_IL2CPP: System.Attribute
    {
        public IgnoreTest_IL2CPP(string msg = null) { }
    }
#else
    internal class IgnoreTest_IL2CPP : IgnoreAttribute
    {
        public IgnoreTest_IL2CPP(string msg = null) : base($"Test ignored on IL2CPP builds{(msg == null ? "." : $": {msg}")}") { }
    }
#endif

    public partial class EmptySystem : SystemBase
    {
        protected override void OnUpdate() {}

        public new EntityQuery GetEntityQuery(params EntityQueryDesc[] queriesDesc)
        {
            return base.GetEntityQuery(queriesDesc);
        }

        public new EntityQuery GetEntityQuery(params ComponentType[] componentTypes)
        {
            return base.GetEntityQuery(componentTypes);
        }

        public new EntityQuery GetEntityQuery(NativeArray<ComponentType> componentTypes)
        {
            return base.GetEntityQuery(componentTypes);
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    public class ECSTestsCommonBase
    {
        [SetUp]
        public virtual void Setup()
        {
        }

        [TearDown]
        public virtual void TearDown()
        {
        }

        [BurstDiscard]
        static public void TestBurstCompiled(ref bool falseIfNot)
        {
            falseIfNot = false;
        }

        [BurstCompile(CompileSynchronously = true)]
        static public bool IsBurstEnabled()
        {
            bool burstCompiled = true;
            TestBurstCompiled(ref burstCompiled);
            return burstCompiled;
        }

    }

    public abstract class ECSTestsFixture : ECSTestsCommonBase
    {
        protected World m_PreviousWorld;
        protected World World;
        protected PlayerLoopSystem m_PreviousPlayerLoop;
        protected EntityManager m_Manager;
        protected EntityManager.EntityManagerDebug m_ManagerDebug;

        protected int StressTestEntityCount = 1000;
        private bool JobsDebuggerWasEnabled;

        [SetUp]
        public override void Setup()
        {
            base.Setup();

            // unit tests preserve the current player loop to restore later, and start from a blank slate.
            m_PreviousPlayerLoop = PlayerLoop.GetCurrentPlayerLoop();
            PlayerLoop.SetPlayerLoop(PlayerLoop.GetDefaultPlayerLoop());

            m_PreviousWorld = World.DefaultGameObjectInjectionWorld;
            World = World.DefaultGameObjectInjectionWorld = new World("Test World");
            World.UpdateAllocatorEnableBlockFree = true;
            m_Manager = World.EntityManager;
            m_ManagerDebug = new EntityManager.EntityManagerDebug(m_Manager);

            // Many ECS tests will only pass if the Jobs Debugger enabled;
            // force it enabled for all tests, and restore the original value at teardown.
            JobsDebuggerWasEnabled = JobsUtility.JobDebuggerEnabled;
            JobsUtility.JobDebuggerEnabled = true;

            JobsUtility.ClearSystemIds();

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            // In case entities journaling is initialized, clear it
            EntitiesJournaling.Clear();
#endif
        }

        [TearDown]
        public override void TearDown()
        {
            if (World != null && World.IsCreated)
            {
                // Note that World.Dispose() already completes all jobs. But some tests may leave tests running when
                // they return, but we can't safely run an internal consistency check with jobs running, so we
                // explicitly complete them here as well.
                World.EntityManager.CompleteAllTrackedJobs();

                // TODO(DOTS-9429): We should not need to explicitly destroy all systems here.
                // World.Dispose() already handles this. However, we currently need to destroy all systems before
                // calling CheckInternalConsistency, or else some tests trigger false positives (due to EntityQuery
                // filters holding references to shared component values, etc.).
                // We can't safely destroy all systems while jobs are running, so this call must come after the
                // CompleteAllTrackedJobs() call above.
                World.DestroyAllSystemsAndLogException(out bool errorsWhileDestroyingSystems);
                Assert.IsFalse(errorsWhileDestroyingSystems,
                    "One or more exceptions were thrown while destroying systems during test teardown; consult the log for details.");

                m_ManagerDebug.CheckInternalConsistency();

                World.Dispose();
                World = null;

                World.DefaultGameObjectInjectionWorld = m_PreviousWorld;
                m_PreviousWorld = null;
                m_Manager = default;
            }

            JobsUtility.JobDebuggerEnabled = JobsDebuggerWasEnabled;
            JobsUtility.ClearSystemIds();

            PlayerLoop.SetPlayerLoop(m_PreviousPlayerLoop);

            base.TearDown();
        }

        public void AssertDoesNotExist(Entity entity)
        {
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsFalse(m_Manager.Exists(entity));
        }

        public void AssertComponentData(Entity entity, int index)
        {
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsTrue(m_Manager.Exists(entity));

            Assert.AreEqual(-index, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
            Assert.AreEqual(-index, m_Manager.GetComponentData<EcsTestData2>(entity).value1);
            Assert.AreEqual(index, m_Manager.GetComponentData<EcsTestData>(entity).value);
        }

        public Entity CreateEntityWithDefaultData(int index)
        {
            var entity = m_Manager.CreateEntity(typeof(EcsTestData), typeof(EcsTestData2));

            // HasComponent & Exists setup correctly
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData>(entity));
            Assert.IsTrue(m_Manager.HasComponent<EcsTestData2>(entity));
            Assert.IsFalse(m_Manager.HasComponent<EcsTestData3>(entity));
            Assert.IsTrue(m_Manager.Exists(entity));

            // Create must initialize values to zero
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity).value0);
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData2>(entity).value1);
            Assert.AreEqual(0, m_Manager.GetComponentData<EcsTestData>(entity).value);

            // Setup some non zero default values
            m_Manager.SetComponentData(entity, new EcsTestData2(-index));
            m_Manager.SetComponentData(entity, new EcsTestData(index));

            AssertComponentData(entity, index);

            return entity;
        }

        public void AssertSameChunk(Entity e0, Entity e1)
        {
            Assert.AreEqual(m_Manager.GetChunk(e0), m_Manager.GetChunk(e1));
        }

        public void AssetHasChangeVersion<T>(Entity e, uint version) where T :
#if UNITY_DISABLE_MANAGED_COMPONENTS
        struct,
#endif
        IComponentData
        {
            var type = m_Manager.GetComponentTypeHandle<T>(true);
            var chunk = m_Manager.GetChunk(e);
            Assert.AreEqual(version, chunk.GetChangeVersion(ref type));
            Assert.IsFalse(chunk.DidChange(ref type, version));
            Assert.IsTrue(chunk.DidChange(ref type, version - 1));
        }

        public void AssetHasChunkOrderVersion(Entity e, uint version)
        {
            var chunk = m_Manager.GetChunk(e);
            Assert.AreEqual(version, chunk.GetOrderVersion());
        }

        public void AssetHasBufferChangeVersion<T>(Entity e, uint version) where T : unmanaged, IBufferElementData
        {
            var type = m_Manager.GetBufferTypeHandle<T>(true);
            var chunk = m_Manager.GetChunk(e);
            Assert.AreEqual(version, chunk.GetChangeVersion(ref type));
            Assert.IsFalse(chunk.DidChange(ref type, version));
            Assert.IsTrue(chunk.DidChange(ref type, version - 1));
        }

        public void AssetHasSharedChangeVersion<T>(Entity e, uint version) where T : unmanaged, ISharedComponentData
        {
            var type = m_Manager.GetSharedComponentTypeHandle<T>();
            var chunk = m_Manager.GetChunk(e);
            Assert.AreEqual(version, chunk.GetChangeVersion(type));
            Assert.IsFalse(chunk.DidChange(type, version));
            Assert.IsTrue(chunk.DidChange(type, version - 1));
        }

        partial class EntityForEachSystem : SystemBase
        {
            protected override void OnUpdate() {}
        }

        public EmptySystem EmptySystem
        {
            get
            {
                return World.DefaultGameObjectInjectionWorld.GetOrCreateSystemManaged<EmptySystem>();
            }
        }
    }
}
