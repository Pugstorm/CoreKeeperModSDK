using System.Diagnostics;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Burst;
using Unity.Networking.Transport.Utilities;

namespace Unity.NetCode
{
#if UNITY_EDITOR || NETCODE_DEBUG
    internal struct NetworkTimeSystemStats : IComponentData
    {
        public float timeScale;
        public float interpTimeScale;
        private float averageTimeScale;
        private float averageInterpTimeScale;
        public float currentInterpolationFrames;
        public int timeScaleSamples;
        public int interpTimeScaleSamples;

        public void UpdateStats(float predictionTimeScale, float interpolationTimeScale, float interpolationFrames)
        {
            timeScale += predictionTimeScale;
            ++timeScaleSamples;
            interpTimeScale += interpolationTimeScale;
            ++interpTimeScaleSamples;
            currentInterpolationFrames = interpolationFrames;
        }

        public float GetAverageTimeScale()
        {
            if (timeScaleSamples > 0)
            {
                averageTimeScale = timeScale / timeScaleSamples;
                timeScale = 0;
                timeScaleSamples = 0;
            }

            return averageTimeScale;
        }

        public float GetAverageIterpTimeScale()
        {
            if (interpTimeScaleSamples > 0)
            {
                averageInterpTimeScale = interpTimeScale / interpTimeScaleSamples;
                interpTimeScale = 0;
                interpTimeScaleSamples = 0;
            }
            return averageInterpTimeScale;
        }
    }
#endif

    /// <summary>
    /// Stores the internal state of the NetworkTimeSystem.
    /// The component should be used for pure inspection or backup the data.
    /// Please don't change the the state values direclty.
    /// </summary>
    public struct NetworkTimeSystemData : IComponentData
    {
        /// <summary>
        /// The calculated intepolated tick, used to display interpolated ghosts.
        /// </summary>
        public NetworkTick interpolateTargetTick;
        /// <summary>
        /// The residual tick portion of the interpolateTargetTick.
        /// </summary>
        public float subInterpolateTargetTick;
        /// <summary>
        /// The estimated tick at which the server will received the client commands.
        /// </summary>
        public NetworkTick predictTargetTick;
        /// <summary>
        /// The residual tick portion of the predictTargetTick.
        /// </summary>
        public float subPredictTargetTick;
        /// <summary>
        /// The current interpolation delay ticks, used to offset the last estimated server tick in the past.
        /// </summary>
        public float currentInterpolationFrames;
        /// <summary>
        /// The latest snapshot tick received from the server. Used to calculate the delta ticks in between snapshot.
        /// </summary>
        public NetworkTick latestSnapshot;
        /// <summary>
        /// An internal estimate of the tick we are suppose to receive from server. PredictedTick and InterpolatedTick
        /// are extrapolotated from that.
        /// </summary>
        public NetworkTick latestSnapshotEstimate;
        /// <summary>
        /// the fixed point exponential average of the difference in between the estimated tick and the actual snapshot tick
        /// received from the server. Used to adjust the <see cref="latestSnapshotEstimate"/>.
        /// </summary>
        public int latestSnapshotAge;
        /// <summary>
        /// The average of the delta ticks in between snapshot. Is the current perceived estimate of the SimulationTickRate/SnapshotTickRate. Ex:
        /// If the server send at 30hz and the sim is 60hz the avg ratio should be 2
        /// </summary>
        public float avgDeltaSimTicks;
        /// <summary>
        ///The "std" deviation / jitter (actually an approximation of it) of the perceived netTickRate.
        /// </summary>
        public float devDeltaSimTicks;
        /// <summary>
        /// The local timestamp when received the last packet. Used to calculated the perceived packet arrival rate.
        /// </summary>
        public uint lastTimeStamp;
        /// <summary>
        /// The packet arrival rate exponential average
        /// </summary>
        public float avgPacketInterArrival;

