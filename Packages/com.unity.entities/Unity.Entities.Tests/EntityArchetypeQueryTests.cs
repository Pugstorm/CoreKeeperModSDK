using System;
using System.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    class EntityArchetypeQueryTests : ECSTestsFixture
    {
        [Test]
        public unsafe void ArchetypeQuery_TypesAreSorted()
        {
            var sortedComponentTypes = new ComponentType[] {
                typeof(EcsTestDataEnableable),
                typeof(EcsTestDataEnableable2),
                typeof(EcsTestDataEnableable3),
                typeof(EcsTestDataEnableable4),
            };
            Array.Sort(sortedComponentTypes);
            var reversedComponentTypes = new ComponentType[sortedComponentTypes.Length];
            sortedComponentTypes.CopyTo(reversedComponentTypes, 0);
            Array.Reverse(reversedComponentTypes);
            // All
            fixed (ComponentType* types = &reversedComponentTypes[0])
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAll(types, sortedComponentTypes.Length)
                    .Build(m_Manager);
                var queryData = query._GetImpl()->_QueryData;
                Assert.AreEqual(1, queryData->ArchetypeQueryCount);
                var aq = queryData->ArchetypeQueries[0];
                Assert.AreEqual(sortedComponentTypes.Length, aq.AllCount);
                for (int i = 0; i < aq.AllCount; ++i)
                    Assert.AreEqual(sortedComponentTypes[i].TypeIndex, aq.All[i], $"Mismatch at All[{i}]");
            }
            // Any
            fixed (ComponentType* types = &reversedComponentTypes[0])
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAny(types, sortedComponentTypes.Length)
                    .Build(m_Manager);
                var queryData = query._GetImpl()->_QueryData;
                Assert.AreEqual(1, queryData->ArchetypeQueryCount);
                var aq = queryData->ArchetypeQueries[0];
                Assert.AreEqual(sortedComponentTypes.Length, aq.AnyCount);
                for (int i = 0; i < aq.AnyCount; ++i)
                    Assert.AreEqual(sortedComponentTypes[i].TypeIndex, aq.Any[i], $"Mismatch at Any[{i}]");
            }
            // None
            fixed (ComponentType* types = &reversedComponentTypes[0])
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithNone(types, sortedComponentTypes.Length)
                    .Build(m_Manager);
                var queryData = query._GetImpl()->_QueryData;
                Assert.AreEqual(1, queryData->ArchetypeQueryCount);
                var aq = queryData->ArchetypeQueries[0];
                Assert.AreEqual(sortedComponentTypes.Length, aq.NoneCount);
                for (int i = 0; i < aq.NoneCount; ++i)
                    Assert.AreEqual(sortedComponentTypes[i].TypeIndex, aq.None[i], $"Mismatch at None[{i}]");
            }
            // Disabled
            fixed (ComponentType* types = &reversedComponentTypes[0])
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithDisabled(types, sortedComponentTypes.Length)
                    .Build(m_Manager);
                var queryData = query._GetImpl()->_QueryData;
                Assert.AreEqual(1, queryData->ArchetypeQueryCount);
                var aq = queryData->ArchetypeQueries[0];
                Assert.AreEqual(sortedComponentTypes.Length, aq.DisabledCount);
                for (int i = 0; i < aq.DisabledCount; ++i)
                    Assert.AreEqual(sortedComponentTypes[i].TypeIndex, aq.Disabled[i], $"Mismatch at Disabled[{i}]");
            }
            // Absent
            fixed (ComponentType* types = &reversedComponentTypes[0])
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithAbsent(types, sortedComponentTypes.Length)
                    .Build(m_Manager);
                var queryData = query._GetImpl()->_QueryData;
                Assert.AreEqual(1, queryData->ArchetypeQueryCount);
                var aq = queryData->ArchetypeQueries[0];
                Assert.AreEqual(sortedComponentTypes.Length, aq.AbsentCount);
                for (int i = 0; i < aq.AbsentCount; ++i)
                    Assert.AreEqual(sortedComponentTypes[i].TypeIndex, aq.Absent[i], $"Mismatch at Absent[{i}]");
            }
            // Present
            fixed (ComponentType* types = &reversedComponentTypes[0])
            {
                var query = new EntityQueryBuilder(Allocator.Temp)
                    .WithPresent(types, sortedComponentTypes.Length)
                    .Build(m_Manager);
                var queryData = query._GetImpl()->_QueryData;
                Assert.AreEqual(1, queryData->ArchetypeQueryCount);
                var aq = queryData->ArchetypeQueries[0];
                Assert.AreEqual(sortedComponentTypes.Length, aq.PresentCount);
                for (int i = 0; i < aq.PresentCount; ++i)
                    Assert.AreEqual(sortedComponentTypes[i].TypeIndex, aq.Present[i], $"Mismatch at Present[{i}]");
            }
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilter_IdenticalIds_InDifferentFilters_Throws()
        {
            Assert.Throws<EntityQueryDescValidationException>(() =>
            {
                new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<EcsTestData>()
                    .WithNone<EcsTestData>()
                    .Build(m_Manager);
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilter_IdenticalIds_InSameFilter_Throws()
        {
            Assert.Throws<EntityQueryDescValidationException>(() =>
            {
                new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<EcsTestData,EcsTestData>()
                    .Build(m_Manager);
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilter_MultipleIdenticalIds_Throws()
        {
            Assert.Throws<EntityQueryDescValidationException>(() =>
            {
                new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<EcsTestData,EcsTestData2>()
                    .WithNone<EcsTestData3,EcsTestData4>()
                    .WithAny<EcsTestData,EcsTestData4>()
                    .Build(m_Manager);
            });
        }

        [Test]
        public void EntityQueryFilter_SeparatedIds()
        {
            Assert.DoesNotThrow(() =>
            {
                new EntityQueryBuilder(Allocator.Temp)
                    .WithAll<EcsTestData,EcsTestData2>()
                    .WithNone<EcsTestData3,EcsTestData4>()
                    .WithAny<EcsTestData5>()
                    .Build(m_Manager);
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilter_CannotContainExcludeComponentType_All_Throws()
        {
            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(EcsTestData), ComponentType.Exclude<EcsTestData2>() },
            };

            Assert.Throws<ArgumentException>(() =>
            {
                queryDesc.Validate();
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilterCannotContainExcludeComponentType_Any_Throws()
        {
            var queryDesc = new EntityQueryDesc
            {
                Any = new ComponentType[] {typeof(EcsTestData), ComponentType.Exclude<EcsTestData2>() },
            };

            Assert.Throws<ArgumentException>(() =>
            {
                queryDesc.Validate();
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilterCannotContainExcludeComponentType_None_Throws()
        {
            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(EcsTestData) },
                None = new ComponentType[] {typeof(EcsTestData3), ComponentType.Exclude<EcsTestData4>() },
            };

            Assert.Throws<ArgumentException>(() =>
            {
                queryDesc.Validate();
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilterCannotContainExcludeComponentType_Disabled_Throws()
        {
            var queryDesc = new EntityQueryDesc
            {
                Disabled = new ComponentType[] {typeof(EcsTestDataEnableable), ComponentType.Exclude<EcsTestDataEnableable2>() },
            };

            Assert.Throws<ArgumentException>(() =>
            {
                queryDesc.Validate();
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilterCannotContainExcludeComponentType_Absent_Throws()
        {
            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(EcsTestData) },
                Absent = new ComponentType[] {typeof(EcsTestData3), ComponentType.Exclude<EcsTestData4>() },
            };

            Assert.Throws<ArgumentException>(() =>
            {
                queryDesc.Validate();
            });
        }

        [Test]
        [TestRequiresDotsDebugOrCollectionChecks("Test requires entity query safety checks")]
        public void EntityQueryFilterCannotContainExcludeComponentType_Present_Throws()
        {
            var queryDesc = new EntityQueryDesc
            {
                All = new ComponentType[] {typeof(EcsTestData) },
                Present = new ComponentType[] {typeof(EcsTestData3), ComponentType.Exclude<EcsTestData4>() },
            };

            Assert.Throws<ArgumentException>(() =>
            {
                queryDesc.Validate();
            });
        }
    }
}
