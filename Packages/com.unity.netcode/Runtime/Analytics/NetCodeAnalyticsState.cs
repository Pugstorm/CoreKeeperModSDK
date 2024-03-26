#if UNITY_EDITOR
using System.Linq;
using Unity.Entities;
using UnityEditor;
using UnityEngine.Assertions;

namespace Unity.NetCode.Analytics
{
    internal static class NetCodeAnalyticsState
    {
        const string NetCodePlayerCount = "netcode_player_count";
        public static void SetPlayerCount(int playerCount)
        {
            SessionState.SetInt(NetCodePlayerCount, playerCount);
        }

        public static int GetPlayerCount()
        {
            return SessionState.GetInt(NetCodePlayerCount, 0);
        }

        public static uint GetUpdateLength(World world)
        {
            using var query = world.EntityManager.CreateEntityQuery(ComponentType.ReadOnly<GhostSendSystemAnalyticsData>());
            if (query.CalculateEntityCount() != 1)
            {
                return 0;
            }

            var sendSystemAnalyticsData = world.EntityManager.GetComponentData<GhostSendSystemAnalyticsData>(query.GetSingletonEntity());
            return ComputeAverageUpdateLengths(sendSystemAnalyticsData);
        }

        static uint ComputeAverageUpdateLengths(GhostSendSystemAnalyticsData sendSystemAnalyticsData)
        {
            var sums = sendSystemAnalyticsData.UpdateLenSums.Where(update => update != 0).ToArray();
            var numberOfSums = (uint)sums.Length;
            if (numberOfSums == 0)
            {
                return 0;
            }

            uint average = 0;
            var updates = sendSystemAnalyticsData.NumberOfUpdates.Where(update => update != 0).ToArray();
            Assert.AreEqual(numberOfSums, updates.Length);
            for (var index = 0; index < sums.Length; index++)
            {
                var sum = sums[index];
                average += sum / updates[index];
            }

            return average / numberOfSums;
        }
    }
}
#endif
