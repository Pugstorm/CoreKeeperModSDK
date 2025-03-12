using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Physics.Tests.Base.Containers
{
    class EventStreamTests
    {
        private unsafe void WriteEvent(CollisionEventData collisionEvent, ref NativeStream.Writer collisionEventWriter)
        {
            int numContactPoints = collisionEvent.NumNarrowPhaseContactPoints;
            int size = CollisionEventData.CalculateSize(numContactPoints);

            collisionEventWriter.Write(size);
            byte* eventPtr = collisionEventWriter.Allocate(size);
            ref CollisionEventData eventRef = ref UnsafeUtility.AsRef<CollisionEventData>(eventPtr);
            eventRef = collisionEvent;
            for (int i = 0; i < numContactPoints; i++)
            {
                eventRef.AccessContactPoint(i) = new ContactPoint();
            }
        }

        [Test]
        public void ReadCollisionEvents()
        {
            // Allocate a native stream for up to 10 parallel writes
            var collisionEventStream = new NativeStream(10, Allocator.TempJob);

            // Do a couple of writes to different forEach indices
            int writeCount = 0;
            unsafe
            {
                NativeStream.Writer collisionEventWriter = collisionEventStream.AsWriter();

                var collisionEventData = new CollisionEventData();

                collisionEventWriter.BeginForEachIndex(1);
                {
                    collisionEventData.NumNarrowPhaseContactPoints = 1;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;

                    collisionEventData.NumNarrowPhaseContactPoints = 4;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;
                }
                collisionEventWriter.EndForEachIndex();

                collisionEventWriter.BeginForEachIndex(3);
                {
                    collisionEventData.NumNarrowPhaseContactPoints = 3;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;
                }
                collisionEventWriter.EndForEachIndex();

                collisionEventWriter.BeginForEachIndex(5);
                {
                    collisionEventData.NumNarrowPhaseContactPoints = 4;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;

                    collisionEventData.NumNarrowPhaseContactPoints = 2;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;
                }
                collisionEventWriter.EndForEachIndex();

                collisionEventWriter.BeginForEachIndex(7);
                {
                    collisionEventData.NumNarrowPhaseContactPoints = 1;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;
                }
                collisionEventWriter.EndForEachIndex();

                collisionEventWriter.BeginForEachIndex(9);
                {
                    collisionEventData.NumNarrowPhaseContactPoints = 4;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;

                    collisionEventData.NumNarrowPhaseContactPoints = 1;
                    WriteEvent(collisionEventData, ref collisionEventWriter);
                    writeCount++;
                }
                collisionEventWriter.EndForEachIndex();
            }

            PhysicsWorld dummyWorld = new PhysicsWorld(10, 10, 0);
            float timeStep = 0.0f;
            NativeArray<Velocity> inputVelocities = new NativeArray<Velocity>(dummyWorld.NumDynamicBodies, Allocator.Temp);

            // Iterate over written events and make sure they are all read
            CollisionEvents collisionEvents = new CollisionEvents(collisionEventStream, inputVelocities, timeStep);
            int readCount = 0;
            foreach (var collisionEvent in collisionEvents)
            {
                readCount++;
            }

            Assert.IsTrue(readCount == writeCount);

            // Cleanup
            var disposeJob = collisionEventStream.Dispose(default);
            disposeJob.Complete();
            dummyWorld.Dispose();
        }

        [Test]
        public void ReadTriggerEvents()
        {
            // Allocate a native stream for up to 10 parallel writes
            var triggerEventStream = new NativeStream(10, Allocator.TempJob);

            // Do a couple of writes to different forEach indices
            int writeCount = 0;
            {
                NativeStream.Writer triggerEventWriter = triggerEventStream.AsWriter();

                triggerEventWriter.BeginForEachIndex(1);
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.EndForEachIndex();

                triggerEventWriter.BeginForEachIndex(3);
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.EndForEachIndex();

                triggerEventWriter.BeginForEachIndex(5);
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.EndForEachIndex();

                triggerEventWriter.BeginForEachIndex(7);
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.EndForEachIndex();

                triggerEventWriter.BeginForEachIndex(9);
                triggerEventWriter.Write(new TriggerEvent());
                writeCount++;
                triggerEventWriter.EndForEachIndex();
            }

            PhysicsWorld dummyWorld = new PhysicsWorld(10, 10, 0);

            // Iterate over written events and make sure they are all read
            TriggerEvents triggerEvents = new TriggerEvents(triggerEventStream);
            int readCount = 0;
            foreach (var triggerEvent in triggerEvents)
            {
                readCount++;
            }

            Assert.IsTrue(readCount == writeCount);

            // Cleanup
            var disposeJob = triggerEventStream.Dispose(default);
            disposeJob.Complete();
            dummyWorld.Dispose();
        }
    }
}
