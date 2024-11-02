using System.Collections.Generic;
using Unity.Collections;
using Unity.Entities;
using Unity.Entities.Serialization;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;
using UnityObject = UnityEngine.Object;

namespace Unity.Scenes
{
    /// <summary>
    /// Utility class for serializing and deserializing <see cref="World"/> objects and the associated UnityEngine.Object references.
    /// </summary>
    [MovedFrom(true, "Unity.Entities.Serialization", "Unity.Entities.Hybrid")]
    public static class SerializeUtilityHybrid
    {
        /// <summary>
        /// Serializes a <see cref="World"/> using a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="manager">The <see cref="EntityManager"/> of the serialized world.</param>
        /// <param name="writer">The serialization object.</param>
        /// <param name="objRefs">Contains the UnityEngine.Object references extracted during serialization.</param>
        public static void Serialize(EntityManager manager, BinaryWriter writer, out ReferencedUnityObjects objRefs)
        {
            SerializeUtility.SerializeWorld(manager, writer, out var referencedObjects);
            SerializeObjectReferences((UnityEngine.Object[])referencedObjects, out objRefs);
        }

        /// <summary>
        /// Serializes a <see cref="World"/> using a <see cref="BinaryWriter"/>.
        /// </summary>
        /// <param name="manager">The <see cref="EntityManager"/> of the serialized world.</param>
        /// <param name="writer">The serialization object.</param>
        /// <param name="objRefs">Contains the UnityEngine.Object references extracted during serialization.</param>
        /// <param name="entityRemapInfos">Entity remapping which is applied during serialization.</param>
        public static void Serialize(EntityManager manager, BinaryWriter writer, out ReferencedUnityObjects objRefs, NativeArray<EntityRemapUtility.EntityRemapInfo> entityRemapInfos)
        {
            SerializeUtility.SerializeWorld(manager, writer, out var referencedObjects, entityRemapInfos);
            SerializeObjectReferences((UnityEngine.Object[])referencedObjects, out objRefs);
        }

        /// <summary>
        /// Deserializes a <see cref="World"/> object.
        /// </summary>
        /// <param name="manager">The <see cref="EntityManager"/> of the deserialized world.</param>
        /// <param name="reader">The deserialization object.</param>
        /// <param name="objRefs">The UnityEngine.Object references that are patched in during deserialization.</param>
        public static void Deserialize(EntityManager manager, BinaryReader reader, ReferencedUnityObjects objRefs)
        {
            DeserializeObjectReferences(objRefs, out var objectReferences);
            var transaction = manager.BeginExclusiveEntityTransaction();
            SerializeUtility.DeserializeWorld(transaction, reader, objectReferences);
            manager.EndExclusiveEntityTransaction();
        }

        /// <summary>
        /// Serializes an array of UnityEngine.Object references as a ScriptableObject.
        /// </summary>
        /// <param name="referencedObjects">The array of UnityEngine.Object references.</param>
        /// <param name="objRefs">The ScriptableObject containing the serialized result.</param>
        public static void SerializeObjectReferences(UnityEngine.Object[] referencedObjects, out ReferencedUnityObjects objRefs)
        {
            objRefs = null;

            if (referencedObjects?.Length > 0)
            {
                objRefs = UnityEngine.ScriptableObject.CreateInstance<ReferencedUnityObjects>();
                objRefs.Array = referencedObjects;

                var companionObjectIndices = new List<int>();

                for (int i = 0; i != objRefs.Array.Length; i++)
                {
                    var obj = objRefs.Array[i];
                    if (obj != null && obj is GameObject gameObject)
                    {
                        if (gameObject.scene.IsValid())
                        {
                            // Add companion entry, this allows us to differentiate Prefab references and Companion Objects at runtime deserialization
                            companionObjectIndices.Add(i);
                        }
                    }
                }

                objRefs.CompanionObjectIndices = companionObjectIndices.ToArray();
            }
        }

        /// <summary>
        /// Deserializes a <see cref="ReferencedUnityObjects"/> object, returning the array of UnityEngine.Object references.
        /// </summary>
        /// <param name="objRefs">The serialized UnityEngine.Object references.</param>
        /// <param name="objectReferences">The array of UnityEngine.Object references to be applied on the deserialized <see cref="World"/> object.</param>
        public static void DeserializeObjectReferences(ReferencedUnityObjects objRefs, out UnityEngine.Object[] objectReferences)
        {
            if (objRefs == null)
            {
                objectReferences = null;
                return;
            }

            objectReferences = new UnityEngine.Object[objRefs.Array.Length];

            // NOTE: Object references must not include fake object references, they must be real null.
            // The Unity.Properties deserializer can't handle them correctly.
            // We might want to add support for handling fake null,
            // but it would require tight integration in the deserialize function so that a correct fake null unityengine.object can be constructed on deserialize
            for (int i = 0; i != objRefs.Array.Length; i++)
            {
                if (objRefs.Array[i] != null)
                    objectReferences[i] = objRefs.Array[i];
            }

#if UNITY_EDITOR && !UNITY_DISABLE_MANAGED_COMPONENTS
            foreach (var companionIndex in objRefs.CompanionObjectIndices)
            {
                var source = (UnityEngine.GameObject) objectReferences[companionIndex];
                CompanionGameObjectUtility.SetCompanionFlags(source);
            }
#else
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            NativeArray<int> newGameObjects = new NativeArray<int>(objRefs.CompanionObjectIndices.Length, Allocator.Temp);
            NativeHashMap<int, int> sourceInstanceIDToNewIndex = new NativeHashMap<int, int>(objRefs.CompanionObjectIndices.Length, Allocator.Temp);
            UnityEngine.GameObject[] newGameObjectsArray = new GameObject[objRefs.CompanionObjectIndices.Length];

            for (int i = 0; i < objRefs.CompanionObjectIndices.Length; i++)
            {
                var companionIndex = objRefs.CompanionObjectIndices[i];
                var source = (UnityEngine.GameObject)objectReferences[companionIndex];
                var sourceInstanceId = source.GetInstanceID();

                var newGameObject = UnityEngine.Object.Instantiate(source);
                var newGameObjectInstanceId = newGameObject.GetInstanceID();
                objectReferences[companionIndex] = newGameObject;
                newGameObjects[i] = newGameObjectInstanceId;
                newGameObjectsArray[i] = newGameObject;

                sourceInstanceIDToNewIndex[sourceInstanceId] = i;
            }

            for (int i = 0; i != objectReferences.Length; i++)
            {
                if (objectReferences[i] is UnityEngine.Component component)
                {
                    var newGameObjectIndex = sourceInstanceIDToNewIndex[component.gameObject.GetInstanceID()];
                    var newGameObject = newGameObjectsArray[newGameObjectIndex];
                    objectReferences[i] = newGameObject.GetComponent(component.GetType());
                }
            }
            UnityEngine.SceneManagement.SceneManager.MoveGameObjectsToScene(newGameObjects, scene);

            newGameObjects.Dispose();
            sourceInstanceIDToNewIndex.Dispose();
#endif
        }
    }
}
