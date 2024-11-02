using UnityEngine;
using Unity.Entities;

namespace Doc.CodeSamples.Tests
{
    #region ship-example
    public struct Ship : IComponentData
    {
        public float speed;
        // Other data
    }

    public class ShipAuthoring : MonoBehaviour
    {
        public float speed;
        // Other authoring data

        public class Baker : Baker<ShipAuthoring>
        {
            public override void Bake(ShipAuthoring authoring)
            {
                // Set the transform usage flag to Dynamic
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new Ship
                {
                    speed = authoring.speed
                    // Assign other data
                });
            }
        }
    }
    #endregion

    #region custom-data
    public struct CustomRenderingData : IComponentData
    {
        // Data
    }

    public class CustomRenderingDataAuthoring : MonoBehaviour
    {
        // Authoring data

        public class Baker : Baker<CustomRenderingDataAuthoring>
        {
            public override void Bake(CustomRenderingDataAuthoring authoring)
            {
                // Set transform usage flag to Renderable
                var entity = GetEntity(TransformUsageFlags.Renderable);
                AddComponent(entity, new CustomRenderingData
                {
                    // Assign Data
                });
            }
        }
    }
    #endregion
}