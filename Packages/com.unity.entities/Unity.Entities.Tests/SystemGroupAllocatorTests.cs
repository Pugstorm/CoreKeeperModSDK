using System;
using System.Collections.Generic;
using NUnit.Framework;
using Unity.Core;
using Unity.Mathematics;
using Unity.Collections;
using Unity.Entities;

[assembly: RegisterGenericComponentType(typeof(Unity.Entities.Tests.SystemGroupAllocatorTests.NativeListComponent<TimeData>))]
[assembly: RegisterGenericComponentType(typeof(Unity.Entities.Tests.SystemGroupAllocatorTests.NativeListComponent<int>))]

namespace Unity.Entities.Tests
{
    public partial class SystemGroupAllocatorTests : ECSTestsFixture
    {
        const int SystemGroupAllocateSize = 32 * 1024; // 32k
        partial class RecordUpdateTimesSystem : SystemBase
        {
            public List<TimeData> Updates = new List<TimeData>();
            protected override void OnUpdate()
            {
                Updates.Add(World.Time);
            }
        }
        unsafe partial class AllocatorBlocksSystem : SystemBase
        {
            static int SystemUpdateCount = 1;
            static int Clamp = 0;
            static int MaxSystemUpdateCount = 2;

            public List<int> Blocks = new List<int>();
            public static void Reset(int maxCount = 2, int clamp = 0)
            {
                SystemUpdateCount = 1;
                Clamp = clamp;
                MaxSystemUpdateCount = maxCount;
            }
            protected override void OnUpdate()
            {
                if (Clamp > 0 && (SystemUpdateCount > MaxSystemUpdateCount))
                {
                    return;
                }

                var allocator = WorldUpdateAllocator;
                ref var rewindableAllocator = ref WorldRewindableAllocator;

                // blocks after rewind
                Blocks.Add(rewindableAllocator.BlocksAllocated);

                // Repeat allocat pattern 2x, 4x and 8x of SystemGroupAllocateSize of int,
                // which is 256k, 512k, 1024k and 128k
                int allocSize = (int)math.pow(2, (SystemUpdateCount % 4)) * SystemGroupAllocateSize;
                var array = CollectionHelper.CreateNativeArray<int>(allocSize, allocator);

                // blocks after allocation
                Blocks.Add(rewindableAllocator.BlocksAllocated);

                SystemUpdateCount++;
            }
        }

        unsafe partial class AllocateNativeArraySystem : SystemBase
        {
            static int First = 1;
            public NativeArray<int> GroupAllocatorArray = default;

            public static void Reset()
            {
                First = 1;
            }

            protected override void OnUpdate()
            {
                var allocator = WorldUpdateAllocator;

                // Only allocate when First is 1
                if (First == 1)
                {
                    GroupAllocatorArray = CollectionHelper.CreateNativeArray<int>(10, allocator);

                    for (int i = 0; i < 10; i++)
                    {
                        GroupAllocatorArray[i] = i;
                    }

                    First = 0;
                }
            }
        }

        [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
        [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
        public partial class FixedStepTestSimulationSystemGroup : ComponentSystemGroup
        {
            /// <summary>
            /// Set the timestep use by this group, in seconds. The default value is 1/60 seconds.
            /// This value will be clamped to the range [0.0001f ... 10.0f].
            /// </summary>
            public float Timestep
            {
                get => RateManager != null ? RateManager.Timestep : 0;
                set
                {
                    if (RateManager != null)
                        RateManager.Timestep = value;
                }
            }

            /// <summary>
            /// Default constructor
            /// </summary>
            public FixedStepTestSimulationSystemGroup()
            {
                float defaultFixedTimestep = 1.0f / 60.0f;

                // Set FixedRateSimpleManager to be the rate manager and create a system group allocator
                SetRateManagerCreateAllocator(new RateUtils.FixedRateSimpleManager(defaultFixedTimestep));
            }
        }

        [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor | WorldSystemFilterFlags.ThinClientSimulation)]
        [UpdateInGroup(typeof(FixedStepSimulationSystemGroup), OrderFirst = true)]
        public partial class FixedStepNoAllocatorSystemGroup : ComponentSystemGroup
        {
            /// <summary>
            /// Set the timestep use by this group, in seconds. The default value is 1/60 seconds.
            /// This value will be clamped to the range [0.0001f ... 10.0f].
            /// </summary>
            public float Timestep
            {
                get => RateManager != null ? RateManager.Timestep : 0;
                set
                {
                    if (RateManager != null)
                        RateManager.Timestep = value;
                }
            }

            /// <summary>
            /// Default constructor
            /// </summary>
            public FixedStepNoAllocatorSystemGroup()
            {
                float defaultFixedTimestep = 1.0f / 60.0f;
                RateManager = new RateUtils.FixedRateSimpleManager(defaultFixedTimestep);
            }
        }

        partial class RecordUpdateTimesSystemInner : SystemBase
        {
            public List<TimeData> Updates = new List<TimeData>();
            protected override void OnUpdate()
            {
                Updates.Add(World.Time);
            }
        }
        unsafe partial class AllocatorBlocksSystemInner : SystemBase
        {
            static int SystemUpdateCount = 1;
            static int Clamp = 0;
            static int MaxSystemUpdateCount = 2;

            public List<int> Blocks = new List<int>();
            public static void Reset(int maxCount = 2, int clamp = 0)
            {
                SystemUpdateCount = 1;
                Clamp = clamp;
                MaxSystemUpdateCount = maxCount;
            }
            protected override void OnUpdate()
            {
                if (Clamp > 0 && (SystemUpdateCount > MaxSystemUpdateCount))
                {
                    return;
                }

                var allocator = WorldUpdateAllocator;
                ref var rewindableAllocator = ref WorldRewindableAllocator;

                // blocks after rewind
                Blocks.Add(rewindableAllocator.BlocksAllocated);

                // Repeat allocat pattern 2x, 4x and 8x of SystemGroupAllocateSize of int,
                // which is 256k, 512k, 1024k and 128k
                int allocSize = (int)math.pow(2, (SystemUpdateCount % 4)) * SystemGroupAllocateSize;
                var array = CollectionHelper.CreateNativeArray<int>(allocSize, allocator);

                // blocks after allocation
                Blocks.Add(rewindableAllocator.BlocksAllocated);

                SystemUpdateCount++;
            }
        }

        unsafe partial class AllocateNativeArraySystemInner : SystemBase
        {
            static int First = 1;
            public NativeArray<int> GroupAllocatorArray = default;

            public static void Reset()
            {
                First = 1;
            }

            protected override void OnUpdate()
            {
                var allocator = WorldUpdateAllocator;

                // Only allocate when First is 1
                if (First == 1)
                {
                    GroupAllocatorArray = CollectionHelper.CreateNativeArray<int>(10, allocator);

                    for (int i = 0; i < 10; i++)
                    {
                        GroupAllocatorArray[i] = i;
                    }

                    First = 0;
                }
            }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_TwoUpdates_Rewind()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystem = World.CreateSystemManaged<AllocateNativeArraySystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            fixedSimGroup.AddSystemToUpdateList(allocNativeArraySystem);
            fixedSimGroup.SortSystems();

            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            fixedSimGroup.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0
            World.PushTime(new TimeData(1.0f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                });

            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, SystemGroupAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                    2, 3,   // SystemUpdateCount = 2, SystemGroupAllocator allocator0 starts from 2 blocks, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)

                            // After SystemUpdateCount = 2 is done, SystemGroupAllocator is switched to allocator1, rewinds allocator1
                });

