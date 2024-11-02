using System;
using Unity.Collections;
using System.Collections.Generic;
using System.Text;
using System.Xml;
#if UNITY_EDITOR
using Unity.Entities.Build;
#endif
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Entities.Baking;
using UnityEngine;
using Unity.Entities.Conversion;
using Unity.Mathematics;

namespace Unity.Entities
{
    /// <summary>
    /// This type is used to identify identities that had any baking done on them before the BakingSystems ran
    /// </summary>
    [TemporaryBakingType]
    public struct BakedEntity : IComponentData {}

    /// <summary>
    /// This class contains all the methods to bake a authoring component to an Entity.
    /// </summary>
    public abstract unsafe class IBaker
    {
        internal abstract void InvokeBake(in BakerExecutionState state);
        internal abstract Type GetAuthoringType();
        private const bool kDefaultIncludeInactive = true;

        /// <summary>
        /// Represents the execution state of the Baker
        /// </summary>
        internal struct BakerExecutionState
        {
            internal Component                                AuthoringSource;
            internal GameObject                               AuthoringObject;
            internal int                                      AuthoringId;
            internal BakerState*                              BakerState;
            internal BakerEntityUsage*                        Usage;
            internal BakeDependencies.RecordedDependencies*   Dependencies;
            internal BakerDebugState*                         DebugState;
            internal BakedEntityData*                         BakedEntityData;
            internal Entity                                   PrimaryEntity;
            internal EntityCommandBuffer                      Ecb;
            internal BakerDebugState.DebugState               DebugIndex;
            internal BlobAssetStore                           BlobAssetStore;
            internal World                                    World;
#if UNITY_EDITOR
            internal IEntitiesPlayerSettings                  DotsSettings;
#endif

            /// <summary>Exists to give context to error messages.</summary>
            internal string ErrorContextString()
            {
                if (!AuthoringObject)
                    return $"Baked in World '{World.Name}'."; // World at least gives some context. E.g. With incremental baking, it'll include scene name.
                var scene = AuthoringObject.scene; // Scene can be null (i.e. Invalid) when the AuthoringObject is a prefab that is only referenced in a SubScene.
                var sceneInfo = scene.IsValid() && !string.IsNullOrWhiteSpace(scene.path) ? $" in {(scene.isSubScene ? "SubScene" : "Scene")} '{scene.path}'" : "";
                return $"GameObject '{AuthoringObject.name}'{sceneInfo}, baked in World '{World.Name}'.";
            }
        }

        internal BakerExecutionState  _State;

        /// <summary>
        /// Get the GUID of the current scene
        /// </summary>
        /// <returns>The scene GUID</returns>
        public Hash128 GetSceneGUID()
        {
            return _State.BakedEntityData->_SceneGUID;
        }

#region GetComponent Methods

        /// <summary>
        /// Retrieves the component of Type T in the GameObject
        /// </summary>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponent<T>() where T : Component
        {
            var gameObject = _State.AuthoringObject;
            return GetComponentInternal<T>(gameObject);
        }

        /// <summary>
        /// Retrieves the component of Type T in the GameObject
        /// </summary>
        /// <param name="component">The Object to get the component from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponent<T>(Component component) where T : Component
        {
            return GetComponentInternal<T>(component.gameObject);
        }

        /// <summary>
        /// Retrieves the component of Type T in the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the component from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponent<T>(GameObject gameObject) where T : Component
        {
            return GetComponentInternal<T>(gameObject);
        }

        /// <summary>
        /// Retrieves the component of Type T in the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the component from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        private T GetComponentInternal<T>(GameObject gameObject) where T : Component
        {
            var hasComponent = gameObject.TryGetComponent<T>(out var returnedComponent);

            _State.Dependencies->DependOnGetComponent(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), hasComponent ? returnedComponent.GetInstanceID() : 0, BakeDependencies.GetComponentDependencyType.GetComponent);

            // Transform component takes an implicit dependency on the entire parent hierarchy
            // since transform.position and friends returns a value calculated from all parents
            var transform = returnedComponent as Transform;
            if (transform != null)
                _State.Dependencies->DependOnParentTransformHierarchy(transform);

