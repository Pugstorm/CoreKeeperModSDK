using System;
using Unity.Entities;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Collections;
using Unity.NetCode.LowLevel.Unsafe;
using Unity.Burst;
using Unity.Jobs;
using System.Runtime.InteropServices;
using Unity.Assertions;
using Unity.Burst.Intrinsics;

namespace Unity.NetCode
{

    /// <summary>
    /// Singleton used to register a <see cref="SmoothingAction"/> for a certain component type.
    /// The <see cref="SmoothingAction"/> is used to change the component value over time correct misprediction. Two different types of
    /// smoothing action can be registered:
    /// <para>- A smoothing action without argument. See <see cref="RegisterSmoothingAction{T}"/></para>
    /// <para>- A smoothing action that take a component data as argument. See <see cref="RegisterSmoothingAction{T,U}"/></para>
    /// </summary>
    public struct GhostPredictionSmoothing : IComponentData
    {
        internal GhostPredictionSmoothing(NativeParallelHashMap<ComponentType, SmoothingActionState> actions, NativeList<ComponentType> userComp, EntityQuery singletonQuery)
        {
            m_SmoothingActions = actions;
            m_UserSpecifiedComponentData = userComp;
            m_SingletonQuery = singletonQuery;
        }

        /// <summary>
        /// All the smoothing action must have this signature. The smoothing actions must also be burst compatible.
        /// </summary>
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        public delegate void SmoothingActionDelegate(IntPtr currentData, IntPtr previousData, IntPtr userData);

        internal unsafe struct SmoothingActionState
        {
            public int compIndex;
            public int compSize;
            public int serializerIndex;
            public int entityIndex;
            public int userTypeId;
            public int userTypeSize;
            public byte* backupData;
            public PortableFunctionPointer<SmoothingActionDelegate> action;
        }

        NativeList<ComponentType> m_UserSpecifiedComponentData;
        NativeParallelHashMap<ComponentType, SmoothingActionState> m_SmoothingActions;
        EntityQuery m_SingletonQuery;

        /// <summary>
        /// Register a smoothing function that does not take any argument for the specified component type.
        /// </summary>
        /// <param name="entityManager">The EntityManager in the destination world</param>
        /// <param name="action">A burstable function pointer to the method that implement the smooting</param>
        /// <typeparam name="T">The component type. Must implement the IComponentData interface</typeparam>
        /// <returns>True if the action has been registered. False, in case of error or if the action has been already registered</returns>
        public bool RegisterSmoothingAction<T>(EntityManager entityManager, PortableFunctionPointer<SmoothingActionDelegate> action) where T : struct, IComponentData
        {
            var type = ComponentType.ReadWrite<T>();
            if (type.IsBuffer)
            {
                UnityEngine.Debug.LogError("Smoothing actions are not supported for buffers");
                return false;
            }
            if (m_SmoothingActions.ContainsKey(type))
            {
                UnityEngine.Debug.LogError($"There is already a action registered for the type {type.ToString()}");
                return false;
            }

            var actionData = new SmoothingActionState
            {
                action = action,
                compIndex = -1,
                compSize = -1,
                serializerIndex = -1,
                entityIndex = -1,
                backupData = null,
                userTypeId = -1,
                userTypeSize = -1
            };

            m_SmoothingActions.Add(type, actionData);
            if (!m_SingletonQuery.HasSingleton<GhostPredictionSmoothingSystem.SmoothingAction>())
            {
                entityManager.CreateEntity(ComponentType.ReadOnly<GhostPredictionSmoothingSystem.SmoothingAction>());
            }
            return true;
        }

        /// <summary>
        /// Register a smoothing function that take a user specified component data as argument.
        /// A maximum of 8 different component data type can be used to pass data to the smoothing functions.
        /// There is no limitation in the number of smoothing action, component type pairs that can be registed.
        /// </summary>
        /// <param name="entityManager">The EntityManager in the destination world</param>
        /// <param name="action">A burstable function pointer to the method that implement the smooting</param>
        /// <typeparam name="T">The component type. Must implement the IComponentData interface</typeparam>
        /// <typeparam name="U">The user data type that should be passed as argument to the function</typeparam>
        /// <returns>True if the action has been registered. False, in case of error or if the action has been already registered</returns>
        public bool RegisterSmoothingAction<T, U>(EntityManager entityManager, PortableFunctionPointer<SmoothingActionDelegate> action)
            where T : struct, IComponentData
            where U : struct, IComponentData
        {
            if (!RegisterSmoothingAction<T>(entityManager, action))
                return false;

            var type = ComponentType.ReadWrite<T>();
            var userType = ComponentType.ReadWrite<U>();
            var userTypeId = -1;
            for (int i = 0; i < m_UserSpecifiedComponentData.Length; ++i)
            {
                if (userType == m_UserSpecifiedComponentData[i])
                {
                    userTypeId = i;
                    break;
                }
            }
            if (userTypeId == -1)
            {
                if (m_UserSpecifiedComponentData.Length == 8)
                {
                    UnityEngine.Debug.LogError("There can only be 8 components registered as user data.");

                    m_SmoothingActions.Remove(type);

                    return false;
                }
                m_UserSpecifiedComponentData.Add(userType);
                userTypeId = m_UserSpecifiedComponentData.Length - 1;
            }
            var actionState = m_SmoothingActions[type];
            actionState.userTypeId = userTypeId;
            actionState.userTypeSize = UnsafeUtility.SizeOf<U>();

            m_SmoothingActions[type] = actionState;
            return true;
        }
    }