            // Allocation from allocator0 is still valid.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystem.GroupAllocatorArray[i], i);
            }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_GradualRecovery_RemoveFromFixedStep()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystem = World.CreateSystemManaged<AllocateNativeArraySystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            fixedSimGroup.AddSystemToUpdateList(allocNativeArraySystem);
            fixedSimGroup.SortSystems();

            float dt = 0.125f;
            fixedSimGroup.Timestep = dt;
            World.MaximumDeltaTime = dt;
            // Simulate a frame spike
            // The recovery should be spread over several frames; instead of 3 ticks after the first Update(),
            // we should see at most two ticks per update until the group catches up to the elapsed time.
            World.PushTime(new TimeData(3 * dt, 0.01f));

            // first group of updates
            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            fixedSimGroup.Update();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0*dt, dt), // first Update() always ticks at t=0
                    new TimeData(1*dt, dt),
                });
            updateTimesSystem.Updates.Clear();

            // Allocated with RateSystemGroupDoubleAllocators
            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, SystemGroupAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                    2, 3,   // SystemUpdateCount = 2, SystemGroupAllocator allocator is not switched, still allocator0, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)

                            // After SystemUpdateCount = 2 is done, SystemGroupAllocator is switched to allocator1, rewinds allocator1
                });
            allocatorBlocksSystem.Blocks.Clear();

            // Second group of updates
            fixedSimGroup.RemoveSystemFromUpdateList(allocNativeArraySystem);
            fixedSimGroup.Update();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(2*dt, dt),
                });
            updateTimesSystem.Updates.Clear();

            // Allocated with RateSystemGroupDoubleAllocators
            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 3, SystemGroupAllocator allocator1, starts from 1 block, after allocating 1024k, there are 2 blocks with block size (128, 1024k)

                            // After SystemUpdateCount = 3 is done, SystemGroupAllocator is switched to allocator0, rewinds allocator0
                });
            allocatorBlocksSystem.Blocks.Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            // Allocations from SystemGroupAllocator allocator0 is not valid anymore
            Assert.Throws<ObjectDisposedException>(() =>
            {
                allocNativeArraySystem.GroupAllocatorArray[0] = 0xEF;
            });
