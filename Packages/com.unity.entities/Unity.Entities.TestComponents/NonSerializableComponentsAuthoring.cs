using System;
using UnityEngine;

namespace Unity.Entities.TestComponents
{

    public struct EcsTestDataPointer : IComponentData { public IntPtr value; }

    public struct EcsTestDataEntityShared : ISharedComponentData { public Entity value; }

    public class NonSerializableComponentsAuthoring : MonoBehaviour { }

    public class NonSerializableComponentsBaker : Baker<NonSerializableComponentsAuthoring>
    {
        public override void Bake(NonSerializableComponentsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<EcsTestDataPointer>(entity);
        }
    }

    [DisableAutoCreation]
    public class NonSerializableSharedComponentsBaker : Baker<NonSerializableComponentsAuthoring>
    {
        public override void Bake(NonSerializableComponentsAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<EcsTestDataEntityShared>(entity);
        }
    }

    [DisableAutoCreation]
    public class NonSerializableComponentsAdditionalEntityBaker : Baker<NonSerializableComponentsAuthoring>
    {
        public override void Bake(NonSerializableComponentsAuthoring authoring)
        {
            var additionalEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, "B");
            AddComponent<EcsTestDataPointer>(additionalEntity);
        }
    }

    [DisableAutoCreation]
    public class NonSerializableSharedComponentAdditionalEntityBaker : Baker<NonSerializableComponentsAuthoring>
    {
        public override void Bake(NonSerializableComponentsAuthoring authoring)
        {
            var additionalEntity = CreateAdditionalEntity(TransformUsageFlags.None, false, "B");
            AddComponent<EcsTestDataEntityShared>(additionalEntity);
        }
    }
}
