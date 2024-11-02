using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using VerifyCS =
    Unity.Entities.SourceGenerators.Test.CSharpSourceGeneratorVerifier<
        Unity.Entities.SourceGen.SystemGenerator.SystemGenerator>;

namespace Unity.Entities.SourceGenerators;

[TestClass]
public class SystemAPIVerifyTests
{
    [TestMethod]
    public async Task SystemMethodWithComponentAccessInvocation()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using static Unity.Entities.SystemAPI;

public struct RotationSpeed : IComponentData
{
    public float RadiansPerSecond;
}

[BurstCompile]
public partial struct RotationSpeedSystemForEachISystem : ISystem
{
    public void OnCreate(ref SystemState state) {}
    public void OnDestroy(ref SystemState state) {}

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (rotationSpeed, entity) in Query<RefRO<RotationSpeed>>().WithEntityAccess())
        {
            var rotation = GetComponent<EcsTestData>(entity);
        }
    }
}
";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(SystemMethodWithComponentAccessInvocation), "Test0__System_19875963020.g.cs");
    }

    [TestMethod]
    public async Task SystemMethodWithBufferAccessInvocation()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using static Unity.Entities.SystemAPI;

public struct BufferData : IBufferElementData
{
    public float RadiansPerSecond;
}

[BurstCompile]
public partial struct RotationSpeedSystemForEachISystem : ISystem
{
    public void OnCreate(ref SystemState state) {}
    public void OnDestroy(ref SystemState state) {}

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        foreach (var (data, entity) in Query<RefRO<EcsTestData>>().WithEntityAccess())
        {
            var lookup_rw = GetBufferLookup<BufferData>(false);
            var lookup_ro = GetBufferLookup<BufferData>(true);

            if (HasBuffer<BufferData>(entity))
            {
                var rotation = GetBuffer<BufferData>(entity);
            }
        }
    }
}
";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(SystemMethodWithBufferAccessInvocation), "Test0__System_19875963020.g.cs");
    }

    [TestMethod]
    public async Task SystemMethodWithStorageInfoInvocation()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using static Unity.Entities.SystemAPI;

public struct BufferData : IBufferElementData
{
    public float RadiansPerSecond;
}

[BurstCompile]
public partial struct RotationSpeedSystemForEachISystem : ISystem
{
    public void OnCreate(ref SystemState state) {}
    public void OnDestroy(ref SystemState state) {}

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        var storageInfo = GetEntityStorageInfoLookup();
        foreach (var (data, entity) in Query<RefRO<EcsTestData>>().WithEntityAccess())
        {
            var check1 = Exists(entity);
            var check2 = storageInfo.Exists(entity);
        }
    }
}
";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(SystemMethodWithStorageInfoInvocation), "Test0__System_19875963020.g.cs");
    }

    [TestMethod]
    public async Task SystemMethodWithAspectInvocation()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using static Unity.Entities.SystemAPI;

[BurstCompile]
public partial struct RotationSpeedSystemForEachISystem : ISystem
{
    public void OnCreate(ref SystemState state) {}
    public void OnDestroy(ref SystemState state) {}

    [BurstCompile]
    public void OnUpdate(ref SystemState state)
    {
        Entity entity = default;
        var testAspectRO = GetAspect<EcsTestAspect>(entity);
    }
}
";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(SystemMethodWithAspectInvocation), "Test0__System_19875963020.g.cs");
    }

    [TestMethod]
    public async Task SystemMethodWithManagedComponent()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;
using static Unity.Entities.SystemAPI;
public partial struct SomeSystem : ISystem {
    public void OnCreate(ref SystemState state){}
    public void OnDestroy(ref SystemState state){}
    public void OnUpdate(ref SystemState state){
        var e = state.EntityManager.CreateEntity();
        state.EntityManager.AddComponentData(e, new EcsTestManagedComponent{value = ""cake""});
        var comp = SystemAPI.ManagedAPI.GetSingleton<EcsTestManagedComponent>().value;
    }
}
";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(SystemMethodWithManagedComponent), "Test0__System_19875963020.g.cs");
    }

    [TestMethod]
    public async Task SystemAPIInPartialMethod()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct PartialMethodSystem
{
    partial void CustomOnUpdate(ref SystemState state)
    {
        var tickSingleton2 = SystemAPI.GetSingleton<EcsTestData>();
    }
}

public unsafe partial struct PartialMethodSystem : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        CustomOnUpdate(ref state);
    }

    partial void CustomOnUpdate(ref SystemState state);
}
";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(SystemAPIInPartialMethod), "Test0__System_19875963020.g.cs");
    }

    [TestMethod]
    public async Task NestedSystemAPIInvocation_Example1()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct NestedSystemAPIInvocation_Example1 : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        ToggleEnabled(default(Entity), ref state);
    }

    void ToggleEnabled(Entity entity, ref SystemState state)
    {
        SystemAPI.SetComponentEnabled<EcsTestDataEnableable>(entity, !SystemAPI.IsComponentEnabled<EcsTestDataEnableable>(entity));
    }
}";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(NestedSystemAPIInvocation_Example1), "Test0__System_19875963020.g.cs");
    }
    [TestMethod]
    public async Task NestedSystemAPIInvocation_Example2()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct NestedSystemAPIInvocation_Example2 : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var foo = SystemAPI.ManagedAPI.GetComponent<EcsTestManagedComponent>(SystemAPI.GetSingletonEntity<EcsTestData>());
    }
}";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(NestedSystemAPIInvocation_Example2), "Test0__System_19875963020.g.cs");
    }

    [TestMethod]
    public async Task NestedGetSingletonEntity()
    {
        const string testSource = @"
using Unity.Burst;
using Unity.Entities;
using Unity.Entities.Tests;

public unsafe partial struct NestedGetSingletonEntity : ISystem
{
    public void OnUpdate(ref SystemState state)
    {
        var entityQuery = new EntityQuery();
        var foo = SystemAPI.GetComponent<EcsTestData>(entityQuery.GetSingletonEntity());
    }
}";

        await VerifyCS.VerifySourceGeneratorAsync(testSource, nameof(NestedGetSingletonEntity), "Test0__System_19875963020.g.cs");
    }
}