#endif

            // Third group of updates
            fixedSimGroup.RemoveSystemFromUpdateList(allocatorBlocksSystem);
            fixedSimGroup.Update();

            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(3*dt, dt),
                });
            updateTimesSystem.Updates.Clear();

            var simGroup = World.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            simGroup.AddSystemToUpdateList(allocNativeArraySystem);
            simGroup.SortSystems();

            AllocateNativeArraySystem.Reset();
            simGroup.Update();
            simGroup.Update();

            // Allocated with world update allocator
            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 1, // SystemUpdateCount = 4, WorldUpdateAllocator allocator0, starts from 1 block, after allocating 128k, there is 1 block with block size (128k)

                    2, 3, // SystemUpdateCount = 5, WorldUpdateAllocator allocator0, starts from 2 blocks (allocNativeArraySystem allocated some memory), after allocating 256k,
                          // there are 3 blocks with block size (128k, 256k, 512k)
                });

            // Allocation from WorldUpdateAllocator allocator0 is still valid
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystem.GroupAllocatorArray[i], i);
            }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_MultipleUpdates_AllocationBlocksAreCorrect()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            fixedSimGroup.SortSystems();

            AllocatorBlocksSystem.Reset();
            fixedSimGroup.Timestep = 1.0f;
            World.MaximumDeltaTime = 10.0f;
            // Simulate a large elapsed time since the previous frame. (the deltaTime here is irrelevant)
            World.PushTime(new TimeData(6.5f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                    new TimeData(2.0f, 1.0f),
                    new TimeData(3.0f, 1.0f),
                    new TimeData(4.0f, 1.0f),
                    new TimeData(5.0f, 1.0f),
                    new TimeData(6.0f, 1.0f),
                });

            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, SystemGroupAllocator, allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                    2, 3,   // SystemUpdateCount = 2, SystemGroupAllocator, allocator0 starts from 2 blocks, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)

                    3, 4,   // SystemUpdateCount = 3, SystemGroupAllocator, allocator0 starts from 3 blocks, after allocating 1024k, there are 4 blocks with block size (128k, 256k, 512k, 1024k)

                    4, 4,   // SystemUpdateCount = 4, SystemGroupAllocator, allocator0 starts from 4 blocks, after allocating 128k (memory available in block[0])
                            // there are 4 blocks with block size (128k, 256k, 512k. 1024k, 2048k)

                    4, 5,   // SystemUpdateCount = 5, SystemGroupAllocator, allocator0, starts from 4 blocks, after allocating 256k, there are 5 blocks with block size (128k, 256k, 512k. 1024k, 2048k)

                    5, 5,   // SystemUpdateCount = 6, SystemGroupAllocator, allocator0, starts from 5 blocks, after allocating 512k, there are 5 blocks with block size (128k, 256k, 512k. 1024k, 2048k)

                    5, 5,   // SystemUpdateCount = 7, SystemGroupAllocator, allocator0, starts from 5 blocks, after allocating 1024k, there are 5 blocks with block size (128k, 256k, 512k. 1024k, 2048k)

                            // After SystemUpdateCount = 7 is done, SystemGroupAllocator is switched to allocator1, rewinds allocator1
                });
        }

        [Test]
        public void SimulationSystemGroup_TwoUpdates_NotRewind()
        {
            var simGroup = World.CreateSystemManaged<SimulationSystemGroup>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystem = World.CreateSystemManaged<AllocateNativeArraySystem>();
            simGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            simGroup.AddSystemToUpdateList(allocNativeArraySystem);
            simGroup.SortSystems();

            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            simGroup.Update();
            simGroup.Update();

            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 2, // SystemUpdateCount = 1, WorldUpdateAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                    2, 3, // SystemUpdateCount = 2, WorldUpdateAllocator allocator0 starts from 2 blocks (allocNativeArraySystem allocated some memory), after allocating 512k,
                          // there are 3 blocks with block size (128k, 256k, 512k)
                });

            // Allocation from WorldUpdateAllocator is still valid
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystem.GroupAllocatorArray[i], i);
            }
        }

        [Test]
        public void VariableRateSimulationSystemGroup_TwoUpdates_Rewind()
        {
            var variableRateSimGroup = World.CreateSystemManaged<VariableRateSimulationSystemGroup>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystem = World.CreateSystemManaged<AllocateNativeArraySystem>();
            variableRateSimGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            variableRateSimGroup.AddSystemToUpdateList(allocNativeArraySystem);
            variableRateSimGroup.SortSystems();

            AllocatorBlocksSystem.Reset(2, 1);
            AllocateNativeArraySystem.Reset();
            variableRateSimGroup.Update();
            System.Threading.Thread.Sleep(300);
            variableRateSimGroup.Update();

            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, SystemGroupAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                            // After SystemUpdateCount = 1 is done, SystemGroupAllocator is switched to allocator1, rewinds allocator1
                    
                    1, 2,   // SystemUpdateCount = 2, SystemGroupAllocator allocator1 starts from 1 block, after allocating 512k, there are 2 blocks with block size (128k, 512k)

                            // After SystemUpdateCount = 1 is done, SystemGroupAllocator is switched to allocator0, rewinds allocator0
                });

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.Throws<ObjectDisposedException>(() =>
            {
                allocNativeArraySystem.GroupAllocatorArray[0] = 0xEF;
            });
