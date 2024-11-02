using System.Collections.Generic;
using UnityEngine;

namespace Unity.Entities.Editor
{
    static class EntityBakingEditorUtility
    {
        static EntityBakingEditorUtility()
        {
            TypeManager.Initialize();
        }

        public static unsafe void GetBakingData(List<GameObject> gameObjects, World world, List<EntityBakingData> result)
        {
            result.Clear();
            if (world == null)
                return;

            using var pooledList = PooledList<Entity>.Make();
            var cachedEntities = pooledList.List;

            var lookup = world.EntityManager.Debug.GetCachedEntityGUIDToEntityIndexLookup();
            var access = world.EntityManager.GetCheckedEntityDataAccess();

            foreach (var gameObject in gameObjects)
            {
                if (!IsGameObjectBaked(gameObject))
                    continue;

                cachedEntities.Clear();

                foreach (var e in lookup.GetValuesForKey(gameObject.GetInstanceID()))
                {
                    var data = access->GetComponentData<EntityGuid>(e);

                    // Detect primary entity.
                    if (data.Serial == 0)
                        cachedEntities.Insert(0, e);
                    else
                        cachedEntities.Add(e);
                }

                if (cachedEntities.Count == 0)
                    continue;

                result.Add(new EntityBakingData
                {
                    PrimaryEntity = cachedEntities[0],
                    AdditionalEntities = cachedEntities.ToArray(),
                    EntityManager = world.EntityManager,
                });
            }
        }

        public static unsafe EntityBakingData GetBakingData(GameObject gameObject, World world)
        {
            if (world == null || !IsGameObjectBaked(gameObject))
                return EntityBakingData.Null;

            using var pooledList = PooledList<Entity>.Make();
            var cachedEntities = pooledList.List;

            var lookup = world.EntityManager.Debug.GetCachedEntityGUIDToEntityIndexLookup();
            var access = world.EntityManager.GetCheckedEntityDataAccess();

            foreach (var e in lookup.GetValuesForKey(gameObject.GetInstanceID()))
            {
                var data = access->GetComponentData<EntityGuid>(e);

                // Detect primary entity.
                if (data.Serial == 0)
                    cachedEntities.Insert(0, e);
                else
                    cachedEntities.Add(e);
            }

            if (cachedEntities.Count == 0)
                return EntityBakingData.Null;

            return new EntityBakingData
            {
                PrimaryEntity = cachedEntities[0],
                AdditionalEntities = cachedEntities.ToArray(),
                EntityManager = world.EntityManager,
            };
        }

        public static bool IsGameObjectBaked(GameObject gameObject)
            => GameObjectBakingEditorUtility.GetGameObjectBakingResultStatus(gameObject).IsBaked();
    }
}
