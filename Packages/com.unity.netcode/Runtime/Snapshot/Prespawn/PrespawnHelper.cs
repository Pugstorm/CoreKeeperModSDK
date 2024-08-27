using System;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.NetCode.LowLevel.Unsafe;

namespace Unity.NetCode
{
    internal static class PrespawnHelper
    {
        public const uint PrespawnGhostIdBase = 0x80000000;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public int MakePrespawnGhostId(int ghostId)
        {
            return (int) (PrespawnGhostIdBase | ghostId);
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public bool IsPrespawnGhostId(int ghostId)
        {
            return (ghostId & PrespawnGhostIdBase) != 0;
        }
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public bool IsRuntimeSpawnedGhost(int ghostId)
        {
            return (ghostId & PrespawnGhostIdBase) == 0;
        }

        //return a valid index if a reserved range that contains the ghost id is present, otherwise 0
        static public int GhostIdRangeIndex(ref this DynamicBuffer<PrespawnGhostIdRange> ranges , long ghostId)
        {
            ghostId &= ~PrespawnHelper.PrespawnGhostIdBase;
            for (int i = 0; i < ranges.Length; ++i)
            {
                if(ranges[i].Reserved != 0 &&
                   ghostId >= ranges[i].FirstGhostId &&
                   ghostId < ranges[i].FirstGhostId + ranges[i].Count)
                    return i;
            }
            return -1;
        }

        static public Entity CreatePrespawnSceneListGhostPrefab(EntityManager entityManager)
        {
            var e = entityManager.CreateEntity();
            entityManager.AddBuffer<PrespawnSceneLoaded>(e);

            // Use predicted ghost mode, so we always get the latest received value instead of waiting for the interpolation delay
            var config = new GhostPrefabCreation.Config
            {
                Name = "PrespawnSceneList",
                Importance = 1,
                SupportedGhostModes = GhostModeMask.Predicted,
                DefaultGhostMode = GhostMode.Predicted,
                OptimizationMode = GhostOptimizationMode.Static,
                UsePreSerialization = false
            };

            //I need an unique identifier and should not clash with any loaded prefab.
            GhostPrefabCreation.ConvertToGhostPrefab(entityManager, e, config);

            return e;
        }

        public struct GhostIdInterval: IComparable<GhostIdInterval>
        {
            public int Begin;
            public int End;

            public GhostIdInterval(int begin, int end)
            {
                Begin = begin;
                End = end;
            }
            //Simplified sorting for non overlapping intervals
            public int CompareTo(GhostIdInterval other)
            {
                return Begin.CompareTo(other.Begin);
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static public void PopulateSceneHashLookupTable(EntityQuery query, EntityManager entityManager, NativeParallelHashMap<int, ulong> hashMap)
        {
            var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            var sharedComponentType = entityManager.GetDynamicSharedComponentTypeHandle(ComponentType.ReadOnly<SubSceneGhostComponentHash>());
            hashMap.Clear();
            for (int i = 0; i < chunks.Length; ++i)
            {
                var sharedComponentIndex = chunks[i].GetSharedComponentIndex(ref sharedComponentType);
                var sharedComponentValue = entityManager.GetSharedComponent<SubSceneGhostComponentHash>(sharedComponentIndex);
                hashMap.TryAdd(sharedComponentIndex, sharedComponentValue.Value);
            }
        }


        static public void UpdatePrespawnAckSceneMap(ref ConnectionStateData connectionState,
            Entity PrespawnSceneLoadedEntity,
            in BufferLookup<PrespawnSectionAck> prespawnAckFromEntity,
            in BufferLookup<PrespawnSceneLoaded> prespawnSceneLoadedFromEntity)
        {
            var connectionEntity = connectionState.Entity;
            var clientPrespawnSceneMap = connectionState.AckedPrespawnSceneMap;
            var prespawnSceneLoaded = prespawnSceneLoadedFromEntity[PrespawnSceneLoadedEntity];
            ref var newLoadedRanges = ref connectionState.NewLoadedPrespawnRanges;
            newLoadedRanges.Clear();
            if (!prespawnAckFromEntity.HasBuffer(connectionEntity))
            {
                clientPrespawnSceneMap.Clear();
                return;
            }
            var prespawnAck = prespawnAckFromEntity[connectionEntity];
            var newMap = new NativeParallelHashMap<ulong, int>(prespawnAck.Length, Allocator.Temp);
            for (int i = 0; i < prespawnAck.Length; ++i)
            {
                if(!clientPrespawnSceneMap.ContainsKey(prespawnAck[i].SceneHash))
                    newMap.Add(prespawnAck[i].SceneHash, 1);
                else
                    newMap.Add(prespawnAck[i].SceneHash, 0);
            }
            clientPrespawnSceneMap.Clear();
            for (int i = 0; i < prespawnSceneLoaded.Length; ++i)
            {
                if (newMap.TryGetValue(prespawnSceneLoaded[i].SubSceneHash, out var present))
                {
                    clientPrespawnSceneMap.TryAdd(prespawnSceneLoaded[i].SubSceneHash, 1);
                    //Brand new
                    if(present == 1)
                    {
                        newLoadedRanges.Add(new GhostIdInterval(
                            PrespawnHelper.MakePrespawnGhostId(prespawnSceneLoaded[i].FirstGhostId),
                            PrespawnHelper.MakePrespawnGhostId(prespawnSceneLoaded[i].FirstGhostId + prespawnSceneLoaded[i].PrespawnCount - 1)));
                    }
                }
            }
            newLoadedRanges.Sort();
        }
    }

    internal static class PrespawnSubsceneElementExtensions
    {
        public static int IndexOf(this DynamicBuffer<PrespawnSceneLoaded> subsceneElements, ulong hash)
        {
            for (int i = 0; i < subsceneElements.Length; ++i)
            {
                if (subsceneElements[i].SubSceneHash == hash)
                    return i;
            }

            return -1;
        }

        public static int IndexOf(this DynamicBuffer<PrespawnSectionAck> subsceneElements, ulong hash)
        {
            for (int i = 0; i < subsceneElements.Length; ++i)
            {
                if (subsceneElements[i].SceneHash == hash)
                    return i;
            }

            return -1;
        }
        public static bool RemoveScene(this DynamicBuffer<PrespawnSectionAck> subsceneElements, ulong hash)
        {
            for (int i = 0; i < subsceneElements.Length; ++i)
            {
                if (subsceneElements[i].SceneHash == hash)
                {
                    subsceneElements.RemoveAt(i);
                    return true;
                }
            }

            return false;
        }
    }

}