#endif
        }

        [Test]
        public void FixedStepSimulationSystemGroup_NestedWith_SimulationSystemGroup()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystem = World.CreateSystemManaged<AllocateNativeArraySystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            fixedSimGroup.AddSystemToUpdateList(allocNativeArraySystem);
            fixedSimGroup.SortSystems();

            var simGroup = World.CreateSystemManaged<SimulationSystemGroup>();
            var allocatorBlocksSystemInner = World.CreateSystemManaged<AllocatorBlocksSystemInner>();
            var allocNativeArraySystemInner = World.CreateSystemManaged<AllocateNativeArraySystemInner>();
            simGroup.AddSystemToUpdateList(allocatorBlocksSystemInner);
            simGroup.AddSystemToUpdateList(allocNativeArraySystemInner);
            simGroup.SortSystems();

            // Outer group nested with inner group
            fixedSimGroup.AddSystemToUpdateList(simGroup);
            fixedSimGroup.SortSystems();

            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            AllocatorBlocksSystemInner.Reset();
            AllocateNativeArraySystemInner.Reset();

            fixedSimGroup.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0
            World.PushTime(new TimeData(1.0f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();

            // Inner with group owned allocator
            // When outer system group, fixedSimGroup, gets updated, its group allocator points to the newly created sytem group allocator.
            // For inner system group, simGroup, it does not have its own system group allocator.  When update, its group allocator remains
            // pointing to the outer system group allocator.
            CollectionAssert.AreEqual(allocatorBlocksSystemInner.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, SystemGroupAllocator of outer system group, allocator0, starts from 1 block,
                            // after allocating 256k, there are 2 blocks with block size (128k, 256k).

                            // After inner system group, simGroup updates, SystemUpdateCount = 1 of outer system group, allocatorBlocksSystem gets updated.
                            // After allocateing 256k, outer SystemGroupAllocator allocator0 contains 3 blocks with block size (128k, 256k, 512k)

                    3, 4,   // SystemUpdateCount = 2, outer SystemGroupAllocator, allocator0, has 3 blocks,
                            // after allocating 512k, there are 4 blocks with block size (128k, 256k, 512k, 1024k).

                            // After inner system group, simGroup updates, SystemUpdateCount = 2 of outer system group, allocatorBlocksSystem gets updated.
                            // After allocateing 512k, outer SystemGroupAllocator allocator0, contains 4 blocks with block size (128k, 256k, 512k, 1024k)
                });

            // After outer system group, fixedSimGroup gets update, outer SystemGroupAllocator is switched to allocator1, rewinds allocator1.
            // Allocation from SystemGroupAllocator allocator0 is still valid.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystemInner.GroupAllocatorArray[i], i);
            }

            // Outer with group owned update allocator
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                });

            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    2, 3,   // Inner system group, simGroup SystemUpdateCount = 1 was updated, SystemGroupAllocator allocator0, has 2 blocks with block size (128k, 256k)
                            // Outer system group, allocatorBlocksSystem SystemUpdateCount = 1 updates, SystemGroupAllocator allocator0, after allocateing 256k,
                            // there are 3 blocks with block size (128k, 256k, 512k)

                            // Inner system group, simGroup SystemUpdateCount = 2 was updated, SystemGroupAllocator allocator0, after allocating 512k,
                            // there are 4 blocks with block size (128k, 256k, 512k, 1024k).
                            
                    4, 4,   // SystemGroupAllocator, allocator0, starts from 4 blocks with block size (128k, 256k, 512k, 1024k).
                            // Outer system group, allocatorBlocksSystem SystemUpdateCount = 2 updates, SystemGroupAllocator allocator0, after allocateing 512k (512k fits into the 4th block),
                            // there are 4 blocks with block size (128k, 256k, 512k, 1024k).
                });

            // After outer system group, fixedSimGroup gets update, outer SystemGroupAllocator is switched to allocator1, rewinds allocator1.
            // Allocation from SystemGroupAllocator allocator0 is still valid.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystem.GroupAllocatorArray[i], i);
            }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_NestedWith_RateManagerWithNoAllocatorSimulationSystemGroup()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystem = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystem = World.CreateSystemManaged<AllocateNativeArraySystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesSystem);
            fixedSimGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            fixedSimGroup.AddSystemToUpdateList(allocNativeArraySystem);
            fixedSimGroup.SortSystems();

            var fixedNoAllocatorSimGroup = World.CreateSystemManaged<FixedStepNoAllocatorSystemGroup>();
            var allocatorBlocksSystemInner = World.CreateSystemManaged<AllocatorBlocksSystemInner>();
            var allocNativeArraySystemInner = World.CreateSystemManaged<AllocateNativeArraySystemInner>();
            fixedNoAllocatorSimGroup.AddSystemToUpdateList(allocatorBlocksSystemInner);
            fixedNoAllocatorSimGroup.AddSystemToUpdateList(allocNativeArraySystemInner);
            fixedNoAllocatorSimGroup.SortSystems();

            // Outer group nested with inner group
            fixedSimGroup.AddSystemToUpdateList(fixedNoAllocatorSimGroup);
            fixedSimGroup.SortSystems();

            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            AllocatorBlocksSystemInner.Reset();
            AllocateNativeArraySystemInner.Reset();

            fixedSimGroup.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0
            World.PushTime(new TimeData(1.0f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();

            // When outer system group, FixedStepSimulationSystemGroup, gets updated, its group allocator points to the newly created sytem group allocator.
            // For inner system group, fixedNoAllocatorSimGroup, it does not have its own system group allocator.  When update, its group allocator remains
            // pointing to the outer system group allocator.
            CollectionAssert.AreEqual(allocatorBlocksSystemInner.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, outer SystemGroupAllocator, allocator0, starts from 1 block,
                            // after allocating 256k, there are 2 blocks with block size (128k, 256k).

                            // After inner system group, fixedNoAllocatorSimGroup updates, SystemUpdateCount = 1 of outer system group, allocatorBlocksSystem gets updated.
                            // After allocateing 256k, outer SystemGroupAllocator allocator0, has 3 blocks with block size (128k, 256k, 512k)

                    3, 4,   // SystemUpdateCount = 2, outer SystemGroupAllocator, allocator0, has 3 blocks,
                            // after allocating 512k, there are 4 blocks with block size (128k, 256k, 512k, 1024k).

                            // After inner system group, fixedNoAllocatorSimGroup update, SystemUpdateCount = 2 of outer system group, allocatorBlocksSystem gets updated.
                            // After allocateing 512k, outer SystemGroupAllocator, allocator0, has 4 blocks with block size (128k, 256k, 512k, 1024k)
                });

            // After outer system group, fixedSimGroup gets update, outer SystemGroupAllocator is switched to allocator1, rewinds allocator1.
            // Allocation from SystemGroupAllocator allocator0 is still valid.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystemInner.GroupAllocatorArray[i], i);
            }

            // Outer with group owned update allocator
            CollectionAssert.AreEqual(updateTimesSystem.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                });

            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    2, 3,   // Inner system group, fixedNoAllocatorSimGroup SystemUpdateCount = 1 was updated, SystemGroupAllocator allocator0, has 2 blocks with block size (128k, 256k)
                            // Outer system group, allocatorBlocksSystem SystemUpdateCount = 1 update, SystemGroupAllocator allocator0, after allocateing 256k,
                            // there are 3 blocks with block size (128k, 256k, 512k)

                            // Inner system group, fixedNoAllocatorSimGroup SystemUpdateCount = 2 was updated, SystemGroupAllocator allocator0, after allocating 512k,
                            // there are 4 blocks with block size (128k, 256k, 512k, 1024k).
                            
                    4, 4,   // SystemGroupAllocator, allocator0, starts from 4 blocks with block size (128k, 256k, 512k, 1024k).
                            // Outer system group, allocatorBlocksSystem SystemUpdateCount = 2 update, SystemGroupAllocator allocator0, after allocateing 512k (512k fits into the 4th block),
                            // there are 4 blocks with block size (128k, 256k, 512k, 1024k).
                });

            // After outer system group, fixedSimGroup gets update, outer SystemGroupAllocator is switched to allocator1, rewinds allocator1.
            // Allocation from SystemGroupAllocator allocator0 is still valid.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystem.GroupAllocatorArray[i], i);
            }
        }

        [Test]
        public void RateManagerWithNoAllocatorSimulationSystemGroup_TwoUpdates_NotRewind()
        {
            var fixedNoAllocatorSimGroup = World.CreateSystemManaged<FixedStepNoAllocatorSystemGroup>();
            var allocatorBlocksSystem = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystem = World.CreateSystemManaged<AllocateNativeArraySystem>();
            fixedNoAllocatorSimGroup.AddSystemToUpdateList(allocatorBlocksSystem);
            fixedNoAllocatorSimGroup.AddSystemToUpdateList(allocNativeArraySystem);
            fixedNoAllocatorSimGroup.SortSystems();

            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            World.PushTime(new TimeData(1.0f, 0.01f));
            fixedNoAllocatorSimGroup.Update();
            fixedNoAllocatorSimGroup.Update();
            World.PopTime();

            CollectionAssert.AreEqual(allocatorBlocksSystem.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, WorldUpdateAllocator allocator0, starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                    2, 3,   // SystemUpdateCount = 2, WorldUpdateAllocator allocator0, starts from 2 blocks, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)
                });

            // Allocation from WorldUpdateAllocator allocator0 is still valid
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(allocNativeArraySystem.GroupAllocatorArray[i], i);
            }
        }

        internal struct NativeListComponent<T> : IComponentData
            where T : unmanaged
        {
            public NativeList<T> data;
        }

        [Test]
        public void FixedStepSimulationSystemGroup_NestedSystemGroups()
        {
            var fixedSimGroupInner = World.CreateSystemManaged<FixedStepTestSimulationSystemGroup>();
            var updateTimesSystemInner = World.CreateSystemManaged<RecordUpdateTimesSystemInner>();
            var allocatorBlocksSystemInner = World.CreateSystemManaged<AllocatorBlocksSystemInner>();
            var allocNativeArraySystemInner = World.CreateSystemManaged<AllocateNativeArraySystemInner>();
            fixedSimGroupInner.AddSystemToUpdateList(updateTimesSystemInner);
            fixedSimGroupInner.AddSystemToUpdateList(allocatorBlocksSystemInner);
            fixedSimGroupInner.AddSystemToUpdateList(allocNativeArraySystemInner);
            fixedSimGroupInner.SortSystems();

            AllocatorBlocksSystemInner.Reset();
            AllocateNativeArraySystemInner.Reset();
            fixedSimGroupInner.Timestep = 1.0f;

            var fixedSimGroupOuter = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystemOuter = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            var allocatorBlocksSystemOuter = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystemOuter = World.CreateSystemManaged<AllocateNativeArraySystem>();
            fixedSimGroupOuter.AddSystemToUpdateList(updateTimesSystemOuter);
            fixedSimGroupOuter.AddSystemToUpdateList(allocatorBlocksSystemOuter);
            fixedSimGroupOuter.AddSystemToUpdateList(allocNativeArraySystemOuter);

            // Outer group nested with inner group
            fixedSimGroupOuter.AddSystemToUpdateList(fixedSimGroupInner);
            fixedSimGroupOuter.SortSystems();

            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            fixedSimGroupOuter.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0
            World.PushTime(new TimeData(2.5f, 0.01f));
            fixedSimGroupOuter.Update();
            fixedSimGroupOuter.Update();
            World.PopTime();
            CollectionAssert.AreEqual(updateTimesSystemOuter.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                    new TimeData(2.0f, 1.0f),
                });            

            CollectionAssert.AreEqual(allocatorBlocksSystemOuter.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, Outer SystemGroupAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                    2, 3,   // SystemUpdateCount = 2, Outer SystemGroupAllocator allocator is not switched, still allocator0, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)
                            // After SystemUpdateCount = 2 is done, Outer SystemGroupAllocator is switched to allocator1, rewinds allocator1

                    1, 2,   // SystemUpdateCount = 3, Outer SystemGroupAllocator, allocator1 starts from 1 block, after allocating 1024k, there are 2 blocks with block size (128k, 1024k)
                            // After SystemUpdateCount = 3 is done, Outer SystemGroupAllocator is switched to allocator0, rewinds allocator0
                });

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.Throws<ObjectDisposedException>(() =>
            {
                allocNativeArraySystemOuter.GroupAllocatorArray[0] = 0xEF;
            });
