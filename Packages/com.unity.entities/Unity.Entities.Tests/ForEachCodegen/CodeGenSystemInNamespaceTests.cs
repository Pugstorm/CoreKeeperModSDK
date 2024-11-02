using System;
using NUnit.Framework;
using Unity.Entities;
using Unity.Entities.Tests;

namespace MyNamespace.System
{
    public partial class EntitiesForEachSystemInNamespaceEndingInSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Entities.ForEach((in EcsTestData c) => { }).Run();
        }
    }

    public partial struct TestJob : IJobEntity
    {
        public void Execute(Entity entity) { }
    }

    public partial class JobWithCodeSystemInNamespaceEndingInSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            Job.WithCode(() => { }).Run();
        }
    }

    public partial class IJobEntitySystemInNamespaceEndingInSystem : SystemBase
    {
        protected override void OnUpdate()
        {
            new TestJob().Run();
        }
    }

    readonly partial struct AspectInNamespaceEndingInSystem : IAspect
    {
        public readonly RefRW<EcsTestData> TestComponent;
    }

    public partial struct IdiomaticForeachComponentSystemInNamespaceEndingInSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var data in SystemAPI.Query<RefRO<EcsTestData>, RefRW<EcsTestData2>>())
            {
            }
        }
    }

    public partial struct IdiomaticForeachAspectSystemInNamespaceEndingInSystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var data in SystemAPI.Query<AspectInNamespaceEndingInSystem>())
            {
            }
        }
    }
}

namespace Unity.Entities.CodeGen.Tests
{
    public partial class CodeGenSystemInNamespaceTests : ECSTestsFixture
    {
        static object[] Source =
        {
            new TestCaseData(typeof(MyNamespace.System.EntitiesForEachSystemInNamespaceEndingInSystem)),
            new TestCaseData(typeof(MyNamespace.System.JobWithCodeSystemInNamespaceEndingInSystem)),
            new TestCaseData(typeof(MyNamespace.System.IJobEntitySystemInNamespaceEndingInSystem)),
            new TestCaseData(typeof(MyNamespace.System.IdiomaticForeachComponentSystemInNamespaceEndingInSystem)),
            new TestCaseData(typeof(MyNamespace.System.IdiomaticForeachAspectSystemInNamespaceEndingInSystem))
        };

        [TestCaseSource(nameof(Source))]
        public void CodeGenNamespaceTests_GenerateNoErrors(Type type)
        {
            Assert.DoesNotThrow(() => Create(type).Update());
        }

        World Create(Type type)
        {
            if (type.IsClass && World.GetOrCreateSystemManaged(type) is SystemBase)
                return World;

            if (type.IsValueType &&
                World.GetOrCreateSystem(type) != default)
                return World;

            throw new ArgumentException($"{type} system cannot be created.");
        }
    }
}
