using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using Unity.Entities.SourceGen.Common;
using Unity.Entities.SourceGen.SystemGenerator.Common;
using Unity.Entities.SourceGen.SystemGenerator.SystemAPI.QueryBuilder;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators
{
    [TestClass]
    public class SystemAPIQueryBuilderErrorTests
    {
        [TestMethod]
        public async Task SGQB001_MultipleWithOptionsInvocations()
        {
            const string source = @"
                using Unity.Entities;
                using Unity.Entities.Tests;
                partial class SomeSystem : SystemBase
                {
                    protected override void OnUpdate()
                    {
                        var query = {|#0:SystemAPI.QueryBuilder().WithAll<EcsTestData>().WithOptions(EntityQueryOptions.IncludePrefab).WithOptions(EntityQueryOptions.IncludeDisabledEntities).Build()|};
                        EntityManager.AddComponent<EcsTestTag>(query);
                    }
                }";
            var expected = VerifyCS.CompilerError(nameof(SystemApiQueryBuilderErrors.SGQB001)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
        }

        [TestMethod]
        public async Task SGQC005_WithGenericTypeArgument()
        {
            const string source = @"
            using Unity.Entities;
            using Unity.Entities.Tests;
            partial class SomeSystem<T> : SystemBase where T : struct, IComponentData
            {
                protected override void OnUpdate()
                {
                    var query = {|#0:SystemAPI.QueryBuilder().WithAll<T>().Build()|};
                    EntityManager.AddComponent<EcsTestTag>(query);
                }
            }";
            var expected = VerifyCS.CompilerError(nameof(QueryConstructionErrors.SGQC005)).WithLocation(0);
            await VerifyCS.VerifySourceGeneratorAsync(source, expected);
        }
    }
}