#endif

            CollectionAssert.AreEqual(updateTimesSystemInner.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                    new TimeData(2.0f, 1.0f),
                });

            CollectionAssert.AreEqual(allocatorBlocksSystemInner.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, Inner SystemGroupAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)
                            // After SystemUpdateCount = 1 is done, Inner SystemGroupAllocator is switched to allocator1, rewinds allocator1

                    1, 2,   // SystemUpdateCount = 2, Inner SystemGroupAllocator allocator1 starts from 1 block, after allocating 512k, there are 2 blocks with block size (128k, 512k)
                            // After SystemUpdateCount = 2 is done, Inner SystemGroupAllocator is switched to allocator0, rewinds allocator0

                    2, 3,   // SystemUpdateCount = 3, Inner SystemGroupAllocator, allocator0 starts from 2 blocks, after allocating 1024k, there are 3 blocks with block size (128k, 256k, 1024k)
                            // After SystemUpdateCount = 3 is done, Inner SystemGroupAllocator is switched to allocator1, rewinds allocator1
                });

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.Throws<ObjectDisposedException>(() =>
            {
                allocNativeArraySystemInner.GroupAllocatorArray[0] = 0xEF;
            });
#endif
        }

        [Test]
        public void FixedStepSimulationSystemGroup_NotNestedSystemGroups()
        {
            var fixedSimGroupA = World.CreateSystemManaged<FixedStepTestSimulationSystemGroup>();
            var updateTimesSystemA = World.CreateSystemManaged<RecordUpdateTimesSystemInner>();
            var allocatorBlocksSystemA = World.CreateSystemManaged<AllocatorBlocksSystemInner>();
            var allocNativeArraySystemA = World.CreateSystemManaged<AllocateNativeArraySystemInner>();
            fixedSimGroupA.AddSystemToUpdateList(updateTimesSystemA);
            fixedSimGroupA.AddSystemToUpdateList(allocatorBlocksSystemA);
            fixedSimGroupA.AddSystemToUpdateList(allocNativeArraySystemA);
            fixedSimGroupA.SortSystems();

            AllocatorBlocksSystemInner.Reset();
            AllocateNativeArraySystemInner.Reset();
            fixedSimGroupA.Timestep = 1.0f;
            fixedSimGroupA.Update();

            var fixedSimGroupB = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesSystemB = World.CreateSystemManaged<RecordUpdateTimesSystem>();
            var allocatorBlocksSystemB = World.CreateSystemManaged<AllocatorBlocksSystem>();
            var allocNativeArraySystemB = World.CreateSystemManaged<AllocateNativeArraySystem>();
            fixedSimGroupB.AddSystemToUpdateList(updateTimesSystemB);
            fixedSimGroupB.AddSystemToUpdateList(allocatorBlocksSystemB);
            fixedSimGroupB.AddSystemToUpdateList(allocNativeArraySystemB);
            fixedSimGroupB.SortSystems();

            AllocatorBlocksSystem.Reset();
            AllocateNativeArraySystem.Reset();
            fixedSimGroupB.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0
            World.PushTime(new TimeData(2.5f, 0.01f));
            fixedSimGroupB.Update();
            fixedSimGroupB.Update();
            World.PopTime();

            // Group A update 2 more times
            fixedSimGroupA.Update();
            fixedSimGroupA.Update();

            CollectionAssert.AreEqual(updateTimesSystemB.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                    new TimeData(2.0f, 1.0f),
                });

            CollectionAssert.AreEqual(allocatorBlocksSystemB.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, fixedSimGroupB SystemGroupAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                    2, 3,   // SystemUpdateCount = 2, fixedSimGroupB SystemGroupAllocator allocator0 has 2 blocks, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)

                            // After SystemUpdateCount = 2 is done, fixedSimGroupB SystemGroupAllocator is switched to allocator1, rewinds allocator1
                            
                    1, 2,   // SystemUpdateCount = 3, fixedSimGroupB SystemGroupAllocator allocator1 starts from 1 block , after allocating 1024k, after 2 blocks with block size (128k, 1024k)

                            // After SystemUpdateCount = 3 is done, fixedSimGroupB SystemGroupAllocator is switched to allocator0, rewinds allocator0
                });

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.Throws<ObjectDisposedException>(() =>
            {
                allocNativeArraySystemB.GroupAllocatorArray[0] = 0xEF;
            });
