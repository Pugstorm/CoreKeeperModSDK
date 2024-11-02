using System;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public struct BindingRegistryManualTestComponent : IComponentData
    {
        public float BindFloat;
        public int BindInt;
        public bool BindBool;
    }

    public class BindingRegistryManualTestAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(BindingRegistryManualTestComponent),
            nameof(BindingRegistryManualTestComponent.BindFloat))]
        public float FloatField = 10.0f;

        [RegisterBinding(typeof(BindingRegistryManualTestComponent),
            nameof(BindingRegistryManualTestComponent.BindInt))]
        public int IntField = 5;

        [RegisterBinding(typeof(BindingRegistryManualTestComponent),
            nameof(BindingRegistryManualTestComponent.BindBool))]
        public bool BoolField = true;

        class Baker : Baker<BindingRegistryManualTestAuthoring>
        {
            public override void Bake(BindingRegistryManualTestAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BindingRegistryManualTestComponent
                        {BindFloat = authoring.FloatField, BindInt = authoring.IntField, BindBool = authoring.BoolField});
            }
        }
    }

    public struct BindingRegistrySeparateFieldsComponent : IComponentData
    {
        public float2 BindFloat2;
    }

    public class BindingRegistrySeparateFieldsAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(BindingRegistrySeparateFieldsComponent), nameof(BindingRegistrySeparateFieldsComponent.BindFloat2) + ".x")]
        public float FloatField1 = 5.0f;
        [RegisterBinding(typeof(BindingRegistrySeparateFieldsComponent), nameof(BindingRegistrySeparateFieldsComponent.BindFloat2) + ".y")]
        public float FloatField2 = 0.0f;

        class Baker : Baker<BindingRegistrySeparateFieldsAuthoring>
        {
            public override void Bake(BindingRegistrySeparateFieldsAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BindingRegistrySeparateFieldsComponent
                    { BindFloat2 = new float2(authoring.FloatField1, authoring.FloatField2) });
            }
        }
    }

    public struct BindingRegistryColorComponent : IComponentData
    {
        public float4 BindColor;
    }

    public class BindingRegistryColorAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(BindingRegistryColorComponent), nameof(BindingRegistryColorComponent.BindColor))]
        public UnityEngine.Color Color = Color.yellow;

        class Baker : Baker<BindingRegistryColorAuthoring>
        {
            public override void Bake(BindingRegistryColorAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BindingRegistryColorComponent
                {
                    BindColor = new float4(authoring.Color.r, authoring.Color.g, authoring.Color.b, authoring.Color.a)
                });
            }
        }
    }

    public struct BindingRegistryVectorComponent : IComponentData
    {
        public float4 BindFloat4;
    }

    public class BindingRegistryVectorAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(BindingRegistryVectorComponent), nameof(BindingRegistryVectorComponent.BindFloat4))]
        public UnityEngine.Vector4 Vector4 = new Vector4(0f, 1f, 2f, 3f);

        class Baker : Baker<BindingRegistryVectorAuthoring>
        {
            public override void Bake(BindingRegistryVectorAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BindingRegistryVectorComponent
                {
                    BindFloat4 = new float4(authoring.Vector4.x, authoring.Vector4.y, authoring.Vector4.z, authoring.Vector4.w)
                });
            }
        }
    }

    public struct BindingRegistryNestedFieldsComponent : IComponentData
    {
        public float4 BindFloat4;
    }

    public class BindingRegistryNestedFieldsAuthoring : MonoBehaviour
    {
        [Serializable]
        public struct NestedStruct
        {
            public float Float;
            public float3 Float3;
        }

        [RegisterBinding(nameof(NestedStruct.Float), typeof(BindingRegistryNestedFieldsComponent), nameof(BindingRegistryNestedFieldsComponent.BindFloat4) + ".x")]
        [RegisterBinding(nameof(NestedStruct.Float3) + ".x", typeof(BindingRegistryNestedFieldsComponent), nameof(BindingRegistryNestedFieldsComponent.BindFloat4) + ".y")]
        [RegisterBinding(nameof(NestedStruct.Float3) + ".y", typeof(BindingRegistryNestedFieldsComponent), nameof(BindingRegistryNestedFieldsComponent.BindFloat4) + ".z")]
        [RegisterBinding(nameof(NestedStruct.Float3) + ".z", typeof(BindingRegistryNestedFieldsComponent), nameof(BindingRegistryNestedFieldsComponent.BindFloat4) + ".w")]
        public NestedStruct NestedData;

        class Baker : Baker<BindingRegistryNestedFieldsAuthoring>
        {
            public override void Bake(BindingRegistryNestedFieldsAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BindingRegistryNestedFieldsComponent
                {
                    BindFloat4 = new float4(authoring.NestedData.Float, authoring.NestedData.Float3)
                });
            }
        }
    }


}