            return returnedComponent;
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject
        /// </summary>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponents<T>(List<T> components) where T : Component
        {
            var gameObject = _State.AuthoringObject;
            GetComponents<T>(gameObject, components);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject
        /// </summary>
        /// <param name="component">The Object to get the components from</param>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponents<T>(Component component, List<T> components) where T : Component
        {
            GetComponents<T>(component.gameObject, components);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the components from</param>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponents<T>(GameObject gameObject, List<T> components) where T : Component
        {
            gameObject.GetComponents<T>(components);

            _State.Dependencies->DependOnGetComponents(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), components, BakeDependencies.GetComponentDependencyType.GetComponent);

            foreach (var component in components)
            {
                // Transform component takes an implicit dependency on the entire parent hierarchy
                // since transform.position and friends returns a value calculated from all parents
                var transform = component as Transform;
                if (transform != null)
                    _State.Dependencies->DependOnParentTransformHierarchy(transform);
            }
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject
        /// </summary>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponents<T>() where T : Component
        {
            var gameObject = _State.AuthoringObject;
            return GetComponents<T>(gameObject);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject
        /// </summary>
        /// <param name="component">The Object to get the components from</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponents<T>(Component component) where T : Component
        {
            return GetComponents<T>(component.gameObject);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the components from</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponents<T>(GameObject gameObject) where T : Component
        {
            var components = gameObject.GetComponents<T>();

            _State.Dependencies->DependOnGetComponents(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), components, BakeDependencies.GetComponentDependencyType.GetComponent);

            foreach (var component in components)
            {
                // Transform component takes an implicit dependency on the entire parent hierarchy
                // since transform.position and friends returns a value calculated from all parents
                var transform = component as Transform;
                if (transform != null)
                    _State.Dependencies->DependOnParentTransformHierarchy(transform);
            }
            return components;
        }

        /// <summary>
        /// Retrieves the component of Type T in the GameObject or any of its parents
        /// </summary>
        /// <typeparam name="T">The type of Component to retrieve</typeparam>
        /// <returns>Returns a component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponentInParent<T>() where T : Component
        {
            return GetComponentInParent<T>(_State.AuthoringObject);
        }

        /// <summary>
        /// Retrieves the component of Type T in the GameObject or any of its parents
        /// </summary>
        /// <param name="component">The Object to get the component from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponentInParent<T>(Component component) where T : Component
        {
            return GetComponentInParent<T>(component.gameObject);
        }

        /// <summary>
        /// Retrieves the component of Type T in the GameObject or any of its parents
        /// </summary>
        /// <param name="gameObject">The GameObject to get the component from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponentInParent<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponentInParent<T>(kDefaultIncludeInactive);

            _State.Dependencies->DependOnGetComponent(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), component != null ? component.GetInstanceID() : 0, BakeDependencies.GetComponentDependencyType.GetComponentInParent);

            // Transform component takes an implicit dependency on the entire parent hierarchy
            // since transform.position and friends returns a value calculated from all parents
            var transform = component as Transform;
            if (transform != null)
                _State.Dependencies->DependOnParentTransformHierarchy(transform);

            return component;
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject or any of its parents. Works recursively.
        /// </summary>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponentsInParent<T>(List<T> components) where T : Component
        {
            GetComponentsInParent<T>(_State.AuthoringObject, components);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject or any of its parents. Works recursively.
        /// </summary>
        /// <param name="component">The Object to get the components from</param>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponentsInParent<T>(Component component, List<T> components) where T : Component
        {
            GetComponentsInParent<T>(component.gameObject, components);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject or any of its parents. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the components from</param>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponentsInParent<T>(GameObject gameObject, List<T> components) where T : Component
        {
            gameObject.GetComponentsInParent<T>(kDefaultIncludeInactive, components);

            _State.Dependencies->DependOnGetComponents(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), components, BakeDependencies.GetComponentDependencyType.GetComponentInParent);

            foreach (var component in components)
            {
                // Transform component takes an implicit dependency on the entire parent hierarchy
                // since transform.position and friends returns a value calculated from all parents
                var transform = component as Transform;
                if (transform != null)
                    _State.Dependencies->DependOnParentTransformHierarchy(transform);
            }
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject or any of its parents. Works recursively.
        /// </summary>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponentsInParent<T>() where T : Component
        {
            return GetComponentsInParent<T>(_State.AuthoringObject);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject or any of its parents. Works recursively.
        /// </summary>
        /// <param name="component">The Object to get the components from</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponentsInParent<T>(Component component) where T : Component
        {
            return GetComponentsInParent<T>(component.gameObject);
        }

        /// <summary>
        /// Returns all components of Type T in the GameObject or any of its parents. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the components from</param>
        /// <typeparam name="T">The type of components to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponentsInParent<T>(GameObject gameObject) where T : Component
        {
            var components = gameObject.GetComponentsInParent<T>(kDefaultIncludeInactive);
            _State.Dependencies->DependOnGetComponents(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), components, BakeDependencies.GetComponentDependencyType.GetComponentInParent);

            foreach (var component in components)
            {
                // Transform component takes an implicit dependency on the entire parent hierarchy
                // since transform.position and friends returns a value calculated from all parents
                var transform = component as Transform;
                if (transform != null)
                    _State.Dependencies->DependOnParentTransformHierarchy(transform);
            }
            return components;
        }

        /// <summary>
        /// Returns the component of Type T in the GameObject or any of its children using depth first search
        /// </summary>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponentInChildren<T>() where T : Component
        {
            var gameObject = _State.AuthoringObject;
            return GetComponentInChildren<T>(gameObject);
        }

        /// <summary>
        /// Returns the component of Type T in the GameObject or any of its children using depth first search
        /// </summary>
        /// <param name="component">The Object to get the component from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponentInChildren<T>(Component component) where T : Component
        {
            return GetComponentInChildren<T>(component.gameObject);
        }

        /// <summary>
        /// Returns the component of Type T in the GameObject or any of its children using depth first search
        /// </summary>
        /// <param name="gameObject">The GameObject to get the component from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The component if a component matching the type is found, null otherwise</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public T GetComponentInChildren<T>(GameObject gameObject) where T : Component
        {
            var component = gameObject.GetComponentInChildren<T>(kDefaultIncludeInactive);

            _State.Dependencies->DependOnGetComponent(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), component != null ? component.GetInstanceID() : 0, BakeDependencies.GetComponentDependencyType.GetComponentInChildren);

            // Transform component takes an implicit dependency on the entire parent hierarchy
            // since transform.position and friends returns a value calculated from all parents
            var transform = component as Transform;
            if (transform != null)
                _State.Dependencies->DependOnParentTransformHierarchy(transform);

            return component;
        }

        /// <summary>
        /// Returns all components of Type type in the GameObject or any of its children using depth first search. Works recursively.
        /// </summary>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        public void GetComponentsInChildren<T>(List<T> components) where T : Component
        {
            GetComponentsInChildren<T>(_State.AuthoringObject, components);
        }

        /// <summary>
        /// Returns all components of Type type in the GameObject or any of its children using depth first search. Works recursively.
        /// </summary>
        /// <param name="refComponent">The Object to get the components from</param>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponentsInChildren<T>(Component refComponent, List<T> components) where T : Component
        {
            GetComponentsInChildren<T>(refComponent.gameObject, components);
        }

        /// <summary>
        /// Returns all components of Type type in the GameObject or any of its children using depth first search. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the components from</param>
        /// <param name="components">The components of Type T</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <remarks>This will take a dependency on the components</remarks>
        public void GetComponentsInChildren<T>(GameObject gameObject, List<T> components) where T : Component
        {
            gameObject.GetComponentsInChildren(kDefaultIncludeInactive, components);

            _State.Dependencies->DependOnGetComponents(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), components, BakeDependencies.GetComponentDependencyType.GetComponentInChildren);

            foreach (var component in components)
            {
                // Transform component takes an implicit dependency on the entire parent hierarchy
                // since transform.position and friends returns a value calculated from all parents
                var transform = component as Transform;
                if (transform != null)
                    _State.Dependencies->DependOnParentTransformHierarchy(transform);
            }
        }

        /// <summary>
        /// Returns all components of Type type in the GameObject or any of its children using depth first search. Works recursively.
        /// </summary>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponentsInChildren<T>() where T : Component
        {
            return GetComponentsInChildren<T>(_State.AuthoringObject);
        }

        /// <summary>
        /// Returns all components of Type type in the GameObject or any of its children using depth first search. Works recursively.
        /// </summary>
        /// <param name="component">The Object to get the components from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponentsInChildren<T>(Component component) where T : Component
        {
            return GetComponentsInChildren<T>(component.gameObject);
        }

        /// <summary>
        /// Returns all components of Type type in the GameObject or any of its children using depth first search. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the components from</param>
        /// <typeparam name="T">The type of component to retrieve</typeparam>
        /// <returns>The components of Type T</returns>
        /// <remarks>This will take a dependency on the components</remarks>
        public T[] GetComponentsInChildren<T>(GameObject gameObject) where T : Component
        {
            var components = gameObject.GetComponentsInChildren<T>(kDefaultIncludeInactive);

            _State.Dependencies->DependOnGetComponents(gameObject.GetInstanceID(), TypeManager.GetTypeIndex<T>(), components, BakeDependencies.GetComponentDependencyType.GetComponentInChildren);

            foreach (var component in components)
            {
                // Transform component takes an implicit dependency on the entire parent hierarchy
                // since transform.position and friends returns a value calculated from all parents
                var transform = component as Transform;
                if (transform != null)
                    _State.Dependencies->DependOnParentTransformHierarchy(transform);
            }
            return components;
        }

#endregion

#region Get Methods

        /// <summary>
        /// Returns the parent of the GameObject
        /// </summary>
        /// <returns>The parent if one is found or null otherwise</returns>
        /// <remarks>This will take a dependency on the parent</remarks>
        public GameObject GetParent()
        {
            return GetParent(_State.AuthoringObject);
        }

        /// <summary>
        /// Returns the parent of the GameObject
        /// </summary>
        /// <param name="component">The Object to get the parent from</param>
        /// <returns>The parent if one is found or null otherwise</returns>
        /// <remarks>This will take a dependency on the parent</remarks>
        public GameObject GetParent(Component component)
        {
            return GetParent(component.gameObject);
        }

        /// <summary>
        /// Returns the parent of the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the parent from</param>
        /// <returns>The parent if one is found or null otherwise</returns>
        /// <remarks>This will take a dependency on the parent</remarks>
        public GameObject GetParent(GameObject gameObject)
        {
            var parentTransform = gameObject.transform.parent;
            GameObject parent = null;
            if (parentTransform)
            {
                parent = parentTransform.gameObject;
            }

            _State.Dependencies->DependOnGetHierarchySingle(gameObject.GetInstanceID(), parent != null ? parent.GetInstanceID() : 0, 0, BakeDependencies.GetHierarchySingleDependencyType.Parent);

            return parent;
        }

        /// <summary>
        /// Returns the parents of the GameObject
        /// </summary>
        /// <returns>The parents of the GameObject</returns>
        /// <remarks>This will take a dependency on the parents</remarks>
        public GameObject[] GetParents()
        {
            return GetParents(_State.AuthoringObject);
        }

        /// <summary>
        /// Returns the parents of the GameObject
        /// </summary>
        /// <param name="component">The Object to get the parents from</param>
        /// <returns>The parents of the GameObject</returns>
        /// <remarks>This will take a dependency on the parents</remarks>
        public GameObject[] GetParents(Component component)
        {
            return GetParents(component.gameObject);
        }

        /// <summary>
        /// Returns the parents of the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the parents from</param>
        /// <returns>The parents of the GameObject</returns>
        /// <remarks>This will take a dependency on the parents</remarks>
        public GameObject[] GetParents(GameObject gameObject)
        {
            List<GameObject> result = new List<GameObject>();
            GetParents(gameObject, result);
            return result.ToArray();
        }

        /// <summary>
        /// Returns the parents of the GameObject
        /// </summary>
        /// <param name="parents">The parents of the GameObject</param>
        /// <remarks>This will take a dependency on the parents</remarks>
        public void GetParents(List<GameObject> parents)
        {
            GetParents(_State.AuthoringObject, parents);
        }

        /// <summary>
        /// Returns the parents of the GameObject
        /// </summary>
        /// <param name="component">The Object to get the parents from</param>
        /// <param name="parents">The parents of the GameObject</param>
        /// <remarks>This will take a dependency on the parents</remarks>
        public void GetParents(Component component, List<GameObject> parents)
        {
            GetParents(component.gameObject, parents);
        }

        /// <summary>
        /// Returns the parents of the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the parents from</param>
        /// <param name="parents">The parents of the GameObject</param>
        /// <remarks>This will take a dependency on the parents</remarks>
        public void GetParents(GameObject gameObject, List<GameObject> parents)
        {
            parents.Clear();
            Transform parentTransform = gameObject.transform.parent;
            while (parentTransform != null)
            {
                parents.Add(parentTransform.gameObject);
                parentTransform = parentTransform.parent;
            }

            _State.Dependencies->DependOnGetHierarchy(gameObject.GetInstanceID(), parents, BakeDependencies.GetHierarchyDependencyType.Parent);
        }

        /// <summary>
        /// Returns the child of the GameObject
        /// </summary>
        /// <param name="childIndex">The index of the child to return</param>
        /// <returns>The child with matching index if found, null otherwise</returns>
        /// <remarks>This will take a dependency on the child</remarks>
        public GameObject GetChild(int childIndex)
        {
            return GetChild(_State.AuthoringObject, childIndex);
        }

        /// <summary>
        /// Returns the child of the GameObject
        /// </summary>
        /// <param name="component">The Object to get the child from</param>
        /// <param name="childIndex">The index of the child to return</param>
        /// <returns>The child with matching index if found, null otherwise</returns>
        /// <remarks>This will take a dependency on the child</remarks>
        public GameObject GetChild(Component component, int childIndex)
        {
            return GetChild(component.gameObject, childIndex);
        }

        /// <summary>
        /// Returns the child of the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the child from</param>
        /// <param name="childIndex">The index of the child to return</param>
        /// <returns>The child with matching index if found, null otherwise</returns>
        /// <remarks>This will take a dependency on the child</remarks>
        public GameObject GetChild(GameObject gameObject, int childIndex)
        {
            var childTransform = gameObject.transform.GetChild(childIndex);
            GameObject child = null;
            if (childTransform)
            {
                child = childTransform.gameObject;
            }

            _State.Dependencies->DependOnGetHierarchySingle(gameObject.GetInstanceID(), child != null ? child.GetInstanceID() : 0, 0, BakeDependencies.GetHierarchySingleDependencyType.Child);

            return child;
        }

        /// <summary>
        /// Returns the children of the GameObject
        /// </summary>
        /// <param name="includeChildrenRecursively">Whether all children in the hierarchy should be added recursively</param>
        /// <returns>The children of the GameObject</returns>
        /// <remarks>This will take a dependency on the children</remarks>
        public GameObject[] GetChildren(bool includeChildrenRecursively = false)
        {
            return GetChildren(_State.AuthoringObject, includeChildrenRecursively);
        }

        /// <summary>
        /// Returns the children of the GameObject
        /// </summary>
        /// <param name="component">The Object to get the children from</param>
        /// <param name="includeChildrenRecursively">Whether all children in the hierarchy should be added recursively</param>
        /// <returns>The children of the GameObject</returns>
        /// <remarks>This will take a dependency on the children</remarks>
        public GameObject[] GetChildren(Component component, bool includeChildrenRecursively = false)
        {
            return GetChildren(component.gameObject, includeChildrenRecursively);
        }

        /// <summary>
        /// Returns the children of the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the children from</param>
        /// <param name="includeChildrenRecursively">Whether all children in the hierarchy should be added recursively</param>
        /// <returns>The children of the GameObject</returns>
        /// <remarks>This will take a dependency on the children</remarks>
        public GameObject[] GetChildren(GameObject gameObject, bool includeChildrenRecursively = false)
        {
            List<GameObject> gameObjectList = new List<GameObject>();
            GetChildren(gameObject, gameObjectList, includeChildrenRecursively);
            return gameObjectList.ToArray();
        }

        /// <summary>
        /// Returns the children of the GameObject
        /// </summary>
        /// <param name="gameObjects">The children of the GameObject</param>
        /// <param name="includeChildrenRecursively">Whether all children in the hierarchy should be added recursively</param>
        /// <remarks>This will take a dependency on the children</remarks>
        public void GetChildren(List<GameObject> gameObjects, bool includeChildrenRecursively = false)
        {
            GetChildren(_State.AuthoringObject, gameObjects, includeChildrenRecursively);
        }

        /// <summary>
        /// Returns the children of the GameObject
        /// </summary>
        /// <param name="refComponent">The Object to get the children from</param>
        /// <param name="gameObjects">The children of the GameObject</param>
        /// <param name="includeChildrenRecursively">Whether all children in the hierarchy should be added recursively</param>
        /// <remarks>This will take a dependency on the children</remarks>
        public void GetChildren(Component refComponent, List<GameObject> gameObjects, bool includeChildrenRecursively = false)
        {
            GetChildren(refComponent.gameObject, gameObjects, includeChildrenRecursively);
        }

        /// <summary>
        /// Returns the children of the GameObject
        /// </summary>
        /// <param name="gameObject">The GameObject to get the children from</param>
        /// <param name="gameObjects">The children of the GameObject</param>
        /// <param name="includeChildrenRecursively">Whether all children in the hierarchy should be added recursively</param>
        /// <remarks>This will take a dependency on the children</remarks>
        public void GetChildren(GameObject gameObject, List<GameObject> gameObjects, bool includeChildrenRecursively = false)
        {
            gameObjects.Clear();

            if (!includeChildrenRecursively)
            {
                Transform rootTransform = gameObject.transform;
                foreach (Transform child in rootTransform)
                {
                    gameObjects.Add(child.gameObject);
                }
                _State.Dependencies->DependOnGetHierarchy(gameObject.GetInstanceID(), gameObjects, BakeDependencies.GetHierarchyDependencyType.ImmediateChildren);
            }
            else
            {
                var transforms = gameObject.GetComponentsInChildren<Transform>(true);
                // Skipping first one intentionally, as it is from the gameObject itself
                for (int index = 1; index < transforms.Length; ++index)
                {
                    gameObjects.Add(transforms[index].gameObject);
                }
                _State.Dependencies->DependOnGetHierarchy(gameObject.GetInstanceID(), gameObjects, BakeDependencies.GetHierarchyDependencyType.AllChildren);
            }
        }

        /// <summary>
        /// Gets the number of children.
        /// </summary>
        /// <returns>Returns the number of children.</returns>
        /// <remarks>This takes a dependency on the child count.</remarks>
        public int GetChildCount()
        {
            return GetChildCount(_State.AuthoringObject);
        }

        /// <summary>
        /// Gets the number of children for a given component.
        /// </summary>
        /// <param name="component">The Object to get the child count from.</param>
        /// <returns>Returns the number of children.</returns>
        /// <remarks>This takes a dependency on the child count.</remarks>
        public int GetChildCount(Component component)
        {
            return GetChildCount(component.gameObject);
        }

        /// <summary>
        /// Gets the number of children for a given GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the child count from.</param>
        /// <returns>Returns the number of children.</returns>
        /// <remarks>This takes a dependency on the child count.</remarks>
        public int GetChildCount(GameObject gameObject)
        {
            var childCount = gameObject.transform.childCount;
            _State.Dependencies->DependOnGetHierarchySingle(gameObject.GetInstanceID(), childCount, 0, BakeDependencies.GetHierarchySingleDependencyType.ChildCount);
            return childCount;
        }

        /// <summary>
        /// Gets the name of the GameObject.
        /// </summary>
        /// <returns>Returns the name of the GameObject.</returns>
        /// <remarks>This takes a dependency on the name.</remarks>
        public string GetName()
        {
            return GetName(_State.AuthoringObject);
        }

        /// <summary>
        /// Gets the name of the GameObject for a given component.
        /// </summary>
        /// <param name="component">The Object to get the tag from.</param>
        /// <returns>Returns the name of the GameObject.</returns>
        /// <remarks>This takes a dependency on the name.</remarks>
        public string GetName(Component component)
        {
            return GetName(component.gameObject);
        }

        /// <summary>
        /// Gets the name of the GameObject for a given GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the name from.</param>
        /// <returns>Returns the name of the GameObject.</returns>
        /// <remarks>This takes a dependency on the name.</remarks>
        public string GetName(GameObject gameObject)
        {
            string name = gameObject.name;

            _State.Dependencies->DependOnObjectName(gameObject.GetInstanceID(), _State.AuthoringId, name);

            return name;
        }

        /// <summary>
        /// Gets the layer of the GameObject.
        /// </summary>
        /// <returns>Returns the layer of the GameObject.</returns>
        /// <remarks>This takes a dependency on the layer.</remarks>
        public int GetLayer()
        {
            return GetLayer(_State.AuthoringObject);
        }

        /// <summary>
        /// Gets the layer of the GameObject for a given component.
        /// </summary>
        /// <param name="component">The Object to get the tag from</param>
        /// <returns>Returns the layer of the GameObject.</returns>
        /// <remarks>This takes a dependency on the layer.</remarks>
        public int GetLayer(Component component)
        {
            return GetLayer(component.gameObject);
        }

        /// <summary>
        /// Gets the layer of the GameObject for a given GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the layer from</param>
        /// <returns>Returns the layer of the GameObject.</returns>
        /// <remarks>This takes a dependency on the layer.</remarks>
        public int GetLayer(GameObject gameObject)
        {
            int layer = gameObject.layer;

            _State.Dependencies->DependOnObjectLayer(gameObject.GetInstanceID(), _State.AuthoringId, layer);

            return layer;
        }

        /// <summary>
        /// Gets the tag of the GameObject.
        /// </summary>
        /// <returns>Returns the tag of the GameObject.</returns>
        /// <remarks>This takes a dependency on the tag.</remarks>
        public string GetTag()
        {
            return GetTag(_State.AuthoringObject);
        }

        /// <summary>
        /// Gets the tag of the GameObject for a given component.
        /// </summary>
        /// <param name="component">The Object to get the tag from.</param>
        /// <returns>Returns the tag of the GameObject.</returns>
        /// <remarks>This takes a dependency on the tag</remarks>
        public string GetTag(Component component)
        {
            return GetTag(component.gameObject);
        }

        /// <summary>
        /// Gets the tag of the GameObject for a given GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to get the tag from.</param>
        /// <returns>Returns the tag of the GameObject.</returns>
        /// <remarks>This takes a dependency on the tag</remarks>
        public string GetTag(GameObject gameObject)
        {
            string tag = gameObject.tag;

            _State.Dependencies->DependOnObjectTag(gameObject.GetInstanceID(), _State.AuthoringId, tag);

            return tag;
        }

        /// <summary>
        /// Returns the primary Entity
        /// </summary>
        /// <returns>The requested Entity</returns>
        /// <remarks>Implicitly it access the entity with TransformUsageFlags.Dynamic as TransformUsageFlags.</remarks>
        [Obsolete("Use the version of the function with the explicit TransformUsageFlag parameter (RemovedAfter Entities 1.0)")]
        public Entity GetEntity()
        {
            return GetEntity(TransformUsageFlags.Dynamic);
        }

        /// <summary>
        /// Returns the Entity associated with a GameObject
        /// </summary>
        /// <param name="authoring">The GameObject whose Entity is requested</param>
        /// <returns>The requested Entity if found, null otherwise</returns>
        /// <remarks>Implicitly it access the entity with TransformUsageFlags.Dynamic as TransformUsageFlags.</remarks>
        [Obsolete("Use the version of the function with the explicit TransformUsageFlag parameter (RemovedAfter Entities 1.0)")]
        public Entity GetEntity(GameObject authoring)
        {
            return GetEntity(authoring, TransformUsageFlags.Dynamic);
        }

        /// <summary>
        /// Returns the Entity associated with an Object
        /// </summary>
        /// <param name="authoring">The Object whose Entity is requested</param>
        /// <returns>The requested Entity if found, null otherwise</returns>
        /// <remarks>Implicitly it access the entity with TransformUsageFlags.Dynamic as TransformUsageFlags.</remarks>
        [Obsolete("Use the version of the function with the explicit TransformUsageFlag parameter (RemovedAfter Entities 1.0)")]
        public Entity GetEntity(Component authoring)
        {
            return GetEntity(authoring, TransformUsageFlags.Dynamic);
        }

        /// <summary>
        /// Returns the primary Entity
        /// </summary>
        /// <param name="flags">The flags to add to this Entity</param>
        /// <returns>The requested Entity</returns>
        public Entity GetEntity(TransformUsageFlags flags)
        {
            _State.Usage->PrimaryEntityFlags.Add(flags);
            return _State.PrimaryEntity;
        }

        /// <summary>
        /// Returns the Entity associated with a GameObject
        /// </summary>
        /// <param name="authoring">The GameObject whose Entity is requested</param>
        /// <param name="flags">The flags to add to this Entity</param>
        /// <returns>The requested Entity if found, null otherwise</returns>
        public Entity GetEntity(GameObject authoring, TransformUsageFlags flags)
        {
            if (authoring == null)
                return Entity.Null;

            if (_State.AuthoringObject == authoring)
            {
                _State.Usage->PrimaryEntityFlags.Add(flags);
                return _State.PrimaryEntity;
            }
            else
            {
                var entity = _State.BakedEntityData->GetEntity(authoring);
                _State.Usage->ReferencedEntityUsages.Add(new BakerEntityUsage.ReferencedEntityUsage(entity, flags));

#if UNITY_EDITOR
                if (authoring.IsPrefab())
                {
                    var prefabInstanceId = authoring.GetInstanceID();
                    if (!_State.BakerState->ReferencedPrefabs.Contains(prefabInstanceId))
                    {
                        _State.BakerState->ReferencedPrefabs.Add(prefabInstanceId);
                        _State.BakedEntityData->AddPrefabRef(prefabInstanceId);
                    }

                    DependsOn(authoring);
                }
#endif

                return entity;
            }
        }

        /// <summary>
        /// Returns the Entity associated with an Object
        /// </summary>
        /// <param name="authoring">The Object whose Entity is requested</param>
        /// <param name="flags">The flags to add to this Entity</param>
        /// <returns>The requested Entity if found, null otherwise</returns>
        public Entity GetEntity(Component authoring, TransformUsageFlags flags)
        {
            if (authoring == null)
                return Entity.Null;
            return GetEntity(authoring.gameObject, flags);
        }

        /// <summary>
        /// Returns the Entity for the baked game object without establishing a dependency.
        /// If no other bakes uses GetEntity to dependency this Entity, it will be stripped before baking pushes it into the live world.
        /// This is useful for attaching meta data that is safe to not deploy in the final game if the entity serves no other purpose.
        /// </summary>
        /// <returns>The Entity associated with the authoring component</returns>
        public Entity GetEntityWithoutDependency()
        {
            return _State.PrimaryEntity;
        }

#endregion

#region Check State

        /// <summary>
        /// Checks if the GameObject is active
        /// </summary>
        /// <returns>Returns true if the GameObject is active.</returns>
        /// <remarks>This takes a dependency on the active state.</remarks>
        public bool IsActive()
        {
            return IsActive(_State.AuthoringObject);
        }

        /// <summary>
        /// Checks if the GameObject is active for a given component.
        /// </summary>
        /// <param name="component">The Object to check.</param>
        /// <returns>Returns true if the GameObject is active.</returns>
        /// <remarks>This takes a dependency on the active state.</remarks>
        public bool IsActive(Component component)
        {
            return IsActive(component.gameObject);
        }

        /// <summary>
        /// Checks if the GameObject is active for a given GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to check.</param>
        /// <returns>Returns true if the GameObject is active.</returns>
        /// <remarks>This takes a dependency on the active state.</remarks>
        public bool IsActive(GameObject gameObject)
        {
            var isActive = UnityEngineExtensions.IsActiveIgnorePrefab(gameObject);

            _State.Dependencies->DependOnActive(gameObject.GetInstanceID(), _State.AuthoringId, isActive);

            return isActive;
        }

        /// <summary>
        /// Checks if the GameObject is active and enabled.
        /// </summary>
        /// <returns>Returns true if the GameObject is active and enabled.</returns>
        /// <remarks>This takes a dependency on the active and enable state</remarks>
        public bool IsActiveAndEnabled()
        {
            if (this is GameObjectBaker)
                throw new InvalidOperationException("The IsActiveAndEnabled() method cannot be called from a GameObjectBaker." +
                    $"If you need to depend on the GameObject active state, use IsActive() instead. {_State.ErrorContextString()}");

            return IsActiveAndEnabled(_State.AuthoringSource);
        }

        /// <summary>
        /// Checks if the GameObject is active and enabled for a given component.
        /// </summary>
        /// <param name="component">The Object to check.</param>
        /// <returns>Returns true if the GameObject is active and enabled.</returns>
        /// <remarks>This takes a dependency on the active and enable state.</remarks>
        public bool IsActiveAndEnabled(Component component)
        {
            bool isActiveAndEnabled = IsActive(component.gameObject);

            var behaviour = component as Behaviour;
            if (behaviour != null)
            {
                DependsOn(component);
                isActiveAndEnabled = isActiveAndEnabled && behaviour.isActiveAndEnabled;
            }
            return isActiveAndEnabled;
        }

        /// <summary>
        /// Checks if the GameObject is static.
        /// </summary>
        /// <returns>Returns true if the GameObject is static.</returns>
        /// <remarks>This takes a dependency on the static state.</remarks>
        public bool IsStatic()
        {
            return IsStatic(_State.AuthoringObject);
        }

        /// <summary>
        /// Checks if the GameObject is static for a given component.
        /// </summary>
        /// <param name="component">The Object to check.</param>
        /// <returns>Returns true if the GameObject is static.</returns>
        /// <remarks>This takes a dependency on the static state.</remarks>
        public bool IsStatic(Component component)
        {
            return IsStatic(component.gameObject);
        }

        /// <summary>
        /// Checks if the GameObject is static for a given GameObject.
        /// </summary>
        /// <param name="gameObject">The GameObject to check</param>
        /// <returns>Returns true if the object or one of its ancestors is static, otherwise returns false.</returns>
        /// <remarks>This takes a dependency on the static state</remarks>
        public bool IsStatic(GameObject gameObject)
        {
            // Intentionally using gameObject.GetComponentInParent instead of the baker version.
            // We want this baker to trigger when the overall static value changes, not StaticOptimizeEntity is added/removed
            var staticOptimizeEntity = gameObject.GetComponentInParent<StaticOptimizeEntity>(gameObject) != null;
            bool isStatic = (staticOptimizeEntity || InternalIsStaticRecursive(gameObject));

            _State.Dependencies->DependOnStatic(gameObject.GetInstanceID(), _State.AuthoringId, isStatic);

            return isStatic;
        }

        /// <summary>
        /// Returns if the GameObject or one of its parents is static
        /// </summary>
        /// <param name="gameObject">The GameObject to check.</param>
        /// <returns>Returns true if the object or one of its ancestors is static, otherwise returns false.</returns>
        private static bool InternalIsStaticRecursive(GameObject gameObject)
        {
            var current = gameObject.transform;
            while (current)
            {
                if (current.gameObject.isStatic)
                    return true;
                current = current.parent;
            }
            return false;
        }

        /// <summary>
        /// Checks if the the authoring component is baked in the Client World
        /// </summary>
        /// <returns>True if the NetCode package present and the authoring component is baked in the Client World</returns>
        public bool IsClient()
        {
            return (_State.World.Flags&WorldFlags.GameClient) == WorldFlags.GameClient;
        }

        /// <summary>
        /// Checks if the the authoring component is baked in the Server World
        /// </summary>
        /// <returns>True if the NetCode package present and the authoring component is baked in the Server World</returns>
        public bool IsServer()
        {
            return (_State.World.Flags&WorldFlags.GameServer) == WorldFlags.GameServer;
        }

#endregion

#region Declare Dependencies

        /// <summary>
        /// This will take a dependency on Object of type T.
        /// </summary>
        /// <param name="dependency">The Object to take a dependency on.</param>
        /// <typeparam name="T">The type of the object. Must be derived from UnityEngine.Object.</typeparam>
        /// <returns>The Object of type T if a dependency was taken, null otherwise.</returns>
        public T DependsOn<T>(T dependency) where T : UnityEngine.Object
        {
            _State.Dependencies->DependResolveReference(_State.AuthoringSource.GetInstanceID(), dependency);

            // Transform component takes an implicit dependency on the entire parent hierarchy
            // since transform.position and friends returns a value calculated from all parents
            var transform = dependency as Transform;
            if (transform != null)
                _State.Dependencies->DependOnParentTransformHierarchy(transform);

            return dependency;
        }

        /// <summary>
        /// This will take a dependency on the first component of type T in the GameObject or any of its parent. Works recursively.
        /// </summary>
        /// <typeparam name="T">The type of the component to take a dependency on</typeparam>
        public void DependsOnComponentInParent<T>() where T : Component
        {
            GetComponentInParent<T>();
        }

        /// <summary>
        /// This will take a dependency on the first component of type T in the GameObject or any of its parent. Works recursively.
        /// </summary>
        /// <param name="component">The Object to take the component dependency on</param>
        /// <typeparam name="T">The type of the component to take a dependency on</typeparam>
        public void DependsOnComponentInParent<T>(Component component) where T : Component
        {
            GetComponentInParent<T>(component);
        }

        /// <summary>
        /// This will take a dependency on the first component of type T in the GameObject or any of its parent. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to take the component dependency on</param>
        /// <typeparam name="T">The type of the component to take a dependency on</typeparam>
        public void DependsOnComponentInParent<T>(GameObject gameObject) where T : Component
        {
            GetComponentInParent<T>(gameObject);
        }

        /// <summary>
        /// This will take a dependency on the components of type T in the GameObject or any of its parent. Works recursively.
        /// </summary>
        /// <typeparam name="T">The type of the components to take a dependency on</typeparam>
        public void DependsOnComponentsInParent<T>() where T : Component
        {
            GetComponentsInParent<T>();
        }

        /// <summary>
        /// This will take a dependency on the components of type T in the GameObject or any of its parent. Works recursively.
        /// </summary>
        /// <param name="component">The Object to take the components' dependency on</param>
        /// <typeparam name="T">The type of the components to take a dependency on</typeparam>
        public void DependsOnComponentsInParent<T>(Component component) where T : Component
        {
            GetComponentsInParent<T>(component);
        }

        /// <summary>
        /// This will take a dependency on the components of type T in the GameObject or any of its parent. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to take the components' dependency on</param>
        /// <typeparam name="T">The type of the components to take a dependency on</typeparam>
        public void DependsOnComponentsInParent<T>(GameObject gameObject) where T : Component
        {
            GetComponentsInParent<T>(gameObject);
        }

        /// <summary>
        /// This will take a dependency on the component of type T in the GameObject or any of its children. Works recursively.
        /// </summary>
        /// <typeparam name="T">The type of the component to take a dependency on</typeparam>
        public void DependsOnComponentInChildren<T>() where T : Component
        {
            GetComponentInChildren<T>();
        }

        /// <summary>
        /// This will take a dependency on the component of type T in the GameObject or any of its children. Works recursively.
        /// </summary>
        /// <param name="component">The Object to take the component dependency on</param>
        /// <typeparam name="T">The type of the component to take a dependency on</typeparam>
        public void DependsOnComponentInChildren<T>(Component component) where T : Component
        {
            GetComponentInChildren<T>(component);
        }

        /// <summary>
        /// This will take a dependency on the component of type T in the GameObject or any of its children. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to take the component dependency on</param>
        /// <typeparam name="T">The type of the component to take a dependency on</typeparam>
        public void DependsOnComponentInChildren<T>(GameObject gameObject) where T : Component
        {
            GetComponentInChildren<T>(gameObject);
        }

        /// <summary>
        /// This will take a dependency on the components of type T in the GameObject or any of its children. Works recursively.
        /// </summary>
        /// <typeparam name="T">The type of the components to take a dependency on</typeparam>
        public void DependsOnComponentsInChildren<T>() where T : Component
        {
            GetComponentsInChildren<T>();
        }

        /// <summary>
        /// This will take a dependency on the components of type T in the GameObject or any of its children. Works recursively.
        /// </summary>
        /// <param name="gameObject">The GameObject to take the components' dependency on</param>
        /// <typeparam name="T">The type of the components to take a dependency on</typeparam>
        public void DependsOnComponentsInChildren<T>(GameObject gameObject) where T : Component
        {
            GetComponentsInChildren<T>(gameObject);
        }

        /// <summary>
        /// This will take a dependency on the components of type T in the GameObject or any of its children. Works recursively.
        /// </summary>
        /// <param name="component">The Object to take the components' dependency on</param>
        /// <typeparam name="T">The type of the components to take a dependency on</typeparam>
        public void DependsOnComponentsInChildren<T>(Component component) where T : Component
        {
            GetComponentsInChildren<T>(component);
        }

        /// <summary>
        /// This will take a dependency on the light baking, causing the component to bake every time light mapping is baked.
        /// </summary>
        public void DependsOnLightBaking()
        {
            _State.Dependencies->DependOnLightBaking();
        }

        #endregion

#region Debug Tracking and Validation

        /// <summary>
        /// Adds debug tracking for the Entity and Component pair, throws an Exception otherwise
        /// </summary>
        /// <param name="entity">The Entity to track</param>
        /// <param name="typeIndex">The index of the Component type to track</param>
        /// <exception cref="InvalidOperationException"></exception>
        void AddDebugTrackingForComponent(Entity entity, TypeIndex typeIndex)
        {
            var entityComponentPair = new BakerDebugState.EntityComponentPair(entity, typeIndex);

            if (_State.DebugState->addedComponentsByEntity.TryAdd(entityComponentPair, _State.DebugIndex))
                return;

            _State.DebugState->addedComponentsByEntity.TryGetValue(entityComponentPair, out var debugIndex);
            var bakerName = this.GetType().FullName;

            var previousBakers = BakerDataUtility.GetBakers(debugIndex.TypeIndex);
            if (previousBakers != null)
            {
                var previousBaker = previousBakers[debugIndex.IndexInBakerArray].Baker;
                var previousBakerName = previousBaker.GetType().FullName;
                var authoringComponentName = _State.AuthoringSource.GetType().FullName;
                throw new InvalidOperationException(
                    $"Baking error: Attempt to add duplicate component {TypeManager.GetTypeInfo(typeIndex).DebugTypeName} for Baker {bakerName} with authoring component {authoringComponentName}.  Previous component added by Baker {previousBakerName}. {_State.ErrorContextString()}");
            }
        }

        /// <summary>
        /// Checks if the component on the Entity has been added by this Baker, throws an Exception otherwise
        /// </summary>
        /// <param name="entity">The Entity to track</param>
        /// <param name="typeIndex">The index of the Component type to track</param>
        /// <exception cref="InvalidOperationException"></exception>
        void CheckComponentHasBeenAddedByThisBaker(Entity entity, TypeIndex typeIndex)
        {
            var entityComponentPair = new BakerDebugState.EntityComponentPair(entity, typeIndex);

            var hasComponent = _State.DebugState->addedComponentsByEntity.TryGetValue(entityComponentPair, out var debugIndex);
            if (hasComponent && debugIndex.Equals(_State.DebugIndex))
                return;
            var bakerName = this.GetType().Name;
            var authoringComponentName = _State.AuthoringSource.GetType().Name;

            if (hasComponent)
            {
                var previousBakers = BakerDataUtility.GetBakers(debugIndex.TypeIndex);
                var previousBaker = previousBakers[debugIndex.IndexInBakerArray].Baker;
                var previousBakerName = previousBaker.GetType().Name;
                throw new InvalidOperationException($"Baking error: Attempt to set component {TypeManager.GetTypeInfo(typeIndex).DebugTypeName} for Baker {bakerName} with authoring component {authoringComponentName} but the component was added by a different Baker {previousBakerName}. {_State.ErrorContextString()}");
            }
            else
            {
                throw new InvalidOperationException($"Baking error: Attempt to set component {TypeManager.GetTypeInfo(typeIndex).DebugTypeName} for Baker {bakerName} with authoring component {authoringComponentName} but the component hasn't been added by the baker yet. {_State.ErrorContextString()}");
            }
        }

        /// <summary>
        /// Adds debug tracking for the Entity and Component pair
        /// </summary>
        /// <param name="entity">The Entity to track</param>
        /// <param name="type">The type of the Component to track</param>
        void AddDebugTrackingForComponent(Entity entity, ComponentType type)
        {
            AddDebugTrackingForComponent(entity, type.TypeIndex);
        }

        /// <summary>
        /// Adds debug tracking for the Entity and Component pair
        /// </summary>
        /// <param name="entity">The Entity to track</param>
        /// <typeparam name="T">The type of the Component to track</typeparam>
        void AddDebugTrackingForComponent<T>(Entity entity)
        {
            ComponentType type = ComponentType.ReadWrite<T>();
            AddDebugTrackingForComponent(entity, type.TypeIndex);
        }

        /// <summary>
        /// Adds debug tracking for multiple Entity and Component pairs
        /// </summary>
        /// <param name="entity">The Entity to track</param>
        /// <param name="typeSet">The types of the Component to track</param>
        void AddDebugTrackingForComponent(Entity entity, in ComponentTypeSet typeSet)
        {
            for (int i=0,n=typeSet.Length; i<n; ++i)
                AddDebugTrackingForComponent(entity, typeSet.GetComponentType(i));
        }

        /// <summary>
        /// Adds debug tracking for a Component added to the primary Entity
        /// </summary>
        /// <param name="componentType">The type of the Component to track</param>
        void AddTrackingForComponent(ComponentType componentType)
        {
            var typeIndex = componentType.TypeIndex;
            _State.BakerState->AddedComponents.Add(typeIndex);
        }

        /// <summary>
        /// Adds debug tracking for a Component added to the primary Entity
        /// </summary>
        /// <typeparam name="T">The type of the Component to track</typeparam>
        void AddTrackingForComponent<T>()
        {
            AddTrackingForComponent(ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Adds debug tracking for a Component added to the primary Entity
        /// </summary>
        /// <param name="typeSet">The types of the Component to track</param>
        void AddTrackingForComponent(in ComponentTypeSet typeSet)
        {
            for (int i=0,n=typeSet.Length; i<n; ++i)
                AddTrackingForComponent(typeSet.GetComponentType(i));
        }

        /// <summary>
        /// Checks that the Entity is valid and owned by the current authoring component,throws an exception otherwise
        /// </summary>
        /// <param name="entity">The Entity to check</param>
        /// <exception cref="InvalidOperationException"></exception>
        void CheckValidAdditionalEntity(Entity entity)
        {
            if (!_State.BakerState->Entities.Contains(entity))
                throw new InvalidOperationException($"Entity {entity} doesn't belong to the current authoring component. {_State.ErrorContextString()}");
        }

        #endregion

#region Blob Assets

        /// <summary>
        /// Adds a BlobAsset to the primary Entity
        /// </summary>
        /// <param name="blobAssetReference">The BlobAssetReference of the BlobAsset to add</param>
        /// <param name="objectHash">The hash of the added BlobAsset</param>
        /// <typeparam name="T">The type of BlobAsset to add</typeparam>
        /// <remarks>This will take a dependency on the component</remarks>
        public void AddBlobAsset<T>(ref BlobAssetReference<T> blobAssetReference, out Hash128 objectHash) where T : unmanaged
        {
            // TryAdd already prevents double entries
            _State.BlobAssetStore.TryAdd(ref blobAssetReference, out Hash128 tempObjectHash);
            objectHash = tempObjectHash;
        }

        /// <summary>
        /// Adds a BlobAsset to the primary Entity with a custom hash
        /// </summary>
        /// <param name="blobAssetReference">The BlobAssetReference of the added BlobAsset to add</param>
        /// <param name="customHash">The hash that is used to add the BlobAsset to the Entity</param>
        /// <typeparam name="T">The type of BlobAsset to add</typeparam>
        /// <remarks>This will take a dependency on the component</remarks>
        public void AddBlobAssetWithCustomHash<T>(ref BlobAssetReference<T> blobAssetReference, Hash128 customHash) where T : unmanaged
        {
            if (!_State.BlobAssetStore.TryGet(customHash, out BlobAssetReference<T> existingBlobAssetReference))
            {
                _State.BlobAssetStore.TryAdd(customHash, ref blobAssetReference);
            }
            else
            {
                blobAssetReference = existingBlobAssetReference;
            }
        }

        /// <summary>
        /// Gets the BlobAssetReference based on a hash
        /// </summary>
        /// <param name="hash">The hash of the BlobAssetReference to get</param>
        /// <param name="blobAssetReference">The BlobAssetReference associated with the hash if found, default otherwise</param>
        /// <typeparam name="T">The type of BlobAsset to get</typeparam>
        /// <returns>True if the BlobAssetReference is found, otherwise False</returns>
        /// <remarks>This will take a dependency on the component</remarks>
        public bool TryGetBlobAssetReference<T>(Hash128 hash, out BlobAssetReference<T> blobAssetReference) where T : unmanaged
        {
            if (_State.BlobAssetStore.TryGet(hash, out blobAssetReference))
            {
                return true;
            }

            blobAssetReference = default;
            return false;
        }

#endregion

#region Add and Set Components on Entities

        /// <summary>
        /// Adds a component of type T to the primary Entity
        /// </summary>
        /// <typeparam name="T">The type of component to add</typeparam>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AddComponent<T>() where T : unmanaged, IComponentData
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<T>(entity);
        }

        /// <summary>
        /// Adds a component of type T to the primary Entity
        /// </summary>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AddComponent<T>(in T component) where T : unmanaged, IComponentData
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent<T>(entity, component);
        }

        /// <summary>
        /// Adds a component of type T to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the component to</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        public void AddComponent<T>(Entity entity)
        {
            AddComponent(entity, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Adds a component of type T to a set of Entities
        /// </summary>
        /// <param name="entities">The Entities to add the component to</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        internal void AddComponent<T>(NativeArray<Entity> entities)
        {
            AddComponent(entities, ComponentType.ReadWrite<T>());
        }

        /// <summary>
        /// Adds a component of type T to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the component to</param>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        public void AddComponent<T>(Entity entity, in T component) where T : unmanaged, IComponentData
        {
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent<T>(entity);
                AddTrackingForComponent<T>();
            }
            else
                CheckValidAdditionalEntity(entity);

            _State.Ecb.AddComponent(entity, component);
        }

        /// <summary>
        /// Adds a component of type T to a set of Entities
        /// </summary>
        /// <param name="entities">The Entities to add the component to</param>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        internal void AddComponent<T>(NativeArray<Entity> entities, in T component) where T : unmanaged, IComponentData
        {
            // Currently doesn't work for the primary entity
            foreach (var entity in entities)
                CheckValidAdditionalEntity(entity);
            _State.Ecb.AddComponent(entities, component);
        }

        /// <summary>
        /// Adds a component of type ComponentType to the primary Entity
        /// </summary>
        /// <param name="componentType">The type of component to add</param>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AddComponent(ComponentType componentType)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, componentType);
        }

        /// <summary>
        /// Adds a component of type ComponentType to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the component to</param>
        /// <param name="componentType">The type of component to add</param>
        public void AddComponent(Entity entity, ComponentType componentType)
        {
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent(entity, componentType);
                AddTrackingForComponent(componentType);
            }
            else
                CheckValidAdditionalEntity(entity);

            _State.Ecb.AddComponent(entity, componentType);
        }

        /// <summary>
        /// Adds a component of type ComponentType to a set of Entities
        /// </summary>
        /// <param name="entities">The Entities to add the component to</param>
        /// <param name="componentType">The type of component to add</param>
        internal void AddComponent(NativeArray<Entity> entities, ComponentType componentType)
        {
            // Currently doesn't work for the primary entity
            foreach (var entity in entities)
                CheckValidAdditionalEntity(entity);

            _State.Ecb.AddComponent(entities, componentType);
        }

        /// <summary>
        /// Adds a component to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the component to</param>
        /// <param name="typeIndex">The index of the type of component to add</param>
        /// <param name="typeSize">The size of the type of component to add</param>
        /// <param name="componentDataPtr">The pointer to the component data</param>
        internal void UnsafeAddComponent(Entity entity, TypeIndex typeIndex, int typeSize, void* componentDataPtr)
        {
            var ctype = new ComponentType {AccessModeType = ComponentType.AccessMode.ReadWrite, TypeIndex = typeIndex};
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent(entity, ctype);
                AddTrackingForComponent(ctype);
            }
            else
                CheckValidAdditionalEntity(entity);

            _State.Ecb.UnsafeAddComponent(entity, typeIndex, typeSize, componentDataPtr);
        }