#endif

            CollectionAssert.AreEqual(updateTimesSystemA.Updates,
                new[]
                {
                    new TimeData(0.0f, 1.0f),
                    new TimeData(1.0f, 1.0f),
                    new TimeData(2.0f, 1.0f),
                });

            CollectionAssert.AreEqual(allocatorBlocksSystemA.Blocks,
                new[]
                {
                    1, 2,   // SystemUpdateCount = 1, fixedSimGroupA SystemGroupAllocator allocator0 starts from 1 block, after allocating 256k, there are 2 blocks with block size (128k, 256k)

                            // After SystemUpdateCount = 1 is done, fixedSimGroupA SystemGroupAllocator is switched to allocator1, rewinds allocator1

                    1, 2,   // SystemUpdateCount = 2, fixedSimGroupA SystemGroupAllocator allocator1, starts from 1 block, after allocating 512k, there are 2 blocks with block size (128k, 512k)

                            // After SystemUpdateCount = 2 is done, fixedSimGroupA SystemGroupAllocator is switched to allocator0, rewinds allocator0

                    2, 3,   // SystemUpdateCount = 3, fixedSimGroupA SystemGroupAllocator allocator0 starts from 2 blocks, after allocating 1024k, there are 3 blocks with block size (128k, 256k, 1024k)

                            // After SystemUpdateCount = 3 is done, fixedSimGroupA SystemGroupAllocator is switched to allocator1, rewinds allocator1
                });

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            Assert.Throws<ObjectDisposedException>(() =>
            {
                allocNativeArraySystemA.GroupAllocatorArray[0] = 0xEF;
            });
