using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

public class TestDeletePrimary : MonoBehaviour
{
    public int value;
    public bool delete;
}

public struct TestDeletePrimaryComponent : IComponentData
{
    public int value;
    public bool delete;
}

public class TestDeletePrimaryBaker : Baker<TestDeletePrimary>
{
    public override void Bake(TestDeletePrimary authoring)
    {
        // This test shouldn't require transform components
        var entity = GetEntity(TransformUsageFlags.None);

        AddComponent(entity, new TestDeletePrimaryComponent(){
            value = authoring.value,
            delete = authoring.delete
        });
    }
}

[WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
partial class DeleteComponentBakingSystem : SystemBase
{
    private EntityQuery query;

    protected override void OnCreate()
    {
        query = GetEntityQuery(typeof(TestDeletePrimaryComponent));
    }

    protected override void OnUpdate()
    {
        NativeList<Entity> toDelete = new NativeList<Entity>(Allocator.Temp);
        Entities.ForEach((Entity entity, in TestDeletePrimaryComponent auth) =>
        {
            if (auth.delete)
            {
                toDelete.Add(entity);
            }
        }).Run();

        if (toDelete.Length > 0)
        {
            EntityManager.DestroyEntity(toDelete.AsArray());
        }

        toDelete.Dispose();
    }
}
