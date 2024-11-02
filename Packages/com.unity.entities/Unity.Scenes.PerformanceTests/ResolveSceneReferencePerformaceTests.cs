using System.Collections.Generic;
using NUnit.Framework;
using Unity.PerformanceTesting;

namespace Unity.Scenes.PerformanceTests
{
    [Category("Performance")]
    public class ResolveSceneReferencePerformaceTests
    {
        [Test, Performance]
        public static void GetLoadPathFromArtifactPaths_Performance()
        {
            string[] testPaths = new[]
            {
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.0.entities",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.0.asset",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.0.weakassetrefs",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.10.entities",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.10.asset",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.10.weakassetrefs",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.20.entities",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.20.asset",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.20.weakassetrefs",
                "VirtualArtifacts/Extra/e3/e3d8c8b443a8b5151a7b437546b3b53e.entityheader"
            };

            var loadPaths = new List<string>(128);

            Measure.Method(() =>
            {
                for (int i = 0; i < 10; ++i)
                {
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesHeader));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesBinary, 0));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesUnityObjectReferences, 0));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesWeakAssetRefs, 0));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesBinary, 10));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesUnityObjectReferences, 10));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesWeakAssetRefs, 10));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesBinary, 20));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesUnityObjectReferences, 20));
                    loadPaths.Add(EntityScenesPaths.GetLoadPathFromArtifactPaths(testPaths, EntityScenesPaths.PathType.EntitiesWeakAssetRefs, 20));
                }
            })
            .WarmupCount(1)
            .MeasurementCount(10)
            .SetUp(() => loadPaths.Clear())
            .Run();
        }
    }
}
