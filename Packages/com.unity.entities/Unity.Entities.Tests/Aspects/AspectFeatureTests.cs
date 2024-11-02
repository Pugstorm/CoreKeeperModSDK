using System;
using NUnit.Framework;
using Unity.Entities;
using static Unity.Entities.SystemAPI;

namespace Unity.Entities.Tests.Aspects.FunctionalTests
{
    #region DynamicBufferField

    partial class BufferInAspect : AspectFunctionalTest
    {
        readonly partial struct AspectWithDynamicBuffer : IAspect
        {
            public readonly DynamicBuffer<MyBufferElement> MyBuffer;

            public TestData Read(TestData data)
            {
                data.Data = MyBuffer[0].Value;
                ++data.OperationCount;
                return data;
            }

            public TestData Write(TestData data)
            {
                if (MyBuffer.Length > 0)
                    MyBuffer.RemoveAt(0);
                MyBuffer.Add(new MyBufferElement { Value = data.Data });
                ++data.OperationCount;
                return data;
            }
        }
        partial struct BufferInAspectTestISystem : ISystem, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }

            public void OnUpdate(ref SystemState state)
            {
                var testData = UseCase.TestData;

                if (UseCase.TestPermutation(SystemKind.ISystem, ContextKind.Foreach, AccessKind.ReadWriteAccess))
                {
                    foreach (AspectWithDynamicBuffer test in SystemAPI.Query<AspectWithDynamicBuffer>())
                        testData = test.Write(testData);
                    // There is currently no way to get the value from the buffer to perform the assert. A generic fall-back assert will be performed inside TestUseCase instead
                    //Assert.AreEqual(UseCase.ValueToWrite, SystemAPI.EntityManager.GetBuffer<MyBufferElement>(testData.DataEntity)[0]);
                }

                UseCase.TestData = testData;
            }
        }
        partial class BufferInAspectTestSystemBase : SystemBase, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }
            protected override void OnUpdate()
            {
                var testData = UseCase.TestData;

                if (UseCase.TestPermutation(SystemKind.SystemBase, ContextKind.GetAspect, AccessKind.ReadWriteAccess))
                {
                    Entities.ForEach((Entity entity, DynamicBuffer<MyBufferElement> buffer) => testData =
                        SystemAPI.GetAspect<AspectWithDynamicBuffer>(entity).Write(testData)).Run();
                    Assert.AreEqual(UseCase.ValueToWrite, EntityManager.GetBuffer<MyBufferElement>(testData.DataEntity)[0].Value);
                }

                UseCase.TestData = testData;
            }
        }
        [Test]
        public void UseCases([Values] SystemKind systemKind, [Values] ContextKind contextKind, [Values] AccessKind accessKind)
        {

            // create an empty entity that should not be grabbed by the aspect query.
            var entityEmtpy = m_Manager.CreateEntity();

            // create an entity that will hold all components required by the aspect
            var entity = m_Manager.CreateEntity();

            var useCase = MakeUseCase(entity, systemKind, contextKind, accessKind, expectedOperationCount: 1);

            var buffer = m_Manager.AddBuffer<MyBufferElement>(entity);
            buffer.Add(new MyBufferElement { Value = useCase.ValueInitial });

            TestUseCase<BufferInAspectTestISystem, BufferInAspectTestSystemBase>(useCase,
                    getWrittenValue: () => buffer[0].Value); ;
        }
    }
    #endregion


    #region EntityField
    partial class EntityInAspect : AspectFunctionalTest
    {
        readonly partial struct AspectWithEntity : IAspect
        {
            public readonly RefRW<EcsTestData> Translation;
            public readonly Entity Entity;

            public TestData Read(TestData data)
            {
                data.Data = Entity.Index;
                ++data.OperationCount;
                return data;
            }

            // Do not need a write method as it is not possible to write to the Entity.
        }
        partial struct EntityInAspectTestISystem : ISystem, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }

            public void OnUpdate(ref SystemState state)
            {
                var testData = UseCase.TestData;

                if (UseCase.TestPermutation(SystemKind.ISystem, ContextKind.Foreach, AccessKind.ReadWriteAccess))
                    foreach (AspectWithEntity test in SystemAPI.Query<AspectWithEntity>())
                        testData = test.Read(testData);

                UseCase.TestData = testData;
            }
        }

        partial class EntityInAspectTestSystemBase : SystemBase, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }

            protected override void OnUpdate()
            {
                var testData = UseCase.TestData;

                if (UseCase.TestPermutation(SystemKind.SystemBase, ContextKind.GetAspect, AccessKind.ReadWriteAccess))
                    Entities.ForEach((Entity entity, in EcsTestData ecsData) => testData =
                        SystemAPI.GetAspect<AspectWithEntity>(entity).Read(testData)).Run();

                UseCase.TestData = testData;
            }
        }

        [Test]
        public void UseCases([Values] SystemKind systemKind, [Values] ContextKind contextKind, [Values] AccessKind accessKind)
        {
            // create an empty entity that should not be grabbed by the aspect query.
            var entityEmtpy = m_Manager.CreateEntity();

            // create an entity that will hold all components required by the aspect
            var entity = m_Manager.CreateEntity();
            m_Manager.AddComponent<EcsTestData>(entity);

            var useCase = MakeUseCase(entity, systemKind, contextKind, accessKind, expectedOperationCount:1);

            // The aspect will read from its Entity.Index field and compare with ValueToRead
            useCase.ValueToRead = entity.Index;

            TestUseCase<EntityInAspectTestISystem, EntityInAspectTestSystemBase>(useCase);
        }
    }

    #endregion

    #region ZeroSizeComponentField

    #region ZeroSizeComponentField Components
    struct ZeroSizeComponent : IComponentData
    {
        public int SomeMethod() => default;
        public static int StaticValue = default;
        public const int ConstValue = default;
    }
    struct ZeroSizeComponentNested : IComponentData
    {
        public ZeroSizeComponent Sub;
    }

    struct NonZeroSizeComponent : IComponentData
    {
        public int value;
        public NonZeroSizeComponent(int value) => this.value = value;
    }
    struct NonZeroSizeComponentNested : IComponentData
    {
        public NonZeroSizeComponent Sub;
        public NonZeroSizeComponentNested(int value)=> Sub = new NonZeroSizeComponent(value);
    }

    enum NonZeroSizeEnum
    {
        E0,
        E1
    }
    // Instances of `struct TestComp<TValue> : IComponentData`
    struct TestComp_NonZeroSizeEnum_ : IComponentData
    {
        public NonZeroSizeEnum Value;
    }
    struct TestComp_BlobAssetReference_int__ : IComponentData
    {
        public BlobAssetReference<int> Value;
    }
    struct TestComp_int_ : IComponentData
    {
        public int Value;
    }
    struct TestComp_IntPtr_ : IComponentData
    {
        public IntPtr Value;
    }
    struct TestComp_intPtr_ : IComponentData
    {
        public unsafe int* Value;
    }
    #endregion

    #region ZeroSizeComponentField Aspects
    readonly partial struct TestAspectZeroSize : IAspect
    {
        public readonly Entity Self;
        public readonly RefRO<ZeroSizeComponentNested> ZeroSizeComponent;

        public TestData Read(TestData data)
        {
            // Read from this.Data
            data.Data = AspectFunctionalTest.k_ValueRead;
            ++data.OperationCount;
            return data;
        }
    }

    readonly partial struct TestAspectIncludeZeroSize : IAspect
    {
        public readonly RefRW<EcsTestData> Data;
        public readonly RefRO<ZeroSizeComponentNested> ZeroSizeComponent;

        public TestData Read(TestData data)
        {
            // Read from this.Data
            data.Data = Data.ValueRO.value;
            ++data.OperationCount;
            return data;
        }

        public TestData Write(TestData data)
        {
            // Write to this.Data
            Data.ValueRW = new EcsTestData(data.Data);
            ++data.OperationCount;
            return data;
        }
    }

    // Instances of `readonly partial struct TestAspect<TComponent, TSelf> : IAspect<TSelf>`
    //TestAspectIncludeZeroSize
    readonly partial struct AspectWithNonZeroSizeEnumComponent : IAspect
    {
        public readonly RefRW<TestComp_NonZeroSizeEnum_> NonZeroSizeEnumComponent;

        public TestData Read(TestData data)
        {
            // Read from this.Data
            data.Data = (int)NonZeroSizeEnumComponent.ValueRO.Value;
            ++data.OperationCount;
            return data;
        }

        public TestData Write(TestData data)
        {
            // Write to this.Data
            NonZeroSizeEnumComponent.ValueRW = new TestComp_NonZeroSizeEnum_ { Value = (NonZeroSizeEnum)data.Data };
            ++data.OperationCount;
            return data;
        }
    }
    readonly partial struct AspectWithNonZeroSizeComponent : IAspect
    {
        public readonly RefRW<NonZeroSizeComponentNested> NonZeroSizeComponent;

        public TestData Read(TestData data)
        {
            // Read from this.Data
            data.Data = NonZeroSizeComponent.ValueRO.Sub.value;
            ++data.OperationCount;
            return data;
        }

        public TestData Write(TestData data)
        {
            // Write to this.Data
            NonZeroSizeComponent.ValueRW = new NonZeroSizeComponentNested(data.Data);
            ++data.OperationCount;
            return data;
        }
    }
    readonly partial struct TestAspect_TestComp_BlobAssetReference_int___ : IAspect
    {
        public readonly RefRW<TestComp_BlobAssetReference_int__> Component;
        public TestData Read(TestData data)
        {
            // Cannot read our test data from the Component, return expected value
            data.Data = AspectFunctionalTest.k_ValueRead;
            ++data.OperationCount;
            return data;
        }
    }
    readonly partial struct TestAspect_TestComp_int__ : IAspect
    {
        public readonly RefRW<TestComp_int_> Component;
        public TestData Read(TestData data)
        {
            // Read from this.Data
            data.Data = (int)Component.ValueRO.Value;
            ++data.OperationCount;
            return data;
        }

        public TestData Write(TestData data)
        {
            // Write to this.Data
            Component.ValueRW = new TestComp_int_ { Value = data.Data };
            ++data.OperationCount;
            return data;
        }
    }
    readonly partial struct TestAspect_TestComp_IntPtr__ : IAspect
    {
        public readonly RefRW<TestComp_IntPtr_> Component;
        public TestData Read(TestData data)
        {
            // Cannot read our test data from the Component, return expected value
            data.Data = AspectFunctionalTest.k_ValueRead;
            ++data.OperationCount;
            return data;
        }
    }
    readonly partial struct TestAspect_TestComp_intPtr__ : IAspect
    {
        public readonly RefRW<TestComp_intPtr_> Component;

        public TestData Read(TestData data)
        {
            // Cannot read our test data from the Component, return expected value
            data.Data = AspectFunctionalTest.k_ValueRead;
            ++data.OperationCount;
            return data;
        }

    }
    #endregion

    partial class ZeroSizeComponentInAspect : AspectFunctionalTest
    {
        #region Zero-size test cases

        partial struct ZeroSizeComponentInAspectTestISystem : ISystem, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }

            public void OnUpdate(ref SystemState state)
            {
                var testData = UseCase.TestData;

                if (UseCase.TestPermutation(SystemKind.ISystem, ContextKind.Foreach, AccessKind.ReadWriteAccess))
                    foreach (var test in SystemAPI.Query<TestAspectIncludeZeroSize>())
                        testData = test.Write(testData);

                UseCase.TestData = testData;
            }
        }

        partial class ZeroSizeComponentInAspectTestSystemBase : SystemBase, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }

            protected override void OnUpdate()
            {
                var testData = UseCase.TestData;

                if (UseCase.TestPermutation(SystemKind.SystemBase, ContextKind.GetAspect, AccessKind.ReadWriteAccess))
                    Entities.ForEach((Entity entity, in EcsTestData ecsData) => testData =
                        SystemAPI.GetAspect<TestAspectIncludeZeroSize>(entity).Write(testData)).Run();

                UseCase.TestData = testData;
            }
        }

        [Test]
        public void ZeroSizeUseCases([Values] SystemKind systemKind, [Values] ContextKind contextKind, [Values] AccessKind accessKind)
        {
            // create an empty entity that should not be grabbed by the aspect query.
            var entityEmtpy = m_Manager.CreateEntity();

            // create an entity that will hold all components required by the aspect
            var entity = m_Manager.CreateEntity();
            var entityZeroSize = m_Manager.CreateEntity();

            var useCase = MakeUseCase(entity, systemKind, contextKind, accessKind, expectedOperationCount: 1);
            m_Manager.AddComponent<ZeroSizeComponentNested>(entity);
            m_Manager.AddComponentData(entity, new EcsTestData(useCase.ValueInitial));
            m_Manager.AddComponent<ZeroSizeComponentNested>(entityZeroSize);

            TestUseCase<ZeroSizeComponentInAspectTestISystem, ZeroSizeComponentInAspectTestSystemBase>(useCase,
                    getWrittenValue:()=> m_Manager.GetComponentData<EcsTestData>(entity).value);
        }
        #endregion

        #region Non-zero-size test cases

        public enum NonZeroReason
        {
            BecauseNestedInt,
            BecauseEnum,
            BecauseBlobAssetReference_int_,
            BecauseInt,
            BecauseIntPtr,
            BecauseActualIntPtr,
        }

        partial struct NonZeroSizeComponentInAspectTestISystem : ISystem, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }

            public NonZeroReason NonZeroReason;
            public void OnUpdate(ref SystemState state)
            {
                var testData = UseCase.TestData;
                if (UseCase.TestPermutation(SystemKind.ISystem, ContextKind.Foreach, AccessKind.ReadWriteAccess))
                {
                    switch (NonZeroReason)
                    {
                        case NonZeroReason.BecauseEnum:
                            foreach (AspectWithNonZeroSizeEnumComponent test in SystemAPI.Query<AspectWithNonZeroSizeEnumComponent>())
                                testData = test.Write(testData);
                            break;
                        case NonZeroReason.BecauseNestedInt:
                            foreach (AspectWithNonZeroSizeComponent test in SystemAPI.Query<AspectWithNonZeroSizeComponent>())
                                testData = test.Write(testData);
                            break;
                        case NonZeroReason.BecauseInt:
                            foreach (var test in SystemAPI.Query<TestAspect_TestComp_int__>())
                                testData = test.Write(testData);
                            break;
                        case NonZeroReason.BecauseBlobAssetReference_int_:
                        case NonZeroReason.BecauseIntPtr:
                        case NonZeroReason.BecauseActualIntPtr:
                            UseCase.MarkNotSupported();
                            break;
                    }
                }
                UseCase.TestData = testData;
            }
        }

        partial class NonZeroSizeComponentInAspectTestSystemBase : SystemBase, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }
            public NonZeroReason NonZeroReason;
            protected override void OnUpdate()
            {
                var testData = UseCase.TestData;
                if (UseCase.TestPermutation(SystemKind.SystemBase, ContextKind.GetAspect, AccessKind.ReadWriteAccess))
                {
                    switch (NonZeroReason)
                    {
                        case NonZeroReason.BecauseEnum:
                            Entities.ForEach((Entity entity, in TestComp_NonZeroSizeEnum_ ecsData) => testData =
                                SystemAPI.GetAspect<AspectWithNonZeroSizeEnumComponent>(entity).Write(testData)).Run();
                            break;
                        case NonZeroReason.BecauseNestedInt:
                            Entities.ForEach((Entity entity, in NonZeroSizeComponentNested ecsData) => testData =
                                SystemAPI.GetAspect<AspectWithNonZeroSizeComponent>(entity).Write(testData)).Run();
                            break;
                        case NonZeroReason.BecauseInt:
                            Entities.ForEach((Entity entity, in TestComp_int_ ecsData) => testData =
                                SystemAPI.GetAspect<TestAspect_TestComp_int__>(entity).Write(testData)).Run();
                            break;
                        case NonZeroReason.BecauseBlobAssetReference_int_:
                        case NonZeroReason.BecauseIntPtr:
                        case NonZeroReason.BecauseActualIntPtr:
                            UseCase.MarkNotSupported();
                            break;

                    }
                }
                UseCase.TestData = testData;
            }
        }
        [Test]
        public void NonZeroSizeUseCases([Values] SystemKind systemKind, [Values] ContextKind contextKind, [Values] AccessKind accessKind, [Values] NonZeroReason nonZeroReason)
        {
            // create an empty entity that should not be grabbed by the aspect query.
            var entityEmtpy = m_Manager.CreateEntity();

            // create an entity that will hold all components required by the aspect
            var entity = m_Manager.CreateEntity();

            var systemI = World.GetOrCreateSystem<NonZeroSizeComponentInAspectTestISystem>();
            var systemBase = World.GetOrCreateSystemManaged<NonZeroSizeComponentInAspectTestSystemBase>();
            World.Unmanaged.GetUnsafeSystemRef<NonZeroSizeComponentInAspectTestISystem>(systemI).NonZeroReason = nonZeroReason;
            systemBase.NonZeroReason = nonZeroReason;

            var useCase = MakeUseCase(entity, systemKind, contextKind, accessKind, expectedOperationCount:1);
            Func<int> funcGetWrittenValue = default;
            switch (nonZeroReason)
            {
                case NonZeroReason.BecauseEnum:
                    m_Manager.AddComponentData(entity, new TestComp_NonZeroSizeEnum_ { Value = (NonZeroSizeEnum)useCase.ValueInitial});
                    funcGetWrittenValue = () => (int)m_Manager.GetComponentData<TestComp_NonZeroSizeEnum_>(entity).Value;
                    break;
                case NonZeroReason.BecauseNestedInt:
                    m_Manager.AddComponentData(entity, new NonZeroSizeComponentNested(useCase.ValueInitial));
                    funcGetWrittenValue = () => m_Manager.GetComponentData<NonZeroSizeComponentNested>(entity).Sub.value;
                    break;
                case NonZeroReason.BecauseBlobAssetReference_int_:
                    m_Manager.AddComponentData(entity, new TestComp_BlobAssetReference_int__());
                    // write not possible
                    if(accessKind == AccessKind.ReadWriteAccess)
                        useCase.ExpectedOperationCount = 0;
                    break;
                case NonZeroReason.BecauseInt:
                    m_Manager.AddComponentData(entity, new TestComp_int_{ Value = useCase.ValueInitial });
                    funcGetWrittenValue = () => m_Manager.GetComponentData<TestComp_int_>(entity).Value;
                    break;
                case NonZeroReason.BecauseIntPtr:
                    m_Manager.AddComponentData(entity, new TestComp_IntPtr_());
                    // write not possible
                    if (accessKind == AccessKind.ReadWriteAccess)
                        useCase.ExpectedOperationCount = 0;
                    break;
                case NonZeroReason.BecauseActualIntPtr:
                    m_Manager.AddComponentData(entity, new TestComp_intPtr_());
                    // write not possible
                    if (accessKind == AccessKind.ReadWriteAccess)
                        useCase.ExpectedOperationCount = 0;
                    break;
            }
            TestUseCase<NonZeroSizeComponentInAspectTestISystem, NonZeroSizeComponentInAspectTestSystemBase>(useCase,
                getWrittenValue: funcGetWrittenValue);
        }
        #endregion
    }
    #endregion
}