        /// <summary>
        /// Adds multiple components of types ComponentType to the primary Entity
        /// </summary>
        /// <param name="componentTypeSet">The types of components to add</param>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AddComponent(in ComponentTypeSet componentTypeSet)
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponent(entity, componentTypeSet);
        }

        /// <summary>
        /// Add multiple components of types ComponentType to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the components to</param>
        /// <param name="componentTypeSet">The types of components to add</param>
        public void AddComponent(Entity entity, in ComponentTypeSet componentTypeSet)
        {
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent(entity, componentTypeSet);

                for (int i = 0; i < componentTypeSet.Length; i++)
                    _State.BakerState->AddedComponents.Add(componentTypeSet.GetTypeIndex(i));
            }
            else
                CheckValidAdditionalEntity(entity);

            _State.Ecb.AddComponent(entity, componentTypeSet);
        }

        /// <summary>
        /// Add multiple components of types ComponentType to a set of Entities
        /// </summary>
        /// <param name="entities">The Entities to add the components to</param>
        /// <param name="componentTypeSet">The types of components to add</param>
        internal void AddComponent(NativeArray<Entity> entities, in ComponentTypeSet componentTypeSet)
        {
            // Currently doesn't work for the primary entity
            foreach (var entity in entities)
                CheckValidAdditionalEntity(entity);
            _State.Ecb.AddComponent(entities, in componentTypeSet);
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        /// <summary>
        /// Adds a managed component of type T to the primary Entity
        /// </summary>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AddComponentObject<T>(T component) where T : class
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddComponentObject<T>(entity, component);
        }

        /// <summary>
        /// Adds a managed component of type T to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the component to</param>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        public void AddComponentObject<T>(Entity entity, T component) where T : class
        {
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent(entity, ComponentType.ReadWrite<T>());
                AddTrackingForComponent<T>();
            }
            else
                CheckValidAdditionalEntity(entity);

            _State.Ecb.AddComponent(entity, component);
        }