        /// <summary>
        /// Setup the internal state when the first snapshot data is received from the server.
        /// </summary>
        /// <param name="snapshot">The snapshot tick received by the server </param>
        /// <param name="currentTs">The current local timestamp (in ms)</param>
        /// <param name="commandSlack">The <see cref="ClientTickRate.TargetCommandSlack"/></param>
        /// <param name="rtt">The current calculated round trip time</param>
        /// <param name="devRtt">The current calculated round trip jitter</param>
        /// <param name="interpolationDelay">The desired interpolation delay (in ticks)</param>
        /// <param name="simTickRate">The <see cref="ClientServerTickRate.SimulationTickRate"/></param>
        /// <param name="netTickRate">The packet interarrival, in simulation ticks.</param>
        internal void InitWithFirstSnapshot(NetworkTick snapshot, uint currentTs, uint commandSlack,
            float rtt, float devRtt, float interpolationDelay, int simTickRate, int netTickRate)
        {
            var rttTicks = ((uint) rtt * simTickRate + 999) / 1000;
            latestSnapshot = snapshot;
            latestSnapshotEstimate = snapshot;
            latestSnapshotAge = 0;
            predictTargetTick = snapshot;
            predictTargetTick.Add(commandSlack + (uint)rttTicks);
            //initial guess estimate for the interpolation frame. Uses the DeviatioRTT as a measurement of the jitter in the snapshot rate
            avgDeltaSimTicks = netTickRate;
            devDeltaSimTicks = (devRtt * netTickRate / 1000f);
            avgPacketInterArrival = ((float)1000)/(netTickRate*simTickRate);
            //the interpolation delay (the wanted ticks) need to be multiplayer ratio NetworkRate/SimTickRate.
            //i.e if the server send at 20hz but the sim is at 60hz, the delta in between ticks is 3.
            //so if you want to be behind 3 snapshot (aka 3 ticks if sent at sim rate), you behind 9 ticks from the
            //last received (more or less)
            currentInterpolationFrames = interpolationDelay*netTickRate + 2f*devDeltaSimTicks;
            interpolateTargetTick = snapshot;
            interpolateTargetTick.Subtract((uint)currentInterpolationFrames);
            subPredictTargetTick = 0f;
            lastTimeStamp = currentTs;
        }

        /// <summary>
        /// Used to update the internal state when a new snapshot data is received from the server.
        /// </summary>
        internal void UpdateWithLastSnapshot(uint currentTimeTs, NetworkTick snapshotTick)
        {
            int snapshotAge = latestSnapshotEstimate.TicksSince(snapshotTick);
            int snapshotDeltaSimTicks = snapshotTick.TicksSince(latestSnapshot);
            float deltaTimestamp = currentTimeTs - lastTimeStamp;
            lastTimeStamp = currentTimeTs;
            latestSnapshotAge = (latestSnapshotAge * 7 + (snapshotAge << 8)) / 8;
            latestSnapshot = snapshotTick;
            //The perceived tick rate moving average should react a little faster to changes than the snapshot age.
            //This help avoiding the situation where the client 'consumes' snapshot packets at double the rate of the server
            //in case the server run at low frame rate. We are using double the TCP spec (0.125) as factor for this.
            //TODO: add peak detection to change how to react to delta changes (faster or slower)
            avgDeltaSimTicks = math.lerp(avgDeltaSimTicks, snapshotDeltaSimTicks, 0.25f);
            devDeltaSimTicks = math.lerp(devDeltaSimTicks, math.abs(snapshotDeltaSimTicks - avgDeltaSimTicks), 0.25f);
            avgPacketInterArrival = math.lerp(avgPacketInterArrival, deltaTimestamp, 0.25f);
        }
    }

