using System;
using System.Collections.Generic;
using System.Text;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.Analytics;
using UnityEditor;
using UnityEngine.Analytics;

namespace Unity.NetCode.Editor
{
    [InitializeOnLoad]
    internal static class PlayStateNotifier
    {
        static PlayStateNotifier()
        {
            EditorApplication.playModeStateChanged += ModeChanged;
        }

        static void ModeChanged(PlayModeStateChange playModeState)
        {
            if (playModeState != PlayModeStateChange.ExitingPlayMode)
            {
                return;
            }

            var scaleData = ComputeScaleData();
            GhostComponentAnalytics.SendGhostComponentScale(scaleData);
            var configurationData = ComputeConfigurationData();
            GhostComponentAnalytics.SendGhostComponentConfiguration(configurationData);
        }

        static GhostScaleAnalyticsData ComputeScaleData()
        {
            var data = new GhostScaleAnalyticsData
            {
                playerCount = NetCodeAnalyticsState.GetPlayerCount(),
                settings = new PlaymodeSettings
                {
                    thinClientCount = MultiplayerPlayModePreferences.RequestedNumThinClients,
                    simulatorEnabled = MultiplayerPlayModePreferences.SimulatorEnabled,
                    delay = MultiplayerPlayModePreferences.PacketDelayMs,
                    dropPercentage = MultiplayerPlayModePreferences.PacketDropPercentage,
                    jitter = MultiplayerPlayModePreferences.PacketJitterMs,
                    playModeType = MultiplayerPlayModePreferences.RequestedPlayType.ToString()
                }
            };

            var spawnedGhostCount = 0;
            uint serializedSent = 0;
            uint amount = 0;
            var ghostTypes = new List<GhostTypeData>();

            foreach (var world in World.All)
            {
                if (!world.IsServer())
                {
                    continue;
                }

                CollectGhostTypes(world.EntityManager, ghostTypes);
                var sent = NetCodeAnalyticsState.GetUpdateLength(world);
                if (sent > 0)
                {
                    amount++;
                    serializedSent += sent;
                }

                spawnedGhostCount += CountSpawnedGhosts(world.EntityManager);
            }

            data.ghostTypes = ghostTypes.ToArray();
            data.spawnedGhostCount = spawnedGhostCount;
            data.ghostTypeCount = ghostTypes.Count;
            if (amount == 0)
            {
                data.averageGhostInSnapshot = 0;
            }
            else
            {
                data.averageGhostInSnapshot = serializedSent / amount;
            }

            return data;
        }

        static GhostConfigurationAnalyticsData[] ComputeConfigurationData()
        {
            var data = NetCodeAnalytics.RetrieveGhostComponents();
            NetCodeAnalytics.ClearGhostComponents();
            return data;
        }

        static int CountSpawnedGhosts(EntityManager entityManager)
        {
            var spawnedGhostEntityQuery =
                entityManager.CreateEntityQuery(ComponentType.ReadOnly<SpawnedGhostEntityMap>());
            if (spawnedGhostEntityQuery.CalculateEntityCount() != 1)
            {
                return 0;
            }

            var ghostMapSingleton =
                spawnedGhostEntityQuery.ToComponentDataArray<SpawnedGhostEntityMap>(Allocator.Temp)[0];
            return ghostMapSingleton.Value.Count();
        }

        static void CollectGhostTypes(EntityManager entityManager, List<GhostTypeData> ghostTypes)
        {
            var ghostCollectionQuery = entityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostCollection>());
            if (ghostCollectionQuery.CalculateEntityCount() != 1)
            {
                return;
            }

            var ghostCollection = ghostCollectionQuery.GetSingletonEntity();
            var ghostCollectionPrefab = entityManager.GetBuffer<GhostCollectionPrefab>(ghostCollection);
            var ghostCollectionPrefabSerializers =
                entityManager.GetBuffer<GhostCollectionPrefabSerializer>(ghostCollection);

