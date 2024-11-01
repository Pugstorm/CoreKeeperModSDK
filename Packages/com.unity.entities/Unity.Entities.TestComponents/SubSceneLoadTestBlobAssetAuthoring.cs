using System;
using Unity.Entities;
using UnityEngine;

namespace Unity.Scenes.Editor.Tests
{
    [AddComponentMenu("")]
    public class SubSceneLoadTestBlobAssetAuthoring : MonoBehaviour
    {
        public bool UseNullBlobAsset;
        public int Int;
        public int PtrInt;
        public string String;
        public string[] Strings;
    }

    public struct SubSceneLoadTestBlobAssetComponent : IComponentData
    {
        public BlobAssetReference<SubSceneLoadTestBlobAsset> BlobAsset;
    }

    public class SubsceneLoadTestBlobAssetBaker : Baker<SubSceneLoadTestBlobAssetAuthoring>
    {
        public override void Bake(SubSceneLoadTestBlobAssetAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            if (authoring.UseNullBlobAsset)
            {
                AddComponent(entity, new SubSceneLoadTestBlobAssetComponent
                {
                    BlobAsset = BlobAssetReference<SubSceneLoadTestBlobAsset>.Null
                });
            }
            else
            {
                AddComponent(entity, new SubSceneLoadTestBlobAssetComponent()
                {
                    BlobAsset = SubSceneLoadTestBlobAsset.Make(authoring.Int, authoring.PtrInt, authoring.String, authoring.Strings)
                });
            }
        }
    }
}
