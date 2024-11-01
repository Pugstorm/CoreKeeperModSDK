using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Unity.Entities.SourceGen.SystemGenerator.SystemAPI;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class SystemAPIErrorTests
{
    [TestMethod]
    public async Task SGSA0001()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using static Unity.Entities.SystemAPI;

            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state) {
                    Idk<EcsTestData>();
                }

                public void Idk<T>() where T:struct,IComponentData{
                    var hadComp = {|#0:HasComponent<T>(default)|};
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemApiContextErrors.SGSA0001)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }

    [TestMethod]
    public async Task SGSA0002()
    {
        const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            using static Unity.Entities.SystemAPI;

            partial struct SomeSystem : ISystem {
                public void OnUpdate(ref SystemState state) {
                    var ro = false;
                    var lookup = {|#0:GetComponentLookup<EcsTestData>(ro)|};
                }
            }";
        var expected = VerifyCS.CompilerError(nameof(SystemApiContextErrors.SGSA0002)).WithLocation(0);
        await VerifyCS.VerifySourceGeneratorAsync(source, expected);
    }
}
