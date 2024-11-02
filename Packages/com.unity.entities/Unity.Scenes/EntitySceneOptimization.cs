
using System;
using System.Collections.Generic;
using Unity.Collections;

namespace Unity.Entities.Streaming
{
    [DisableAutoCreation]
    [WorldSystemFilter(WorldSystemFilterFlags.EntitySceneOptimizations)]
    internal partial class OptimizationGroup : ComponentSystemGroup
    {
    }

    internal static class EntitySceneOptimization
    {
        static void RemoveCleanupComponents(EntityManager entityManager)
        {
            foreach (var s in TypeManager.AllTypes)
            {
                if (TypeManager.IsCleanupComponent(s.TypeIndex))
                {
                    entityManager.RemoveComponent(entityManager.UniversalQueryWithSystems, ComponentType.FromTypeIndex(s.TypeIndex));
                }
            }
        }

        internal static void OptimizeInternal(World world, NativeList<SystemTypeIndex> systemTypes)
        {
            var entityManager = world.EntityManager;

            var group = world.GetOrCreateSystemManaged<OptimizationGroup>();

            for (int i = 0; i < systemTypes.Length; i++)
                AddSystemAndLogException(world, group, systemTypes[i]);
            group.SortSystems();

            // foreach (var system in group.Systems)
            //    Debug.Log(system.GetType());

            // Run first pass (This might add / remove a bunch of components and thus invalidate some of chunk component data caches)
            group.Update();

            // Run all systems again (For example chunk bounding volumes might be out of sync after various remove / add from previous pass)
            // But now we are sure that no more re-ordering will happen.
            group.Update();

            RemoveCleanupComponents(entityManager);
        }

        internal static void AddSystemAndLogException(World world, ComponentSystemGroup group, SystemTypeIndex type)
        {
            try
            {
                group.AddSystemToUpdateList(world.GetOrCreateSystemManaged(type));
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }
        }

        internal static void Optimize(World world)
        {
            var systemList = DefaultWorldInitialization.GetAllSystemTypeIndices(WorldSystemFilterFlags.EntitySceneOptimizations);
            OptimizeInternal(world, systemList);
        }
    }
}
