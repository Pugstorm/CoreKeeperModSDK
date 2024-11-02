using System;
using NUnit.Framework;

namespace Unity.Entities.Tests.Aspects.FunctionalTests
{
    public enum OperationKind
    {
        Read,
        Write
    }

    public enum SystemKind
    {
        ISystem,
        SystemBase
    }

    public enum ContextKind
    {
        GetAspect,
        Foreach,
    }

    public enum AccessKind
    {
        ReadWriteAccess
    }

    public enum OptionalKind
    {
        NoOptionalComponent,
        WithOptionalComponent,
    }

    /// <summary>
    /// Data used by the test system to perform their operations
    /// </summary>
    public struct TestData
    {
        public int Data;
        public Entity DataEntity;
        public int OperationCount;
    }

    public struct UseCase
    {
        public SystemKind SystemKind;
        public ContextKind ContextKind;
        public AccessKind AccessKind;

        /// <summary>
        /// Values used during the read/write operations
        /// </summary>
        public int ValueInitial;
        public int ValueToOverwrite;
        public int ValueToRead;
        public int ValueToWrite;

        /// <summary>
        /// Number of operation to be expected.
        /// if your system entity query is expected to return N number of entities,
        /// increase TestData.OperationCount in your loop and set this value to N
        /// </summary>
        public int ExpectedOperationCount;

        public TestData TestData;

        /// <summary>
        /// Tell if the use case was tested
        /// </summary>
        public bool IsTested;

        /// <summary>
        /// Tell if this use case is supported by our API.
        /// </summary>
        public bool IsSupported;

        public UseCase(Entity entity, SystemKind systemKind, ContextKind contextKind, AccessKind accessKind, int valueToOverwrite, int valueToRead, int valueToWrite, int expectedOperationCount)
        {
            SystemKind = systemKind;
            ContextKind = contextKind;
            AccessKind = accessKind;
            ValueToOverwrite = valueToOverwrite;
            ValueInitial = ValueToOverwrite;
            ValueToRead = valueToRead;
            ValueToWrite = valueToWrite;
            ExpectedOperationCount = expectedOperationCount;
            TestData = new TestData
            {
                Data = valueToWrite,
                DataEntity = entity,
                OperationCount = 0
            };
            IsTested = false;

            // These use-cases are currently not supported
            if (systemKind == SystemKind.ISystem && contextKind == ContextKind.GetAspect)
                IsSupported = false;
            else if(systemKind == SystemKind.SystemBase && contextKind == ContextKind.Foreach)
                IsSupported = false;
            else
                IsSupported = true;

        }

