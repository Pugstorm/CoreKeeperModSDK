using Unity.Mathematics;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public struct BindingRegistryIntComponent : IComponentData
    {
        public int Int1;
        public int2 Int2;
        public int3 Int3;
        public int4 Int4;
    }

    public class BindingRegistryIntComponentAuthoring : MonoBehaviour
    {
        [RegisterBinding(typeof(BindingRegistryIntComponent), nameof(BindingRegistryIntComponent.Int1))]
        public int Int1;
        [RegisterBinding(typeof(BindingRegistryIntComponent), nameof(BindingRegistryIntComponent.Int2))]
        public int2 Int2;
        [RegisterBinding(typeof(BindingRegistryIntComponent), nameof(BindingRegistryIntComponent.Int3))]
        public int3 Int3;
        [RegisterBinding(typeof(BindingRegistryIntComponent), nameof(BindingRegistryIntComponent.Int4))]
        public int4 Int4;

        class Baker : Baker<BindingRegistryIntComponentAuthoring>
        {
            public override void Bake(BindingRegistryIntComponentAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new BindingRegistryIntComponent
                {
                    Int1 = authoring.Int1,
                    Int2 = authoring.Int2,
                    Int3 = authoring.Int3,
                    Int4 = authoring.Int4,
                });
            }
        }
    }
}
