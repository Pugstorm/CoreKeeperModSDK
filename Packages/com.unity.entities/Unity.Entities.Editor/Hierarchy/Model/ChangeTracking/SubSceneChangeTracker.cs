using System;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Editor.Bridge;
using Unity.Jobs;
using Unity.Scenes;

namespace Unity.Entities.Editor
{
    class SubSceneChangeTracker : IDisposable
    {
        readonly SimpleDiffer<Hash128> m_SubScenesDiffer;
        readonly SimpleDiffer<(Entity, SceneReference)> m_EntityScenesDiffer;
        readonly SimpleDiffer<SceneTag> m_SceneTagDiffer;

        EntityQuery m_Query;
        World m_World;

        public SubSceneChangeTracker()
        {
            m_SubScenesDiffer = new SimpleDiffer<Hash128>(128, Allocator.Persistent);
            m_EntityScenesDiffer = new SimpleDiffer<(Entity, SceneReference)>(128, Allocator.Persistent);
            m_SceneTagDiffer = new SimpleDiffer<SceneTag>(128, Allocator.Persistent);
        }

        public void SetWorld(World world)
        {
            if (m_World == world)
                return;

            m_SubScenesDiffer.Clear();
            m_EntityScenesDiffer.Clear();
            m_SceneTagDiffer.Clear();

            m_World = world;
            if (m_World != null && m_World.IsCreated)
            {
                var entityManager = m_World.EntityManager;

                // Cannot create entity query during exclusive transaction, so make sure to complete it
                if (!entityManager.CanBeginExclusiveEntityTransaction())
                    entityManager.EndExclusiveEntityTransaction();

                using var qb = new EntityQueryBuilder(Allocator.TempJob);
                m_Query = qb.WithAll<SceneReference>().Build(entityManager);
            }
        }

        public void GetChanges(SubSceneMapChanges changes)
        {
            using var _ = EditorPerformanceTrackerBridge.CreateEditorPerformanceTracker($"{nameof(SubSceneChangeTracker)}.{nameof(GetChanges)}");

            changes.Clear();

            var allSubScenes = (List<SubScene>)SubScene.AllSubScenes;

            var subScenes = new NativeList<Hash128>(allSubScenes.Count, Allocator.TempJob);
            foreach (var subScene in allSubScenes)
            {
                // Filter out subscenes that are not tied to scene assets yet
                if (subScene.SceneGUID != default)
                    subScenes.Add(subScene.SceneGUID);
            }

            m_SubScenesDiffer.GetCreatedAndRemovedItems(subScenes.AsArray(), changes.CreatedSubScenes, changes.RemovedSubScenes);
            subScenes.Dispose();

            if (m_World is null)
                return;

            var entityScenesEntities = m_Query.ToEntityListAsync(Allocator.TempJob, out var entityScenesJobHandle);
            var entityScenesComponents = m_Query.ToComponentDataListAsync<SceneReference>(Allocator.TempJob, out var entityScenesComponentsJobHandle);
            JobHandle.CombineDependencies(entityScenesJobHandle, entityScenesComponentsJobHandle).Complete();
            var entityScenes = new NativeArray<(Entity, SceneReference)>(entityScenesEntities.Length, Allocator.Temp);
            for (var i = 0; i < entityScenes.Length; i++)
            {
                entityScenes[i] = (entityScenesEntities[i], entityScenesComponents[i]);
            }

            m_EntityScenesDiffer.GetCreatedAndRemovedItems(entityScenes, changes.CreatedEntityScenes, changes.RemovedEntityScenes);
            entityScenes.Dispose();
            entityScenesEntities.Dispose();
            entityScenesComponents.Dispose();

            unsafe
            {
                m_World.EntityManager.GetCheckedEntityDataAccess()->EntityComponentStore->GetAllUniqueSharedComponents_Unmanaged<SceneTag>(out var allSceneTags, Allocator.Temp);
                var validSceneTags = new NativeList<SceneTag>(allSceneTags.Length, Allocator.Temp);
                foreach (var sceneTag in allSceneTags)
                {
                    if (m_World.EntityManager.Exists(sceneTag.SceneEntity))
                    {
                        validSceneTags.Add(sceneTag);
                    }
                }

                m_SceneTagDiffer.GetCreatedAndRemovedItems(validSceneTags.AsArray(), changes.CreatedSceneTags, changes.RemovedSceneTags);
                allSceneTags.Dispose();
                validSceneTags.Dispose();
            }
        }

        public void Dispose()
        {
            m_SubScenesDiffer.Dispose();
            m_EntityScenesDiffer.Dispose();
            m_SceneTagDiffer.Dispose();
        }

        public readonly struct SubSceneMapChanges : IDisposable
        {
            public readonly NativeList<Hash128> CreatedSubScenes;
            public readonly NativeList<Hash128> RemovedSubScenes;
            public readonly NativeList<(Entity, SceneReference)> CreatedEntityScenes;
            public readonly NativeList<(Entity, SceneReference)> RemovedEntityScenes;
            public readonly NativeList<SceneTag> CreatedSceneTags;
            public readonly NativeList<SceneTag> RemovedSceneTags;

            public SubSceneMapChanges(int initialCapacity, Allocator allocator)
            {
                CreatedSubScenes = new NativeList<Hash128>(initialCapacity, allocator);
                RemovedSubScenes = new NativeList<Hash128>(initialCapacity, allocator);
                CreatedEntityScenes = new NativeList<(Entity, SceneReference)>(initialCapacity, allocator);
                RemovedEntityScenes = new NativeList<(Entity, SceneReference)>(initialCapacity, allocator);
                CreatedSceneTags = new NativeList<SceneTag>(initialCapacity, allocator);
                RemovedSceneTags = new NativeList<SceneTag>(initialCapacity, allocator);
            }

            public bool HasChanges() =>
                CreatedSubScenes.Length > 0 ||
                RemovedSubScenes.Length > 0 ||
                CreatedEntityScenes.Length > 0 ||
                RemovedEntityScenes.Length > 0 ||
                CreatedSceneTags.Length > 0 ||
                RemovedSceneTags.Length > 0;

            public void Dispose()
            {
                CreatedSubScenes.Dispose();
                RemovedSubScenes.Dispose();
                CreatedEntityScenes.Dispose();
                RemovedEntityScenes.Dispose();
                CreatedSceneTags.Dispose();
                RemovedSceneTags.Dispose();
            }

            public void Clear()
            {
                CreatedSubScenes.Clear();
                RemovedSubScenes.Clear();
                CreatedEntityScenes.Clear();
                RemovedEntityScenes.Clear();
                CreatedSceneTags.Clear();
                RemovedSceneTags.Clear();
            }
        }
    }
}
