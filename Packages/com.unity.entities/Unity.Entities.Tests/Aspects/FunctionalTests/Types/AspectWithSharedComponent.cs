using System;
using Unity.Entities.Tests.Aspects.FunctionalTests;

namespace Unity.Entities.Tests.Aspects.Types
{
    readonly partial struct AspectWithSharedComponent : IAspect
    {
        public readonly EcsTestSharedComp Field;

        public static Entity ApplyTo(EntityManager entityManager, Entity entity, int initialValue)
        {
            if (!entityManager.HasComponent<EcsTestSharedComp>(entity))
                entityManager.AddComponent<EcsTestSharedComp>(entity);
            entityManager.SetSharedComponent(entity, new EcsTestSharedComp(initialValue));
            return entity;
        }

        public TestData Read(TestData data)
        {
            data.Data = Field.value;
            ++data.OperationCount;
            return data;
        }
    }

    readonly partial struct AspectWithSharedComponent2 : IAspect
    {
        public readonly EcsTestSharedComp2 Field;

        public static Entity ApplyTo(EntityManager entityManager, Entity entity, int initialValue)
        {
            if (!entityManager.HasComponent<EcsTestSharedComp>(entity))
                entityManager.AddComponent<EcsTestSharedComp>(entity);
            entityManager.SetSharedComponent(entity, new EcsTestSharedComp(initialValue));
            return entity;
        }

        public TestData Read(TestData data)
        {
            data.Data = Field.value0;
            ++data.OperationCount;
            return data;
        }
    }
    readonly partial struct AspectWithSharedComponent3 : IAspect
    {
        public readonly EcsTestSharedComp3 Field;

        public static Entity ApplyTo(EntityManager entityManager, Entity entity, int initialValue)
        {
            if (!entityManager.HasComponent<AspectWithSharedComponent3>(entity))
                entityManager.AddComponent<AspectWithSharedComponent3>(entity);
            entityManager.SetSharedComponent(entity, new EcsTestSharedComp(initialValue));
            return entity;
        }

        public TestData Read(TestData data)
        {
            data.Data = Field.value0;
            ++data.OperationCount;
            return data;
        }
    }
    readonly partial struct AspectWithSharedComponent4 : IAspect
    {
        public readonly EcsTestSharedComp4 Field;

        public static Entity ApplyTo(EntityManager entityManager, Entity entity, int initialValue)
        {
            if (!entityManager.HasComponent<AspectWithSharedComponent4>(entity))
                entityManager.AddComponent<AspectWithSharedComponent4>(entity);
            entityManager.SetSharedComponent(entity, new EcsTestSharedComp(initialValue));
            return entity;
        }

        public TestData Read(TestData data)
        {
            data.Data = Field.value0;
            ++data.OperationCount;
            return data;
        }
    }
}
