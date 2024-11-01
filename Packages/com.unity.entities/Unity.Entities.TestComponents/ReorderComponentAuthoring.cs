using UnityEngine;

namespace Unity.Entities.Tests
{
    public struct ReorderTestComponent : IComponentData
    {
        public int Value;
    }

    [AddComponentMenu("")]
    public class ReorderComponentAuthoring : MonoBehaviour
    {
        class Baker : Baker<ReorderComponentAuthoring>
        {
            public override void Bake(ReorderComponentAuthoring authoring)
            {
                var value = GetComponent<ParentTestClass>().Value;
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ReorderTestComponent{Value = value});
            }
        }
    }


    public class ParentTestClass : MonoBehaviour
    {
        public int Value = 0;
    }

    public class ChildATestClass : ParentTestClass
    {
        public ChildATestClass()
        {
            Value = 1;
        }
    }

    public class ChildBTestClass : ParentTestClass
    {
        public ChildBTestClass()
        {
            Value = 2;
        }
    }
}
