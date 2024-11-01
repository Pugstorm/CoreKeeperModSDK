namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Entities.Content;
    using UnityEngine;

    public class SceneRefSample : MonoBehaviour
    {
        public WeakObjectSceneReference scene;
        class SceneRefSampleBaker : Baker<SceneRefSample>
        {
            public override void Bake(SceneRefSample authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                AddComponent(entity, new SceneComponentData { scene = authoring.scene });
            }
        }
    }

    public struct SceneComponentData : IComponentData
    {
        public WeakObjectSceneReference scene;
    }
    #endregion
}
