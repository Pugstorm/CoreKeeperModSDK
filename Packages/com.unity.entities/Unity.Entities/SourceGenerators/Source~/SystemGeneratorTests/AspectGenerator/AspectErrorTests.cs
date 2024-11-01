using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Unity.Entities.SourceGen.Aspect;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.Aspect.AspectGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class AspectErrorTests
{
    [TestMethod]
    public async Task SGA0001_MultipleFieldsOfSameFieldType()
    {
        const string source = @"
            using Unity.Entities;
            public readonly partial struct MyAspect : IAspect
            {
                public readonly RefRW<Unity.Entities.Tests.EcsTestData> Data;
                public readonly RefRW<Unity.Entities.Tests.EcsTestData> {|#0:Data1|};
            }";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0001)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGA0001_MultipleRefFieldsToSameIComponentDataType()
    {
        const string source = @"
            using Unity.Entities;
            public readonly partial struct MyAspect : IAspect
            {
                public readonly RefRO<Unity.Entities.Tests.EcsTestData> RO_Data;
                public readonly RefRW<Unity.Entities.Tests.EcsTestData> {|#0:RW_Data|};
            }";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0001)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGA0002_ImplementedIAspectCreateOfDifferentType()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Collections.LowLevel.Unsafe;
            public readonly partial struct MyStruct : IAspect { readonly RefRO<Unity.Entities.Tests.EcsTestData> data; }
            {|#0:public readonly partial struct MyAspect1 : IAspect, IAspectCreate<MyStruct>
            {
                public readonly RefRW<Unity.Entities.Tests.EcsTestData2> Data;
                public MyStruct CreateAspect(Entity entity, ref SystemState system) => default;
                public void AddComponentRequirementsTo(ref UnsafeList<ComponentType> all){}
                public void CompleteDependencyBeforeRO(ref SystemState state){}
                public void CompleteDependencyBeforeRW(ref SystemState state){}
            }|}";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0002)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGA0004_Empty()
    {
        const string source = @"
            using Unity.Entities;
            {|#0:public readonly partial struct MyAspect : IAspect
            {
            }|}";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0004)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGA0005_NotReadonly()
    {
        const string source = @"
            using Unity.Entities;
            {|#0:public partial struct MyAspect : IAspect
            {
                RefRO<Unity.Entities.Tests.EcsTestData> data;
            }|}";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0005)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGA0006_EntityFieldDuplicate()
    {
        const string source = @"
            using Unity.Entities;
            public readonly partial struct MyAspect : IAspect
            {
                readonly Entity e0;
                readonly Entity {|#0:e1|};
                readonly RefRO<Unity.Entities.Tests.EcsTestData> data;
            }";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0006)).WithLocation(0);
        var falsePositiveFixPlease = VerifyCS.CompilerError(nameof(AspectErrors.SGA0007)).WithLocation(0); // Todo: SGA0007 shouldn't throw if SGA0006 does.
        await VerifyCS.VerifySourceGeneratorAsync(source, expected, falsePositiveFixPlease);
    }

    [TestMethod]
    public async Task SGA0007_DataField()
    {
        const string source = @"
            using Unity.Entities;
            public readonly partial struct MyAspect : IAspect
            {
                public readonly int {|#0:i|} = 1;
                readonly RefRO<Unity.Entities.Tests.EcsTestData> data;
            }";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0007)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGA0009_GenericAspect_Not_Supported()
    {
        const string source = @"
            using Unity.Entities;

            {|#0:public readonly partial struct GenericAspect<T> : IAspect
            {
                public readonly RefRW<Unity.Entities.Tests.EcsTestData> Data;
            }|}";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0009)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGA0011_ReadOnlyRefRW_NotSupported()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;

            {|#1:public readonly partial struct TestAspect : IAspect
            {
                [Unity.Collections.ReadOnly]
                readonly Unity.Entities.RefRW<EcsTestData> {|#0:Data|};
            }|}";
        var expected = VerifyCS.CompilerError(nameof(AspectErrors.SGA0011)).WithLocation(0);
        var falsePositiveFixPlease1 = VerifyCS.CompilerError(nameof(AspectErrors.SGA0007)).WithLocation(0); // Todo: SGA0011 shouldn't cause other SGAXXXX to throw
        var falsePositiveFixPlease2 = VerifyCS.CompilerError(nameof(AspectErrors.SGA0004)).WithLocation(1); // Todo: SGA0011 shouldn't cause other SGAXXXX to throw
        await VerifyCS.VerifySourceGeneratorAsync(source, expected, falsePositiveFixPlease1, falsePositiveFixPlease2);
    }
}