    /// <summary>
    /// <para>System responsible for estimating the <see cref="NetworkTime.ServerTick"/> and <see cref="NetworkTime.InterpolationTick"/>
    /// using the current round trip time (see <see cref="NetworkSnapshotAck"/>) and feedback from the server (see <see cref="NetworkSnapshotAck.ServerCommandAge"/>).</para>
    /// <para>The system tries to keep the server tick (present on the client) ahead of the server, such that input commands (see <see cref="ICommandData"/> and <see cref="IInputComponentData"/>)
    /// are received <i>before</i> the server needs them for the simulation.
    /// The system speeds up and slows down the client simulation elapsed delta time to compensate for changes in the network condition, and makes the reported
    /// <see cref="NetworkSnapshotAck.ServerCommandAge"/> close to the <see cref="ClientTickRate.TargetCommandSlack"/>.</para>
    /// <para>This time synchronization start taking place as soon as the first snapshot is received by the client. Because of that,
    /// until the client <see cref="NetworkStreamConnection"/> is not set in-game (see <see cref="NetworkStreamInGame"/>), the calculated
    /// server tick and interpolated are always 0.</para>
    /// <para>In the case where the client and server world are on the same process, and an IPC connection is used (see <see cref="TransportType.IPC"/>),
    /// some special optimizations can be applied. E.g. In this case the client should always run 1 tick per frame (server and client update in tandem).</para>
    /// </summary>
    [BurstCompile]
    [WorldSystemFilter(WorldSystemFilterFlags.ClientSimulation|WorldSystemFilterFlags.ThinClientSimulation)]
    [UpdateInGroup(typeof(InitializationSystemGroup))]
    [UpdateAfter(typeof(UpdateWorldTimeSystem))]
    public partial struct NetworkTimeSystem : ISystem, ISystemStartStop
    {
        /// <summary>
        /// Size of the array used to store the partial compensation for the
        /// </summary>
        private const int CommandAgeAdjustmentLength = 64;
        /// <summary>
        /// The current age adjustment slot
        /// </summary>
        private int commandAgeAdjustmentSlot;
        /// <summary>
        /// The partial adjustments did to the server tick prediction, ssed to avoid compensating for the delayed feedback from the server.
        /// </summary>
        private FixedList512Bytes<float> commandAgeAdjustment;

        /// <summary>
        /// A new <see cref="ClientTickRate"/> instance initialized with good and sensible default values.
        /// </summary>
        public static ClientTickRate DefaultClientTickRate => new ClientTickRate
        {
            InterpolationTimeNetTicks = 2,
            MaxExtrapolationTimeSimTicks = 20,
            MaxPredictAheadTimeMS = 500,
            TargetCommandSlack = 2,
            CommandAgeCorrectionFraction = 0.1f,
            PredictionTimeScaleMin = 0.9f,
            PredictionTimeScaleMax = 1.1f,
            InterpolationDelayJitterScale = 1.25f,
            InterpolationDelayMaxDeltaTicksFraction = 0.1f,
            InterpolationDelayCorrectionFraction = 0.1f,
            InterpolationTimeScaleMin = 0.85f,
            InterpolationTimeScaleMax = 1.1f
        };

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        static void ValidateClientTickRate(in ClientTickRate tickRate, in NetDebug netDebug)
        {
            if(tickRate.MaxPredictAheadTimeMS > 500f)
                netDebug.LogError("MaxPredictAheadTimeMS must be less then 500ms");
            if(tickRate.PredictionTimeScaleMin < 0.01f || tickRate.PredictionTimeScaleMin >= 1.0f)
                netDebug.LogError("PredictionTimeScaleMin must be in range [0.01, 1.0)");
            if(tickRate.PredictionTimeScaleMax < 1f || tickRate.PredictionTimeScaleMax > 2f)
                netDebug.LogError("PredictionTimeScaleMin must be in range (1.00, 2.0]");
            if(tickRate.InterpolationTimeScaleMin < 0.01f || tickRate.InterpolationTimeScaleMin > 1f)
                netDebug.LogError("InterpolationTimeScaleMin must be in range [0.01, 1.0)");
            if(tickRate.InterpolationTimeScaleMax < 0.01f || tickRate.InterpolationTimeScaleMax > 2f)
                netDebug.LogError("InterpolationTimeScaleMax must be in range (1.00, 2.0]");
            if(tickRate.InterpolationDelayJitterScale < 0f || tickRate.InterpolationDelayJitterScale > 3f)
                netDebug.LogError("InterpolationDelayJitterScale must be in range (0, 3]");
            if(tickRate.InterpolationDelayMaxDeltaTicksFraction < 0f || tickRate.InterpolationDelayMaxDeltaTicksFraction > 1f)
                netDebug.LogError("InterpolationDelayMaxDeltaTicksFraction must be in range (0, 1)");
            if(tickRate.InterpolationDelayCorrectionFraction < 0f || tickRate.InterpolationDelayCorrectionFraction > 1f)
                netDebug.LogError("InterpolationDelayCorrectionFraction must be in range (0, 1)");
        }


#if UNITY_EDITOR || NETCODE_DEBUG || UNITY_INCLUDE_TESTS
        internal static uint s_FixedTimestampMS{get{return s_FixedTime.Data.FixedTimestampMS;} set{s_FixedTime.Data.FixedTimestampMS = value;}}
        private struct FixedTime
        {
            public uint FixedTimestampMS;
            internal uint PrevTimestampMS;
            internal uint TimestampAdjustMS;
        }
        private static readonly SharedStatic<FixedTime> s_FixedTime = SharedStatic<FixedTime>.GetOrCreate<FixedTime>();

