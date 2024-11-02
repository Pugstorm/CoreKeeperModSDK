using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Burst.Intrinsics;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine.TestTools;

#if UNITY_EDITOR
/// <summary>
/// Test that various combinations of throwing exceptions or normal runs keep the correct system state \
/// (IsInForEachDisallowStructuralChange World.ExecutingSystem, GetCurrentSystemFromJobDebugger)
/// </summary>
partial class SystemSafetyTests_SystemStateConsistency : ECSTestsFixture
{
   public unsafe static void CheckCorrectStateInSystem(ref SystemState systemState, bool inForEach)
    {
        if (inForEach != systemState.EntityManager.Debug.IsInForEachDisallowStructuralChange)
        {
            if (inForEach)
                throw new ArgumentException("We are in a Entities.ForEach but IsInForEachDisallowStructuralChange is false");
            else
                throw new ArgumentException("We are not in a Entities.ForEach but IsInForEachDisallowStructuralChange is true");
        }

        if (systemState.WorldUnmanaged.ExecutingSystem != systemState.m_Handle)
            throw new ArgumentException("ExecutingSystem is not set up correctly during system execution");

        //NOTE: can't use SystemState.GetCurrentSystemFromJobDebugger here directly since that accesses managed data at the moment.
        if (SystemState.GetCurrentSystemIDFromJobDebugger() != systemState.m_SystemID)
            throw new ArgumentException("GetCurrentSystemFromJobDebugger is wrong");
    }

    public unsafe static void CheckCorrectStateOutsideSystem(WorldUnmanaged world)
    {
        if (world.EntityManager.Debug.IsInForEachDisallowStructuralChange)
            throw new ArgumentException("We are not in a system but IsInForEachDisallowStructuralChange is true");

        if (world.ExecutingSystem != default)
            throw new ArgumentException("ExecutingSystem was not restored back to default");

        if (SystemState.GetCurrentSystemIDFromJobDebugger() != 0)
            throw new ArgumentException($"We are not in a system but GetCurrentSystemFromJobDebugger is not null {SystemState.GetCurrentSystemIDFromJobDebugger()}");
    }

    class ExpectedException : System.Exception
    {
    }

    struct ThrowExceptionJob : IJobChunk
    {
        public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
        {
            throw new ExpectedException();
        }
    }

    [BurstCompile(CompileSynchronously = true)]
    partial struct ThrowDuringIJobChunkISystem : ISystem
    {
        EntityQuery _Query;

        public void OnCreate(ref SystemState state)
        {
            _Query = state.GetEntityQuery(typeof(EcsTestData));
        }

        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref SystemState state)
        {
            CheckCorrectStateInSystem(ref state, false);

            var job = new ThrowExceptionJob {};
            Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, _Query);
        }
    }

    partial class ThrowDuringIJobChunkSystemBase : SystemBase
    {
        EntityQuery _Query;

        override protected void OnCreate()
        {
            _Query = GetEntityQuery(typeof(EcsTestData));
        }

        unsafe protected override void OnUpdate()
        {
            CheckCorrectStateInSystem(ref *m_StatePtr, false);

            var job = new ThrowExceptionJob {};
            Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, _Query);
        }
    }

    [Test]
