using NUnit.Framework;
using Unity.Collections;
using Unity.Core;
using Unity.Entities;

// This is the example code used in
// Packages/com.unity.entities/Documentation~/allocators-custom-prebuilt.md

internal class ExampleWordUpdateAllocator
{
    #region world-update-allocator-system-base
    // Access world update allocator through SystemBase.WorldUpdateAllocator.
    unsafe partial class AllocateNativeArraySystem : SystemBase
    {
        public NativeArray<int> nativeArray = default;

        protected override void OnUpdate()
        {
            // Get world update allocator through SystemBase.WorldUpdateAllocator.
            var allocator = WorldUpdateAllocator;

            // Create a native array using world update allocator.
            nativeArray = CollectionHelper.CreateNativeArray<int>(5, allocator);
            for (int i = 0; i < 5; i++)
            {
                nativeArray[i] = i;
            }
        }
    }
    #endregion // world-update-allocator-system-base


    [Test]
    #region world-update-allocator-world
    // Access world update allocator through World.UpdateAllocator.
    public void WorldUpdateAllocatorFromWorld_works()
    {
        // Create a test world.
        World world = new World("Test World");

        // Create a native array using world update allocator.
        var nativeArray = CollectionHelper.CreateNativeArray<int>(5, world.UpdateAllocator.ToAllocator);
        for (int i = 0; i < 5; i++)
        {
            nativeArray[i] = i;
        }

        Assert.AreEqual(nativeArray[3], 3);

        // Dispose the test world.
        world.Dispose();
    }
    #endregion // world-update-allocator-world

    [Test]
    public void WorldUpdateAllocatorFromSystemBase_works()
    {
        // Create a test world.
        World world = new World("Test World");

        // Create a system that use world update allocator. 
        var allocNativeArraySystem = world.CreateSystemManaged<AllocateNativeArraySystem>();
        allocNativeArraySystem.Update();
        Assert.AreEqual(allocNativeArraySystem.nativeArray[3], 3);

        // Dispose the test world.
        world.Dispose();
    }

    #region world-update-allocator-system-state
    // Access world update allocator through SystemState.WorldUpdateAllocator.
    unsafe partial struct AllocateNativeArrayISystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            // Get world update allocator through SystemState.WorldUpdateAllocator.
            var allocator = state.WorldUpdateAllocator;

            // Create a native array using world update allocator.
            var nativeArray = CollectionHelper.CreateNativeArray<int>(10, allocator);

            for (int i = 0; i < 10; i++)
            {
                nativeArray[i] = i;
            }
        }
    }
    #endregion // world-update-allocator-state

    [Test]
    public void WorldUpdateAllocatorFromSystemState_works()
    {
        // Create a test world.
        World world = new World("Test World");

        // Create a system that use world update allocator. 
        var allocNativeArrayISystem = world.CreateSystem<AllocateNativeArrayISystem>();
        allocNativeArrayISystem.Update(world.Unmanaged);

        // Dispose the test world.
        world.Dispose();
    }
}


internal class ExampleSystemGroupAllocator
{
    #region system-group-allocator
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
    public partial class ExampleFixedStepSimulationSystemGroup : ComponentSystemGroup
    {
        // Set the timestep use by this group, in seconds. The default value is 1/60 seconds.
        // This value will be clamped to the range [0.0001f ... 10.0f].
        public float Timestep
        {
            get => RateManager != null ? RateManager.Timestep : 0;
            set
            {
                if (RateManager != null)
                    RateManager.Timestep = value;
            }
        }

        // Default constructor
        public ExampleFixedStepSimulationSystemGroup()
        {
            float defaultFixedTimestep = 1.0f / 60.0f;

            // Set FixedRateSimpleManager to be the rate manager and create a system group allocator
            SetRateManagerCreateAllocator(new RateUtils.FixedRateSimpleManager(defaultFixedTimestep));
        }
    }

    unsafe partial class ExampleSystemGroupAllocatorSystem : SystemBase
    {
        public NativeArray<int> nativeArray = default;

        protected override void OnUpdate()
        {
            // After this system is added to ExampleFixedStepSimulationSystemGroup,
            // world update allocator here is replaced with the system group allocator.
            var allocator = WorldUpdateAllocator;

            // It is actually system group allocator that creates the native array.
            nativeArray = CollectionHelper.CreateNativeArray<int>(5, allocator);
            for (int i = 0; i < 5; i++)
            {
                nativeArray[i] = i;
            }
        }
    }

    [Test]
    public void UseSystemGroupAllocator_Works()
    {
        // Create a test world.
        World world = new World("Test World");

        var exampleFixedSimGroup = world.CreateSystemManaged<ExampleFixedStepSimulationSystemGroup>();
        var exampleSystem = world.CreateSystemManaged<ExampleSystemGroupAllocatorSystem>();

        exampleFixedSimGroup.Update();

        // Dispose the test world
        world.Dispose();
    }
    #endregion // system-group-allocator
}

