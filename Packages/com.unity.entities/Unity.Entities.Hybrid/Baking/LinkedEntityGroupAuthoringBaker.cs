using UnityEngine;

namespace Unity.Entities.Hybrid.Baking
{
    [BakingType]
    internal struct LinkedEntityGroupBakingData : IBufferElementData
    {
        public Entity Value;

        public static implicit operator LinkedEntityGroupBakingData(Entity e)
        {
            return new LinkedEntityGroupBakingData {Value = e};
        }
    }

    internal class LinkedEntityGroupAuthoringBaker : Baker<LinkedEntityGroupAuthoring>
    {
        public override void Bake(LinkedEntityGroupAuthoring authoring)
        {
            //Retrieve all children and add their primary entities to the LinkedEntityGroup buffer
            var childrenGameObjects = GetChildren(true);

            // Link entity groups don't require any Transform component on the root of the link entity group
            var rootEntity = GetEntity(authoring, TransformUsageFlags.None);

            var buffer = AddBuffer<LinkedEntityGroupBakingData>(rootEntity);
            buffer.Add(rootEntity);

            foreach (var childGameObject in childrenGameObjects)
            {
                // Link entity groups don't require any Transform component on the entities belonging to the link entity group
                var childEntity = GetEntity(childGameObject, TransformUsageFlags.None);
                buffer.Add(childEntity);
            }
        }
    }
}
