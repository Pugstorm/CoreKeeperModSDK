#if ENABLE_UNITY_COLLECTIONS_CHECKS || UNITY_DOTS_DEBUG
using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    public struct ECSTestEnableableBuffer : IBufferElementData, IEnableableComponent
    {
        public float Value;
    }

    [BurstCompile]
    partial class EntitiesExceptionTestSystem : SystemBase
    {
        protected override void OnUpdate() { }

        [BurstCompile(CompileSynchronously = true)]
        public void CallSystemAPIHasSingleton<T>() where T : unmanaged =>
            SystemAPI.HasSingleton<T>();

        [BurstCompile(CompileSynchronously = true)]
        public void EntityManager_CreateSingleton<T>() where T : unmanaged, IComponentData =>
            EntityManager.CreateSingleton<T>();

        [BurstCompile(CompileSynchronously = true)]
        public void EntityQuery_GetSingleton<T>() where T : unmanaged, IComponentData
        {
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(EntityManager);
            query.GetSingleton<T>();
        }

        [BurstCompile(CompileSynchronously = true)]
        public void EntityQuery_GetSingletonBuffer<T>() where T : unmanaged, IBufferElementData
        {
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(EntityManager);
            query.GetSingletonBuffer<T>();
        }

        [BurstCompile(CompileSynchronously = true)]
        public void EntityQuery_HasSingleton<T>() where T : unmanaged, IComponentData
        {
            using var query = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().Build(EntityManager);
            query.HasSingleton<T>();
        }
    }
    class EntitiesExceptionTests : ECSTestsFixture
    {
        EntitiesExceptionTestSystem TestSystem;

        [SetUp]
        public void SetUp()
        {
            TestSystem = World.GetOrCreateSystemManaged<EntitiesExceptionTestSystem>();
        }

        [Test]
        public void HasSingleton_WithEnabledComponent_GeneratesException()
        {
            var exceptionStr = "Can't call HasSingleton<Unity.Entities.Tests.EcsTestDataEnableable>() with enableable component type Unity.Entities.Tests.EcsTestDataEnableable.";
            Assert.That(() => TestSystem.CallSystemAPIHasSingleton<EcsTestDataEnableable>(),
                Throws.Exception.TypeOf<InvalidOperationException>().With.Message.Contains(exceptionStr));
        }

        [Test]
        public void EntityManager_CreateSingleton_EnableableComponent_GeneratesException()
        {
            var exceptionStr = "Singleton component Unity.Entities.Tests.EcsTestDataEnableable can not be created because it is an enableable component type.";
            Assert.That(() => TestSystem.EntityManager_CreateSingleton<EcsTestDataEnableable>(),
                Throws.Exception.TypeOf<InvalidOperationException>().With.Message.Contains(exceptionStr));
        }

        [Test]
        public void GetSingleton_GetSingleton_ZeroSizedComponent_GeneratesException()
        {
            var exceptionStr = "Can't call GetSingleton<Unity.Entities.Tests.EcsTestTag>() with zero-size type Unity.Entities.Tests.EcsTestTag.";
            Assert.That(() => TestSystem.EntityQuery_GetSingleton<EcsTestTag>(),
                Throws.Exception.TypeOf<InvalidOperationException>().With.Message.Contains(exceptionStr));
        }

        [Test]
        public void GetSingleton_GetSingleton_EnableableComponent_GeneratesException()
        {
            var exceptionStr = "Can't call GetSingleton<Unity.Entities.Tests.EcsTestDataEnableable>() with enableable component type Unity.Entities.Tests.EcsTestDataEnableable.";
            Assert.That(() => TestSystem.EntityQuery_GetSingleton<EcsTestDataEnableable>(),
                Throws.Exception.TypeOf<InvalidOperationException>().With.Message.Contains(exceptionStr));
        }

        [Test]
        public void GetSingleton_GetSingleton_NoMatching_GeneratesException()
        {
            var exceptionStr = "GetSingleton<Unity.Entities.Tests.EcsTestData>() requires that exactly one entity exists that match this query, but there are none. Are you missing a call to RequireForUpdate<T>()? You could also use TryGetSingleton<T>()";
            Assert.That(() => TestSystem.EntityQuery_GetSingleton<EcsTestData>(),
                Throws.Exception.TypeOf<InvalidOperationException>().With.Message.Contains(exceptionStr));
        }

        [Test]
        public void GetSingleton_GetSingletonBuffer_EnableableComponent_GeneratesException()
        {
            var exceptionStr = "Can't call GetSingletonBuffer<Unity.Entities.Tests.ECSTestEnableableBuffer>() with enableable component type Unity.Entities.Tests.ECSTestEnableableBuffer.";
            Assert.That(() => TestSystem.EntityQuery_GetSingletonBuffer<ECSTestEnableableBuffer>(),
                Throws.Exception.TypeOf<InvalidOperationException>().With.Message.Contains(exceptionStr));
        }

        [Test]
        public void GetSingleton_HasSingleton_EnableableComponent_GeneratesException()
        {
            var exceptionStr = "Can't call HasSingleton<Unity.Entities.Tests.EcsTestDataEnableable>() with enableable component type Unity.Entities.Tests.EcsTestDataEnableable.";
            Assert.That(() => TestSystem.EntityQuery_HasSingleton<EcsTestDataEnableable>(),
                Throws.Exception.TypeOf<InvalidOperationException>().With.Message.Contains(exceptionStr));
        }
    }
}
#endif
