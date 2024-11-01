using System;
using NUnit.Framework;

namespace Unity.Entities.Tests
{
    partial class ComponentDataRefTests : ECSTestsFixture
    {
        [Test]
        public void TestDataRefFromEntity([Values]bool optional)
        {
            var e = m_Manager.CreateEntity(typeof(EcsTestData));
            m_Manager.SetComponentData(e, new EcsTestData(1));

            var testData = optional
                ? EmptySystem.GetComponentLookup<EcsTestData>().GetRefRWOptional(e)
                : EmptySystem.GetComponentLookup<EcsTestData>().GetRefRW(e);

            testData.ValueRW.value = 5;
            Assert.AreEqual(5, testData.ValueRO.value);
            Assert.AreEqual(5, m_Manager.GetComponentData<EcsTestData>(e).value);

            Assert.IsTrue(testData.IsValid);
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity component data safety checks")]
        public void TestMissingGetDataRef()
        {
            var e = m_Manager.CreateEntity();

            Assert.Throws<ArgumentException>(() => EmptySystem.GetComponentLookup<EcsTestData>().GetRefRW(e));
            Assert.Throws<ArgumentException>(() => EmptySystem.GetComponentLookup<EcsTestData>().GetRefRW(Entity.Null));
        }

        // This test works in all managed player configs whether safety checks are enabled or not since accessing a null with throw
        // however the error message will be different depending if safety checks are enabled or not
        [Test]
        [IgnoreTest_IL2CPP("IL2CPP will not throw a null check when reading null and instead will crash.")]
        public void TestOptionalMissing()
        {
            var e = m_Manager.CreateEntity();

            var missingData = EmptySystem.GetComponentLookup<EcsTestData>().GetRefRWOptional(e);
            var missingData2 = EmptySystem.GetComponentLookup<EcsTestData>().GetRefRWOptional(Entity.Null);

            Assert.IsFalse(missingData.IsValid);
            Assert.IsFalse(missingData2.IsValid);

            //NOTE: it would be better if we can throw a objectdisposedexception.
            // But right now there is no simple way to constructing a safety handle that is invalid but not null.
            Assert.Throws<NullReferenceException>(() => { missingData.ValueRW.value = 5; });
            Assert.Throws<NullReferenceException>(() => { Debug.Log(missingData.ValueRO.value); });

            Assert.Throws<NullReferenceException>(() => { missingData2.ValueRW.value = 5; });
            Assert.Throws<NullReferenceException>(() => { Debug.Log(missingData2.ValueRO.value); });
        }
    }
}