#endif
        }

        partial struct RecordUpdateTimesISystem : ISystem
        {
            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle, new NativeListComponent<TimeData> {
                    data = new NativeList<TimeData>(state.WorldUnmanaged.UpdateAllocator.ToAllocator)
                });
            }

            public void OnUpdate(ref SystemState state)
            {
                var updatesList = state.EntityManager.GetComponentDataRW<NativeListComponent<TimeData>>(state.SystemHandle).ValueRW;
                updatesList.data.Add(state.World.Time);
            }
        }

        partial struct AllocatorBlocksISystem : ISystem
        {
            static int SystemUpdateCount = 1;
            static int Clamp = 0;
            static int MaxSystemUpdateCount = 2;

            public static void Reset(int maxCount = 2, int clamp = 0)
            {
                SystemUpdateCount = 1;
                Clamp = clamp;
                MaxSystemUpdateCount = maxCount;
            }

            public void OnCreate(ref SystemState state)
            {
                state.EntityManager.AddComponentData(state.SystemHandle, new NativeListComponent<int>
                {
                    data = new NativeList<int>(state.WorldUnmanaged.UpdateAllocator.ToAllocator)
                });
            }

            public void OnUpdate(ref SystemState state)
            {
                if (Clamp > 0 && (SystemUpdateCount > MaxSystemUpdateCount))
                {
                    return;
                }

                var allocator = state.WorldUpdateAllocator;
                ref var rewindableAllocator = ref state.WorldRewindableAllocator;

                ref var blocksList = ref state.EntityManager.GetComponentDataRW<NativeListComponent<int>>(state.SystemHandle).ValueRW;

                // blocks after rewind
                blocksList.data.Add(rewindableAllocator.BlocksAllocated);

                // Repeat allocat pattern 2x, 4x and 8x of SystemGroupAllocateSize of int,
                // which is 256k, 512k, 1024k and 128k
                int allocSize = (int)math.pow(2, (SystemUpdateCount % 4)) * SystemGroupAllocateSize;
                var array = CollectionHelper.CreateNativeArray<int>(allocSize, allocator);

                // blocks after allocation
                blocksList.data.Add(rewindableAllocator.BlocksAllocated);

                SystemUpdateCount++;
            }
        }

        struct AllocateNativeArrayComponent : IComponentData
        {
            public NativeArray<int> GroupAllocatorArray;
        }

        unsafe partial struct AllocateNativeArrayISystem : ISystem
        {
            static int First = 1;

            public static void Reset(World world, SystemHandle instanceHandle)
            {
                First = 1;
                world.EntityManager.AddComponent<AllocateNativeArrayComponent>(instanceHandle);
            }
            public void OnUpdate(ref SystemState state)
            {
                var allocator = state.WorldUpdateAllocator;

                // Only allocate when First is 1
                if (First == 1)
                {
                    ref var allocArrays = ref state.EntityManager.GetComponentDataRW<AllocateNativeArrayComponent>(state.SystemHandle).ValueRW;

                    allocArrays.GroupAllocatorArray = CollectionHelper.CreateNativeArray<int>(10, allocator);

                    for (int i = 0; i < 10; i++)
                    {
                        allocArrays.GroupAllocatorArray[i] = i;
                    }

                    First = 0;
                }
            }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_ISystem_TwoUpdates()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesISystem = World.CreateSystem<RecordUpdateTimesISystem>();
            var allocatorBlocksISystem = World.CreateSystem<AllocatorBlocksISystem>();
            var allocNativeArrayISystem = World.CreateSystem<AllocateNativeArrayISystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesISystem);
            fixedSimGroup.AddSystemToUpdateList(allocatorBlocksISystem);
            fixedSimGroup.AddSystemToUpdateList(allocNativeArrayISystem);
            fixedSimGroup.SortSystems();

            AllocatorBlocksISystem.Reset();
            AllocateNativeArrayISystem.Reset(World, allocNativeArrayISystem);
            fixedSimGroup.Timestep = 1.0f;
            // The first fixed-timestep group update always includes an update at elapsedTime=0
            World.PushTime(new TimeData(1.0f, 0.01f));
            fixedSimGroup.Update();
            World.PopTime();

            var updatesList = World.EntityManager.GetComponentData<NativeListComponent<TimeData>>(updateTimesISystem);

            Assert.AreEqual(updatesList.data.ElementAt(0), new TimeData(0.0f, 1.0f));
            Assert.AreEqual(updatesList.data.ElementAt(1), new TimeData(1.0f, 1.0f));

            // SystemUpdateCount = 1, allocator0 after rewind 1 block, allocate 256k, after 2 blocks with block size (128k, 256k)
            var blocksList = World.EntityManager.GetComponentData<NativeListComponent<int>>(allocatorBlocksISystem);

            // SystemUpdateCount = 1, SystemGroupAllocator allocator0 starts from 1 block with block size (128k)
            Assert.AreEqual(blocksList.data.ElementAt(0), 1);
            // SystemUpdateCount = 1, SystemGroupAllocator allocator0, after allocating 256k, there are 2 blocks with block size (128k, 256k)
            Assert.AreEqual(blocksList.data.ElementAt(1), 2);
            // SystemUpdateCount = 2, SystemGroupAllocator allocator is not switched, still allocator0, there are 2 blocks with block size (128k, 256k)
            Assert.AreEqual(blocksList.data.ElementAt(2), 2);
            // SystemUpdateCount = 2, SystemGroupAllocator allocator is not switched, still allocator0, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)
            // After SystemUpdateCount = 2 is done, SystemGroupAllocator is switched to allocator1, rewinds allocator1
            Assert.AreEqual(blocksList.data.ElementAt(3), 3);

            var allocArrays = World.EntityManager.GetComponentData<AllocateNativeArrayComponent>(allocNativeArrayISystem);
            var groupAllocatorArray = allocArrays.GroupAllocatorArray;
            // Allocation from SystemGroupAllocator allocator0 is still valid.
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(groupAllocatorArray[i], i);
            }
        }

        [Test]
        public void FixedStepSimulationSystemGroup_ISystem_RemoveFromFixedStep()
        {
            var fixedSimGroup = World.CreateSystemManaged<FixedStepSimulationSystemGroup>();
            var updateTimesISystem = World.CreateSystem<RecordUpdateTimesISystem>();
            var allocatorBlocksISystem = World.CreateSystem<AllocatorBlocksISystem>();
            var allocNativeArrayISystem = World.CreateSystem<AllocateNativeArrayISystem>();
            fixedSimGroup.AddSystemToUpdateList(updateTimesISystem);
            fixedSimGroup.AddSystemToUpdateList(allocatorBlocksISystem);
            fixedSimGroup.AddSystemToUpdateList(allocNativeArrayISystem);
            fixedSimGroup.SortSystems();

            float dt = 0.125f;
            fixedSimGroup.Timestep = dt;
            World.MaximumDeltaTime = dt;
            // Simulate a frame spike
            // The recovery should be spread over several frames; instead of 3 ticks after the first Update(),
            // we should see at most two ticks per update until the group catches up to the elapsed time.
            World.PushTime(new TimeData(3 * dt, 0.01f));

            // first group of updates ------------------
            AllocatorBlocksISystem.Reset();
            AllocateNativeArrayISystem.Reset(World, allocNativeArrayISystem);
            fixedSimGroup.Update();

            var updatesList = World.EntityManager.GetComponentData<NativeListComponent<TimeData>>(updateTimesISystem);

            Assert.AreEqual(updatesList.data.ElementAt(0), new TimeData(0 * dt, dt));
            Assert.AreEqual(updatesList.data.ElementAt(1), new TimeData(1 * dt, dt));
            updatesList.data.Clear();

            var blocksList = World.EntityManager.GetComponentData<NativeListComponent<int>>(allocatorBlocksISystem);
            // SystemUpdateCount = 1, SystemGroupAllocator allocator0 starts from 1 block with block size (128k)
            Assert.AreEqual(blocksList.data.ElementAt(0), 1);
            // SystemUpdateCount = 1, SystemGroupAllocator allocator0, after allocating 256k, there are 2 blocks with block size (128k, 256k)
            Assert.AreEqual(blocksList.data.ElementAt(1), 2);
            // SystemUpdateCount = 2, SystemGroupAllocator allocator is not switched, still allocator0, there are 2 blocks with block size (128k, 256k)
            Assert.AreEqual(blocksList.data.ElementAt(2), 2);
            // SystemUpdateCount = 2, SystemGroupAllocator allocator is not switched, still allocator0, after allocating 512k, there are 3 blocks with block size (128k, 256k, 512k)
            // After SystemUpdateCount = 2 is done, SystemGroupAllocator is switched to allocator1, rewinds allocator1
            Assert.AreEqual(blocksList.data.ElementAt(3), 3);
            blocksList.data.Clear();

            // Allocated with RateSystemGroupDoubleAllocators
            var allocArrays = World.EntityManager.GetComponentData<AllocateNativeArrayComponent>(allocNativeArrayISystem);

            // Second group of updates ------------------
            fixedSimGroup.RemoveSystemFromUpdateList(allocNativeArrayISystem);
            fixedSimGroup.Update();

            // Need to update local copy of component data
            updatesList = World.EntityManager.GetComponentData<NativeListComponent<TimeData>>(updateTimesISystem);

            Assert.AreEqual(updatesList.data.ElementAt(0), new TimeData(2 * dt, dt));
            updatesList.data.Clear();

            blocksList = World.EntityManager.GetComponentData<NativeListComponent<int>>(allocatorBlocksISystem);
            // SystemUpdateCount = 3, SystemGroupAllocator allocator1, start from 1 block with block size (128k)
            Assert.AreEqual(blocksList.data.ElementAt(0), 1);
            // SystemUpdateCount = 3, SystemGroupAllocator allocator1, after allocating 1024k, there are 2 blocks with block size (128, 1024k)
            // After SystemUpdateCount = 3 is done, SystemGroupAllocator is switched to allocator0, rewinds allocator0
            Assert.AreEqual(blocksList.data.ElementAt(1), 2);
            blocksList.data.Clear();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var groupAllocatorArray = allocArrays.GroupAllocatorArray;
            Assert.Throws<ObjectDisposedException>(() =>
            {
                groupAllocatorArray[0] = 0xEF;
            });
#endif

            // Third group of updates ------------------
            fixedSimGroup.RemoveSystemFromUpdateList(allocatorBlocksISystem);
            fixedSimGroup.Update();

            // Need to update local copy of component data
            updatesList = World.EntityManager.GetComponentData<NativeListComponent<TimeData>>(updateTimesISystem);

            Assert.AreEqual(updatesList.data.ElementAt(0), new TimeData(3 * dt, dt));
            updatesList.data.Clear();

            var simGroup = World.CreateSystemManaged<SimulationSystemGroup>();
            simGroup.AddSystemToUpdateList(allocatorBlocksISystem);
            simGroup.AddSystemToUpdateList(allocNativeArrayISystem);
            simGroup.SortSystems();

            AllocatorBlocksISystem.Reset(3);
            simGroup.Update();
            simGroup.Update();
            simGroup.Update();

            blocksList = World.EntityManager.GetComponentData<NativeListComponent<int>>(allocatorBlocksISystem);

            // SystemUpdateCount = 4, WorldUpdateAllocator allocator0, starts from 1 block with block size (128k)
            Assert.AreEqual(blocksList.data.ElementAt(0), 1);
            // SystemUpdateCount = 4, WorldUpdateAllocator allocator0, after allocating 128k, there is 1 block with block size (128k)
            Assert.AreEqual(blocksList.data.ElementAt(1), 2);
            // SystemUpdateCount = 5, WorldUpdateAllocator allocator0, starts from 2 blocks (allocNativeArraySystem allocated some memory)
            Assert.AreEqual(blocksList.data.ElementAt(2), 2);
            // SystemUpdateCount = 5, WorldUpdateAllocator allocator0, after allocating 256k, there are 3 blocks with block size (128k, 256k, 512k)
            Assert.AreEqual(blocksList.data.ElementAt(3), 3);
            // SystemUpdateCount = 6, WorldUpdateAllocator allocator0, starts from 3 blocks with block size (128k, 256k, 512k)
            Assert.AreEqual(blocksList.data.ElementAt(4), 3);
            // SystemUpdateCount = 6, WorldUpdateAllocator allocator0, after allocating 512k, there are 4 blocks with block size (128k, 256k, 512k, 1024k)
            Assert.AreEqual(blocksList.data.ElementAt(5), 4);
        }

        [Test]
        public void VariableRateSimulationSystemGroup_ISystem_Rewind()
        {
            var variableRateSimGroup = World.CreateSystemManaged<VariableRateSimulationSystemGroup>();
            var allocatorBlocksISystem = World.CreateSystem<AllocatorBlocksISystem>();
            var allocNativeArrayISystem = World.CreateSystem<AllocateNativeArrayISystem>();
            variableRateSimGroup.AddSystemToUpdateList(allocatorBlocksISystem);
            variableRateSimGroup.AddSystemToUpdateList(allocNativeArrayISystem);
            variableRateSimGroup.SortSystems();

            AllocatorBlocksISystem.Reset(2, 1);
            AllocateNativeArrayISystem.Reset(World, allocNativeArrayISystem);
            variableRateSimGroup.Update();
            System.Threading.Thread.Sleep(300);
            variableRateSimGroup.Update();

            var blocksList = World.EntityManager.GetComponentData<NativeListComponent<int>>(allocatorBlocksISystem);

            // SystemUpdateCount = 1, SystemGroupAllocator allocator0, starts from 1 block with block size (128k)
            Assert.AreEqual(blocksList.data.ElementAt(0), 1);
            // SystemUpdateCount = 1, SystemGroupAllocator allocator0, after allocating 256k, there are 2 blocks with block size (128k, 256k)
            // After SystemUpdateCount = 1 is done, SystemGroupAllocator is switched to allocator1, rewinds allocator1
            Assert.AreEqual(blocksList.data.ElementAt(1), 2);
            // SystemUpdateCount = 2, SystemGroupAllocator allocator1, starts from 1 block with block size (128k)
            Assert.AreEqual(blocksList.data.ElementAt(2), 1);
            // SystemUpdateCount = 2, SystemGroupAllocator allocator1, after allocating 512k, there are 2 blocks with block size (128k, 512k)
            // After SystemUpdateCount = 2 is done, SystemGroupAllocator is switched to allocator0, rewinds allocator0
            Assert.AreEqual(blocksList.data.ElementAt(3), 2);

            var allocArrays = World.EntityManager.GetComponentData<AllocateNativeArrayComponent>(allocNativeArrayISystem);

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var groupAllocatorArray = allocArrays.GroupAllocatorArray;
            Assert.Throws<ObjectDisposedException>(() =>
            {
                groupAllocatorArray[0] = 0xEF;
            });
#endif
        }
    }
}
