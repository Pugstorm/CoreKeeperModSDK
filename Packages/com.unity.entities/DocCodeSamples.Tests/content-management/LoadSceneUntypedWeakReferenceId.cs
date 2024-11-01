namespace Doc.CodeSamples.Tests
{
    #region example
    using Unity.Entities;
    using Unity.Entities.Content;
    using Unity.Entities.Serialization;

    public struct SceneUntypedWeakReferenceIdData : IComponentData
    {
        public bool startedLoad;
        public UntypedWeakReferenceId scene;
    }

    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PresentationSystemGroup))]
    public partial struct LoadSceneFromUntypedWeakReferenceIdSystem : ISystem
    {
        public void OnCreate(ref SystemState state) { }
        public void OnDestroy(ref SystemState state) { }
        public void OnUpdate(ref SystemState state)
        {
            foreach (var sceneData in SystemAPI.Query<RefRW<SceneUntypedWeakReferenceIdData>>())
            {
                if (!sceneData.ValueRO.startedLoad)
                {
                    RuntimeContentManager.LoadSceneAsync(sceneData.ValueRO.scene,
                        new Unity.Loading.ContentSceneParameters()
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
