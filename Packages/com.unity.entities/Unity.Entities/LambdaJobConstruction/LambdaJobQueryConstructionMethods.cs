using System;
using Unity.Collections;
using Unity.Entities.CodeGeneratedJobForEach;
using static Unity.Entities.LambdaJobDescriptionConstructionMethods;

namespace Unity.Entities
{
    /// <summary>
    /// Extension methods implementing the fluent API for `Entities.ForEach`.
    /// </summary>
    public static class LambdaJobQueryConstructionMethods
    {
        /// <summary>
        /// Add qualification to the generated query that it should only return entities that do not have the specified component type.
        /// </summary>
        /// <typeparam name="T">Type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAbsent<T>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that do not have the specified component types.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAbsent<T1,T2>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have the specified DISABLED component type.
        /// </summary>
        /// <typeparam name="T">Type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithDisabled<T>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have the specified DISABLED component types.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithDisabled<T1,T2>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have all of the specified component type,
        /// whether or not it is enabled on each entity.
        /// </summary>
        /// <typeparam name="T">First type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithPresent<T>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have all of the specified component type,
        /// whether or not it is enabled on each entity.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithPresent<T1,T2>(this ForEachLambdaJobDescription description)  => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have all of the specified component type,
        /// whether or not it is enabled on each entity.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <typeparam name="T3">Third type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithPresent<T1,T2,T3>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that either 1) do not have the specified component type OR 2) have the specified DISABLED component type.
        /// </summary>
        /// <typeparam name="T">Type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithNone<T>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that either 1) do not have the specified component types OR 2) have the specified DISABLED component types.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithNone<T1,T2>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that do not have the specified component types.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <typeparam name="T3">Third type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithNone<T1,T2,T3>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have any of the specified component type.
        /// </summary>
        /// <typeparam name="T">Type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAny<T>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have any of the specified component types.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAny<T1,T2>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have any of the specified component type.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <typeparam name="T3">Third type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAny<T1,T2,T3>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have all of the specified component type,
        /// and only if the component is enabled on the entity.
        /// </summary>
        /// <typeparam name="T">First type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAll<T>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have all of the specified component type,
        /// and only if the component is enabled on the entity.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAll<T1,T2>(this ForEachLambdaJobDescription description)  => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have all of the specified component type,
        /// and only if the component is enabled on the entity.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <typeparam name="T3">Third type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithAll<T1,T2,T3>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have chunks where the specified component changed
        /// since the last time the system ran.
        /// </summary>
        /// <typeparam name="T">Type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithChangeFilter<T>(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add qualification to the generated query that it should only return entities that have chunks where the specified component changed
        /// since the last time the system ran.
        /// </summary>
        /// <typeparam name="T1">First type of component</typeparam>
        /// <typeparam name="T2">Second type of component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        [AllowMultipleInvocations]
        public static ForEachLambdaJobDescription WithChangeFilter<T1,T2>(this ForEachLambdaJobDescription description) => description;

        /// <summary>Specify an EntityCommandBufferSystem to play back entity commands.</summary>
        /// <remarks>To use this, you must pass an EntityCommands parameter as part of the ForEach() lambda expression. You can use this together with .Run(), .Schedule(), or .ScheduleParallel().</remarks>
        /// <typeparam name="T">Type of EntityCommandBufferSystem</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static ForEachLambdaJobDescription WithDeferredPlaybackSystem<T>(this ForEachLambdaJobDescription description) where T : EntityCommandBufferSystem => description;

        /// <summary>Play back entity commands immediately.</summary>
        /// <remarks>Usage requires an EntityCommands parameter to be passed to the ForEach() lambda function.
        /// May be used only together with .Run().</remarks>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static ForEachLambdaJobDescription WithImmediatePlayback(this ForEachLambdaJobDescription description) => description;

        /// <summary>
        /// Add EntityQueryOptions to the generated query.
        /// </summary>
        /// <param name="options">EntityQueryOptions to add to query</param>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static ForEachLambdaJobDescription WithEntityQueryOptions(this ForEachLambdaJobDescription description, EntityQueryOptions options) => description;

