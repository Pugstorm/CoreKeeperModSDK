using System;
using Unity.Burst;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace Unity.NetCode
{
    /*
        Example 1:
        This registers the DefaultTranslationSmoothingAction for Translation on your predicted Ghost

        World.GetSingleton<GhostPredictionSmoothing>().RegisterSmoothingAction<Translation>(EntityManager, DefaultTranslationSmoothingAction.Action);

        Example 2:
        Here we also register the DefaultUserParamsComponent as user data. Note the DefaultSmoothingActionUserParams must be
        attached to your PredictedGhost.

        World.GetSingleton<GhostPredictionSmoothing>().RegisterSmoothingAction<Translation, DefaultUserParams>(EntityManager, DefaultTranslationSmoothingAction.Action);
    */

    /// <summary>
    /// Add the DefaultSmoothingActionUserParams component to customise on a per-entity basis the prediction error range in which the
    /// position smoothing is active.
    /// </summary>
    [GhostComponent(PrefabType = GhostPrefabType.PredictedClient)]
    public struct DefaultSmoothingActionUserParams : IComponentData
    {
        /// <summary>
        /// If the prediction error is larger than this value, the entity position is snapped to the new value.
        /// </summary>
        public float maxDist;
        /// <summary>
        /// If the prediction error is smaller than this value, the entity position is snapped to the new value.
        /// </summary>
        public float delta;
    }

    /// <summary>
    /// The default prediction error <see cref="SmoothingAction"/> function for the <see cref="Translation"/> component.
    /// Supports the user data that lets you customize the clamping and snapping of the translation component (any time the translation prediction error is too large).
    /// </summary>
    [BurstCompile]
    public unsafe struct DefaultTranslationSmoothingAction
    {
        /// <summary>
        /// The default value for the <see cref="DefaultSmoothingActionUserParams"/> if the no user data is passed to the function.
        /// Position is corrected if the prediction error is at least 1 unit (usually mt) and less than 10 unit (usually mt)
        /// </summary>
        public sealed class DefaultStaticUserParams
        {
            /// <summary>
            /// If the prediction error is larger than this value, the entity position is snapped to the new value.
            /// The default threshold is 10 units.
            /// </summary>
            public static readonly SharedStatic<float> maxDist = SharedStatic<float>.GetOrCreate<DefaultStaticUserParams, MaxDistKey>();
            /// <summary>
            /// If the prediction error is smaller than this value, the entity position is snapped to the new value.
            /// The default threshold is 1 units.
            /// </summary>
            public static readonly SharedStatic<float> delta = SharedStatic<float>.GetOrCreate<DefaultStaticUserParams, DeltaKey>();

            static DefaultStaticUserParams()
            {
                maxDist.Data = 10;
                delta.Data = 1;
            }
            class MaxDistKey {}
            class DeltaKey {}
        }

        /// <summary>
        /// Return a the burst compatible function pointer that can be used to register the smoothing action to the
        /// <see cref="GhostPredictionSmoothing"/> singleton.
        /// </summary>
        public static readonly PortableFunctionPointer<GhostPredictionSmoothing.SmoothingActionDelegate> Action = new PortableFunctionPointer<GhostPredictionSmoothing.SmoothingActionDelegate>(SmoothingAction);

        [BurstCompile(DisableDirectCall = true)]
        [AOT.MonoPInvokeCallback(typeof(GhostPredictionSmoothing.SmoothingActionDelegate))]
        private static void SmoothingAction(IntPtr currentData, IntPtr previousData, IntPtr usrData)
        {
            ref var trans = ref UnsafeUtility.AsRef<LocalTransform>((void*)currentData);
            ref var backup = ref UnsafeUtility.AsRef<LocalTransform>((void*)previousData);

            float maxDist = DefaultStaticUserParams.maxDist.Data;
            float delta = DefaultStaticUserParams.delta.Data;

            if (usrData.ToPointer() != null)
            {
                ref var userParam = ref UnsafeUtility.AsRef<DefaultSmoothingActionUserParams>(usrData.ToPointer());
                maxDist = userParam.maxDist;
                delta = userParam.delta;
            }

            var dist = math.distance(trans.Position, backup.Position);
            if (dist < maxDist && dist > delta && dist > 0)
            {
                trans.Position = backup.Position + (trans.Position - backup.Position) * delta / dist;
            }
        }
    }
}
