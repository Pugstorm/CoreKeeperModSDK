using System;
using NUnit.Framework;
#pragma warning disable 649

namespace Unity.Entities.Tests
{
    class TypeIndexOrderTests : ECSTestsFixture
    {
        struct ComponentData1 : IComponentData
        {
            public int data;
        }

        struct Cleanup1 : ICleanupComponentData
        {
            public byte data;
        }

        struct Buffer1 : IBufferElementData
        {
            public short data;
        }

        struct CleanupBuffer1 : ICleanupBufferElementData
        {
            public float data;
        }

        struct Tag1 : IComponentData
        {}

        struct CleanupTag1 : ICleanupComponentData
        {}

        struct Shared1 : ISharedComponentData
        {
            public long data;
        }

        struct CleanupShared1 : ICleanupSharedComponentData
        {
            public int data;
        }

        struct ComponentData2 : IComponentData
        {
            public int data;
        }

        struct CleanupState2 : ICleanupComponentData
        {
            public byte data;
        }

        struct Buffer2 : IBufferElementData
        {
            public short data;
        }

        struct CleanupBuffer2 : ICleanupBufferElementData
        {
            public float data;
        }

        struct Tag2 : IComponentData
        {}

        struct CleanupTag2 : ICleanupComponentData
        {}

        struct Shared2 : ISharedComponentData
        {
            public long data;
        }

        struct CleanupShared2 : ICleanupSharedComponentData
        {
            public int data;
        }

        ComponentType chunk<T>() => ComponentType.ChunkComponent<T>();

        void MatchesTypes<A, B>(params ComponentTypeInArchetype[] types)
        {
            var expected = new ComponentTypeInArchetype[]
            {
                new ComponentTypeInArchetype(typeof(A)),
                new ComponentTypeInArchetype(typeof(B))
            };
            CollectionAssert.AreEquivalent(expected, types);
        }

        void MatchesTypes<A, B, C>(params ComponentTypeInArchetype[] types)
        {
            var expected = new ComponentTypeInArchetype[]
            {
                new ComponentTypeInArchetype(typeof(A)),
                new ComponentTypeInArchetype(typeof(B)),
                new ComponentTypeInArchetype(typeof(C))
            };
            CollectionAssert.AreEquivalent(expected, types);
        }

        void MatchesChunkTypes<A, B>(params ComponentTypeInArchetype[] types)
        {
            var expected = new ComponentTypeInArchetype[]
            {
                new ComponentTypeInArchetype(ComponentType.ChunkComponent<A>()),
                new ComponentTypeInArchetype(ComponentType.ChunkComponent<B>())
            };
            CollectionAssert.AreEquivalent(expected, types);
        }

        void MatchesChunkTypes<A, B, C, D>(params ComponentTypeInArchetype[] types)
        {
            var expected = new ComponentTypeInArchetype[]
            {
                new ComponentTypeInArchetype(ComponentType.ChunkComponent<A>()),
                new ComponentTypeInArchetype(ComponentType.ChunkComponent<B>()),
                new ComponentTypeInArchetype(ComponentType.ChunkComponent<C>()),
                new ComponentTypeInArchetype(ComponentType.ChunkComponent<D>())
            };
            CollectionAssert.AreEquivalent(expected, types);
        }

