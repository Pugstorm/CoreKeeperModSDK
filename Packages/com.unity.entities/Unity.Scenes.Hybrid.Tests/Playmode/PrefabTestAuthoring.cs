using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;

namespace Unity.Scenes.Hybrid.Tests.Playmode
{
    public struct PrefabTestComponent : IComponentData
    {
        public EntityPrefabReference PrefabReference;
        public int PrefabIndex;
    }

#if UNITY_EDITOR
    public class PrefabTestAuthoring : MonoBehaviour
    {
        public GameObject[] _Prefabs;

        class Baker : Baker<PrefabTestAuthoring>
        {
            public override void Bake(PrefabTestAuthoring authoring)
            {
                if (authoring._Prefabs == null || authoring._Prefabs.Length == 0)
                    return;

                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new PrefabTestComponent(){PrefabReference = new EntityPrefabReference(authoring._Prefabs[0]), PrefabIndex = 0});
                for (int i = 1; i < authoring._Prefabs.Length; ++i)
                {
                    var moreEntity = CreateAdditionalEntity(TransformUsageFlags.None);
                    AddComponent(moreEntity, new PrefabTestComponent(){PrefabReference = new EntityPrefabReference(authoring._Prefabs[i]), PrefabIndex = i});
                }
            }
        }
    }
#endif
}