        /// <summary>
        /// Set a shared component filter on the query so that it only matches entities with this shared component value.
        /// </summary>
        /// <param name="sharedComponent">Shared component value</param>
        /// <typeparam name="T">Type of shared component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static ForEachLambdaJobDescription WithSharedComponentFilter<T>(this ForEachLambdaJobDescription description, [AllowDynamicValue] T sharedComponent) where T : struct, ISharedComponentData => description;

        /// <summary>Stores the generated query in a field</summary>
        /// <remarks>Unity calls this before the ForEach invocation, so the query can be used as soon as the system is created.</remarks>
        /// <param name="query">Reference to field in the system to store the query</param>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static ForEachLambdaJobDescription WithStoreEntityQueryInField(this ForEachLambdaJobDescription description, [AllowDynamicValue] ref EntityQuery query) => description;

        /// <summary>
        /// Set a shared component filter on the query so that it only matches entities with these shared component values.
        /// </summary>
        /// <param name="sharedComponent1">First shared component value</param>
        /// <param name="sharedComponent2">Second shared component value</param>
        /// <typeparam name="T1">First type of shared component</typeparam>
        /// <typeparam name="T2">Second type of shared component</typeparam>
        /// <param name="description">The target object</param>
        /// <returns>The target object, suitable for chaining multiple methods</returns>
        public static ForEachLambdaJobDescription WithSharedComponentFilter<T1, T2>(this ForEachLambdaJobDescription description, [AllowDynamicValue] T1 sharedComponent1, [AllowDynamicValue] T2 sharedComponent2) where T1 : struct, ISharedComponentData where T2 : struct, ISharedComponentData => description;

        /// <summary>
        /// Capture the query you have defined using WithAny/WithAll/WithNone
        /// </summary>
        /// <param name="description">The target object</param>
        /// <returns>The EntityQuery corresponding to the query.</returns>
        public static EntityQuery ToQuery(this ForEachLambdaJobDescription description) { ThrowCodeGenException_ForEachLambdaJobDescription(); return default; }