            for (var index = 0; index < ghostCollectionPrefabSerializers.Length; index++)
            {
                var prefabSerializer = ghostCollectionPrefabSerializers[index];
                var ghostPrefab = ghostCollectionPrefab[index].GhostPrefab;
                var archetype = entityManager.GetChunk(ghostPrefab).Archetype;
                var ghostId = entityManager.GetComponentData<GhostType>(ghostPrefab);
                ghostTypes.Add(new GhostTypeData()
                {
                    ghostId = $"{ghostId.guid0:x}{ghostId.guid1:x}{ghostId.guid2:x}{ghostId.guid3:x}",
                    childrenCount = prefabSerializer.NumChildComponents,
                    componentCount = archetype.TypesCount,
                    componentsWithSerializedDataCount = prefabSerializer.NumComponents
                });
            }
        }
    }

    [Serializable]
    struct GhostTypeData
    {
        public string ghostId;
        public int childrenCount;
        public int componentCount;
        public int componentsWithSerializedDataCount;

        public override string ToString()
        {
            return $"{nameof(ghostId)}: {ghostId}, " +
                   $"{nameof(childrenCount)}: {childrenCount}, " +
                   $"{nameof(componentCount)}: {componentCount}, " +
                   $"{nameof(componentsWithSerializedDataCount)}: {componentsWithSerializedDataCount}";
        }
    }

    [Serializable]
    struct GhostScaleAnalyticsData
    {
        public PlaymodeSettings settings;
        public int playerCount;
        public int spawnedGhostCount;
        public int ghostTypeCount;
        public uint averageGhostInSnapshot;
        public GhostTypeData[] ghostTypes;

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.Append($"{nameof(settings)}: {settings}, " +
                           $"{nameof(playerCount)}: {playerCount}, " +
                           $"{nameof(spawnedGhostCount)}: {spawnedGhostCount}, " +
                           $"{nameof(ghostTypeCount)}: {ghostTypeCount}, " +
                           $"{nameof(averageGhostInSnapshot)}: {averageGhostInSnapshot}, {nameof(ghostTypes)}:\n");

            foreach (var ghostTypeData in ghostTypes)
            {
                builder.AppendLine($"[{ghostTypeData}],");
            }

            return builder.ToString();
        }
    }

    [Serializable]
    struct PlaymodeSettings
    {
        public int thinClientCount;
        public bool simulatorEnabled;
        public int delay;
        public int dropPercentage;
        public int jitter;
        public string playModeType;

        public override string ToString()
        {
            return $"{nameof(thinClientCount)}: {thinClientCount}, " +
                   $"{nameof(simulatorEnabled)}: {simulatorEnabled}, " +
                   $"{nameof(delay)}, {delay}, " +
                   $"{nameof(dropPercentage)}, {dropPercentage}, " +
                   $"{nameof(playModeType)}, {playModeType}, " +
                   $"{nameof(jitter)}, {jitter}";
        }
    }

    static class GhostComponentAnalytics
    {
        static bool s_ScaleRegistered = false;
        static bool s_ConfigurationRegistered = false;
        const int k_MaxEventsPerHour = 1000;
        const int k_MaxNumberOfElements = 1000;
        const string k_VendorKey = "unity.netcode";
        const string k_Scale = "NetcodeGhostComponentScale";
        const string k_Configuration = "NetcodeGhostComponentConfiguration";

        /// <summary>
        /// This will add or update the buffer containing the configuration data from a <see cref="GhostAuthoringComponent"/>.
        /// </summary>
        /// <param name="ghostComponent">Retrieve data from this component.</param>
        /// <param name="numVariants">Count of the number of variants on this ghost.</param>
        public static void BufferConfigurationData(GhostAuthoringComponent ghostComponent, int numVariants)
        {
            var analyticsData = new GhostConfigurationAnalyticsData
            {
                id = ghostComponent.prefabId,
                autoCommandTarget = ghostComponent.SupportAutoCommandTarget,
                optimizationMode = ghostComponent.OptimizationMode.ToString(),
                ghostMode = ghostComponent.DefaultGhostMode.ToString(),
                importance = ghostComponent.Importance,
                variance = numVariants,
            };
            NetCodeAnalytics.StoreGhostComponent(analyticsData);
        }

        static bool EnableScaleAnalytics()
        {
            AnalyticsResult result = EditorAnalytics.RegisterEventWithLimit(k_Scale, k_MaxEventsPerHour,
                k_MaxNumberOfElements, k_VendorKey);
            if (result == AnalyticsResult.Ok)
            {
                s_ScaleRegistered = true;
            }

            return s_ScaleRegistered;
        }

        static bool EnableConfigurationAnalytics()
        {
            AnalyticsResult result = EditorAnalytics.RegisterEventWithLimit(k_Configuration, k_MaxEventsPerHour,
                k_MaxNumberOfElements, k_VendorKey);
            if (result == AnalyticsResult.Ok)
            {
                s_ConfigurationRegistered = true;
            }

            return s_ConfigurationRegistered;
        }

        static bool CanSendAnalytics()
        {
            return EditorAnalytics.enabled;
        }

        public static void SendGhostComponentScale(GhostScaleAnalyticsData data)
        {
            if (!CanSendAnalytics() || !EnableScaleAnalytics())
            {
                return;
            }

            EditorAnalytics.SendEventWithLimit(k_Scale, data);
        }

        public static void SendGhostComponentConfiguration(GhostConfigurationAnalyticsData[] data)
        {
            if (!CanSendAnalytics() || !EnableConfigurationAnalytics())
            {
                return;
            }

            foreach (var analyticsData in data)
            {
                EditorAnalytics.SendEventWithLimit(k_Configuration, analyticsData);
            }
        }
    }
}