        [Test]
        public unsafe void TypesInArchetypeAreOrderedAsExpected()
        {
            var archetype = m_Manager.CreateArchetype(
                typeof(ComponentData1), typeof(Cleanup1), typeof(Buffer1), typeof(CleanupBuffer1),
                typeof(Tag1), typeof(CleanupTag1), typeof(Shared1), typeof(CleanupShared1),
                chunk<ComponentData1>(), chunk<Cleanup1>(), chunk<Buffer1>(), chunk<CleanupBuffer1>(),
                chunk<Tag1>(), chunk<CleanupTag1>(),

                typeof(ComponentData2), typeof(CleanupState2), typeof(Buffer2), typeof(CleanupBuffer2),
                typeof(Tag2), typeof(CleanupTag2), typeof(Shared2), typeof(CleanupShared2),
                chunk<ComponentData2>(), chunk<CleanupState2>(), chunk<Buffer2>(), chunk<CleanupBuffer2>(),
                chunk<Tag2>(), chunk<CleanupTag2>());


            Assert.AreEqual(30, archetype.Archetype->TypesCount); //+1 for Simulate

            var entityType = new ComponentTypeInArchetype(typeof(Entity));

            //Expected order: Entity, ComponentData*, Cleanup*, Buffer*, SystemBuffer*, Tag*, SystemTag*,
            // Shared*, SystemShared*, ChunkComponentData* and ChunkTag*, ChunkCleanup* and ChunkSystemTag*,
            // ChunkBuffer*, ChunkSystemBuffer*

            var t = archetype.Archetype->Types;

            Assert.AreEqual(entityType, t[0]);

            MatchesTypes<ComponentData1, ComponentData2>(t[1], t[2]);
            MatchesTypes<Cleanup1, CleanupState2>(t[3], t[4]);
            MatchesTypes<Buffer1, Buffer2>(t[5], t[6]);
            MatchesTypes<CleanupBuffer1, CleanupBuffer2>(t[7], t[8]);
            MatchesTypes<Tag1, Tag2, Simulate>(t[9], t[10], t[11]);
            MatchesTypes<CleanupTag1, CleanupTag2>(t[12], t[13]);
            MatchesTypes<Shared1, Shared2>(t[14], t[15]);
            MatchesTypes<CleanupShared1, CleanupShared2>(t[16], t[17]);

            MatchesChunkTypes<ComponentData1, ComponentData2, Tag1, Tag2>(t[18], t[19], t[20], t[21]);
            MatchesChunkTypes<Cleanup1, CleanupState2, CleanupTag1, CleanupTag2>(t[22], t[23], t[24], t[25]);

            MatchesChunkTypes<Buffer1, Buffer2>(t[26], t[27]);
            MatchesChunkTypes<CleanupBuffer1, CleanupBuffer2>(t[28], t[29]);
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        class ManagedComponentData1 : IComponentData, IEquatable<ManagedComponentData1>
        {
            public int data;

            public bool Equals(ManagedComponentData1 other)
            {
                return data == other.data;
            }

            public override int GetHashCode()
            {
                return data.GetHashCode();
            }
        }

        class ManagedComponentData2 : IComponentData, IEquatable<ManagedComponentData2>
        {
            public int data;

            public bool Equals(ManagedComponentData2 other)
            {
                return data == other.data;
            }

            public override int GetHashCode()
            {
                return data.GetHashCode();
            }
        }

        [Test]
        public unsafe void TypesInArchetypeAreOrderedAsExpected_ManagedComponents()
        {
            var archetype = m_Manager.CreateArchetype(
                typeof(ComponentData1), typeof(Cleanup1), typeof(Buffer1), typeof(CleanupBuffer1),
                typeof(Tag1), typeof(CleanupTag1), typeof(Shared1), typeof(CleanupShared1),
                chunk<ComponentData1>(), chunk<Cleanup1>(), chunk<Buffer1>(), chunk<CleanupBuffer1>(),
                chunk<Tag1>(), chunk<CleanupTag1>(), typeof(ManagedComponentData1),

                typeof(ComponentData2), typeof(CleanupState2), typeof(Buffer2), typeof(CleanupBuffer2),
                typeof(Tag2), typeof(CleanupTag2), typeof(Shared2), typeof(CleanupShared2),
                chunk<ComponentData2>(), chunk<CleanupState2>(), chunk<Buffer2>(), chunk<CleanupBuffer2>(),
                chunk<Tag2>(), chunk<CleanupTag2>(), typeof(ManagedComponentData2));


            Assert.AreEqual(32, archetype.Archetype->TypesCount); // +1 for Simulate

            var entityType = new ComponentTypeInArchetype(typeof(Entity));

            //Expected order: Entity, ComponentData*, Cleanup*, Buffer*, SystemBuffer*, ManagedComponentData *, Tag*,
            // SystemTag*, Shared*, SystemShared*, ChunkComponentData* and ChunkTag*, ChunkCleanup* and ChunkSystemTag*,
            // ChunkBuffer*, ChunkSystemBuffer*

            var t = archetype.Archetype->Types;

            Assert.AreEqual(entityType, t[0]);

            MatchesTypes<ComponentData1, ComponentData2>(t[1], t[2]);
            MatchesTypes<Cleanup1, CleanupState2>(t[3], t[4]);
            MatchesTypes<Buffer1, Buffer2>(t[5], t[6]);
            MatchesTypes<CleanupBuffer1, CleanupBuffer2>(t[7], t[8]);
            MatchesTypes<ManagedComponentData1, ManagedComponentData2>(t[9], t[10]);

            MatchesTypes<Tag1, Tag2, Simulate>(t[11], t[12], t[13]);
            MatchesTypes<CleanupTag1, CleanupTag2>(t[14], t[15]);
            MatchesTypes<Shared1, Shared2>(t[16], t[17]);
            MatchesTypes<CleanupShared1, CleanupShared2>(t[18], t[19]);

            MatchesChunkTypes<ComponentData1, ComponentData2, Tag1, Tag2>(t[20], t[21], t[22], t[23]);
            MatchesChunkTypes<Cleanup1, CleanupState2, CleanupTag1, CleanupTag2>(t[24], t[25], t[26], t[27]);

            MatchesChunkTypes<Buffer1, Buffer2>(t[28], t[29]);
            MatchesChunkTypes<CleanupBuffer1, CleanupBuffer2>(t[30], t[31]);
        }

#endif
    }
}
