namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Entities.Content;

    public struct WeakObjectSceneReferenceData : IComponentData
    {
        public bool startedLoad;
        public WeakObjectSceneReference scene;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct LoadSceneFromWeakObjectReferenceSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
        public void OnUpdate(ref SystemState state)
        {
            foreach (var sceneData in SystemAPI.Query<RefRW<WeakObjectSceneReferenceData>>())
            {
                if (!sceneData.ValueRO.startedLoad)
                {
                    sceneData.ValueRW.scene.LoadAsync(new Unity.Loading.ContentSceneParameters()
                    {
                        loadSceneMode = UnityEngine.SceneManagement.LoadSceneMode.Additive
                    });
                    sceneData.ValueRW.startedLoad = true;
                }
            }
        }
    }
    #endregion
}
