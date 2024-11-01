using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Entities.PerformanceTests
{
    internal static class PerformanceTestHelpers
    {
        public static NativeArray<EntityArchetype> CreateUniqueArchetypes(
            EntityManager manager,
            int numUniqueArchetypes,
            Allocator allocator,
            params ComponentType[] commonComponentTypes)
        {
            var archetypes = CollectionHelper.CreateNativeArray<EntityArchetype>(numUniqueArchetypes, allocator);

            int typeCount = CollectionHelper.Log2Ceil(numUniqueArchetypes);
            for (int i = 0; i < numUniqueArchetypes; i++)
            {
                var typeList = new List<ComponentType>();

                for (int typeIndex = 0; typeIndex < typeCount; typeIndex++)
                {
                    if ((i & (1 << typeIndex)) != 0)
                    {
                        typeList.Add(TestTags.TagTypes[typeIndex]);
                    }
                }

                typeList.AddRange(commonComponentTypes);

                var types = typeList.ToArray();
                archetypes[i] = manager.CreateArchetype(types);
            }

            return archetypes;
        }

        public static class TestTags
        {
            public struct TestTag0 : IComponentData
            {
            }

            public struct TestTag1 : IComponentData
            {
            }

            public struct TestTag2 : IComponentData
            {
            }

            public struct TestTag3 : IComponentData
            {
            }

            public struct TestTag4 : IComponentData
            {
            }

            public struct TestTag5 : IComponentData
            {
            }

            public struct TestTag6 : IComponentData
            {
            }

            public struct TestTag7 : IComponentData
            {
            }

            public struct TestTag8 : IComponentData
            {
            }

            public struct TestTag9 : IComponentData
            {
            }

            public struct TestTag10 : IComponentData
            {
            }

            public struct TestTag11 : IComponentData
            {
            }

            public struct TestTag12 : IComponentData
            {
            }

            public struct TestTag13 : IComponentData
            {
            }

            public struct TestTag14 : IComponentData
            {
            }

            public struct TestTag15 : IComponentData
            {
            }

            public struct TestTag16 : IComponentData
            {
            }

            public struct TestTag17 : IComponentData
            {
            }

            public static Type[] TagTypes =
            {
                typeof(TestTag0),
                typeof(TestTag1),
                typeof(TestTag2),
                typeof(TestTag3),
                typeof(TestTag4),
                typeof(TestTag5),
                typeof(TestTag6),
                typeof(TestTag7),
                typeof(TestTag8),
                typeof(TestTag9),
                typeof(TestTag10),
                typeof(TestTag11),
                typeof(TestTag12),
                typeof(TestTag13),
                typeof(TestTag14),
                typeof(TestTag15),
                typeof(TestTag16),
                typeof(TestTag17),
            };
        }
    }
}
