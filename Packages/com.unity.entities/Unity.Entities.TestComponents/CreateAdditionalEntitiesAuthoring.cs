using System.Globalization;
using UnityEngine;

namespace Unity.Entities.TestComponents
{
    public class CreateAdditionalEntitiesAuthoring : MonoBehaviour
    {
        public int number;
    }

    public class CreateAdditionalEntitiesAuthoringBaker : Baker<CreateAdditionalEntitiesAuthoring>
    {
        public override void Bake(CreateAdditionalEntitiesAuthoring authoring)
        {
            for (int i = 0; i < authoring.number; i++)
            {
                CreateAdditionalEntity(TransformUsageFlags.None);
            }
        }
    }
}
