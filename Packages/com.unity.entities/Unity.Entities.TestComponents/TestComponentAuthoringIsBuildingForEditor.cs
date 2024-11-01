using UnityEngine;

namespace Unity.Entities.Tests
{
    [AddComponentMenu("")]
    public class TestComponentAuthoringIsBuildingForEditor : MonoBehaviour
    {
    }

    class TestComponentAuthoringIsBuildingForEditorBaker : Baker<TestComponentAuthoringIsBuildingForEditor>
    {
        public override void Bake(TestComponentAuthoringIsBuildingForEditor authoring)
        {
#if UNITY_EDITOR
            // This test shouldn't require transform components
            var entity = GetEntity(TransformUsageFlags.None);
            if (IsBakingForEditor())
                AddComponent(entity, new IntTestData(1));
            else
                AddComponent(entity, new IntTestData(2));
#else
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new IntTestData(3));
#endif
        }
    }
}
