using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.Aspect.AspectGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class AspectIntegrationTests
{
    [TestMethod]
    public async Task AspectSimple()
    {
        var testSource = @"
            using Unity.Entities;
            using Unity.Collections;
            using Unity.Entities.Tests;

            // Test: Aspect generation must work when the aspect is declared in global scope
            public readonly partial struct AspectSimple : IAspect
            {
                public readonly RefRW<EcsTestData> Data;
            }";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(AspectSimple), "Test0__Aspect_19875963020.g.cs");
    }

    [TestMethod]
    public async Task AspectComplex()
    {
        var testSource = @"
            using Unity.Entities;
            using Unity.Collections;
            using Unity.Entities.Tests;

            public readonly partial struct AspectSimple : IAspect
            {
                public readonly RefRW<EcsTestData> Data;
            }

            namespace AspectTests
            {
                // Aspect generation must work when the aspect is within a namespace
                public readonly partial struct Aspect2 : IAspect
                {
                    // Test: component type should be correctly without any qualifiers
                    public readonly RefRW<EcsTestData> Data;

                    // Test: component type should be correctly handled with qualifiers
                    public readonly RefRW<Unity.Entities.Tests.EcsTestData2> Data2;

                    // Test: component type should be correctly handled with the 'global' qualifier
                    public readonly RefRW<global::Unity.Entities.Tests.EcsTestData3> Data3;

                    // Test: const data field must *not* be initialized in any generated constructors
                    public const int constI = 10;

                    // Test: RefRO fields must be read-only in the constructed entity query
                    public readonly RefRO<EcsTestData4> DataRO;

                    // Test: RefRO fields must be read-only in the constructed entity query
                    [Optional]
                    public readonly RefRW<EcsTestData5> DataOptional;

                    public readonly DynamicBuffer<EcsIntElement> DynamicBuffer;

                    [ReadOnly] readonly AspectSimple NestedAspectSimple;

                    // Test: Entity field
                    public readonly Entity Self;

                    // Test: EnabledRef
                    public readonly EnabledRefRO<EcsTestDataEnableable> EcsTestDataEnableable;

                    // Test: Shared Components
                    public readonly EcsTestSharedComp EcsTestSharedComp;
                }

                public readonly partial struct AspectNestedAliasing : IAspect
                {
                    // Nest Aspect2
                    public readonly Aspect2 Aspect2;

                    // Alias all fields of Aspect2, copy paste follows:

                    // Test: component type should be correctly without any qualifiers
                    public readonly RefRW<EcsTestData> Data;

                    // Test: component type should be correctly handled with qualifiers
                    public readonly RefRW<Unity.Entities.Tests.EcsTestData2> Data2;

                    // Test: component type should be correctly handled with the 'global' qualifier
                    public readonly RefRW<global::Unity.Entities.Tests.EcsTestData3> Data3;

                    // Test: const data field must *not* be initialized in any generated constructors
                    public const int constI = 10;

                    // Test: RefRO fields must be read-only in the constructed entity query
                    public readonly RefRO<EcsTestData4> DataRO;

                    // Test: RefRO fields must be read-only in the constructed entity query
                    [Optional]
                    public readonly RefRW<EcsTestData5> DataOptional;

                    public readonly DynamicBuffer<EcsIntElement> DynamicBuffer;

                    [ReadOnly] readonly AspectSimple NestedAspectSimple;

                    // Test: Entity field
                    public readonly Entity Self;

                    // Test: EnabledRef
                    public readonly EnabledRefRO<EcsTestDataEnableable> EcsTestDataEnableable;

                    // Test: Shared Components
                    public readonly EcsTestSharedComp EcsTestSharedComp;
                }

                // Test: the aspect generator must support multiple partial declaration of the same aspect.
                public readonly partial struct Aspect2 : global::Unity.Entities.IAspect
                {
                    public int ReadSum()
                    {
                        var v = Data.ValueRO.value +
                            Data2.ValueRO.value0 +
                            Data3.ValueRO.value0 +
                            DataRO.ValueRO.value0;

                        if (DataOptional.IsValid)
                        {
                            v += DataOptional.ValueRO.value0;
                        }
                        return v;
                    }
                    public void WriteAll(int v)
                    {
                        Data.ValueRW.value = v;
                        Data2.ValueRW.value0 = v;
                        Data3.ValueRW.value0 = v;
                        if (DataOptional.IsValid)
                        {
                            DataOptional.ValueRW.value0 = v;
                        }
                    }
                }

                // Test: an aspect declared with the [DisableGeneration] attribute must not be generated.
                [DisableGeneration]
                public partial struct AspectDisableGeneration : IAspect
                {
                    public AspectDisableGeneration CreateAspect(Entity entity, ref SystemState system, bool isReadOnly)
                    {
                        return default;
                    }
                    public RefRW<EcsTestData> Data;
                }
            }";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(AspectComplex), "Test0__Aspect_19875963020.g.cs");
    }

    [TestMethod]
    public async Task AspectEFE()
    {
        var testSource = @"
            using Unity.Entities;
            using Unity.Collections;
            using Unity.Entities.Tests;

            public readonly partial struct MyAspectEFE : IAspect
            {
                public readonly RefRW<Unity.Entities.Tests.EcsTestData> Data;
            }
            public readonly partial struct MyAspectEFE2 : IAspect
            {
                public readonly RefRW<Unity.Entities.Tests.EcsTestData2> Data;
            }
            public partial class AspectTestEFESystem : SystemBase
            {
                protected override void OnUpdate()
                {

                    int count = 0;
                    Entities.ForEach((MyAspectEFE myAspect) => { ++count; }).Run();
                    Entities.ForEach((MyAspectEFE myAspect, MyAspectEFE2 myAspect2) => { ++count; }).Run();
                    Entities.ForEach((Entity e, in EcsTestData data) =>
                    {
                        var a = SystemAPI.GetAspect<MyAspectEFE>(e);
                        ++count;
                    }).Run();
                }
            }";
        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(AspectEFE), "Test0__Aspect_19875963020.g.cs");
    }
    
    [TestMethod]
    public async Task AspectTypeShadowing()
    {
        var testSource = @"
            namespace Test
            {
                namespace Unity.Entities
                {
                    public struct ComponentType { }
                    public struct EntityManager { }
                    public struct Entity { }
                    public struct ComponentLookup { }
                    public struct ComponentTypeHandle { }
                    public struct Archetype { }
                    public struct ArchetypeChunk { }
                    public struct SystemState { }
                    public interface IBufferElement { }
                    public interface IComponentData { }
                    public interface ISystem { }
                    public interface IAspect { }
                }
                public struct ComponentType { }
                public struct EntityManager { }
                public struct Entity { }
                public struct ComponentLookup { }
                public struct ComponentTypeHandle { }
                public struct Archetype { }
                public struct ArchetypeChunk { }
                public struct SystemState { }
                public interface IBufferElement { }
                public interface IComponentData { }
                public interface ISystem { }
                public interface IAspect { }

                public readonly partial struct MyAspect : global::Unity.Entities.IAspect
                {
                    public readonly global::Unity.Entities.RefRW<global::Unity.Entities.Tests.EcsTestData> EcsTestData;
                }
            }";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(AspectTypeShadowing), "Test0__Aspect_19875963020.g.cs");
    }

    [TestMethod]
    public async Task AspectUsing()
    {
        var testSource = @"
            namespace Test
            {
                using global::Unity.Entities;
                public readonly partial struct MyAspect : IAspect
                {
                    public readonly RefRW<global::Unity.Entities.Tests.EcsTestData> EcsTestData;
                }
            }";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(AspectUsing), "Test0__Aspect_19875963020.g.cs");
    }

    [TestMethod]
    public async Task AspectNonAspect()
    {
        var testSource = @"
            namespace Test
            {
                using global::Unity.Entities;
                public interface IAspect { }
                public readonly partial struct MyAspect : IAspect
                {
                    public readonly RefRW<global::Unity.Entities.Tests.EcsTestData> EcsTestData;
                    public struct TypeHandle { }
                }
            }";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(AspectNonAspect));
    }

}