        /// <summary>
        /// Return a low precision real-time stamp that represents the number of milliseconds since the process started.
        /// In Development build and Editor, the maximum reported delta in between two calls of the TimestampMS is capped
        /// to 100 milliseconds.
        /// <remarks>
        /// The TimestampMS is mostly used for sake of time synchronization (for calculting the RTT).
        /// </remarks>
        /// </summary>
        public static uint TimestampMS
        {
            get
            {
                // If fixed timestamp is set, use that
                if (s_FixedTime.Data.FixedTimestampMS != 0)
                    return s_FixedTime.Data.FixedTimestampMS;
                //FIXME If the stopwatch is not high resolution means that it is based on the system timer, witch have a precision of about 10ms
                //This can be a little problematic for computing the right timestamp in general
                var cur = (uint)TimerHelpers.GetCurrentTimestampMS();
                // If more than 100ms passed since last timestamp heck, increase the adjustment so the reported time delta is 100ms
                if (s_FixedTime.Data.PrevTimestampMS != 0 && (cur - s_FixedTime.Data.PrevTimestampMS) > 100)
                {
                    s_FixedTime.Data.TimestampAdjustMS += (cur - s_FixedTime.Data.PrevTimestampMS) - 100;
                }
                s_FixedTime.Data.PrevTimestampMS = cur;
                return cur - s_FixedTime.Data.TimestampAdjustMS;
            }
        }
#else
        /// <summary>
        /// Return a low precision real-time stamp that represents the number of milliseconds since the process started.
        /// In Development build and Editor, the maximum reported delta in between two calls of the TimestampMS is capped
        /// to 100 milliseconds.
        /// <remarks>
        /// The TimestampMS is mostly used for sake of time synchronization (for calculting the RTT).
        /// </remarks>
        /// </summary>
        public static uint TimestampMS =>
            (uint)TimerHelpers.GetCurrentTimestampMS();
#endif



        /// <summary>
        /// Create the <see cref="NetworkTimeSystemData"/> singleton and reset the initial system state.
        /// </summary>
        /// <param name="state"></param>
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
#if UNITY_EDITOR || NETCODE_DEBUG
            var types = new NativeArray<ComponentType>(2, Allocator.Temp);
            types[0] = ComponentType.ReadWrite<NetworkTimeSystemData>();
            types[1] = ComponentType.ReadWrite<NetworkTimeSystemStats>();
#else
            var types = new NativeArray<ComponentType>(1, Allocator.Temp);
            types[0] = ComponentType.ReadWrite<NetworkTimeSystemData>();
#endif
            var netTimeStatEntity = state.EntityManager.CreateEntity(state.EntityManager.CreateArchetype(types));
            FixedString64Bytes singletonName = "NetworkTimeSystemData";
            state.EntityManager.SetName(netTimeStatEntity, singletonName);
            state.RequireForUpdate<NetworkSnapshotAck>();
        }

