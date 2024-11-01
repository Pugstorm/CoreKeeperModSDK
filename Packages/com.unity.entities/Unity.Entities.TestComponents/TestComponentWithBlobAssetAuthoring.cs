using System;
using Unity.Collections;
using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TestComponentWithBlobAssetAuthoring : MonoBehaviour
    {
        public int Version;
        public struct Component : IComponentData
        {
            public BlobAssetReference<int> BlobAssetRef;
        }

        class Baker : Baker<TestComponentWithBlobAssetAuthoring>
        {
            public override void Bake(TestComponentWithBlobAssetAuthoring authoring)
            {
                var blobReference = BlobAssetUtility.CreateBlobAsset(authoring.Version);
                AddBlobAsset(ref blobReference, out Hash128 _);

                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Component
                {
                    BlobAssetRef = blobReference
                });
            }
        }
    }
}
