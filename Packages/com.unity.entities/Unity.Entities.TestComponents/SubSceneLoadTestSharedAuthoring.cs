using System;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Scenes.Editor.Tests
{
    [AddComponentMenu("")]
    public class SubSceneLoadTestSharedAuthoring : MonoBehaviour
    {
        public int Int;
        public Object Asset;
        public string String;
    }

    public struct SubSceneLoadTestSharedComponent : ISharedComponentData, IEquatable<SubSceneLoadTestSharedComponent>
    {
        // Shared components do not support Entity or BlobAssetReference typed fields, hence not tested
        public int Int;
        public Object Asset;
        public string String;

        public bool Equals(SubSceneLoadTestSharedComponent other)
        {
            return Int == other.Int && Equals(Asset, other.Asset) && String == other.String;
        }

        public override bool Equals(object obj)
        {
            return obj is SubSceneLoadTestSharedComponent other && Equals(other);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                var hashCode = Int;
                hashCode = (hashCode * 397) ^ (Asset != null ? Asset.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (String != null ? String.GetHashCode() : 0);
                return hashCode;
            }
        }
    }

    public class SubSceneLoadTestBaker : Baker<SubSceneLoadTestSharedAuthoring>
    {
        public override void Bake(SubSceneLoadTestSharedAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddSharedComponentManaged(entity, new SubSceneLoadTestSharedComponent()
            {
                Int = authoring.Int,
                Asset = authoring.Asset,
                String = authoring.String
            });
        }
    }
}
