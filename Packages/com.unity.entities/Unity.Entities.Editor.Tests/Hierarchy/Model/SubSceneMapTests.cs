using System;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Editor.Tests
{
    [TestFixture]
    class SubSceneMapTests
    {
        [Test]
        public void SubSceneMap_IntegrateChanges_DoesNotThrow_WhenIntegratingSceneTagsReferencingDifferentWorlds()
        {
            using var nodeStore = new HierarchyNodeStore(Allocator.TempJob);
            using var nameStore = new HierarchyNameStore(Allocator.TempJob);
            using var map = new SubSceneMap();
            using var world1 = new World("World1");
            using var world2 = new World("World2");

            var sceneReference1 = new SceneReference { SceneGUID = new Hash128(Guid.NewGuid().ToString("N")) };
            var sceneEntity1 = world1.EntityManager.CreateEntity();
            world1.EntityManager.AddComponentData(sceneEntity1, sceneReference1);
            var sceneReference2 = new SceneReference { SceneGUID = new Hash128(Guid.NewGuid().ToString("N")) };
            var sceneEntity2 = world2.EntityManager.CreateEntity();
            world2.EntityManager.AddComponentData(sceneEntity2, sceneReference2);

            using var changes = new SubSceneChangeTracker.SubSceneMapChanges(1024, Allocator.Temp);
            changes.CreatedSceneTags.Add(new SceneTag { SceneEntity = sceneEntity1 });
            changes.CreatedSceneTags.Add(new SceneTag { SceneEntity = sceneEntity2 });

            Assert.DoesNotThrow(() => map.IntegrateChanges(world1, nodeStore, nameStore, changes));
        }
    }
}
