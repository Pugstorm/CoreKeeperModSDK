using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpIncrementalGeneratorVerifier<
        Unity.Entities.SourceGen.JobEntityGenerator.JobEntityGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class JobEntityVerify
{
    [TestMethod]
    public async Task JobEntityBasic()
    {
        const string source = @"
            using Unity.Entities;

            public partial struct MyJob : IJobEntity
            {
                void Execute(){}
            }";

        await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntityBasic), "Test0__JobEntity_19875963023.g.cs");
    }

    [TestMethod]
    public async Task JobEntityIfDirective()
    {
        const string source = @"
            using Unity.Entities;

            #if !UNITY_EDITOR
            public partial struct MyJob : IJobEntity
            {
                void Execute(){}
            }
            #endif";

        await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntityIfDirective), "Test0__JobEntity_19875963024.g.cs");
    }

    [TestMethod]
    public async Task JobEntityIfDirectiveInUsing()
    {
        const string source = @"
            #if !UNITY_EDITOR
            using Unity.Entities;

            public partial struct MyJob : IJobEntity
            {
                void Execute(){}
            }
            #endif";

        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task JobEntityRemembersEarlyEscape()
    {
        const string source = @"
            using Unity.Entities;
            #if !UNITY_EDITOR
            #endif

            public partial struct MyJob : IJobEntity
            {
                void Execute(){}
            }";

        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task JobEntityRemembersEarlyNestedEscape()
    {
        const string source = @"
            using Unity.Entities;
            #if !UNITY_EDITOR
            class MyClass {}
            #endif

            public partial struct MyJob : IJobEntity
            {
                void Execute(){}
            }";

        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task JobEntityClassEscape()
    {
        const string source = @"
            using Unity.Entities;
            partial class MyClass {
                #region Example
                #endregion
                #region ManagedComponents
                #if UNITY_DISABLE_MANAGED_COMPONENTS
                    void MyFunc1(){}
                    void MyFunc2(){}
                #endif
                public partial struct MyJob : IJobEntity
                {
                    void Execute(){}
                }
                #endregion
            }";

        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task JobEntityIfDefWithAttribute()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            [WithAll(typeof(EcsTestData))]
            partial struct WithFilterButNotQueryStaticJob : IJobEntity { public void Execute(Entity _) {} }
        ";

        await VerifyCS.VerifySourceGeneratorAsync(source);
    }

    [TestMethod]
    public async Task JobEntityDisabledChangeFilter()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            [WithDisabled(typeof(EcsTestDataEnableable))]
            [WithChangeFilter(typeof(EcsTestDataEnableable))]
            partial struct MyJob : IJobEntity { public void Execute(Entity _) {} }
        ";

        await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntityDisabledChangeFilter), "Test0__JobEntity_19875963024.g.cs");
    }

    [TestMethod]
    public async Task JobEntity_WithDisabled_EnabledRefRW_RefRW_QueryIsCorrect()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            [WithDisabled(typeof(EcsTestDataEnableable))]
            partial struct WithDisabled_EnabledRefRW_RefRWJob : IJobEntity
            {
                public void Execute(EnabledRefRW<EcsTestDataEnableable> t1, RefRW<EcsTestDataEnableable> t2)
                {
                }
            }
        ";

        await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntity_WithDisabled_EnabledRefRW_RefRW_QueryIsCorrect), "Test0__JobEntity_19875963024.g.cs");
    }

    [TestMethod]
    public async Task JobEntity_WithDisabled_EnabledRefRW_RefRO_QueryIsCorrect()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            [WithDisabled(typeof(EcsTestDataEnableable))]
            partial struct WithDisabled_EnabledRefRW_RefROJob : IJobEntity
            {
                public void Execute(EnabledRefRW<EcsTestDataEnableable> t1, RefRO<EcsTestDataEnableable> t2)
                {
                }
            }
        ";

        await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntity_WithDisabled_EnabledRefRW_RefRO_QueryIsCorrect), "Test0__JobEntity_19875963024.g.cs");
    }

    [TestMethod]
    public async Task JobEntity_WithDisabled_EnabledRefRW_QueryIsCorrect()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            [WithDisabled(typeof(EcsTestDataEnableable))]
            partial struct WithDisabled_EnabledRefRWJob : IJobEntity
            {
                public void Execute(EnabledRefRW<EcsTestDataEnableable> t1)
                {
                }
            }
        ";

        await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntity_WithDisabled_EnabledRefRW_QueryIsCorrect), "Test0__JobEntity_19875963024.g.cs");
    }

     [TestMethod]
     public async Task JobEntity_WithDisabled_RefRW_QueryIsCorrect()
     {
         const string source = @"
             using Unity.Entities;
             using Unity.Entities.Tests;

             [WithDisabled(typeof(EcsTestDataEnableable))]
             partial struct WithDisabled_RefRWJob : IJobEntity
             {
                 public void Execute(RefRW<EcsTestDataEnableable> t1)
                 {
                 }
             }
         ";

         await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntity_WithDisabled_RefRW_QueryIsCorrect), "Test0__JobEntity_19875963024.g.cs");
     }

     [TestMethod]
     public async Task JobEntity_WithDisabled_EnabledRefRO_RefRW_QueryIsCorrect()
     {
         const string source = @"
             using Unity.Entities;
             using Unity.Entities.Tests;

             [WithDisabled(typeof(EcsTestDataEnableable))]
             partial struct WithDisabled_EnabledRefRO_RefRWJob : IJobEntity
             {
                 public void Execute(EnabledRefRO<EcsTestDataEnableable> t1, RefRW<EcsTestDataEnableable> t2)
                 {
                 }
             }
         ";

         await VerifyCS.VerifySourceGeneratorAsync(source, nameof(JobEntity_WithDisabled_EnabledRefRO_RefRW_QueryIsCorrect), "Test0__JobEntity_19875963024.g.cs");
     }
}