        /// <summary>
        /// Empty method, implement the <see cref="ISystem"/> interface.
        /// </summary>
        /// <param name="state"></param>
        [BurstCompile]
        public void OnStartRunning(ref SystemState state)
        {
        }

        /// <summary>
        /// Reset the <see cref="NetworkTimeSystemData"/> data and some internal variables.
        /// </summary>
        /// <param name="state"></param>
        [BurstCompile]
        public void OnStopRunning(ref SystemState state)
        {
            SystemAPI.SetSingleton(new NetworkTimeSystemData());
        }

        /// <summary>
        /// Implements all the time synchronization logic on the main thread.
        /// </summary>
        /// <param name="state"></param>
        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            SystemAPI.TryGetSingleton<ClientServerTickRate>(out var tickRate);
            tickRate.ResolveDefaults();

            if(!SystemAPI.TryGetSingleton<ClientTickRate>(out var clientTickRate))
                clientTickRate = DefaultClientTickRate;

            ValidateClientTickRate(clientTickRate, SystemAPI.GetSingleton<NetDebug>());

            state.CompleteDependency(); // We complete the dependency. This is needed because NetworkSnapshotAck is written by a job in NetworkStreamReceiveSystem

            var ack = SystemAPI.GetSingleton<NetworkSnapshotAck>();
            bool isInGame = SystemAPI.HasSingleton<NetworkStreamInGame>();

            float deltaTime = SystemAPI.Time.DeltaTime;
            if(isInGame && ClientServerBootstrap.HasServerWorld)
            {
                var maxDeltaTicks = (uint)tickRate.MaxSimulationStepsPerFrame * (uint)tickRate.MaxSimulationStepBatchSize;
                if (deltaTime > (float) maxDeltaTicks / (float) tickRate.SimulationTickRate)
                    deltaTime = (float) maxDeltaTicks / (float) tickRate.SimulationTickRate;
            }
            float deltaTicks = deltaTime * tickRate.SimulationTickRate;
            //If the client is using an IPC connection within an inprocess server we know that
            // latency is 0
            // jitter is 0
            // not packet loss
            //
            // That imply the average/desired command slack is 0 (predict only the next tick)
            // and the (ideal) output are
            // predictTargetTick = latestSnapshot + 1
            // interpolationTicks = max(SimulationRate/NetworkTickRate, clientTickRate.InterpolationTimeNetTicks) (or its equivalent ms version)
            // interpolateTargetTick = latestSnapshot - interpolationTicks
            //
            // However, because the client run at variable frame rate (it is not in sync with the server)
            // - there will be partial ticks
            // - the interpolation tick would vary a little bit (some fraction)
            //
            // We can probably force the InterpolationFrames to be constants but we preferred to have all the code path
            // shared, instead of preferential logic, as much as possible.
            // This can be a further optimasation that can be added later.

            var driverType = SystemAPI.GetSingleton<NetworkStreamDriver>().DriverStore.GetDriverType(NetworkDriverStore.FirstDriverId);
            if (driverType == TransportType.IPC)
            {
                //override this param with 0. The predicted target tick is the latest snapshot received + 1 (the next server tick)
                clientTickRate.TargetCommandSlack = 0;
                //these are 0 and we enforce that here
                ack.DeviationRTT = 0f;
                ack.EstimatedRTT = 1000f/tickRate.SimulationTickRate;
            }

