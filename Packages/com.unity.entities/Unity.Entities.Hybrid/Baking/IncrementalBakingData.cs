using System;
using System.Collections.Generic;
using Unity.Collections;
using UnityEngine;

namespace Unity.Entities.Baking
{
    struct IncrementalBakingData : IDisposable
    {
        public struct ChangedComponentsInfo
        {
            public int       instanceID;
            public TypeIndex unityTypeIndex;
        }

        public struct GameObjectProperties
        {
            public int InstanceID;
            public int NameHash;
            public int TagHash;
            public int Layer;

            public static GameObjectProperties CalculateProperties(GameObject gameObject)
            {
                IncrementalBakingData.GameObjectProperties properties = new IncrementalBakingData.GameObjectProperties()
                {
                    InstanceID = gameObject.GetInstanceID(),
                    NameHash = gameObject.name.GetHashCode(),
                    Layer = gameObject.layer,
                    TagHash = gameObject.tag.GetHashCode()
                };
                return properties;
            }
        }

        public enum ChangedGameObjectMode
        {
            Normal,
            ForceBake,
            RecreateAll
        }

        public NativeList<int> ChangedAssets;
        public NativeList<int> DeletedAssets;
        public NativeList<int> RemovedGameObjects;
        public List<(GameObject gameObject, ChangedGameObjectMode mode)> ChangedGameObjects;
        public NativeList<ChangedComponentsInfo> ChangedComponents;

        public NativeList<GameObjectProperties> ChangedGameObjectProperties;
        public NativeList<IncrementalBakingChanges.ParentChange> ParentChangeInstanceIds;
        public NativeList<int> ParentWithChildrenOrderChangedInstanceIds;
        public bool LightBakingChanged;

        public bool HasStructuralChanges()
        {
            return !DeletedAssets.IsEmpty || !RemovedGameObjects.IsEmpty || ChangedGameObjects.Count != 0 || !ParentChangeInstanceIds.IsEmpty || !ParentWithChildrenOrderChangedInstanceIds.IsEmpty;
        }

        public static IncrementalBakingData Create()
        {
            return new IncrementalBakingData
            {
                RemovedGameObjects = new NativeList<int>(Allocator.Persistent),
                ChangedGameObjects = new List<(GameObject gameObject, ChangedGameObjectMode mode)>(),
                ChangedComponents = new NativeList<ChangedComponentsInfo>(Allocator.Persistent),
                ChangedAssets = new NativeList<int>(Allocator.Persistent),
                DeletedAssets = new NativeList<int>(Allocator.Persistent),
                ChangedGameObjectProperties = new NativeList<GameObjectProperties>(Allocator.Persistent),
                ParentChangeInstanceIds = new NativeList<IncrementalBakingChanges.ParentChange>(Allocator.Persistent),
                ParentWithChildrenOrderChangedInstanceIds = new NativeList<int>(Allocator.Persistent),
                LightBakingChanged = false
            };
        }

        public void Clear()
        {
            ChangedAssets.Clear();
            DeletedAssets.Clear();
            RemovedGameObjects.Clear();
            ChangedGameObjects.Clear();
            ChangedComponents.Clear();
            ChangedGameObjectProperties.Clear();
            ParentChangeInstanceIds.Clear();
            ParentWithChildrenOrderChangedInstanceIds.Clear();
            LightBakingChanged = false;
        }

        public void Dispose()
        {
            if (RemovedGameObjects.IsCreated)
                RemovedGameObjects.Dispose();
            if (ChangedComponents.IsCreated)
                ChangedComponents.Dispose();
            if (ChangedAssets.IsCreated)
                ChangedAssets.Dispose();
            if (DeletedAssets.IsCreated)
                DeletedAssets.Dispose();
            if (ChangedGameObjectProperties.IsCreated)
                ChangedGameObjectProperties.Dispose();
            if (ParentChangeInstanceIds.IsCreated)
                ParentChangeInstanceIds.Dispose();
            if (ParentWithChildrenOrderChangedInstanceIds.IsCreated)
                ParentWithChildrenOrderChangedInstanceIds.Dispose();
        }
    }

        /// <summary>
    /// Contains a summary of all changes that happened since the last conversion.
    /// ATTENTION: This is future public API.
    /// </summary>
    internal struct IncrementalBakingChanges
    {
        /// <summary>
        /// Contains all GameObjects that were changed in some way since the last conversion. This includes changes
        /// to the name, enabled/disabled state, addition or removal of components, and newly created GameObjects.
        /// This does not include GameObjects for which only the data on a component was changed or whose place in the
        /// hierarchy has changed.
        /// </summary>
        public IReadOnlyList<GameObject> ChangedGameObjects;

        /// <summary>
        /// Contains the instance ID of all GameObjects in <see cref="ChangedGameObjects"/>.
        /// </summary>
        public NativeArray<int>.ReadOnly ChangedGameObjectsInstanceIds;

        /// <summary>
        /// Contains all Components that were changed in some way since the last conversion. This does not include new
        /// components by default, only components that were actually changed.
        /// </summary>
        public IReadOnlyList<Component> ChangedComponents;

        /// <summary>
        /// Contains the instance IDs of all GameObjects whose parents have changed.
        /// </summary>
        public NativeArray<ParentChange>.ReadOnly ParentChanges;

        /// <summary>
        /// Describes how a game object's parenting has changed.
        /// </summary>
        public struct ParentChange
        {
            /// <summary>
            /// The instance id of the game object whose parenting has changed.
            /// </summary>
            public int InstanceId;
            /// <summary>
            /// The instance if of the game object that was the previous parent.
            /// </summary>
            public int PreviousParentInstanceId;
            /// <summary>
            /// The instance if of the game object that is the new parent.
            /// </summary>
            public int NewParentInstanceId;
        }

        public void CollectGameObjectsWithComponentChange<T>(NativeList<int> instanceIDs) where T : Component
        {
            var changes = ChangedComponents;
            for (int i = 0; i < changes.Count; i++)
            {
                if (changes[i] is T)
                {
                    instanceIDs.Add(changes[i].gameObject.GetInstanceID());
                }
            }
        }

        /// <summary>
        /// Contains the instance IDs of all GameObjects that were removed since the last conversion.
        /// An object might be removed because it was deleted or moved to another scene.
        /// </summary>
        public NativeArray<int>.ReadOnly RemovedGameObjectInstanceIds;
    }
}
