using NUnit.Framework;

namespace Unity.Entities.Tests
{
    class ArchetypeTests : ECSTestsFixture
    {
        private unsafe T[] ToManagedArray<T>(T* values, int length) where T : unmanaged
        {
            var array = new T[length];
            for (int i = 0; i < length; ++i)
                array[i] = values[i];
            return array;
        }

        [Test]
        unsafe public void DiffArchetype_AddRemove()
        {
            var before = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData3), typeof(EcsTestData4));
            var after = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2), typeof(EcsTestData3), typeof(EcsTestData5));

            var added = stackalloc TypeIndex[after.TypesCount];
            var removed = stackalloc TypeIndex[before.TypesCount];

            EntityArchetype.CalculateDifference(before, after, added, out var addedTypesCount, removed, out var removedTypesCount);

            Assert.AreEqual(2, addedTypesCount);
            Assert.That(ToManagedArray(added, addedTypesCount), Is.EquivalentTo(new TypeIndex[] {
                ComponentType.ReadOnly<EcsTestData2>().TypeIndex,
                ComponentType.ReadOnly<EcsTestData5>().TypeIndex,
            }));
            Assert.AreEqual(1, removedTypesCount);
            Assert.AreEqual(TypeManager.GetTypeIndex(typeof(EcsTestData4)), removed[0]);
        }

        [Test]
        unsafe public void DiffArchetype_AddEmpty()
        {
            var before = m_Manager.CreateArchetype();
            var after = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var added = stackalloc TypeIndex[after.TypesCount];
            var removed = stackalloc TypeIndex[before.TypesCount];

            EntityArchetype.CalculateDifference(before, after, added, out var addedTypesCount, removed, out var removedTypesCount);

            Assert.AreEqual(2, addedTypesCount);
            Assert.That(ToManagedArray(added, addedTypesCount), Is.EquivalentTo(new TypeIndex[] {
                ComponentType.ReadOnly<EcsTestData>().TypeIndex,
                ComponentType.ReadOnly<EcsTestData2>().TypeIndex,
            }));
            Assert.AreEqual(0, removedTypesCount);
        }

        [Test]
        unsafe public void DiffArchetype_RemoveEmpty()
        {
            var before = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var after = m_Manager.CreateArchetype();

            var added = stackalloc TypeIndex[after.TypesCount];
            var removed = stackalloc TypeIndex[before.TypesCount];

            EntityArchetype.CalculateDifference(before, after, added, out var addedTypesCount, removed, out var removedTypesCount);

            Assert.AreEqual(2, removedTypesCount);
            Assert.That(ToManagedArray(removed, removedTypesCount), Is.EquivalentTo(new TypeIndex[] {
                ComponentType.ReadOnly<EcsTestData>().TypeIndex,
                ComponentType.ReadOnly<EcsTestData2>().TypeIndex,
            }));
            Assert.AreEqual(0, addedTypesCount);
        }

        [Test]
        unsafe public void DiffArchetype_NoChange()
        {
            var before = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));
            var after = m_Manager.CreateArchetype(typeof(EcsTestData), typeof(EcsTestData2));

            var added = stackalloc TypeIndex[after.TypesCount];
            var removed = stackalloc TypeIndex[before.TypesCount];

            EntityArchetype.CalculateDifference(before, after, added, out var addedTypesCount, removed, out var removedTypesCount);

            Assert.AreEqual(0, removedTypesCount);
            Assert.AreEqual(0, addedTypesCount);
        }

        [Test]
        unsafe public void DiffArchetype_EmptyEmpty()
        {
            var before = m_Manager.CreateArchetype();
            var after = m_Manager.CreateArchetype();

            var added = stackalloc TypeIndex[after.TypesCount];
            var removed = stackalloc TypeIndex[before.TypesCount];

            EntityArchetype.CalculateDifference(before, after, added, out var addedTypesCount, removed, out var removedTypesCount);

            Assert.AreEqual(0, removedTypesCount);
            Assert.AreEqual(0, addedTypesCount);
        }
    }
}