            var estimatedRTT = math.min(ack.EstimatedRTT, clientTickRate.MaxPredictAheadTimeMS);
            var netTickRate = (tickRate.SimulationTickRate + tickRate.NetworkTickRate - 1) / tickRate.NetworkTickRate;
            // The desired number of interpolation frames depend on the ratio in between the simulation and the network tick rate
            // ex: if the server run the sim at 60hz but send at 20hz we need to stay back at least 3 ticks, or
            // any integer multiple of that
            var interpolationTimeTicks = (int)clientTickRate.InterpolationTimeNetTicks;
            if (clientTickRate.InterpolationTimeMS != 0)
                interpolationTimeTicks = (int)((clientTickRate.InterpolationTimeMS * tickRate.NetworkTickRate + 999) / 1000);
            // Reset the latestSnapshotEstimate if not in game
            ref var netTimeData = ref SystemAPI.GetSingletonRW<NetworkTimeSystemData>().ValueRW;
#if UNITY_EDITOR || NETCODE_DEBUG
            ref var  netTimeDataStats = ref SystemAPI.GetSingletonRW<NetworkTimeSystemStats>().ValueRW;
#endif
            if (netTimeData.latestSnapshotEstimate.IsValid && !isInGame)
                netTimeData.latestSnapshotEstimate = NetworkTick.Invalid;
            if (!netTimeData.latestSnapshotEstimate.IsValid)
            {
                if (!ack.LastReceivedSnapshotByLocal.IsValid)
                {
                    netTimeData = default(NetworkTimeSystemData);
                    return;
                }
                netTimeData.InitWithFirstSnapshot(ack.LastReceivedSnapshotByLocal, TimestampMS, clientTickRate.TargetCommandSlack,
                    ack.EstimatedRTT, ack.DeviationRTT, interpolationTimeTicks, tickRate.SimulationTickRate, netTickRate);

                commandAgeAdjustment.Length = CommandAgeAdjustmentLength;
                for (int i = 0; i < CommandAgeAdjustmentLength; ++i)
                    commandAgeAdjustment[i] = 0;

#if UNITY_EDITOR || NETCODE_DEBUG
                netTimeDataStats = default(NetworkTimeSystemStats);
#endif
            }
            else
            {
                // Add number of ticks based on deltaTime
                netTimeData.latestSnapshotEstimate.Add((uint) deltaTicks);
                //If ack.LastReceivedSnapshotByLocal is 0, it mean that a desync has been detected.
                //Updating the estimates using deltas in that case is completely wrong
                if (netTimeData.latestSnapshot != ack.LastReceivedSnapshotByLocal && ack.LastReceivedSnapshotByLocal.IsValid)
                    netTimeData.UpdateWithLastSnapshot(TimestampMS, ack.LastReceivedSnapshotByLocal);

                //Add the elapsed time. The delta < 0 is necesary to compensate the extra -1 when the delta is negative
                netTimeData.latestSnapshotAge -= (int) (math.frac(deltaTicks) * 256.0f);
                int delta = netTimeData.latestSnapshotAge >> 8;
                if (delta < 0)
                    ++delta;
                if (delta != 0)
                {
                    netTimeData.latestSnapshotEstimate.Subtract((uint) delta);
                    netTimeData.latestSnapshotAge -= delta << 8;
                }
            }
            float predictionTimeScale = 1f;
            float commandAge = ack.ServerCommandAge / 256.0f + clientTickRate.TargetCommandSlack;
            // Check which slot in the circular buffer of command age adjustments the current data should go in
            // use the latestSnapshot and not the LastReceivedSnapshotByLocal because the latter can be reset to 0, causing
            // a wrong reset of the adjustments
            uint rttInTicks = (((uint) estimatedRTT * (uint) tickRate.SimulationTickRate) / 1000);
            commandAge = AdjustCommandAge(netTimeData.latestSnapshot, commandAge, rttInTicks);
            if (math.abs(commandAge) < 10)
            {
                predictionTimeScale = math.clamp(1.0f + clientTickRate.CommandAgeCorrectionFraction * commandAge, clientTickRate.PredictionTimeScaleMin, clientTickRate.PredictionTimeScaleMax);
                netTimeData.subPredictTargetTick += deltaTicks * predictionTimeScale;
                uint pdiff = (uint) netTimeData.subPredictTargetTick;
                netTimeData.subPredictTargetTick -= pdiff;
                netTimeData.predictTargetTick.Add(pdiff);
            }
            else
            {
                var curPredict = netTimeData.latestSnapshotEstimate;
                curPredict.Add(clientTickRate.TargetCommandSlack + ((uint) estimatedRTT * (uint) tickRate.SimulationTickRate + 999) / 1000);
                float predictDelta = (float)(curPredict.TicksSince(netTimeData.predictTargetTick)) - deltaTicks;
                if (math.abs(predictDelta) > 10)
                {
                    //Attention! this may rollback in case we have an high difference in estimate (about 10 ticks greater)
                    //and predictDelta is negative (client is too far ahead)
                    if (predictDelta < 0.0f)
                    {
                        SystemAPI.GetSingleton<NetDebug>().LogError($"Large serverTick prediction error. Server tick rollback to {curPredict} delta: {predictDelta}");
                    }
                    netTimeData.predictTargetTick = curPredict;
                    netTimeData.subPredictTargetTick = 0;
                    for (int i = 0; i < CommandAgeAdjustmentLength; ++i)
                        commandAgeAdjustment[i] = 0;
                }
                else
                {
                    predictionTimeScale = math.clamp(1.0f + clientTickRate.CommandAgeCorrectionFraction * predictDelta, clientTickRate.PredictionTimeScaleMin, clientTickRate.PredictionTimeScaleMax);
                    netTimeData.subPredictTargetTick += deltaTicks * predictionTimeScale;
                    uint pdiff = (uint) netTimeData.subPredictTargetTick;
                    netTimeData.subPredictTargetTick -= pdiff;
                    netTimeData.predictTargetTick.Add(pdiff);
                }
            }