        /// <summary>
        /// Call this in your system to perform the correct operation given the current use case
        /// </summary>
        /// <param name="systemKind"></param>
        /// <param name="contextKind"></param>
        /// <param name="accessKind"></param>
        /// <returns></returns>
        public bool TestPermutation(SystemKind systemKind, ContextKind contextKind, AccessKind accessKind)
        {
            if (SystemKind == systemKind && ContextKind == contextKind && AccessKind == accessKind)
            {
                IsTested = true;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Call this method in your system if a use-case is not supported by our API
        /// </summary>
        /// <param name="systemKind"></param>
        /// <param name="contextKind"></param>
        /// <param name="accessKind"></param>
        public void MarkNotSupported(SystemKind systemKind, ContextKind contextKind, AccessKind accessKind)
        {
            if (SystemKind == systemKind && ContextKind == contextKind && AccessKind == accessKind)
                IsSupported = false;
        }

        /// <summary>
        /// Call this method in your system if the current use-case is not supported by our API
        /// </summary>
        public void MarkNotSupported()
        {
            IsSupported = false;
        }

    }

    public interface IUseCaseTestSystem
    {
        public UseCase UseCase { get; set; }

    }

    class AspectFunctionalTest : ECSTestsFixture
    {
        public const int k_ValueInit = -1;
        public const int k_ValueRead = 42;
        public const int k_ValueWrite = 420;

        public T GetAspect<T>(Entity entity) where T : struct, IAspect, IAspectCreate<T>
        {
            T aspect = default;
            return aspect.CreateAspect(entity, ref EmptySystem.CheckedStateRef);
        }

        public UseCase MakeUseCase(Entity entity, SystemKind systemKind, ContextKind contextKind, AccessKind accessKind, int valueInitial = k_ValueInit, int valueToRead = k_ValueRead, int valueToWrite = k_ValueWrite, int expectedOperationCount = 0)
            => new UseCase(entity, systemKind, contextKind, accessKind, valueInitial, valueToRead, valueToWrite, expectedOperationCount);

        /// <summary>
        /// Execute the proper test for the provided use-case.
        /// </summary>
        /// <typeparam name="TISystem"></typeparam>
        /// <typeparam name="TSystemBase"></typeparam>
        /// <param name="useCase">Use case to test</param>
        /// <param name="testISystem">ISystem to update within the test</param>
        /// <param name="testTSystemBase">SystemBase to update within the test</param>
        /// <param name="getWrittenValue">If your test system writes a value, provide a function to retreive the expected written value.</param>
        /// <returns>if the test pass or is ignored</returns>
        public unsafe bool TestUseCase<TISystem, TSystemBase>(UseCase useCase, Func<int> getWrittenValue = null)
            where TISystem : unmanaged, ISystem, IUseCaseTestSystem
            where TSystemBase : SystemBase, IUseCaseTestSystem
        {
            if (!useCase.IsSupported)
            {
                // Use case is not supported, ignore
                Assert.Ignore();
                return true;
            }

            SystemHandle testISystem = World.GetOrCreateSystem<TISystem>();
            TSystemBase testTSystemBase = World.GetOrCreateSystemManaged<TSystemBase>();

            ref TISystem testISystemStruct = ref World.Unmanaged.GetUnsafeSystemRef<TISystem>(testISystem);

            switch (useCase.SystemKind)
            {
                case SystemKind.ISystem:
                    {
                        testISystemStruct.UseCase = useCase;
                        try
                        {
                            testISystem.Update(World.Unmanaged);
                        }
                        catch (AssertionException)
                        {
                            throw;
                        }
                        catch (Exception exception)
                        {
                            Assert.DoesNotThrow(delegate { throw exception; });
                        }

                        if (testISystemStruct.UseCase.IsTested)
                        {
                            Assert.AreEqual(testISystemStruct.UseCase.ExpectedOperationCount, testISystemStruct.UseCase.TestData.OperationCount, $"Expecting exactly {testISystemStruct.UseCase.ExpectedOperationCount} operation(s) to be performed. (Make sure you increment TestData.OperationCount in your read/write operations)");
                            switch (useCase.AccessKind)
                            {
                                case AccessKind.ReadWriteAccess:
                                    if (getWrittenValue != null)
                                    {
                                        // ReadWrite tests will read from UseCase.TestData.Data and write the result in the aspect data
                                        Assert.AreEqual(useCase.ValueToWrite, getWrittenValue(), "The operation failed to write data to the aspect (using components or otherwise) using the value from TestData.Data");
                                    }
                                    break;
                            }
                            // test after structural change
                            MyAspectMiscTests.CreateEntity(m_Manager, false);
                            try
                            {
                                testISystem.Update(World.Unmanaged);
                            }
                            catch (Exception exception)
                            {
                                Assert.DoesNotThrow(delegate { throw exception; });
                            }
                            return true;
                        }
                        else if (testISystemStruct.UseCase.IsSupported)
                        {
                            Assert.Fail($"Use case {testISystemStruct.UseCase.SystemKind} {testISystemStruct.UseCase.ContextKind} {testISystemStruct.UseCase.AccessKind} was not tested. ");
                            return false;
                        }
                        // Use case is not supported, ignore
                        Assert.Ignore();
                        return true;
                    }
                case SystemKind.SystemBase:
                    {

                        testTSystemBase.UseCase = useCase;
                        try
                        {
                            testTSystemBase.Update();
                        }
                        catch(AssertionException)
                        {
                            throw;
                        }
                        catch(Exception exception)
                        {
                            Assert.DoesNotThrow(delegate { throw exception; });
                        }

                        if (testTSystemBase.UseCase.IsTested)
                        {
                            Assert.AreEqual(testISystemStruct.UseCase.ExpectedOperationCount, testISystemStruct.UseCase.TestData.OperationCount, $"Expecting exactly {testISystemStruct.UseCase.ExpectedOperationCount} operation(s) to be performed. (Make sure you call '++data.OperationCount' in your read/write operations)");
                            switch (useCase.AccessKind)
                            {
                                case AccessKind.ReadWriteAccess:
                                    if (getWrittenValue != null)
                                    {
                                        // ReadWrite tests will read from UseCase.TestData.Data and write the result in the aspect data
                                        Assert.AreEqual(useCase.ValueToWrite, getWrittenValue(), "The operation failed to write data to the aspect (using components or otherwise) using the value from TestData.Data");
                                    }
                                    break;
                            }
                            // test after structural change
                            MyAspectMiscTests.CreateEntity(m_Manager, false);

                            Assert.DoesNotThrow(delegate { testTSystemBase.Update(); }, "System update after a structural change must not yield any exceptions");
                            return true;
                        }
                        else if (testTSystemBase.UseCase.IsSupported)
                        {
                            Assert.Fail($"Use case {testTSystemBase.UseCase.SystemKind} {testTSystemBase.UseCase.ContextKind} {testTSystemBase.UseCase.AccessKind} was not tested. ");
                            return false;
                        }
                        // Use case is not supported, ignore
                        Assert.Ignore();
                        return true;
                    }
            }
            return false;
        }

        /// <summary>
        /// Use this system if a feature is not supported with SystemKind.ISystems
        /// </summary>
        public partial struct UnsupportedISystem : ISystem, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }

            public void OnUpdate(ref SystemState state)
            {
                UseCase.MarkNotSupported(SystemKind.ISystem, ContextKind.GetAspect, AccessKind.ReadWriteAccess);
                UseCase.MarkNotSupported(SystemKind.ISystem, ContextKind.Foreach, AccessKind.ReadWriteAccess);
            }
        }

        /// <summary>
        /// Use this system if a feature is not supported with SystemKind.SystemBase
        /// </summary>
        public partial class UnsupportedSystemBase : SystemBase, IUseCaseTestSystem
        {
            public UseCase UseCase;
            UseCase IUseCaseTestSystem.UseCase { get => UseCase; set => UseCase = value; }
            protected override void OnUpdate()
            {
                UseCase.MarkNotSupported(SystemKind.SystemBase, ContextKind.GetAspect, AccessKind.ReadWriteAccess);
                UseCase.MarkNotSupported(SystemKind.SystemBase, ContextKind.Foreach, AccessKind.ReadWriteAccess);
            }
        }
    }

}