#endif

        /// <summary>
        /// Adds a managed shared component of type T to the primary Entity
        /// </summary>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AddSharedComponentManaged<T>(T component) where T : struct, ISharedComponentData
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddSharedComponentManaged<T>(entity, component);
        }

        /// <summary>
        /// Adds a managed shared component of type T to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the component to</param>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        public void AddSharedComponentManaged<T>(Entity entity, T component) where T : struct, ISharedComponentData
        {
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent(entity, ComponentType.ReadWrite<T>());
                AddTrackingForComponent<T>();
            }
            else
                CheckValidAdditionalEntity(entity);
            _State.Ecb.AddSharedComponentManaged(entity, component);
        }

        /// <summary>
        /// Adds a shared component of type T to the primary Entity
        /// </summary>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AddSharedComponent<T>(T component) where T : unmanaged, ISharedComponentData
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AddSharedComponent<T>(entity, component);
        }

        /// <summary>
        /// Adds a shared component of type T to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the component to</param>
        /// <param name="component">The component to add</param>
        /// <typeparam name="T">The type of component to add</typeparam>
        public void AddSharedComponent<T>(Entity entity, T component) where T : unmanaged, ISharedComponentData
        {
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent(entity, ComponentType.ReadWrite<T>());
                AddTrackingForComponent<T>();
            }
            else
                CheckValidAdditionalEntity(entity);
            _State.Ecb.AddSharedComponent(entity, component);
        }

        /// <summary>
        /// Adds a DynamicBuffer of type T to the primary Entity
        /// </summary>
        /// <typeparam name="T">The type of buffer to add</typeparam>
        /// <returns>The created DynamicBuffer</returns>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public DynamicBuffer<T> AddBuffer<T>() where T : unmanaged, IBufferElementData
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            return AddBuffer<T>(entity);
        }

        /// <summary>
        /// Adds a DynamicBuffer of type T to the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the buffer to</param>
        /// <typeparam name="T">The type of buffer to add</typeparam>
        /// <returns>The created DynamicBuffer</returns>
        public DynamicBuffer<T> AddBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            if (_State.PrimaryEntity == entity)
            {
                // Only track it for Primary Entity, additional entities can only be accessed by the baker that creates them
                AddDebugTrackingForComponent<T>(entity);
                AddTrackingForComponent<T>();
            }
            else
                CheckValidAdditionalEntity(entity);

            return _State.Ecb.AddBuffer<T>(entity);
        }

        /// <summary>
        /// Replaces the value of the component on the Entity.
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific component.
        /// This is not a very common operation in bakers, but sometimes you have utility methods that add the relevant components and initialize them to a reasonable default state for that utility method,
        /// but then your baker needs to override the value of one of those added components to something specific in your particular baker.
        /// </remarks>
        /// <param name="entity">The Entity to set the component to</param>
        /// <param name="component">The component to set</param>
        /// <typeparam name="T">The type of component to set</typeparam>
        public void SetComponent<T>(Entity entity, in T component) where T : unmanaged, IComponentData
        {
            if (_State.PrimaryEntity != entity)
                CheckValidAdditionalEntity(entity);
            else
                CheckComponentHasBeenAddedByThisBaker(entity, TypeManager.GetTypeIndex<T>());

            _State.Ecb.SetComponent(entity, component);
        }

        /// <summary>
        /// Sets the enabled value of the component on the Entity.
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific component.
        /// </remarks>
        /// <param name="entity">The Entity to set the component to</param>
        /// <param name="enabled">True if the specified component should be enabled, or false if it should be disabled</param>
        /// <typeparam name="T">The type of component to set</typeparam>
        public void SetComponentEnabled<T>(Entity entity, bool enabled) where T : struct, IEnableableComponent
        {
            if (_State.PrimaryEntity != entity)
                CheckValidAdditionalEntity(entity);
            else
                CheckComponentHasBeenAddedByThisBaker(entity, TypeManager.GetTypeIndex<T>());

            _State.Ecb.SetComponentEnabled<T>(entity, enabled);
        }

        /// <summary>
        /// Sets the enabled value of the component on the primary Entity.
        /// </summary>
        /// <param name="enabled">True if the specified component should be enabled, or false if it should be disabled</param>
        /// <typeparam name="T">The type of component to set</typeparam>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void SetComponentEnabled<T>(bool enabled) where T : struct, IEnableableComponent
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            SetComponentEnabled<T>(entity, enabled);
        }

         /// <summary>
         /// Replaces the value of the component on the Entity.
         /// </summary>
         /// <param name="entity">The Entity to set the component to</param>
         /// <param name="typeIndex">The index of the type of component to set</param>
         /// <param name="typeSize">The size of the type of component to set</param>
         /// <param name="componentDataPtr">The pointer to the component data</param>
        internal unsafe void UnsafeSetComponent(Entity entity, TypeIndex typeIndex, int typeSize, void* componentDataPtr)
        {
            if (_State.PrimaryEntity != entity)
                CheckValidAdditionalEntity(entity);
            else
                CheckComponentHasBeenAddedByThisBaker(entity, typeIndex);

            _State.Ecb.UnsafeSetComponent(entity, typeIndex, typeSize, componentDataPtr);
        }

        /// <summary>
        /// Replaces the value of the managed shared component on the Entity.
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific component.
        /// This is not a very common operation in bakers, but sometimes you have utility methods that add the relevant components and initialize them to a reasonable default state for that utility method,
        /// but then your baker needs to override the value of one of those added components to something specific in your particular baker.
        /// </remarks>
        /// <param name="entity">The Entity to set the component to</param>
        /// <param name="component">The component to set</param>
        /// <typeparam name="T">The type of component to set</typeparam>
        public void SetSharedComponentManaged<T>(Entity entity, in T component) where T : struct, ISharedComponentData
        {
            if (_State.PrimaryEntity != entity)
                CheckValidAdditionalEntity(entity);
            else
                CheckComponentHasBeenAddedByThisBaker(entity, TypeManager.GetTypeIndex<T>());

            _State.Ecb.SetSharedComponentManaged(entity, component);
        }

        /// <summary>
        /// Replaces the value of the shared component on the Entity.
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific component.
        /// This is not a very common operation in bakers, but sometimes you have utility methods that add the relevant components and initialize them to a reasonable default state for that utility method,
        /// but then your baker needs to override the value of one of those added components to something specific in your particular baker.
        /// </remarks>
        /// <param name="entity">The Entity to set the component to</param>
        /// <param name="component">The component to set</param>
        /// <typeparam name="T">The type of component to set</typeparam>
        public void SetSharedComponent<T>(Entity entity, in T component) where T : unmanaged, ISharedComponentData
        {
            if (_State.PrimaryEntity != entity)
                CheckValidAdditionalEntity(entity);
            else
                CheckComponentHasBeenAddedByThisBaker(entity, TypeManager.GetTypeIndex<T>());

            _State.Ecb.SetSharedComponent(entity, component);
        }

        /// <summary>
        /// Replaces the value of the shared component on a set of Entities
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific component.
        /// This is not a very common operation in bakers, but sometimes you have utility methods that add the relevant components and initialize them to a reasonable default state for that utility method,
        /// but then your baker needs to override the value of one of those added components to something specific in your particular baker.
        /// </remarks>
        /// <param name="entities">The Entities to set the component to</param>
        /// <param name="component">The component to set</param>
        /// <typeparam name="T">The type of component to set</typeparam>
        internal void SetSharedComponent<T>(NativeArray<Entity> entities, in T component) where T : unmanaged, ISharedComponentData
        {
            // Doesn't work for the primary entity
            foreach (var entity in entities)
                CheckValidAdditionalEntity(entity);
            _State.Ecb.SetSharedComponent(entities, component);
        }

        /// <summary>
        /// Replaces a DynamicBuffer of type T on the primary Entity
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific buffer.
        /// This is not a very common operation in bakers, but sometimes you have utility methods that add the relevant buffer and initialize them to a reasonable default state for that utility method,
        /// but then your baker needs to override the value of one of those added buffers to something specific in your particular baker.
        /// </remarks>
        /// <typeparam name="T">The type of buffer to set</typeparam>
        /// <returns>The new DynamicBuffer</returns>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public DynamicBuffer<T> SetBuffer<T>() where T : unmanaged, IBufferElementData
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            return SetBuffer<T>(entity);
        }

        /// <summary>
        /// Replaces a DynamicBuffer of type T on the Entity
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific buffer.
        /// This is not a very common operation in bakers, but sometimes you have utility methods that add the relevant buffer and initialize them to a reasonable default state for that utility method,
        /// but then your baker needs to override the value of one of those added buffers to something specific in your particular baker.
        /// </remarks>
        /// <param name="entity">The Entity to set the buffer on</param>
        /// <typeparam name="T">The type of buffer to set</typeparam>
        /// <returns>The new DynamicBuffer</returns>
        public DynamicBuffer<T> SetBuffer<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            if (_State.PrimaryEntity != entity)
                CheckValidAdditionalEntity(entity);

            return SetBufferInternal<T>(entity);
        }

        /// <summary>
        /// Replaces a DynamicBuffer of type T on the Entity
        /// </summary>
        /// <remarks>
        /// This method can only be invoked if the same baker instance previously added this specific buffer.
        /// This is not a very common operation in bakers, but sometimes you have utility methods that add the relevant buffer and initialize them to a reasonable default state for that utility method,
        /// but then your baker needs to override the value of one of those added buffers to something specific in your particular baker.
        /// </remarks>
        /// <param name="entity">The Entity to set the buffer on</param>
        /// <typeparam name="T">The type of buffer to set</typeparam>
        /// <returns>The new DynamicBuffer</returns>
        private DynamicBuffer<T> SetBufferInternal<T>(Entity entity) where T : unmanaged, IBufferElementData
        {
            if (_State.PrimaryEntity == entity)
                CheckComponentHasBeenAddedByThisBaker(entity, TypeManager.GetTypeIndex<T>());

            return _State.Ecb.SetBuffer<T>(entity);
        }

        /// <summary>
        /// Append to a DynamicBuffer of type T on the primary Entity
        /// </summary>
        /// <param name="element">The element of type T to append to the buffer</param>
        /// <typeparam name="T">The type of buffer to append to</typeparam>
        /// <remarks>Implicitly it will access the primary entity with TransformUsageFlags.Dynamic.</remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public void AppendToBuffer<T>(T element) where T : unmanaged, IBufferElementData
        {
            var entity = GetEntity(TransformUsageFlags.Dynamic);
            AppendToBuffer<T>(entity, element);
        }

        /// <summary>
        /// Append to a DynamicBuffer of type T on the Entity
        /// </summary>
        /// <param name="entity">The Entity to set the buffer on</param>
        /// <param name="element">The element of type T to append to the buffer</param>
        /// <typeparam name="T">The type of buffer to append to</typeparam>
        public void AppendToBuffer<T>(Entity entity, T element) where T : unmanaged, IBufferElementData
        {
            if (_State.PrimaryEntity != entity)
                CheckValidAdditionalEntity(entity);

            AppendToBufferInternal(entity, element);
        }

        /// <summary>
        /// Append to a DynamicBuffer of type T on the Entity
        /// </summary>
        /// <param name="entity">The Entity to set the buffer on</param>
        /// <param name="element">The element of type T to append to the buffer</param>
        /// <typeparam name="T">The type of buffer to append to</typeparam>
        private void AppendToBufferInternal<T>(Entity entity, T element) where T : unmanaged, IBufferElementData
        {
            if (_State.PrimaryEntity == entity)
                CheckComponentHasBeenAddedByThisBaker(entity, TypeManager.GetTypeIndex<T>());

            _State.Ecb.AppendToBuffer(entity, element);
        }