            commandAgeAdjustment[commandAgeAdjustmentSlot] += deltaTicks * (predictionTimeScale - 1.0f);
            //What is the frame we are going to receive next?
            //Our current best estimate is the "latestSnapshotEstimate", that try to guess what is the next frame we are going receive from the server.
            //The interpolation tick should be based on our latestSnapshotEstimate guess and delayed by the some interpolation frame.
            //We use latestSnapshotEstimate as base for the interpolated tick instead of the predicted tick for the following reasons:
            // - The fact the client increase the predicted tick faster, should not cause a faster increment of the interpolation
            // - It more accurately reflect the latest received data, instead of trying to approximate the target from the prediction, that depend on other factors
            //
            // The interpolation frames are calculated as follow:
            // frames = E[avgNetTickRate] + K*std[avgNetTickRate]
            // interpolationTick = latestSnapshotEstimate - frames
            //
            // avgNetTickRate: is calculated based on the delta ticks in between the received snapshot and account for
            //  - packet loss (the interpolation delay should increase)
            //  - server network tick rate changes (the server run slower)
            //  - multiple packets per frames (the interpolation delay should increase)
            // latestSnapshotEstimate: account for latency changes, because it is adjusted based on the delta in between the current estimated and what has been received.
            //
            // Together, latestSnapshotEstimate and avgNetTickRate compensate for the factors that affect the most the increase/decrease of the interpolation delay.
            var delayChangeLimit = deltaTicks*clientTickRate.InterpolationDelayMaxDeltaTicksFraction;
            var deltaInBetweenSnapshotTicks = netTimeData.avgDeltaSimTicks + netTimeData.devDeltaSimTicks * clientTickRate.InterpolationDelayJitterScale;
            //The perceived snapshot inter-arrival in simulation ticks.
            var avgNetRate = (netTimeData.avgPacketInterArrival*tickRate.SimulationTickRate + 999)/1000;
            //The number of interpolation frames is expressed as number of simulation ticks. This is why it is necessary to use the netTickRate/
            float desiredInterpolationDelayTicks = interpolationTimeTicks*netTickRate;
            //Select the largest in between the average snapshot rate (in ticks) and the average snapshot tick delta.
            var clampedDelayTick = math.max(avgNetRate, deltaInBetweenSnapshotTicks);
            //still clamp this as 6 times the desired netTickRate. It is reasonable assumption the server will try to go
            //back to normal
            clampedDelayTick = math.min(clampedDelayTick, 6*netTickRate);
            //If you then have a desiredInterpolationDelayTicks larger that that, we will use your anyway.
            var interpolationFrames = math.max(desiredInterpolationDelayTicks, clampedDelayTick);

