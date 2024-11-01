using System;

namespace Unity.Entities.Tests
{
#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public class CodeGenManagedTestComponent : IComponentData
    {
        public Entity[] Entities;
        public string String;
    }

    [UnityEngine.DisallowMultipleComponent]
    public class CodeGenManagedTestComponentAuthoring : UnityEngine.MonoBehaviour
    {
        public UnityEngine.GameObject[] Entities;
        public string String;

        class CodeGenManagedTestComponentBaker : Unity.Entities.Baker<CodeGenManagedTestComponentAuthoring>
        {
            public override void Bake(CodeGenManagedTestComponentAuthoring authoring)
            {
                Unity.Entities.Tests.CodeGenManagedTestComponent component = new Unity.Entities.Tests.CodeGenManagedTestComponent();
                component.Entities = new Entity[authoring.Entities.Length];
                for (var i = 0; i < component.Entities.Length; ++i)
                    component.Entities[i] = GetEntity(authoring.Entities[i], TransformUsageFlags.None);
                component.String = authoring.String;
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponentObject(entity, component);
            }
        }
    }
#endif
}
