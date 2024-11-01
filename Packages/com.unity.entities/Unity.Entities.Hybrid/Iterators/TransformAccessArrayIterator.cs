using System;
using System.Linq;
using System.Reflection;

using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.Jobs;
using UnityEngine.Scripting;

namespace Unity.Entities
{
    class TransformAccessArrayState : IDisposable
    {
        public TransformAccessArray Data;
        public int OrderVersion;

        public void Dispose()
        {
            if (Data.isCreated)
                Data.Dispose();
        }
    }

    /// <summary>
    /// Allows access to GameObject transform data through an EntityQuery.
    /// </summary>
    public static class EntityQueryExtensionsForTransformAccessArray
    {
        /// <summary>
        /// Allows access to GameObject transform data through an EntityQuery.
        /// </summary>
        /// <param name="query">The query matching entities whose transform data should be gathered</param>
        /// <returns>An object that allows access to entity transform data</returns>
        public static unsafe TransformAccessArray GetTransformAccessArray(this EntityQuery query)
        {
            var state = (TransformAccessArrayState)query._CachedState;

            if (state == null)
                state = new TransformAccessArrayState();

            var orderVersion = query._GetImpl()->_Access->EntityComponentStore->GetComponentTypeOrderVersion(TypeManager.GetTypeIndex<Transform>());

            if (state.Data.isCreated && orderVersion == state.OrderVersion)
                return state.Data;

            state.OrderVersion = orderVersion;

            UnityEngine.Profiling.Profiler.BeginSample("DirtyTransformAccessArrayUpdate");
            var trans = query.ToComponentArray<Transform>();
            if (!state.Data.isCreated)
                state.Data = new TransformAccessArray(trans);
            else
                state.Data.SetTransforms(trans);
            UnityEngine.Profiling.Profiler.EndSample();

            query._CachedState = state;

            return state.Data;
        }
    }
}