#endregion

        /// <summary>
        /// Creates an additional Entity tied to the primary entity.
        /// </summary>
        /// <returns>Returns the newly created entity.</returns>
        /// <remarks>
        /// Additional entities are automatically reverted by the baking system if the source primary entity is removed in a new baking pass.
        /// Additional entities are created with the same active or static state as the Primary Entity. For example, if the authoring object is disabled,
        /// the new additional entity will also have the <see cref="Disabled"/> tag component.
        ///
        /// Implicitly it will create the additional entity with TransformUsageFlags.Dynamic.
        ///
        /// Baking only additional entities are not exported in the runtime data.
        /// </remarks>
        [Obsolete("Use the version of the function with the explicit Entity parameter (RemovedAfter Entities 1.0)")]
        public Entity CreateAdditionalEntity()
        {
            return CreateAdditionalEntity(TransformUsageFlags.Dynamic);
        }

        /// <summary>
        /// Creates an additional Entity tied to the primary entity.
        /// </summary>
        /// <param name="transformUsageFlags">The <see cref="TransformUsageFlags"/> of the additional Entity.</param>
        /// <param name="bakingOnlyEntity">Whether to mark the additional Entity as BakingOnly.</param>
        /// <param name="entityName">The name of the additional Entity.</param>
        /// <returns>Returns the newly created entity.</returns>
        /// <remarks>
        /// Additional entities are automatically reverted by the baking system if the source primary entity is removed in a new baking pass.
        /// Additional entities are created with the same active or static state as the Primary Entity. For example, if the authoring object is disabled,
        /// the new additional entity will also have the <see cref="Disabled"/> tag component.
        ///
        /// Baking only additional entities are not exported in the runtime data.
        /// </remarks>
        public Entity CreateAdditionalEntity(TransformUsageFlags transformUsageFlags, bool bakingOnlyEntity = false, string entityName = "")
        {
            var entity = _State.BakedEntityData->CreateAdditionalEntity(_State.AuthoringObject, _State.AuthoringId, bakingOnlyEntity, entityName);
            _State.BakerState->Entities.Add(entity);
            _State.Usage->ReferencedEntityUsages.Add(new BakerEntityUsage.ReferencedEntityUsage(entity, transformUsageFlags));
            return entity;
        }

        /// <summary>
        /// Creates an array of additional Entities tied to the primary entity.
        /// </summary>
        /// <param name="outputEntities">The output array which will be filled with the additional Entities created. The size of the array specifies the
        /// number of additional entities to be created. The function throws <see cref="ArgumentException"/> if the array is not created yet or is empty.</param>
        /// <param name="transformUsageFlags">The <see cref="TransformUsageFlags"/> of the additional Entity.</param>
        /// <param name="bakingOnlyEntity">Whether to mark the additional Entity as BakingOnly.</param>
        /// <remarks>
        /// Additional entities are automatically reverted by the baking system if the source primary entity is removed in a new baking pass.
        /// Additional entities are created with the same active or static state as the Primary Entity. For example, if the authoring object is disabled,
        /// the new additional entity will also have the <see cref="Disabled"/> tag component.
        ///
        /// Baking only additional entities are not exported in the runtime data.
        /// </remarks>
        public void CreateAdditionalEntities(NativeArray<Entity> outputEntities, TransformUsageFlags transformUsageFlags, bool bakingOnlyEntity = false)
        {
            if (!outputEntities.IsCreated || outputEntities.Length == 0)
                throw new ArgumentException($"{nameof(outputEntities)} is not valid or empty. {_State.ErrorContextString()}");

            _State.BakedEntityData->CreateAdditionalEntities(outputEntities, _State.AuthoringObject, _State.AuthoringId, bakingOnlyEntity);
            foreach (var e in outputEntities)
            {
                _State.BakerState->Entities.Add(e);
                _State.Usage->ReferencedEntityUsages.Add(new BakerEntityUsage.ReferencedEntityUsage(e, transformUsageFlags));
            }
        }

        /// <summary>
        /// Ensures that the Prefab will be baked into a Prefab and present at Runtime
        /// </summary>
        /// <param name="authoring">The Prefab to bake</param>
        public void RegisterPrefabForBaking(GameObject authoring)
        {
            if (authoring == null || _State.AuthoringObject == authoring || !authoring.IsPrefab())
                return;

#if UNITY_EDITOR
            var prefabInstanceId = authoring.GetInstanceID();
            if (!_State.BakerState->ReferencedPrefabs.Contains(prefabInstanceId))
            {
                _State.BakerState->ReferencedPrefabs.Add(prefabInstanceId);
                _State.BakedEntityData->AddPrefabRef(prefabInstanceId);
            }
#endif
        }

        /// <summary>
        /// Adds the TransformUsageFlags to the Flags of the Entity
        /// </summary>
        /// <param name="flags">The Flags to add</param>
        public void AddTransformUsageFlags(TransformUsageFlags flags)
        {
            _State.Usage->PrimaryEntityFlags.Add(flags);
        }

        /// <summary>
        /// Adds the TransformUsageFlags to the Flags of the Entity
        /// </summary>
        /// <param name="entity">The Entity to add the Flags to</param>
        /// <param name="flags">The Flags to add</param>
        public void AddTransformUsageFlags(Entity entity, TransformUsageFlags flags)
        {
            if (entity == _State.PrimaryEntity)
            {
                _State.Usage->PrimaryEntityFlags.Add(flags);
            }
            else if (entity != Entity.Null)
            {
                _State.Usage->ReferencedEntityUsages.Add(new BakerEntityUsage.ReferencedEntityUsage(entity, flags));
            }
        }

        /// <summary>
        /// Check if the Baking is done for the Editor (not for a Build)
        /// </summary>
        /// <returns>Returns true if the baking is complete for the Editor, and false otherwise.</returns>
        public bool IsBakingForEditor()
        {
            if ((_State.BakedEntityData->_ConversionFlags & BakingUtility.BakingFlags.IsBuildingForPlayer) != 0)
                return false;

            return true;
        }

