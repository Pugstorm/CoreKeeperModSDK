using System.IO;
using Unity.Entities;

namespace Unity.Scenes.Editor
{
    internal class SubSceneBaker : Baker<SubScene>
    {
        public override void Bake(SubScene authoring)
        {
            // Subscene components don't require any transform components
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, new SceneReference() {SceneGUID = authoring.SceneGUID});
            if (authoring.AutoLoadScene)
            {
                AddComponent(entity, new RequestSceneLoaded());
            }
        }
    }
}
