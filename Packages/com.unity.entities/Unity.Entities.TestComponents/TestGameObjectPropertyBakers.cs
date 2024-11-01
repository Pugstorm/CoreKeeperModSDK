using Unity.Entities;
using Unity.Entities.Tests;
using UnityEngine;

public struct TestNameComponent : IComponentData
{
    public int value;
}

[DisableAutoCreation]
public class TestNameBaker : Baker<TestNameAuthoring>
{
    public override void Bake(TestNameAuthoring authoring)
    {
        var name = GetName(authoring);
        // This test shouldn't require transform components
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new TestNameComponent
        {
            value = name.GetHashCode()
        });
    }
}

public struct TestLayerComponent : IComponentData
{
    public int value;
}

[DisableAutoCreation]
public class TestLayerBaker : Baker<TestLayerAuthoring>
{
    public override void Bake(TestLayerAuthoring authoring)
    {
        int layer = GetLayer(authoring);
        // This test shouldn't require transform components
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new TestLayerComponent
        {
            value = layer
        });
    }
}

public struct TestTagComponent : IComponentData
{
    public int value;
}

[DisableAutoCreation]
public class TestTagBaker : Baker<TestTagAuthoring>
{
    public override void Bake(TestTagAuthoring authoring)
    {
        var tag = GetTag(authoring);
        // This test shouldn't require transform components
        var entity = GetEntity(TransformUsageFlags.None);
        AddComponent(entity, new TestTagComponent
        {
            value = tag.GetHashCode()
        });
    }
}

[DisableAutoCreation]
public class TestReferenceBaker : Baker<MockDataAuthoring>
{
    public override void Bake(MockDataAuthoring authoring)
    {
        DependsOn(authoring.gameObject);
    }
}

[DisableAutoCreation]
public class TestIsStaticBaker : Baker<MockDataAuthoring>
{
    public override void Bake(MockDataAuthoring authoring)
    {
        IsStatic(authoring);
    }
}