            if (math.abs(interpolationFrames - netTimeData.currentInterpolationFrames) > 10f)
            {
                //with large delta immediately just frame delay.
                netTimeData.currentInterpolationFrames = interpolationFrames;
            }
            else
            {
                //move slowly toward the compute target frames
                netTimeData.currentInterpolationFrames += math.clamp(
                    (interpolationFrames-netTimeData.currentInterpolationFrames)*deltaTime,
                    -delayChangeLimit, delayChangeLimit);
            }

            var newInterpolationTargetTick = netTimeData.latestSnapshotEstimate;
            newInterpolationTargetTick.Subtract((uint)netTimeData.currentInterpolationFrames);
            var targetTickDelta = newInterpolationTargetTick.TicksSince(netTimeData.interpolateTargetTick) - netTimeData.subInterpolateTargetTick - deltaTicks;
            float interpolationTimeScale = 1f;
            //if we are behind (10 tick is quite a lot though, this require 100 frame to recover with 10% deltaTime scaling)
            //We don't check the abs value because for negative delta (we want to move backward) we just scale down the interpolationTimeScale
            if (targetTickDelta < 10)
            {
                interpolationTimeScale = math.clamp(1.0f + targetTickDelta*clientTickRate.InterpolationDelayCorrectionFraction,
                    clientTickRate.InterpolationTimeScaleMin, clientTickRate.InterpolationTimeScaleMax);

                netTimeData.subInterpolateTargetTick += deltaTicks * interpolationTimeScale;
                uint idiff = (uint) netTimeData.subInterpolateTargetTick;
                netTimeData.interpolateTargetTick.Add(idiff);
                netTimeData.subInterpolateTargetTick -= idiff;
            }
            else
            {
                //jump up the scale so that it matches the interpolation tick
                netTimeData.interpolateTargetTick = newInterpolationTargetTick;
                netTimeData.subInterpolateTargetTick = 0f;
            }
#if UNITY_EDITOR || NETCODE_DEBUG
            netTimeDataStats.UpdateStats(predictionTimeScale, interpolationTimeScale, netTimeData.currentInterpolationFrames);
#endif
        }

        /// <summary>
        /// Calculate an adjusted command age using by subtracting all the predicted tick compensations.
        /// (because of the delay response from the server)
        /// </summary>
        /// <param name="lastSnapshot"></param>
        /// <param name="commandAge"></param>
        /// <param name="rttInTicks"></param>
        /// <returns></returns>
        float AdjustCommandAge(in NetworkTick lastSnapshot, float commandAge, uint rttInTicks)
        {
            int curSlot = (int)(lastSnapshot.TickIndexForValidTick % CommandAgeAdjustmentLength);
            // If we moved to a new slot, clear the data between previous and new
            if (curSlot != commandAgeAdjustmentSlot)
            {
                for (int i = (commandAgeAdjustmentSlot + 1) % CommandAgeAdjustmentLength;
                     i != (curSlot+1) % CommandAgeAdjustmentLength;
                     i = (i+1) % CommandAgeAdjustmentLength)
                {
                    commandAgeAdjustment[i] = 0;
                }
                commandAgeAdjustmentSlot = curSlot;
            }
            // round down to whole ticks performed in one rtt
            if (rttInTicks > CommandAgeAdjustmentLength)
                rttInTicks = CommandAgeAdjustmentLength;
            for (int i = 0; i < rttInTicks; ++i)
                commandAge -= commandAgeAdjustment[(CommandAgeAdjustmentLength+commandAgeAdjustmentSlot-i) % CommandAgeAdjustmentLength];
            return commandAge;
        }
    }
}
