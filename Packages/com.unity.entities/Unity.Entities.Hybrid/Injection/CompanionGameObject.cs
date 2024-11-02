#if !UNITY_DISABLE_MANAGED_COMPONENTS
using Unity.Collections;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities
{
#if UNITY_EDITOR
    [InitializeOnLoad] // ensures type manager is initialized on domain reload when not playing
#endif
    static unsafe class AttachToEntityClonerInjection
    {
        // Injection is used to keep everything GameObject related outside of Unity.Entities

        static AttachToEntityClonerInjection()
        {
            Initialize();
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Initialize()
        {
            TypeManager.Initialize();
            ManagedComponentStore.CompanionReferenceTypeIndex = TypeManager.GetTypeIndex(typeof(CompanionReference));
            ManagedComponentStore.CompanionLinkTypeIndex = TypeManager.GetTypeIndex(typeof(CompanionLink));
            ManagedComponentStore.CompanionLinkTransformTypeIndex = TypeManager.GetTypeIndex(typeof(CompanionLinkTransform));
            ManagedComponentStore.InstantiateCompanionComponent = InstantiateCompanionComponentDelegate;
            ManagedComponentStore.AssignCompanionComponentsToCompanionGameObjects = AssignCompanionComponentsToCompanionGameObjectsDelegate;
        }

        /// <summary>
        /// This method will handle the cloning of Hybrid Components (if any) during the batched instantiation of an Entity
        /// </summary>
        /// <param name="srcArray">Array of source managed component indices. One per <paramref name="componentCount"/></param>
        /// <param name="componentCount">Number of component being instantiated</param>
        /// <param name="dstEntities">Array of destination entities. One per <paramref name="instanceCount"/></param>
        /// <param name="dstCompanionLinkIndices">Array of destination CompanionLink indices, can be null if the hybrid components are not owned</param>
        /// <param name="dstArray">Array of destination managed component indices. One per <paramref name="componentCount"/>*<paramref name="instanceCount"/>. All indices for the first component stored first etc.</param>
        /// <param name="instanceCount">Number of instances being created</param>
        /// <param name="managedComponentStore">Managed Store that owns the instances we create</param>
        static void InstantiateCompanionComponentDelegate(int* srcArray, int componentCount, Entity* dstEntities, int* dstCompanionLinkIndices, int* dstComponentLinkIds, int* dstArray, int instanceCount, ManagedComponentStore managedComponentStore)
        {
            if (dstCompanionLinkIndices != null)
            {
                var dstCompanionGameObjects = new GameObject[instanceCount];
                for (int i = 0; i < instanceCount; ++i)
                {
                    var companionLink = (CompanionReference)managedComponentStore.GetManagedComponent(dstCompanionLinkIndices[i]);
                    // Update referenced Instance ID
                    companionLink.Companion.Id.instanceId = dstComponentLinkIds[i];
                    dstCompanionGameObjects[i] = companionLink.Companion;
                    #if UNITY_EDITOR
                    CompanionGameObjectUtility.SetCompanionName(dstEntities[i], dstCompanionGameObjects[i]);
                    #endif
                }

                for (int src = 0; src < componentCount; ++src)
                {
                    var componentType = managedComponentStore.GetManagedComponent(srcArray[src]).GetType();

                    for (int i = 0; i < instanceCount; i++)
                    {
                        var componentInInstance = dstCompanionGameObjects[i].GetComponent(componentType);
                        var dstIndex = src * instanceCount + i;
                        managedComponentStore.SetManagedComponentValue(dstArray[dstIndex], componentInInstance);
                    }
                }
            }
            else
            {
                for (int src = 0; src < componentCount; ++src)
                {
                    var component = managedComponentStore.GetManagedComponent(srcArray[src]);

                    for (int i = 0; i < instanceCount; i++)
                    {
                        var dstIndex = src * instanceCount + i;
                        managedComponentStore.SetManagedComponentValue(dstArray[dstIndex], component);
                    }
                }
            }
        }

        static void AssignCompanionComponentsToCompanionGameObjectsDelegate(EntityManager entityManager, NativeArray<Entity> entities)
        {
            for (int i = 0; i < entities.Length; ++i)
            {
                var entity = entities[i];
                var companionGameObject = entityManager.GetComponentData<CompanionLink>(entity).Companion.Value;

                // Add a CompanionReference
                entityManager.AddComponentObject(entity, new CompanionReference { Companion = companionGameObject });

                var archetypeChunk = entityManager.GetChunk(entities[i]);
                var archetype = archetypeChunk.Archetype.Archetype;

                var types = archetype->Types;
                var firstIndex = archetype->FirstManagedComponent;
                var lastIndex = archetype->ManagedComponentsEnd;

                for (int t = firstIndex; t < lastIndex; ++t)
                {
                    ref readonly var type = ref TypeManager.GetTypeInfo(types[t].TypeIndex);

                    if (type.Category != TypeManager.TypeCategory.UnityEngineObject)
                        continue;

                    var companionComponent = companionGameObject.GetComponent(type.Type);
                    entityManager.SetComponentObject(entity, ComponentType.FromTypeIndex(type.TypeIndex), companionComponent);
                }
            }

            entityManager.RemoveComponent<CompanionGameObjectActiveCleanup>(entities);
        }
    }
}
#endif
