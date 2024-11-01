#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [DisallowMultipleComponent]
    public class WeakMaterialComponentAuthoring : MonoBehaviour
    {
        public Material mat;
    }

    public struct WeakMaterialComponent : ISharedComponentData, IEquatable<WeakMaterialComponent>
    {
        public Material material;

        public bool Equals(WeakMaterialComponent other)
        {
            return Equals(material, other.material);
        }

        public override bool Equals(object obj)
        {
            return obj is WeakMaterialComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            return (material != null ? material.GetHashCode() : 0);
        }
    }

    public class WeakMaterialComponentAuthoringBaker : Baker<WeakMaterialComponentAuthoring>
    {
        public override void Bake(WeakMaterialComponentAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddSharedComponentManaged(entity, new WeakMaterialComponent() { material = authoring.mat });
        }
    }
}
#endif