    /// <summary>
    /// System that corrects the client prediction errors, by applying the smoothing actions
    /// registerd to the <see cref="GhostPredictionSmoothing"/> singleton to to all predicted ghost that miss-predict.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation)]
    [UpdateInGroup(typeof(PredictedSimulationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(GhostPredictionHistorySystem))]
    [BurstCompile]
    public partial struct GhostPredictionSmoothingSystem : ISystem
    {

        EntityQuery m_PredictionQuery;

        NativeList<ComponentType> m_UserSpecifiedComponentData;
        NativeParallelHashMap<ComponentType, GhostPredictionSmoothing.SmoothingActionState> m_SmoothingActions;

        internal struct SmoothingAction : IComponentData {}

        ComponentTypeHandle<GhostInstance> m_GhostComponentHandle;
        ComponentTypeHandle<PredictedGhost> m_PredictedGhostHandle;
        BufferTypeHandle<LinkedEntityGroup> m_LinkedEntityGroupHandle;
        EntityTypeHandle m_EntityTypeHandle;

        BufferLookup<GhostComponentSerializer.State> m_GhostComponentSerializerStateFromEntity;
        BufferLookup<GhostCollectionPrefabSerializer> m_GhostCollectionPrefabSerializerFromEntity;
        BufferLookup<GhostCollectionComponentIndex> m_GhostCollectionComponentIndexFromEntity;

        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<PredictedGhost, GhostInstance>();
            m_PredictionQuery = state.GetEntityQuery(builder);

            m_UserSpecifiedComponentData = new NativeList<ComponentType>(8, Allocator.Persistent);
            m_SmoothingActions = new NativeParallelHashMap<ComponentType, GhostPredictionSmoothing.SmoothingActionState>(32, Allocator.Persistent);

            state.RequireForUpdate<GhostCollection>();
            state.RequireForUpdate<SmoothingAction>();


            m_GhostComponentHandle = state.GetComponentTypeHandle<GhostInstance>(true);
            m_PredictedGhostHandle = state.GetComponentTypeHandle<PredictedGhost>(true);
            m_LinkedEntityGroupHandle = state.GetBufferTypeHandle<LinkedEntityGroup>(true);
            m_EntityTypeHandle = state.GetEntityTypeHandle();

            m_GhostComponentSerializerStateFromEntity = state.GetBufferLookup<GhostComponentSerializer.State>(true);
            m_GhostCollectionPrefabSerializerFromEntity = state.GetBufferLookup<GhostCollectionPrefabSerializer>(true);
            m_GhostCollectionComponentIndexFromEntity = state.GetBufferLookup<GhostCollectionComponentIndex>(true);

            builder = new EntityQueryBuilder(Allocator.Temp).WithAll<SmoothingAction>();
            var enableQuery = state.GetEntityQuery(builder);
            var atype = new NativeArray<ComponentType>(1, Allocator.Temp);
            atype[0] = ComponentType.ReadWrite<GhostPredictionSmoothing>();
            var smoothingSingleton = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(atype));
            FixedString64Bytes singletonName = "GhostPredictionSmoothing-Singleton";
            state.EntityManager.SetName(smoothingSingleton, singletonName);
            SystemAPI.SetSingleton(new GhostPredictionSmoothing(m_SmoothingActions, m_UserSpecifiedComponentData, enableQuery));
        }
        [BurstCompile]
        public void OnDestroy(ref SystemState state)
        {
            m_UserSpecifiedComponentData.Dispose();
            m_SmoothingActions.Dispose();
        }
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var newtorkTime = SystemAPI.GetSingleton<NetworkTime>();
            var lastBackupTime = SystemAPI.GetSingleton<GhostSnapshotLastBackupTick>();

            if (newtorkTime.ServerTick != lastBackupTime.Value)
                return;

            if (m_SmoothingActions.IsEmpty)
            {
                state.EntityManager.DestroyEntity(SystemAPI.GetSingletonEntity<SmoothingAction>());
                return;
            }


            m_GhostComponentHandle.Update(ref state);
            m_PredictedGhostHandle.Update(ref state);
            m_LinkedEntityGroupHandle.Update(ref state);
            m_EntityTypeHandle.Update(ref state);

