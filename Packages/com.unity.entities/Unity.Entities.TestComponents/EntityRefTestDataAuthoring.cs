using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Conversion;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public struct EntityRefTestData : IComponentData
    {
        public Entity Value;

        public EntityRefTestData(Entity value) => Value = value;

        public override string ToString() => Value.ToString();
    }

    [AddComponentMenu("")]
    public class EntityRefTestDataAuthoring : MonoBehaviour
    {
        public GameObject Value;
        public int        AdditionalEntityCount;
        public bool       DeclareLinkedEntityGroup;

        // Empty Update function makes it so that unity shows the UI for the checkbox.
        // We use it for testing stripping of components.
        // ReSharper disable once Unity.RedundantEventFunction
        void Update() {}
    }

    public class EntityRefTestDataBaker : Baker<EntityRefTestDataAuthoring>
    {
        public override void Bake(EntityRefTestDataAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            var refEntity = GetEntity(authoring.Value, TransformUsageFlags.None);
            AddComponent(entity, new EntityRefTestData {Value = refEntity});
            for (int i = 0; i != authoring.AdditionalEntityCount; i++)
            {
                var additional = CreateAdditionalEntity(TransformUsageFlags.None);
                AddComponent(additional, new EntityRefTestData {Value = refEntity});
            }

            //TODO: Needs to declare prefabs as well
        }
    }
}
