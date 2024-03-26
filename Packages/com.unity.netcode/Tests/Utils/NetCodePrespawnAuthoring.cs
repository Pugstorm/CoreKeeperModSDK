using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Tests
{
    public class NetCodePrespawnAuthoring : MonoBehaviour
    {
    }

    class NetCodePrespawnAuthoringBaker : Baker<NetCodePrespawnAuthoring>
    {
        public override void Bake(NetCodePrespawnAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<NetCodePrespawnTag>(entity);
        }
    }
}
