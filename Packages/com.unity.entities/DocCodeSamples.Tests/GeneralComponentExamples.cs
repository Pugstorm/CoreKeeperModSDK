using Unity.Entities;
using Unity.Transforms;
using System;
using UnityEngine;

namespace Doc.CodeSamples.Tests
{
    #region add-component-single-entity
    public partial struct AddComponentToSingleEntitySystemExample : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var entity = state.EntityManager.CreateEntity();
            state.EntityManager.AddComponent<Rotation>(entity);
        }

        }
    #endregion

    #region add-component-multiple-entities
    struct ComponentA : IComponentData {}
    struct ComponentB : IComponentData {}
    public partial struct AddComponentToMultipleEntitiesSystemExample : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var query = state.GetEntityQuery(typeof(ComponentA));
            state.EntityManager.AddComponent<ComponentB>(query);
        }
    }
    #endregion

    #region remove-component
    public partial struct RemoveComponentSystemExample : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            var query = state.GetEntityQuery(typeof(Rotation));
            state.EntityManager.RemoveComponent<Rotation>(query);
        }
    }
    #endregion

#if !UNITY_DISABLE_MANAGED_COMPONENTS
    #region managed-component-external-resource
    public class ManagedComponentWithExternalResource : IComponentData, IDisposable, ICloneable
    {
        public ParticleSystem ParticleSystem;

        public void Dispose()
        {
            UnityEngine.Object.Destroy(ParticleSystem);
        }

        public object Clone()
        {
            return new ManagedComponentWithExternalResource { ParticleSystem = UnityEngine.Object.Instantiate(ParticleSystem) };
        }
    }
    #endregion
#endif
}
