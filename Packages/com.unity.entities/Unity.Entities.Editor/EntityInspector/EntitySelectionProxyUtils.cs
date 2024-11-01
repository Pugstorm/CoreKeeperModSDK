using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Entities.Editor;
using UnityEditor;
using UnityEngine;
using UnityObject = UnityEngine.Object;

static class EntitySelectionProxyUtility
{
    [InitializeOnLoadMethod]
    static void Subscribe()
    {
        HandleUtility.getEntitiesForAuthoringObject += GetEntitiesForAuthoringObject;
        HandleUtility.getAuthoringObjectForEntity += GetAuthoringObjectForEntity;
    }

    static IEnumerable<int> GetEntitiesForAuthoringObject(UnityObject obj)
    {
        World world = World.DefaultGameObjectInjectionWorld;

        if (world == null)
        {
            yield return 0;
        }
        else if (obj is GameObject gameObject)
        {
            var debug = world.EntityManager.Debug;
            var map = debug.GetCachedEntityGUIDToEntityIndexLookup();
            var instanceID = gameObject.GetInstanceID();

            foreach (var entity in map.GetValuesForKey(instanceID))
            {
                yield return entity.Index;
            }
        }
        else if (obj is EntitySelectionProxy proxy)
        {
            yield return proxy.Entity.Index;
        }
    }

    static UnityObject GetAuthoringObjectForEntity(int entityIndex)
    {
        Entity entity = Entity.Null;
        World world = null;
        foreach (var w in World.All)
        {
            entity = w.EntityManager.GetEntityByEntityIndex(entityIndex);
            if (w.EntityManager.Exists(entity))
            {
                world = w;
                break;
            }
        }

        Debug.Assert(entity != Entity.Null, "Entity is invalid.");
        Debug.Assert(world != null, "Entity does not belong to any world.");

        UnityObject authoringObject = world.EntityManager.Debug.GetAuthoringObjectForEntity(entity);

        // If we did not find the GameObject associated with this entity, try to find it in the current selection.
        // We don't want to create a new EntitySelectionProxy for an Entity that is already selected. Otherwise some features like Ctrl+click to deselect an Entity won't work.
        // For example, Ctrl+click is basically checking if the newly picked object is already in the Selection.objects in list. If this is the case, then it deselects it.
        if (authoringObject == null && Selection.objects != null)
        {
            foreach (UnityObject obj in Selection.objects)
            {
                var proxy = obj as EntitySelectionProxy;
                if (proxy != null)
                {
                    if (proxy.Entity == entity)
                    {
                        authoringObject = proxy;
                        break;
                    }
                }
            }
        }

        if (authoringObject == null)
            authoringObject = EntitySelectionProxy.CreateInstance(world, entity);

        return authoringObject;
    }
}
