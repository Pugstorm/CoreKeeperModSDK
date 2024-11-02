using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class DependsOnComponentTransitiveTestAuthoring : MonoBehaviour
    {
        public int SelfValue;
        public DependsOnComponentTransitiveTestAuthoring Dependency;
        static int FindValue(DependsOnComponentTransitiveTestAuthoring a)
        {
            int dist = 0;
            while (a.Dependency != null)
            {
                a = a.Dependency;
                dist++;
            }
            return a.SelfValue + dist;
        }

        public struct Component : IComponentData
        {
            public int Value;
        }

        class Baker : Baker<DependsOnComponentTransitiveTestAuthoring>
        {
            int FindValue(DependsOnComponentTransitiveTestAuthoring a)
            {
                int dist = 0;
                while (DependsOn(a.Dependency) != null)
                {
                    a = a.Dependency;
                    dist++;
                }
                return a.SelfValue + dist;
            }

            public override void Bake(DependsOnComponentTransitiveTestAuthoring authoring)
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