        /// <summary>
        /// Destroys the set of entities defined by the query you have defined using WithAny/WithAll/WithNone.
        /// </summary>
        /// <param name="description">The target object</param>
        public static void DestroyEntity(this ForEachLambdaJobDescription description) => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Adds a component to a set of entities selected by the query you have defined using WithAny/WithAll/WithNone
        /// </summary>
        /// <remarks>
        /// Can add any kind of component.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before adding the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="componentType">The type of component to add.</param>
        /// <param name="description">The target object</param>
        public static void AddComponent(this ForEachLambdaJobDescription description, ComponentType componentType) => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Adds components to a set of entities selected by the query you have defined using WithAny/WithAll/WithNone
        /// </summary>
        /// <remarks>
        /// Can add any kinds of components.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before adding the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="componentTypes">The type of components to add.</param>
        /// <param name="description">The target object</param>
        public static void AddComponent(this ForEachLambdaJobDescription description, in ComponentTypeSet componentTypes) => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Adds a component to a set of entities selected by the query you have defined using WithAny/WithAll/WithNone
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components.
        ///
        /// Adding a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// The added components have the default values for the type.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before adding the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <typeparam name="T">The type of component to add.</typeparam>
        /// <param name="description">The target object</param>
        public static void AddComponent<T>(this ForEachLambdaJobDescription description) where T : unmanaged, IComponentData => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Removes a component from a set of entities selected by the query you have defined using WithAny/WithAll/WithNone
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component.
        ///
        /// It's OK if some or all of the components to remove are already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before removing the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="componentType">The type of component to remove.</param>
        /// <param name="description">The target object</param>
        public static void RemoveComponent(this ForEachLambdaJobDescription description, ComponentType componentType) => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Removes a set of components from a set of entities selected by the query you have defined using
        /// WithAny/WithAll/WithNone
        /// </summary>
        /// <remarks>
        /// Can remove any kinds of components.
        ///
        /// It's OK if some or all of the components to remove are already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before removing the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="componentTypes">The types of components to add.</param>
        /// <param name="description">The target object</param>
        public static void RemoveComponent(this ForEachLambdaJobDescription description, in ComponentTypeSet componentTypes) => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Removes a component from a set of entities selected by the query you have defined using
        ///  WithAny/WithAll/WithNone
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component except chunk components.
        ///
        /// It's OK if the component to remove is already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before removing the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        /// <param name="description">The target object</param>
        public static void RemoveComponent<T>(this ForEachLambdaJobDescription description) where T : unmanaged, IComponentData => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Adds a component to a set of entities selected by the query you have defined using WithAny/WithAll/WithNone
        /// and sets the component of each entity in the query to the value in the component array.
        /// </summary>
        /// <remarks>
        /// Can add any kind of component except chunk components, managed components, and shared components.
        ///
        /// componentArray.Length must match entityQuery.ToEntityArray().Length.
        /// </remarks>
        /// <typeparam name="T">The type of component to add.</typeparam>
        /// <param name="componentArray">NativeArray that contains the components.</param>
        /// <param name="description">The target object</param>
        public static void AddComponentData<T>(this ForEachLambdaJobDescription description, NativeArray<T> componentArray) where T : unmanaged, IComponentData => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Adds a chunk component to each of the chunks identified by the query you have defined using
        /// WithAny/WithAll/WithNone and sets the component values.
        /// </summary>
        /// <remarks>
        /// This function finds all chunks whose archetype satisfies the EntityQuery and adds the specified
        /// component to them.
        ///
        /// A chunk component is common to all entities in a chunk. You can access a chunk <see cref="IComponentData"/>
        /// instance through either the chunk itself or through an entity stored in that chunk.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before adding the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="componentData">The data to set.</param>
        /// <typeparam name="T">The type of component, which must implement IComponentData.</typeparam>
        /// <param name="description">The target object</param>
        public static void AddChunkComponentData<T>(this ForEachLambdaJobDescription description, T componentData) where T : unmanaged, IComponentData => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Removes a component from a set of entities selected by the query you have defined using WithAny/WithAll/WithNone.
        /// </summary>
        /// <remarks>
        /// Can remove any kind of component except chunk components.
        ///
        /// It's OK if the component to remove is already missing from some or all of the entities.
        ///
        /// Removing a component changes an entity's archetype and results in the entity being moved to a different
        /// chunk.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before removing the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <typeparam name="T">The type of component to remove.</typeparam>
        /// <param name="description">The target object</param>
        public static void RemoveChunkComponentData<T>(this ForEachLambdaJobDescription description) where T : unmanaged, IComponentData => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Sets the shared component of all entities in the query you have defined using WithAny/WithAll/WithNone
        /// </summary>
        /// <remarks>
        /// The component data stays in the same chunk, the internal shared component data indices will be adjusted.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before setting the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        /// <param name="description">The target object</param>
        public static void AddSharedComponent<T>(this ForEachLambdaJobDescription description, T componentData) where T : unmanaged, ISharedComponentData => ThrowCodeGenException_ForEachLambdaJobDescription();

        /// <summary>
        /// Sets the shared component of all entities in the query.
        /// </summary>
        /// <remarks>
        /// The component data stays in the same chunk, the internal shared component data indices will be adjusted.
        ///
        /// **Important:** This function creates a sync point, which means that the EntityManager waits for all
        /// currently running Jobs to complete before setting the component and no additional Jobs can start before
        /// the function is finished. A sync point can cause a drop in performance because the ECS framework may not
        /// be able to make use of the processing power of all available cores.
        /// </remarks>
        /// <param name="componentData">A shared component object containing the values to set.</param>
        /// <typeparam name="T">The shared component type.</typeparam>
        /// <param name="description">The target object</param>
        public static void SetSharedComponent<T>(this ForEachLambdaJobDescription description, T componentData) where T : unmanaged, ISharedComponentData => ThrowCodeGenException_ForEachLambdaJobDescription();

        static void ThrowCodeGenException_ForEachLambdaJobDescription() => throw new Exception("This method should have been replaced by codegen");
   }
}
