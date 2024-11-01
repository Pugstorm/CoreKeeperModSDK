using UnityEngine;

namespace Unity.Entities.Tests
{
   // [AddComponentMenu("")]
    public class DependsOnAssetTransitiveTestAuthoring : MonoBehaviour
    {
        public int SelfValue;
        public DependsOnAssetTransitiveTestScriptableObject Dependency;
        static int FindValue(DependsOnAssetTransitiveTestAuthoring a)
        {
            int dist = 0;
            if (a.Dependency != null)
            {
                dist = a.Dependency.SelfValue;
            }
            return a.SelfValue + dist;
        }

        public struct Component : IComponentData
        {
            public int Value;
        }

        class Baker : Baker<DependsOnAssetTransitiveTestAuthoring>
        {
            int FindValue(DependsOnAssetTransitiveTestAuthoring a)
            {
                string name = a.name;
                int dist = 0;
                if (DependsOn(a.Dependency) != null)
                {
                    dist = a.Dependency.SelfValue;
                }
                return a.SelfValue + dist;
            }

            public override void Bake(DependsOnAssetTransitiveTestAuthoring authoring)
            {
                // This test shouldn't require transform components
                var entity = GetEntity(TransformUsageFlags.None);
                AddComponent(entity, new Component
                {
                    Value = FindValue(authoring)
                });
            }
        }
    }


}
