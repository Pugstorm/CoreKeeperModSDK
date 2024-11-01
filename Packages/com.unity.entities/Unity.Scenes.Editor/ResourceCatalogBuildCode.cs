using Unity.Collections;
using Unity.Entities;

namespace Unity.Scenes.Editor
{
    static class ResourceCatalogBuildCode
    {
        public static void WriteCatalogFile(RootSceneInfo[] sceneArray, string sceneInfoPath)
        {
            var dir = System.IO.Path.GetDirectoryName(sceneInfoPath);
            System.IO.Directory.CreateDirectory(dir);
            var builder = new BlobBuilder(Allocator.Temp);
            ref var root = ref builder.ConstructRoot<ResourceCatalogData>();
            var metas = builder.Allocate(ref root.resources, sceneArray.Length);
            for (int i = 0; i < sceneArray.Length; i++)
            {
                metas[i] = new ResourceMetaData()
                {
                    ResourceId = sceneArray[i].Guid,
                    ResourceType = ResourceMetaData.Type.Scene,
                };
            }

            var strings = builder.Allocate(ref root.paths, sceneArray.Length);
            for (int i = 0; i < sceneArray.Length; i++)
                builder.AllocateString(ref strings[i], sceneArray[i].Path);

            BlobAssetReference<ResourceCatalogData>.Write(builder, sceneInfoPath, ResourceCatalogData.CurrentFileFormatVersion);
            builder.Dispose();
        }
    }
}
