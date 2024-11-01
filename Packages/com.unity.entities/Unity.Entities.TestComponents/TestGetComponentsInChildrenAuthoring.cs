using System;
using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class TestGetComponentsInChildrenAuthoring : MonoBehaviour
    {
        public int value;

	    struct ComponentTest1 : IComponentData
		{
			public int Field;
		}

        struct IntElement : IBufferElementData
        {
            public static implicit operator int(IntElement e)
            {
                return e.Value;
            }

            public static implicit operator IntElement(int e)
            {
                return new IntElement {Value = e};
            }

            public int Value;
        }

        class Baker : Baker<TestGetComponentsInChildrenAuthoring>
        {
            public override void Bake(TestGetComponentsInChildrenAuthoring authoring)
            {
                List<Collider> found = new List<Collider>();
				GetComponentsInChildren<Collider>(found);
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new ComponentTest1() {Field = found.Count});

                DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
                foreach (var component in found)
                {
                    buffer.Add(component.GetInstanceID());
                }
            }
        }
    }
}
