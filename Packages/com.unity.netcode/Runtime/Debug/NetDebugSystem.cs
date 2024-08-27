#if UNITY_EDITOR && !NETCODE_NDEBUG
#define NETCODE_DEBUG
#endif
using Unity.Collections;
using Unity.Entities;
using Unity.Burst;
namespace Unity.NetCode
{
    /// <summary>
    /// Systems responsible to initialize and create the <see cref="NetDebug"/> singleton and to flush all logs.
    /// </summary>
    [BurstCompile]
    [RequireMatchingQueriesForUpdate]
    [UpdateInGroup(typeof(GhostSimulationSystemGroup))]
    [WorldSystemFilter(WorldSystemFilterFlags.Default | WorldSystemFilterFlags.ThinClientSimulation)]
    [CreateBefore(typeof(NetworkStreamReceiveSystem))]
    [UpdateAfter(typeof(GhostCollectionSystem))]
    public partial struct NetDebugSystem : ISystem
    {
        private ComponentLookup<GhostPrefabMetaData> m_GhostPrefabMetadata;
        private EntityQuery m_prefabsWithoutDebugNameQuery;
#if NETCODE_DEBUG
        private ComponentLookup<PrefabDebugName> m_PrefabDebugNameData;
        private NativeHashMap<int, FixedString128Bytes> m_ComponentTypeNameLookupData;
#endif

        private void CreateNetDebugSingleton(ref SystemState state)
        {
            var netDebug = new NetDebug();
            netDebug.Initialize();
#if UNITY_EDITOR
            if (MultiplayerPlayModePreferences.ApplyLoggerSettings)
                netDebug.LogLevel = MultiplayerPlayModePreferences.TargetLogLevel;
#endif
#if NETCODE_DEBUG
            m_ComponentTypeNameLookupData = new NativeHashMap<int, FixedString128Bytes>(1024, Allocator.Persistent);
            netDebug.ComponentTypeNameLookup = m_ComponentTypeNameLookupData.AsReadOnly();
#endif
            state.EntityManager.CreateSingleton(netDebug);
        }

        public void OnCreate(ref SystemState state)
        {
            CreateNetDebugSingleton(ref state);
            // Declare write dependency
            SystemAPI.GetSingletonRW<NetDebug>();
            m_GhostPrefabMetadata = state.GetComponentLookup<GhostPrefabMetaData>(true);
#if NETCODE_DEBUG
            m_PrefabDebugNameData = state.GetComponentLookup<PrefabDebugName>();
            m_prefabsWithoutDebugNameQuery = state.GetEntityQuery(
                ComponentType.ReadOnly<Prefab>(),
                ComponentType.ReadOnly<GhostPrefabMetaData>(),
                ComponentType.Exclude<PrefabDebugName>());
#endif
        }


        public void OnDestroy(ref SystemState state)
        {
            SystemAPI.GetSingletonRW<NetDebug>().ValueRW.Dispose();
#if NETCODE_DEBUG
            m_ComponentTypeNameLookupData.Dispose();
#endif
        }

#if NETCODE_DEBUG
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            using var prefabsWithoutDebugName = m_prefabsWithoutDebugNameQuery.ToEntityArray(Allocator.Temp);

            state.EntityManager.AddComponent<PrefabDebugName>(m_prefabsWithoutDebugNameQuery);

            m_GhostPrefabMetadata.Update(ref state);
            m_PrefabDebugNameData.Update(ref state);

            foreach (var entity in prefabsWithoutDebugName)
            {
                var prefabMetaData = m_GhostPrefabMetadata[entity];
                m_PrefabDebugNameData[entity] = new PrefabDebugName
                {
                    PrefabName = new LowLevel.BlobStringText(ref prefabMetaData.Value.Value.Name)
                };
            }

            state.CompleteDependency();
            var ghostComponentTypes = SystemAPI.GetSingletonBuffer<GhostCollectionComponentType>();
            for (var i = 0; i < ghostComponentTypes.Length; ++i)
            {
                var typeIndex = ghostComponentTypes[i].Type.TypeIndex;
                var typeName = ghostComponentTypes[i].Type.ToFixedString();
                m_ComponentTypeNameLookupData.TryAdd(typeIndex, typeName);
            }
        }
#endif
    }
}