#if UNITY_EDITOR
        /// <summary>
        /// Gets the Settings of the DOTS player
        /// </summary>
        /// <returns>The Settings of the DOTS player</returns>
        public IEntitiesPlayerSettings GetDotsSettings()
        {
            return _State.DotsSettings;
        }
#endif
    }

    /// <summary>
    /// Inherit this class to bake an authoring component.
    /// </summary>
    /// <remarks>
    /// Use the methods in <see cref="Baker{TAuthoringType}"/> to access any data outside
    /// of the authoring component. This ensures that the baking pipeline can keep track of any dependencies. This applies to <see cref="GameObject">GameObjects</see>,
    /// <see cref="Component">Components</see>, prefabs, and assets.
    ///
    /// For example you should use <see cref="Baker{TAuthoringType}">Baker&lt;TAuthoringType&gt;.GetComponent&lt;T&gt;()</see> to query the authoring components
    /// instead of <see cref="GameObject.GetComponent{T}()">GameObject.GetComponent&lt;T&gt;()</see>.
    /// </remarks>
    /// <example><code>
    /// public class MyAuthoring : MonoBehaviour
    /// {
    ///     public int Value;
    /// }
    ///
    /// public struct MyComponent : IComponentData
    /// {
    ///     public int Value;
    ///     public float3 Position;
    /// }
    ///
    /// public class MyBaker : Baker&lt;MyAuthoring&gt;
    /// {
    ///     public override void Bake(MyAuthoring authoring)
    ///     {
    ///         // Accessing the transform using Baker function, not the GameObject one
    ///         // so the this baker can keep track of the dependency
    ///         var transform = GetComponent&lt;Transform&gt;();
    ///         var entity = GetEntity(TransformUsageFlags.Dynamic);
    ///         AddComponent(entity, new MyComponent
    ///         {
    ///             Value = authoring.Value,
    ///             Position = transform.position
    ///         } );
    ///     }
    /// }
    /// </code></example>
    /// <typeparam name="TAuthoringType">The type of the authoring component.</typeparam>
    public abstract unsafe class Baker<TAuthoringType> : IBaker
        where TAuthoringType : Component
    {
        /// <summary>
        /// Called in the baking process to bake the authoring component
        /// </summary>
        /// <remarks>
        /// This method will be called for every instance of TAuthoringType in order to bake that instance.
        /// </remarks>
        /// <param name="authoring">The authoring component to bake</param>
        public abstract void Bake(TAuthoringType authoring);

        internal override void InvokeBake(in BakerExecutionState state)
        {
            _State = state;

            // Any Baker on a Transform needs to take an implicit dependency on the hierarchy as the Transform itself changes when the hierarchy does.
            var potentialTransform = state.AuthoringSource as Transform;
            if (potentialTransform != null)
            {
                state.Dependencies->DependOnParentTransformHierarchy(potentialTransform);
            }

            Bake((TAuthoringType) state.AuthoringSource);
            _State = default;
        }

        internal override Type GetAuthoringType()
        {
            return typeof(TAuthoringType);
        }
    }

    /// <summary>
    /// Inherit this class to bake an authoring GameObject.
    /// </summary>
    internal abstract class GameObjectBaker : IBaker
    {
        /// <summary>
        /// Called in the baking process to bake the authoring GameObject.
        /// </summary>
        /// <param name="authoring">The authoring GameObject to bake.</param>
        public abstract void Bake(GameObject authoring);

        internal override void InvokeBake(in BakerExecutionState state)
        {
            _State = state;
            Bake(state.AuthoringObject);
            _State = default;
        }

        internal override Type GetAuthoringType()
        {
            return typeof(GameObject);
        }
    }
}
