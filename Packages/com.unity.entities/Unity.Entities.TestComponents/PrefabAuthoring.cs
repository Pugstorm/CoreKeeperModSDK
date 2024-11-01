using UnityEngine;
using Unity.Entities.Tests;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class PrefabAuthoring : MonoBehaviour { public GameObject Prefab; }

    public class BakerWithPrefabReferenceTestComponent : Baker<PrefabAuthoring>
    {
        public override void Bake(PrefabAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent<MockData>(entity);
            GetEntity(authoring.Prefab, TransformUsageFlags.None);
        }
    }
}
