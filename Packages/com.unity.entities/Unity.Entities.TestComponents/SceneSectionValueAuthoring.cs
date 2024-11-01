using System;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public struct SceneSectionValue : IComponentData
    {
        public SceneSectionValue(int value)
        {
            Value = value;
        }

        public int Value;
    }

    public class SceneSectionValueAuthoring : MonoBehaviour
    {
        public int Value = -1;
    }
    class SceneSectionValueBaker : Baker<SceneSectionValueAuthoring>
    {
        public override void Bake(SceneSectionValueAuthoring authoring)
        {
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SceneSectionValue(authoring.Value));
        }
    }
}
