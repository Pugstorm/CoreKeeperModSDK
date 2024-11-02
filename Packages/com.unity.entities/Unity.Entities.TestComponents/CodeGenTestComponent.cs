using System;

namespace Unity.Entities.Tests
{
    public struct CodeGenTestComponent : IComponentData
    {
        public Entity Entity;
        public float Float;
        public int Int;
        public bool Bool;
        public WorldFlags Enum;
        public char Char;
    }

    [UnityEngine.DisallowMultipleComponent]
    public class CodeGenTestComponentAuthoring : UnityEngine.MonoBehaviour
    {
        public UnityEngine.GameObject Entity;
        [Unity.Entities.RegisterBinding(typeof(CodeGenTestComponent), "Float")]
        public float Float;
        [Unity.Entities.RegisterBinding(typeof(CodeGenTestComponent), "Int")]
        public int Int;
        [Unity.Entities.RegisterBinding(typeof(CodeGenTestComponent), "Bool")]
        public bool Bool;
        [Unity.Entities.RegisterBinding(typeof(CodeGenTestComponent), "Enum")]
        public Unity.Entities.WorldFlags Enum;
        [Unity.Entities.RegisterBinding(typeof(CodeGenTestComponent), "Char")]
        public char Char;

        class CodeGenTestComponentBaker : Unity.Entities.Baker<CodeGenTestComponentAuthoring>
        {
            public override void Bake(CodeGenTestComponentAuthoring authoring)
            {
                Unity.Entities.Tests.CodeGenTestComponent component = default(Unity.Entities.Tests.CodeGenTestComponent);
                component.Entity = GetEntity(authoring.Entity, TransformUsageFlags.None);
                component.Float = authoring.Float;
                component.Int = authoring.Int;
                component.Bool = authoring.Bool;
                component.Enum = authoring.Enum;
                component.Char = authoring.Char;
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, component);
            }
        }
    }
}
