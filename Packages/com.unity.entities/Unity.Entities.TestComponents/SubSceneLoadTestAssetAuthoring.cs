using System;
using Unity.Entities;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Scenes.Editor.Tests
{
    [AddComponentMenu("")]
    public class SubSceneLoadTestAssetAuthoring : MonoBehaviour
    {
        public Object Asset;
    }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    public class SubSceneLoadTestAssetComponent : IComponentData
    {
        public Object Asset;
    }
#endif

    public class SubSceneLoadTestAssetBaker : Baker<SubSceneLoadTestAssetAuthoring>
    {
        public override void Bake(SubSceneLoadTestAssetAuthoring authoring)
        {
#if !UNITY_DISABLE_MANAGED_COMPONENTS
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponentObject(entity, new SubSceneLoadTestAssetComponent
            {
                Asset = authoring.Asset
            });
#endif
        }
    }
}
