using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Unity.Entities.SourceGen.SystemGenerator;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class EntityQueryErrorTests
{
    [TestMethod]
    public async Task DC0062_EntityQueryInvalidMethod()
    {
        const string source = @"
            using Unity.Entities;

            public partial class MySystem : SystemBase
            {
                struct Foo : IComponentData {}

                void MyTest()
                {
                    Entities.WithAll<Foo>().WithName(""Bar"").DestroyEntity();
                }

                protected override void OnUpdate() {}
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0062)).WithSpan(10, 21, 10, 29);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }
}
