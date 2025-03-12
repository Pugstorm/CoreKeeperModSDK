using Unity.Entities;
using UnityEngine;

internal class PhysicsPreserveTransformAuthoring : MonoBehaviour
{
    public class Baker : Baker<PhysicsPreserveTransformAuthoring>
    {
        public override void Bake(PhysicsPreserveTransformAuthoring authoring)
        {
            AddTransformUsageFlags(TransformUsageFlags.Dynamic);
        }
    }
}
