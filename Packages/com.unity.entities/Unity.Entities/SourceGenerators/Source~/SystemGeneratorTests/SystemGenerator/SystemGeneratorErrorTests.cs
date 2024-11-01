using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Unity.Entities.SourceGen.SystemGenerator;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators
{
    [TestClass]
    public class SystemGeneratorErrorTests
    {
        [TestMethod]
        public async Task DC0065_ClassBasedISystem()
        {
            const string src =
                @"using Unity.Entities;
                using Unity.Entities.Tests;
                using Unity.Collections;

                {|#0:partial class ClassBasedISystem : ISystem
                {
                    public void OnCreate(ref SystemState state) { }

                    public void OnUpdate(ref SystemState state) { }

                    public void OnDestroy(ref SystemState state) { }
                }|}";
            var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.DC0065)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(src, expected);
        }

        [TestMethod]
        public async Task PropertyWithNeedForSystemState()
        {
            const string src =
                @"using Unity.Entities;
                using Unity.Entities.Tests;

                partial struct SomeSystem : ISystem
                {
                    public EcsTestData SomeProperty => {|#0:SystemAPI.GetComponent<EcsTestData>(new Entity())|};

                    public void OnCreate(ref SystemState state) { }

                    public void OnUpdate(ref SystemState state) { }

                    public void OnDestroy(ref SystemState state) { }
                }";
            var expected = VerifyCS.CompilerError(nameof(SystemGeneratorErrors.SGSG0001)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(src, expected);
        }
    }
}