            m_GhostComponentSerializerStateFromEntity.Update(ref state);
            m_GhostCollectionPrefabSerializerFromEntity.Update(ref state);
            m_GhostCollectionComponentIndexFromEntity.Update(ref state);
            var smoothingJob = new PredictionSmoothingJob
            {
                predictionState = SystemAPI.GetSingleton<GhostPredictionHistoryState>().PredictionState,
                ghostType = m_GhostComponentHandle,
                predictedGhostType = m_PredictedGhostHandle,
                entityType = m_EntityTypeHandle,

                GhostCollectionSingleton = SystemAPI.GetSingletonEntity<GhostCollection>(),
                GhostComponentCollectionFromEntity = m_GhostComponentSerializerStateFromEntity,
                GhostTypeCollectionFromEntity = m_GhostCollectionPrefabSerializerFromEntity,
                GhostComponentIndexFromEntity = m_GhostCollectionComponentIndexFromEntity,

                childEntityLookup = state.GetEntityStorageInfoLookup(),
                linkedEntityGroupType = m_LinkedEntityGroupHandle,
                tick = newtorkTime.ServerTick,

                smoothingActions = m_SmoothingActions
            };

            var ghostComponentCollection = state.EntityManager.GetBuffer<GhostCollectionComponentType>(smoothingJob.GhostCollectionSingleton);
            DynamicTypeList.PopulateList(ref state, ghostComponentCollection, false, ref smoothingJob.DynamicTypeList);
            DynamicTypeList.PopulateListFromArray(ref state, m_UserSpecifiedComponentData.AsArray(), true, ref smoothingJob.UserList);

