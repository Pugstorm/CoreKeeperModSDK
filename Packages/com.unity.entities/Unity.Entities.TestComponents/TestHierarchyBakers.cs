using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class GetComponentTransformBaker : Baker<TestComponentAuthoring>
    {
        public struct Vector3Element : IComponentData
        {
            public static implicit operator float3(Vector3Element e)
            {
                return e.Value;
            }

            public static implicit operator Vector3Element(float3 e)
            {
                return new Vector3Element {Value = e};
            }

            public float3 Value;
        }

        public override void Bake(TestComponentAuthoring authoring)
        {
            var position = GetComponent<Transform>().position;

            // This test might require transform components
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, new Vector3Element { Value =  position });
        }
    }

    public class GetParentBaker : Baker<TestComponentAuthoring>
	{
        public struct IntElement : IBufferElementData
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

		public override void Bake(TestComponentAuthoring authoring)
		{
            var parent = GetParent();

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
			DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            if (parent)
                buffer.Add(parent.GetInstanceID());
        }
	}

    public class GetParentsBaker : Baker<TestComponentAuthoring>
    {
        public struct IntElement : IBufferElementData
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

        public override void Bake(TestComponentAuthoring authoring)
        {
            var parents = GetParents();

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            if (parents != null)
            {
                foreach (var parent in parents)
                {
                    buffer.Add(parent.GetInstanceID());
                }
            }
        }
    }

    public class GetChildCountBaker : Baker<TestComponentAuthoring>
    {
        public struct IntComponent : IComponentData
        {
            public int Value;
        }

        public override void Bake(TestComponentAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new IntComponent() { Value = GetChildCount() });
        }
    }

    public class GetChildBaker : Baker<TestComponentAuthoring>
    {
        public struct IntElement : IBufferElementData
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

        public override void Bake(TestComponentAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            if (authoring.transform.childCount > 0)
            {
                var child = GetChild(0);
                buffer.Add(child.GetInstanceID());
            }
        }
    }

    public class GetChildrenBaker : Baker<TestComponentAuthoring>
    {
        public static bool Recursive;

        public struct IntElement : IBufferElementData
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

        public override void Bake(TestComponentAuthoring authoring)
        {
            var children = GetChildren(Recursive);

            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            DynamicBuffer<IntElement> buffer = AddBuffer<IntElement>(entity);
            if (children != null)
            {
                foreach (var child in children)
                {
                    buffer.Add(child.GetInstanceID());
                }
            }
        }
    }
}
