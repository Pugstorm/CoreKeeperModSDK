using Unity.Mathematics;

namespace Unity.Entities.Tests
{
    public struct BindingRegistryBoolComponent : IComponentData
    {
        public bool Bool1;
        public bool2 Bool2;
        public bool3 Bool3;
        public bool4 Bool4;
    }

    public class BindingRegistryBoolComponentAuthoring : UnityEngine.MonoBehaviour
    {
        [Unity.Entities.RegisterBinding(typeof(BindingRegistryBoolComponent), nameof(BindingRegistryBoolComponent.Bool1))]
        public bool Bool1;
        [Unity.Entities.RegisterBinding(typeof(BindingRegistryBoolComponent), nameof(BindingRegistryBoolComponent.Bool2))]
        public Unity.Mathematics.bool2 Bool2;
        [Unity.Entities.RegisterBinding(typeof(BindingRegistryBoolComponent), nameof(BindingRegistryBoolComponent.Bool3))]
        public Unity.Mathematics.bool3 Bool3;
        [Unity.Entities.RegisterBinding(typeof(BindingRegistryBoolComponent), nameof(BindingRegistryBoolComponent.Bool4))]
        public Unity.Mathematics.bool4 Bool4;

        class BindingRegistryBoolComponentBaker : Unity.Entities.Baker<BindingRegistryBoolComponentAuthoring>
        {
            public override void Bake(BindingRegistryBoolComponentAuthoring authoring)
            {
                Unity.Entities.Tests.BindingRegistryBoolComponent component = default(Unity.Entities.Tests.BindingRegistryBoolComponent);
                component.Bool1 = authoring.Bool1;
                component.Bool2 = authoring.Bool2;
                component.Bool3 = authoring.Bool3;
                component.Bool4 = authoring.Bool4;
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, component);
            }
        }
    }
}
