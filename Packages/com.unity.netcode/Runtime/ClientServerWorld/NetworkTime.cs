using System;
using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Flags used by <see cref="NetworkTime"/> singleton to add some properties to the current simulated tick.
    /// See the individual flags documentation for further information.
    /// </summary>
    [Flags]
    public enum NetworkTimeFlags : byte
    {
        /// <summary>
        /// Indicate that the current server tick is a predicted one and the simulation is running inside the prediction group.
        /// </summary>
        IsInPredictionLoop = 1 << 0,
        /// <summary>
        /// Only valid inside the prediction loop, the server tick the prediction is starting from.
        /// </summary>
        IsFirstPredictionTick = 1 << 2,
        /// <summary>
        /// Only valid inside the prediction loop, the current server tick which will be the last tick to predict.
        /// </summary>
        IsFinalPredictionTick = 1 << 3,
        /// <summary>
        /// Only valid inside the prediction loop, the current server tick is the last full tick we are predicting. If IsFinalPredictionTick is set
        /// the IsPartial flag must be false. The IsFinalPredictionTick can be also set if the current server tick we are predicting is a full tick.
        /// </summary>
        IsFinalFullPredictionTick = 1 << 4,
        /// <summary>
        /// Only valid on server. True when the current simulated tick is running with a variabled delta time to recover from
        /// a previous long running frame.
        /// </summary>
        IsCatchUpTick = 1 << 5,
        /// <summary>
        /// Only valid inside the prediction loop, the current server tick is a full tick and this is the first time it is being predicting as a non-partial tick.
        /// The IsPartial flag must be false.
        /// This is frequently used to make sure effects which cannot easily be rolled back, such as spawning objects / particles / vfx or playing sounds, only happens once and are not repeated.
        /// </summary>
        IsFirstTimeFullyPredictingTick = 1 << 6,
    }
    /// <summary>
    /// Present on both client and server world, singleton component that contains all the timing characterist of the client/server simulation loop.
    /// </summary>
    public struct NetworkTime : IComponentData
    {
        /// <summary>
        /// The current simulated server tick the server will run this frame. Always start from 1. 0 is consider an invalid value.
        /// The ServerTick value behave differently on client and server.
        /// On the server:
        ///  - it is always a "full" tick
        ///  - strict monontone and continue (up to the wrap around)
        ///  - the same inside or outside the prediction loop
        /// On the client:
        ///  - it is the tick the client predict the server should simulate this frame. Depends on current lag and command slack
        ///  - can be either a full or partial.
        ///  - if the tick is partial, the client would run the simulation for it multiple time, each time with a different delta time proportion
        ///  - it is not monotone:
        ///      - in some rare/recovery situation may rollback or having jump forward (due to time/lag adjustments).
        ///      - during the prediction loop the ServerTick value is changed to match either the last full simulated tick or
        ///        , in case of a rollback because a snapshot has been received, to the oldest received tick among all entities. In both case, and the end of
        ///        of the prediction loop the server tick will be reset to current predicted server tick.
        /// </summary>
        public NetworkTick ServerTick;
        /// <summary>
        /// Only meaningful on the client that run at variable step rate. On the server is always 1.0. Always in range is (0.0 and 1.0].
        /// </summary>
        public float ServerTickFraction;
        /// <summary>
        /// The current interpolated tick (integral part). Always less than the ServerTick on the Client (and equal to ServerTick on the server).
        /// </summary>
        public NetworkTick InterpolationTick;
        /// <summary>
        /// The fractional part of the tick (XXX.fraction). Always in between (0.0, 1.0]
        /// </summary>
        public float InterpolationTickFraction;
        /// <summary>
        /// The number of simulation steps this tick is scaled with. This is used to make one update which covers
        /// N ticks in order to reduce CPU cost. This is always 1 for partial ticks in the prediction loop, but can be more than 1 for partial ticks outside the prediction loop.
        /// </summary>
        public int SimulationStepBatchSize;
        /// <summary>
        ///  For internal use only, special flags that add context and properties to the current server tick value.
        /// </summary>
        internal NetworkTimeFlags Flags;
        /// <summary>
        ///  For internal use only, the total elapsed network time since the world has been created. Different for server and client:
        /// - On the server is advanced at fixed time step interval (depending on ClientServerTickRate)
        /// - On the client use the computed network delta time based on the predicted server tick. The time time is not monotone.
        /// </summary>
        internal double ElapsedNetworkTime;
        /// <summary>
        /// True if the current tick is running with delta time that is a fraction of the ServerTickDeltaTime. Only true on the client when
        /// running at variable frame rate.
        /// </summary>
        public bool IsPartialTick => ServerTickFraction < 1f;
        /// <summary>
        /// Indicate that the current server tick is a predicted one and the simulation is running inside the prediction group.
        /// </summary>
        public bool IsInPredictionLoop => (Flags & NetworkTimeFlags.IsInPredictionLoop) != 0;
        /// <summary>
        /// Only valid inside the prediction loop. The server tick the prediction is starting from.
        /// </summary>
        public bool IsFirstPredictionTick => (Flags & NetworkTimeFlags.IsFirstPredictionTick) != 0;
        /// <summary>
        ///  Only valid inside the prediction loop. The current server tick which will be the last tick to predict
        /// </summary>
        public bool IsFinalPredictionTick => (Flags & NetworkTimeFlags.IsFinalPredictionTick) != 0;
        /// <summary>
        /// Only valid inside the prediction loop. The current server tick which will be the last full tick we are predicting
        /// </summary>
        public bool IsFinalFullPredictionTick => (Flags & NetworkTimeFlags.IsFinalFullPredictionTick) != 0;
        /// <summary>
        /// Only valid inside the prediction loop. True when this `ServerTick` is being predicted in full for the first time.
        /// "In full" meaning the first non-partial simulation tick. I.e. Partial ticks don't count.
        /// </summary>
        public bool IsFirstTimeFullyPredictingTick => (Flags & NetworkTimeFlags.IsFirstTimeFullyPredictingTick) != 0;
        /// <summary>
        /// Only valid on server. True when the current simulated tick is running with a variabled delta time to recover from
        /// a previous long running frame.
        /// </summary>
        public bool IsCatchUpTick => (Flags & NetworkTimeFlags.IsCatchUpTick) != 0;
        /// <summary>
        /// Counts the number of predicted ticks triggered on this frame (while inside the prediction loop).
        /// Thus, client only, and increments BEFORE the tick occurs (i.e. the first predicted tick will have a value of 1).
        /// Outside the prediction loop, records the current or last frames prediction tick count (until prediction restarts).
        /// </summary>
        public int PredictedTickIndex { get; internal set; }
    }

    /// <summary>
    /// Component added to the NetworkTime singleton entity when it is created in a client world. Contains the unscaled application
    /// ElapsedTime and DeltaTime.
    /// </summary>
    public struct UnscaledClientTime : IComponentData
    {
        /// <summary>
        /// The current unscaled elapsed time since the World has been created. Reliably traking the real elapsed time and
        /// it is always consistent in all the client states (connected/disconnected/ingame).
        /// </summary>
        public double UnscaleElapsedTime;
        /// <summary>
        /// The current unscaled delta time since since last frame.
        /// </summary>
        public float UnscaleDeltaTime;
    }

    static class NetworkTimeHelper
    {
        /// <summary>
        /// Return the current ServerTick value if is a fulltick, otherwise the previous one. The returned
        /// server tick value is correctly wrap around (server tick never equal 0)
        /// </summary>
        /// <param name="networkTime"></param>
        /// <returns></returns>
        static public NetworkTick LastFullServerTick(in NetworkTime networkTime)
        {
            var targetTick = networkTime.ServerTick;
            if (targetTick.IsValid && networkTime.IsPartialTick)
            {
                targetTick.Decrement();
            }
            return targetTick;
        }
    }
}