            state.Dependency = smoothingJob.ScheduleParallelByRef(m_PredictionQuery, state.Dependency);
        }

        [BurstCompile]
        struct PredictionSmoothingJob : IJobChunk
        {
            public DynamicTypeList DynamicTypeList;
            public DynamicTypeList UserList;
            public NativeParallelHashMap<ArchetypeChunk, System.IntPtr>.ReadOnly predictionState;

            [ReadOnly] public ComponentTypeHandle<GhostInstance> ghostType;
            [ReadOnly] public ComponentTypeHandle<PredictedGhost> predictedGhostType;
            [ReadOnly] public EntityTypeHandle entityType;

            public Entity GhostCollectionSingleton;
            [ReadOnly] public BufferLookup<GhostComponentSerializer.State> GhostComponentCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionPrefabSerializer> GhostTypeCollectionFromEntity;
            [ReadOnly] public BufferLookup<GhostCollectionComponentIndex> GhostComponentIndexFromEntity;

            [ReadOnly] public EntityStorageInfoLookup childEntityLookup;
            [ReadOnly] public BufferTypeHandle<LinkedEntityGroup> linkedEntityGroupType;

            [ReadOnly] public NativeParallelHashMap<ComponentType, GhostPredictionSmoothing.SmoothingActionState> smoothingActions;
            public NetworkTick tick;

            const GhostSendType requiredSendMask = GhostSendType.OnlyPredictedClients;

            public unsafe void Execute(in ArchetypeChunk chunk, int unfilteredChunkIndex, bool useEnabledMask, in v128 chunkEnabledMask)
            {
                // This job is not written to support queries with enableable component types.
                Assert.IsFalse(useEnabledMask);

                if (!predictionState.TryGetValue(chunk, out var state) ||
                    (*(PredictionBackupState*)state).entityCapacity != chunk.Capacity)
                    return;

                DynamicComponentTypeHandle* ghostChunkComponentTypesPtr = DynamicTypeList.GetData();
                int ghostChunkComponentTypesLength = DynamicTypeList.Length;
                DynamicComponentTypeHandle* userTypes = UserList.GetData();
                int userTypesLength = UserList.Length;

                var GhostTypeCollection = GhostTypeCollectionFromEntity[GhostCollectionSingleton];
                var GhostComponentIndex = GhostComponentIndexFromEntity[GhostCollectionSingleton];
                var GhostComponentCollection = GhostComponentCollectionFromEntity[GhostCollectionSingleton];

                var ghostComponents = chunk.GetNativeArray(ref ghostType);

                int ghostTypeId = ghostComponents.GetFirstGhostTypeId();
                if (ghostTypeId < 0)
                    return;
                if (ghostTypeId >= GhostTypeCollection.Length)
                    return; // serialization data has not been loaded yet. This can only happen for prespawn objects

                var typeData = GhostTypeCollection[ghostTypeId];
                Entity* backupEntities = PredictionBackupState.GetEntities(state);
                var entities = chunk.GetNativeArray(entityType);

                var PredictedGhosts = chunk.GetNativeArray(ref predictedGhostType);

                int numBaseComponents = typeData.NumComponents - typeData.NumChildComponents;
                var actions = new NativeList<GhostPredictionSmoothing.SmoothingActionState>(Allocator.Temp);
                var childActions = new NativeList<GhostPredictionSmoothing.SmoothingActionState>(Allocator.Temp);

                byte* dataPtr = PredictionBackupState.GetData(state);
                // todo: this loop could be cached on chunk.capacity, because now we are re-calculating it everytime.
                for (int comp = 0; comp < typeData.NumComponents; ++comp)
                {
                    int index = typeData.FirstComponent + comp;
                    int compIdx = GhostComponentIndex[index].ComponentIndex;
                    int serializerIdx = GhostComponentIndex[index].SerializerIndex;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
                    if (compIdx >= ghostChunkComponentTypesLength)
                        throw new System.InvalidOperationException("Component index out of range");
#endif
                    if ((GhostComponentIndex[index].SendMask&requiredSendMask) == 0)
                        continue;

                    //Buffer does not have any smoothing
                    if (GhostComponentCollection[serializerIdx].ComponentType.IsBuffer)
                    {
                        dataPtr = PredictionBackupState.GetNextData(dataPtr, GhostComponentSerializer.DynamicBufferComponentSnapshotSize, chunk.Capacity);
                        continue;
                    }
                    var compSize = GhostComponentCollection[serializerIdx].ComponentSize;
                    if (smoothingActions.TryGetValue(GhostComponentCollection[serializerIdx].ComponentType, out var action))
                    {
                        action.compIndex = compIdx;
                        action.compSize = compSize;
                        action.serializerIndex = serializerIdx;
                        action.entityIndex = GhostComponentIndex[index].EntityIndex;
                        action.backupData = dataPtr;

                        if (comp < numBaseComponents)
                            actions.Add(action);
                        else
                            childActions.Add(action);
                    }
                    dataPtr = PredictionBackupState.GetNextData(dataPtr, compSize, chunk.Capacity);
                }

                foreach (var action in actions)
                {
                    if (chunk.Has(ref ghostChunkComponentTypesPtr[action.compIndex]))
                    {
                        for (int ent = 0; ent < entities.Length; ++ent)
                        {
                            // If this entity did not predict anything there was no rollback and no need to debug it
                            if (!PredictedGhosts[ent].ShouldPredict(tick))
                                continue;

                            if (entities[ent] != backupEntities[ent])
                                continue;

                            var compData = (byte*)chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[action.compIndex], action.compSize).GetUnsafePtr();

                            void* usrDataPtr = null;
                            if (action.userTypeId >= 0 && chunk.Has(ref userTypes[action.userTypeId]))
                            {
                                var usrData = (byte*)chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref userTypes[action.userTypeId], action.userTypeSize).GetUnsafeReadOnlyPtr();
                                usrDataPtr = usrData + action.userTypeSize * ent;
                            }

                            action.action.Ptr.Invoke((IntPtr)(compData + action.compSize * ent), (IntPtr)(action.backupData + action.compSize * ent),
                                (IntPtr)usrDataPtr);
                        }
                    }
                }

                var linkedEntityGroupAccessor = chunk.GetBufferAccessor(ref linkedEntityGroupType);
                foreach (var action in childActions)
                {
                    for (int ent = 0, chunkEntityCount = chunk.Count; ent < chunkEntityCount; ++ent)
                    {
                        // If this entity did not predict anything there was no rollback and no need to debug it
                        if (!PredictedGhosts[ent].ShouldPredict(tick))
                            continue;
                        if (entities[ent] != backupEntities[ent])
                            continue;
                        var linkedEntityGroup = linkedEntityGroupAccessor[ent];
                        var childEnt = linkedEntityGroup[action.entityIndex].Value;
                        if (childEntityLookup.TryGetValue(childEnt, out var childChunk) &&
                            childChunk.Chunk.Has(ref ghostChunkComponentTypesPtr[action.compIndex]))
                        {
                            var compData = (byte*)childChunk.Chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref ghostChunkComponentTypesPtr[action.compIndex], action.compSize).GetUnsafePtr();

                            void* usrDataPtr = null;
                            if (action.userTypeId >= 0 && chunk.Has(ref userTypes[action.userTypeId]))
                            {
                                var usrData = (byte*)chunk.GetDynamicComponentDataArrayReinterpret<byte>(ref userTypes[action.userTypeId], action.userTypeSize).GetUnsafeReadOnlyPtr();
                                usrDataPtr = usrData + action.userTypeSize * ent;
                            }
                            action.action.Ptr.Invoke((IntPtr)(compData + action.compSize * childChunk.IndexInChunk), (IntPtr)(action.backupData + action.compSize * ent), (IntPtr)usrDataPtr);
                        }
                    }
                }
            }
        }
    }
}