#if !UNITY_WEBGL
    [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
    public void ForEachProtectionDoesntLeakWhenThrowingISystemBase()
    {
        m_Manager.CreateEntity(typeof(EcsTestData));

        // NOTE: Burst always throws InvalidOperationException independent of what the actual exception was
        if (IsBurstEnabled())
        {
            Assert.Throws<InvalidOperationException>(() =>
            {
                var throwSystem = World.CreateSystem<ThrowDuringIJobChunkISystem>();
                throwSystem.Update(World.Unmanaged);
            });
        }
        else
        {
            Assert.Throws<ExpectedException>(() =>
            {
                var throwSystem = World.CreateSystem<ThrowDuringIJobChunkISystem>();
                throwSystem.Update(World.Unmanaged);
            });
        }

        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }

    [Test]
    public void ForEachProtectionDoesntLeakWhenThrowingSystemBase()
    {
        m_Manager.CreateEntity(typeof(EcsTestData));
        var throwSystem = World.CreateSystemManaged<ThrowDuringIJobChunkSystemBase>();

        Assert.Throws<ExpectedException>(() => { throwSystem.Update(); });

        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }

    partial class ExecuteOtherSystem : SystemBase
    {
        unsafe protected override void OnUpdate()
        {
            CheckCorrectStateInSystem(ref *m_StatePtr, false);

            Assert.Throws<ExpectedException>(() =>
            {
                World.GetOrCreateSystemManaged<ThrowDuringIJobChunkSystemBase>().Update();
            });

            CheckCorrectStateInSystem(ref *m_StatePtr, false);

            // NOTE: Burst always throws InvalidOperationException independent of what the actual exception was
            if (IsBurstEnabled())
            {
                Assert.Throws<InvalidOperationException>(() =>
                {
                    World.GetOrCreateSystem<ThrowDuringIJobChunkISystem>().Update(World.Unmanaged);
                });
            }
            else
            {
                Assert.Throws<ExpectedException>(() =>
                {
                    World.GetOrCreateSystem<ThrowDuringIJobChunkISystem>().Update(World.Unmanaged);
                });
            }

            CheckCorrectStateInSystem(ref *m_StatePtr, false);
        }
    }

    [Test]
#if !UNITY_WEBGL
    [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
    public void CallUpdateFromUpdateWorks()
    {
        m_Manager.CreateEntity(typeof(EcsTestData));

        var system= World.CreateSystemManaged<ExecuteOtherSystem>();
        system.Update();
        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }

    [BurstCompile]
    partial struct ForEachRecursionSystem : ISystem
    {
        EntityQuery _Query;

        unsafe struct RecursiveForEachJob : IJobChunk
        {
            public int depth;
            public NativeReference<int> Sum;
            public EntityQuery Query;
            public EntityManager.EntityManagerDebug Debug;

            public void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                Assert.IsFalse(useEnabledMask);
                if (!Debug.IsInForEachDisallowStructuralChange)
                    throw new ArgumentException();
                if (depth == 5)
                    return;

                Sum.Value++;

                var job = this;
                job.depth++;
                Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, Query);
            }
        }

        public void OnCreate(ref SystemState state)
        {
            _Query = state.GetEntityQuery(typeof(EcsTestData));
        }

        [BurstCompile(CompileSynchronously = true)]
        public void OnUpdate(ref SystemState state)
        {
            CheckCorrectStateInSystem(ref state, false);

            using var value = new NativeReference<int>(state.WorldUnmanaged.UpdateAllocator.ToAllocator);

            var job = new RecursiveForEachJob { depth = 0, Sum = value, Debug = state.EntityManager.Debug, Query = _Query};
            Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, _Query);

            CheckCorrectStateInSystem(ref state, false);

            if (value.Value != 5)
                throw new ArgumentException($"Should process 5 but didn't {value.Value}");
        }
    }

    [Test]
    public void ForEachRecursionWorks()
    {
        m_Manager.CreateEntity(typeof(EcsTestData));

        var system = World.CreateSystem<ForEachRecursionSystem>();
        system.Update(World.Unmanaged);

        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }

    partial class CatchInSystemIsRestoredAfterSystem : SystemBase
    {
        unsafe override protected void OnUpdate()
        {
            try
            {
                var job = new ThrowExceptionJob {};
                Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, GetEntityQuery(ComponentType.ReadOnly<EcsTestData>()));
            }
            catch
            {
            }

            // NOTE:
            // IsInForEachDisallowStructuralChange is expected to be incorrect since we threw an exception inside the system until the system completes.
            // At which point it will be properly restored.
            // This isn't strictly required but it is the current behaviour since we want to avoid try/catch RunWithoutJobs");
            CheckCorrectStateInSystem(ref *m_StatePtr, true);
        }
    }

    [Test]
    public void CatchInSystemIsRestoredAfterSystemRun()
    {
        m_Manager.CreateEntity(typeof(EcsTestData));

        var system = World.CreateSystemManaged<CatchInSystemIsRestoredAfterSystem>();
        system.Update();

        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }

    [Test]
    public void SanityTest()
    {
        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }

    partial struct CatchInSystemIsRestoredAfterISystem : ISystem
    {
        unsafe public void OnUpdate(ref SystemState systemState)
        {
            try
            {
                var job = new ThrowExceptionJob {};
                Unity.Entities.Internal.InternalCompilerInterface.JobChunkInterface.RunByRefWithoutJobs(ref job, systemState.GetEntityQuery(ComponentType.ReadOnly<EcsTestData>()));
            }
            catch
            {
            }

            if (systemState.WorldUnmanaged.ExecutingSystem != systemState.SystemHandle)
                throw new ArgumentException("ExecutingSystem is not correct");
            if (!systemState.EntityManager.Debug.IsInForEachDisallowStructuralChange)
                throw new ArgumentException("IsInForEachDisallowStructuralChange is expected to be incorrect since we threw an exception inside the system until the system completes. At which point it will be properly restored. NOTE: This isn't strictly required but it is the current behaviour since we want to avoid try/catch RunWithoutJobs");
        }
    }

    [Test]
    public void CatchInSystemIsRestoredAfterISystemRun()
    {
        m_Manager.CreateEntity(typeof(EcsTestData));

        var system = World.CreateSystem<CatchInSystemIsRestoredAfterISystem>();
        system.Update(World.Unmanaged);

        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }

    partial class CreateAndUpdateNewWorldInSystem : SystemBase
    {
        unsafe override protected void OnUpdate()
        {
            CheckCorrectStateInSystem(ref *m_StatePtr, false);
            var newWorld = new World("test");
            newWorld.EntityManager.CreateEntity(typeof(EcsTestData));
            //@TODO: run a system
            CheckCorrectStateInSystem(ref *m_StatePtr, false);

            newWorld.CreateSystemManaged<ExecuteOtherSystem>().Update();
            CheckCorrectStateInSystem(ref *m_StatePtr, false);

            newWorld.Dispose();
            CheckCorrectStateInSystem(ref *m_StatePtr, false);
        }
    }

    [Test]
#if !UNITY_WEBGL
    [ConditionalIgnore("IgnoreForCoverage", "Fails randonly when ran with code coverage enabled")]
#endif
    public void CreateAndUpdateNewWorldInSystemTest()
    {
        var system = World.CreateSystemManaged<CreateAndUpdateNewWorldInSystem>();
        system.Update();
        CheckCorrectStateOutsideSystem(World.Unmanaged);
    }
}
#endif
