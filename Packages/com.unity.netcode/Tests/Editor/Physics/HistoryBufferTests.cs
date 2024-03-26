using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.NetCode.Tests;
using Unity.Physics;
using Unity.Transforms;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.NetCode.Physics.Tests
{
    [DisableAutoCreation]
    [RequireMatchingQueriesForUpdate]
    partial class TestPhysicsAndEntityForEach : SystemBase
    {
        protected override void OnUpdate()
        {
            var historyBuffer = new CollisionHistoryBuffer(1);
            Entities
                .WithAll<LocalTransform>()
                .ForEach(() =>
                {
                    historyBuffer.GetCollisionWorldFromTick(new NetworkTick(0),0, out var world);
                }).Schedule();
            Assert.Throws<InvalidOperationException>(()=>
            {
                Dependency.Complete();
            }, "PhysicHistoryBuffer must be declared as ReadOnly if a job does not write to it");

            Entities
                .WithAll<LocalTransform>()
                .WithoutBurst()
                //.WithReadOnly(historyBuffer)
                .ForEach(() =>
            {
                historyBuffer.GetCollisionWorldFromTick(new NetworkTick(0),0, out var world);
            }).Schedule();
            Assert.DoesNotThrow(()=>
            {
                Dependency.Complete();
            });
            historyBuffer.Dispose();
        }
    }

    public class HistoryBufferTests
    {
        static void InitializeHistory(ref CollisionHistoryBuffer buffer, in CollisionWorld collisionWorld)
        {
            for (int i = 0; i < CollisionHistoryBuffer.Capacity; ++i)
            {
                buffer.CloneCollisionWorld(i, collisionWorld);
            }
        }
        [Test]
        public void CreatePhysicsHistoryBuffer_AllWorldsAreInitializedToDefault()
        {
            //Initialized with CollisionHistoryBuffer.Capacity to allow access the full buffer.
            using (var historyBuffer = new CollisionHistoryBuffer(CollisionHistoryBuffer.Capacity))
            {
                for(int i=0;i<CollisionHistoryBuffer.Capacity;++i)
                {
                    historyBuffer.GetCollisionWorldFromTick(new NetworkTick(0), 0, out var world);
                    Assert.IsFalse(world.NumBodies > 0);
                }
            }
        }

        [Test]
        public void CreatePhysicsHistoryBuffer_LargerThenCapacityThrow()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() =>
            {
                new CollisionHistoryBuffer(CollisionHistoryBuffer.Capacity + 1);
            });
        }

        [Test]
        public void PhysicsHistoryBuffer_CanBeDisposedAndNotLeak()
        {
            var historyBuffer = new CollisionHistoryBuffer(1);
            using (var world = new CollisionWorld(0, 0))
            {
                for (int i = 0; i < CollisionHistoryBuffer.Capacity; ++i)
                {
                    historyBuffer.CloneCollisionWorld(i, world);
                }
            }
            Assert.DoesNotThrow(() =>
            {
                historyBuffer.Dispose();
            });
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        [Test]
        public void PhysicsHistoryBuffer_AccessingDisposedBufferThrows()
        {
            var historyBuffer = new CollisionHistoryBuffer(1);
            historyBuffer.Dispose();
            using (var collisionWorld = new CollisionWorld(1, 0))
            {
                //2020_0_11 trigger InvalidArgumentException
                //2020_0_12 might trigger InvalidArgumentException or ObjectDisposedException
                //Looking at the code the triggering logic is quite involved and depends on where the method is called
                //and some other conditions.
                //This is why I used a more generic Catch clause instead of Throws
                Assert.Catch(()=> { historyBuffer.GetCollisionWorldFromTick(new NetworkTick(0), 0, out var world); });
                Assert.Catch(()=> { historyBuffer.CloneCollisionWorld(0, collisionWorld); });
                Assert.Catch(()=> {  historyBuffer.DisposeIndex(0); });
                Assert.Catch(()=> {  historyBuffer.Dispose(); });
            }

        }
#endif
        [Test]
        public void PhysicsHistoryBuffer_Read_Write_OutsideJob()
        {
            using(var historyBuffer = new CollisionHistoryBuffer(1))
            {
                using(var collisionWorld = new CollisionWorld(1, 0))
                {
                    Assert.DoesNotThrow(() =>
                    {
                        historyBuffer.GetCollisionWorldFromTick(new NetworkTick(0), 0, out var world);
                    });
                    Assert.DoesNotThrow(() =>
                    {
                        historyBuffer.CloneCollisionWorld(0, collisionWorld);
                    });
                    Assert.DoesNotThrow(() =>
                    {
                        historyBuffer.DisposeIndex(0);
                    });
                }
            }
        }

        [Test]
        public void PhysicsHistoryBuffer_DisposeBeforeCloningDoesNotLeak()
        {
            using (var historyBuffer = new CollisionHistoryBuffer(CollisionHistoryBuffer.Capacity))
            {
                using (var collisionWorld = new CollisionWorld(1, 1))
                {
                    for (int i = 0; i < CollisionHistoryBuffer.Capacity; ++i)
                    {
                        historyBuffer.DisposeIndex(i);
                        historyBuffer.CloneCollisionWorld(i, collisionWorld);
                    }
                }
            }
        }

        [BurstCompile]
        struct CloneCollisionWorldJob : IJob
        {
            public CollisionHistoryBufferRef historyBuffer;
            public void Execute()
            {
                historyBuffer.GetCollisionWorldFromTick(new NetworkTick(0),0, out var world);
            }
        }

        [Test]
        public void PhysicsHistoryBuffer_DoesNotThrowsIfUsedInsideAJob()
        {
            using (var historyBuffer = new CollisionHistoryBuffer(1))
            {
                using (var collisionWorld = new CollisionWorld(1, 0))
                {
                    for (int i = 0; i < CollisionHistoryBuffer.Capacity; ++i)
                    {
                        historyBuffer.CloneCollisionWorld(i, collisionWorld);
                    }
                    Assert.DoesNotThrow(() =>
                    {
                        var handle = new CloneCollisionWorldJob { historyBuffer = historyBuffer.AsCollisionHistoryBufferRef(),}.Schedule();
                        handle.Complete();
                    });
                }
            }
        }

        [BurstCompile]
        struct ReadCollisionWorldJob : IJobParallelFor
        {
            [ReadOnly] public CollisionHistoryBufferRef HistoryBufferBuffer;
            public NativeArray<int> result;
            public void Execute(int index)
            {
                HistoryBufferBuffer.GetCollisionWorldFromTick(new NetworkTick(0), 0, out var world);
                var rayInput = new Unity.Physics.RaycastInput();
                rayInput.Start = float3.zero;
                rayInput.End = new float3(1.0f, 1.0f, 1.0f);
                rayInput.Filter = Unity.Physics.CollisionFilter.Default;
                bool hit = world.CastRay(rayInput);
                result[index] = hit ? 1 : 0;
            }
        }

        [BurstCompile]
        struct ReadOnlyCollisionWorldJob : IJob
        {
            [ReadOnly] public CollisionHistoryBufferRef CollisionHistoryBufferRef;
            public void Execute()
            {
                CollisionHistoryBufferRef.GetCollisionWorldFromTick(new NetworkTick(0), 0, out var world);
            }
        }

        [Test]
        public void PhysicsHistoryBuffer_ShouldUseParallelReaderInJob()
        {
            using (var historyBuffer = new CollisionHistoryBuffer(1))
            {
                using (var collisionWorld = new CollisionWorld(1, 0))
                {
                    for (int i = 0; i < CollisionHistoryBuffer.Capacity; ++i)
                    {
                        historyBuffer.CloneCollisionWorld(i, collisionWorld);
                    }
                    Assert.DoesNotThrow(() =>
                    {
                        var handle = new ReadOnlyCollisionWorldJob {CollisionHistoryBufferRef = historyBuffer.AsCollisionHistoryBufferRef(),}
                            .Schedule();
                        handle.Complete();
                    });
                }
            }
        }

        [Test]
        public void PhysicsHistoryBuffer_ParallelReaderThrowIfUsingADisposedBuffer()
        {
            var historyBuffer = new CollisionHistoryBuffer(1);
            using (var collisionWorld = new CollisionWorld(1, 0))
            {
                for (int i = 0; i < CollisionHistoryBuffer.Capacity; ++i)
                {
                    historyBuffer.CloneCollisionWorld(i, collisionWorld);
                }
            }
            historyBuffer.Dispose();
            //Depend on the 2020_X version
            Assert.Catch(() =>
            {
                var handle = new ReadOnlyCollisionWorldJob
                {
                    CollisionHistoryBufferRef = historyBuffer.AsCollisionHistoryBufferRef(),
                }.Schedule();
                handle.Complete();
            });
        }

        [Test]
        public void PhysicsHistoryBuffer_TestPhysicsSystem()
        {
            using (var testWorld = new NetCodeTestWorld())
            {
                testWorld.DriverSimulatedDelay = 50;
                testWorld.TestSpecificAdditionalAssemblies.Add("Unity.NetCode.Physics,");
                testWorld.TestSpecificAdditionalAssemblies.Add("Unity.Physics,");
                testWorld.Bootstrap(true);

                testWorld.CreateWorlds(true, 0);

                Assert.IsTrue(testWorld.CreateGhostCollection());
                for (int i = 0; i < 10; ++i)
                {
                    var cubeGameObject = new GameObject();
                    cubeGameObject.name = "SimpleCube";
                    var collider = cubeGameObject.AddComponent<UnityEngine.BoxCollider>();
                    collider.size = new Vector3(1,1,1);
                    testWorld.SpawnOnServer(cubeGameObject);
                }

                for (int i = 0; i < 200; ++i)
                {
                    testWorld.Tick(16f/1000f);
                }
            }
        }

        [Test]
        public void PhysicsHistoryBuffer_CanBeReadInParallel()
        {
            using (var historyBuffer = new CollisionHistoryBuffer(1))
            {
                using (var collisionWorld = new CollisionWorld(1, 0))
                {
                    for (int i = 0; i < CollisionHistoryBuffer.Capacity; ++i)
                    {
                        historyBuffer.CloneCollisionWorld(i, collisionWorld);
                    }
                }
                var myArray = new NativeArray<int>(3, Allocator.TempJob);
                var handle = new ReadCollisionWorldJob
                {
                    HistoryBufferBuffer = historyBuffer.AsCollisionHistoryBufferRef(),
                    result = myArray,
                }.Schedule(myArray.Length, 1);
                handle = myArray.Dispose(handle);
                Assert.DoesNotThrow(() => { handle.Complete(); });
            }
        }

        [Test]
        public void PhysicsHistoryBuffer_SupportEntityForEach()
        {
            var entityWorld = new World("NetCodeTest");
            entityWorld.GetOrCreateSystemManaged<TestPhysicsAndEntityForEach>();
            var archetype = entityWorld.EntityManager.CreateArchetype(typeof(LocalTransform));
            var entities = entityWorld.EntityManager.CreateEntity(archetype, 10, Allocator.Temp);
            entities.Dispose();
            Assert.DoesNotThrow(() => { entityWorld.Update(); });
            entityWorld.Dispose();
        }
    }
}
