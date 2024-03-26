using Unity.Entities;

namespace Unity.NetCode
{
    /// <summary>
    /// Create a ClientServerTickRate singleton to configure the client and server simulation simulation time step,
    /// and the server packet send rate.
    /// The singleton can be created at runtime or by adding the component to a singleton entity in sub-scene.
    /// It is not mandatory to create the singleton in the client worlds (while it is considered best practice), since the
    /// relevant settings for the client (the <see cref="SimulationTickRate"/> and <see cref="NetworkTickRate"/>) are synced
    /// as part of the initial handshake (<see cref="ClientServerTickRateRefreshRequest"/>).
    /// The ClientServerTickRate should also be used to customise other server only timing settings, such as
    /// the maximum number of tick per frame, tick batching (<see cref="MaxSimulationStepBatchSize"/> and others. See the
    /// individual fields documentation for more information.
    /// </summary>
    /// <remarks>
    /// It is not mandatory to set all the fields to a proper value when creating the singleton.
    /// It is sufficient to change only the relevant setting, and call the <see cref="ResolveDefaults"/> method to
    /// configure the fields that does not have a value set.
    /// </remarks>
    public struct ClientServerTickRate : IComponentData
    {
        /// <summary>
        /// Enum to control how the simulation should deal with running at a higher frame rate than simulation rate.
        /// </summary>
        public enum FrameRateMode
        {
            /// <summary>
            /// Use `Sleep` if running in a server-only build, otherwise use `BusyWait`.
            /// </summary>
            Auto,
            /// <summary>
            /// Let the game loop run at full frequency and skip simulation updates if the accumulated delta time is less than the simulation frequency.
            /// </summary>
            BusyWait,
            /// <summary>
            /// Use `Application.TargetFrameRate` to limit the game loop frequency to the simulation frequency.
            /// </summary>
            Sleep
        }

        /// <summary>
        /// The fixed simulation frequency on the server and prediction loop. The client can render
        /// at a higher or lower rate than this.
        /// </summary>
        public int SimulationTickRate;

        /// <summary>1f / <see cref="SimulationTickRate"/>. Think of this as the netcode version of `fixedDeltaTime`.</summary>
        public float SimulationFixedTimeStep => 1f / SimulationTickRate;

        /// <summary>
        /// The rate at which the server sends snapshots to the clients. This can be lower than than
        /// the simulation frequency which means the server only sends new snapshots to the clients
        /// every N frames. The effect of this on the client is similar to having a higher ping,
        /// on the server it will save CPU time and bandwidth.
        /// </summary>
        public int NetworkTickRate;
        /// <summary>
        /// If the server updates at a lower rate than the simulation tick rate it will perform
        /// multiple ticks in the same frame. This setting puts a limit on how many such updates
        /// it can do in a single frame. When this limit is reached the simulation time will update
        /// slower than real time.
        /// The network tick rate only applies to snapshots, the frequency commands and RPCs is not
        /// affected by this setting.
        /// </summary>
        public int MaxSimulationStepsPerFrame;
        /// <summary>
        /// If the server cannot keep up with the simulation frequency with running `MaxSimulationStepsPerFrame`
        /// ticks it is possible to allow each tick to run with a longer delta time in order to keep the game
        /// time updating correctly. This means that instead of running two ticks with delta time N each, the
        /// system will run a single tick with delta time 2*N. It is a less expensive but more inaccurate way
        /// of dealing with server performance spikes, it also requires the game logic to be able to handle it.
        /// </summary>
        public int MaxSimulationStepBatchSize;
        /// <summary>
        /// If the server is capable of updating more often than the simulation tick rate it can either
        /// skip the simulation tick for some updates (`BusyWait`) or limit the updates using
        /// `Application.TargetFrameRate` (`Sleep`). `Auto` makes it use `Sleep` for dedicated server
        /// builds and `BusyWait` for client and server builds (as well as the editor).
        /// </summary>
        public FrameRateMode TargetFrameRateMode;
        /// <summary>
        /// If the server has to run multiple simulation ticks in the same frame the server can either
        /// send snapshots for all those ticks or just the last one.
        /// </summary>
        public bool SendSnapshotsForCatchUpTicks
        {
            get { return m_SendSnapshotsForCatchUpTicks == 1; }
            set { m_SendSnapshotsForCatchUpTicks = value ? (byte)1 : (byte)0; }
        }
        private byte m_SendSnapshotsForCatchUpTicks;


        /// <summary>
        /// Set all the properties that hasn't been changed by the user or that have invalid ranges to a proper default value.
        /// In particular this guarantee that both <see cref="NetworkTickRate"/> and <see cref="SimulationTickRate"/> are never 0.
        /// </summary>
        public void ResolveDefaults()
        {
            if (SimulationTickRate <= 0)
                SimulationTickRate = 60;
            if (NetworkTickRate <= 0)
                NetworkTickRate = SimulationTickRate;
            if (MaxSimulationStepsPerFrame <= 0)
                MaxSimulationStepsPerFrame = 1;
            if (MaxSimulationStepBatchSize <= 0)
                MaxSimulationStepBatchSize = 4;
        }
    }

    /// <summary>
    /// RPC sent as part of the initial handshake from server to client to match the simulation tick rate properties
    /// on the client with those present on the server.
    /// </summary>
    internal struct ClientServerTickRateRefreshRequest : IComponentData
    {
        /// <summary>
        /// The simulation rate setting on the server
        /// </summary>
        public int SimulationTickRate;
        /// <summary>
        /// The rate at which the packet are sent to the client
        /// </summary>
        public int NetworkTickRate;
        /// <summary>
        /// The maximum step the server can do in one frame. Used to properly sync the prediction loop.
        ///  See <see cref="ClientServerTickRate.MaxSimulationStepsPerFrame"/>
        /// </summary>
        public int MaxSimulationStepsPerFrame;
        /// <summary>
        /// The maximum number of step that can be batched togeher when the server is caching up because of slow
        /// frame rate. See <see cref="ClientServerTickRate.MaxSimulationStepBatchSize"/>
        /// </summary>
        public int MaxSimulationStepBatchSize;
    }

    /// <summary>
    /// Create a ClientTickRate singleton in the client world (either at runtime or by loading it from sub-scene)
    /// to configure all the network time synchronization, interpolation delay, prediction batching and other setting for the client.
    /// See the individual fields for more information about the individual properties.
    /// </summary>
    public struct ClientTickRate : IComponentData
    {
        /// <summary>
        /// The number of network ticks to use as an interpolation buffer for interpolated ghosts.
        /// </summary>
        public uint InterpolationTimeNetTicks;
        /// <summary>
        /// The time in ms to use as an interpolation buffer for interpolated ghosts, this will take precedence and override the
        /// interpolation time in ticks if specified.
        /// </summary>
        public uint InterpolationTimeMS;
        /// <summary>
        /// The maximum time in simulation ticks which the client can extrapolate ahead when data is missing.
        /// </summary>
        public uint MaxExtrapolationTimeSimTicks;
        /// <summary>
        /// This is the maximum accepted ping, rtt will be clamped to this value when calculating server tick on the client,
        /// which means if ping is higher than this the server will get old commands.
        /// Increasing this makes the client able to deal with higher ping, but the client needs to run more prediction steps which takes more CPU time
        /// </summary>
        public uint MaxPredictAheadTimeMS;
        /// <summary>
        /// Specifies the number of simulation ticks the client tries to make sure the commands are received by the server
        /// before they are used on the server.
        /// </summary>
        public uint TargetCommandSlack;
        /// <summary>
        /// The client can batch simulation steps in the prediction loop. This setting controls
        /// how many simulation steps the simulation can batch for ticks which have previously
        /// been predicted.
        /// Setting this to a value larger than 1 will save performance, but the gameplay systems
        /// needs to be adapted.
        /// </summary>
        public int MaxPredictionStepBatchSizeRepeatedTick;
        /// <summary>
        /// The client can batch simulation steps in the prediction loop. This setting controls
        /// how many simulation steps the simulation can batch for ticks which are being predicted
        /// for the first time.
        /// Setting this to a value larger than 1 will save performance, but the gameplay systems
        /// needs to be adapted.
        /// </summary>
        public int MaxPredictionStepBatchSizeFirstTimeTick;
        /// <summary>
        /// Multiplier used to compensate received snapshot rate jitter when calculating the Interpolation Delay.
        /// Default Value: 1.25.
        /// </summary>
        public float InterpolationDelayJitterScale;
        /// <summary>
        /// Used to limit the maximum InterpolationDelay changes in one frame, as percentage of the frame deltaTicks.
        /// Default value: 10% of the frame delta ticks. Smaller values will result in slow adaptation to the network state (loss and jitter)
        /// but would result in smooth delay changes. Larger values would make the InterpolationDelay change quickly adapt but
        /// may cause sudden jump in the interpolated values.
        /// Good ranges: [0.10 - 0.3]
        /// </summary>
        public float InterpolationDelayMaxDeltaTicksFraction;
        /// <summary>
        /// The percentage of the error in the interpolation delay that can be corrected in one frame. Used to control InterpolationTickTimeScale.
        /// Must be in range (0, 1).
        /// <code>
        ///              ________ Max
        ///            /
        ///           /
        /// Min _____/____________
        ///                         InterpolationDelayDelta
        /// </code>
        /// DefaultValue: 10% of the delta in between the current and next desired interpolation tick.
        /// Good ranges: [0.075 - 0.2]
        /// </summary>
        public float InterpolationDelayCorrectionFraction;
        /// <summary>
        /// The minimum value for the InterpolateTimeScale. Must be in range (0, 1) Default: 0.85.
        /// </summary>
        public float InterpolationTimeScaleMin;
        /// <summary>
        /// The maximum value for the InterpolateTimeScale. Must be greater that 1.0. Default: 1.1.
        /// </summary>
        public float InterpolationTimeScaleMax;
        /// <summary>
        /// The percentage of the error in the predicted server tick that can be corrected each frame. Used to control the client deltaTime scaling, used to
        /// slow-down/speed-up the server tick estimate.
        /// Must be in (0, 1) range.
        /// <code>
        ///
        ///              ________ Max
        ///             /
        ///            /
        /// Min ______/__________
        ///                      CommandAge
        /// </code>
        /// DefaultValue: 10% of the error.
        /// The two major causes affecting the command age are:
        ///  - Network condition (Latency and Jitter)
        ///  - Server performance (running below the target frame rate)
        ///
        /// Small time scale values allow for smooth adjustments of the prediction tick but slower reaction to changes in both network and server frame rate.
        /// By using larger values, is faster to recovery to desync situation (caused by bad network and condition or/and slow server performance) but the
        /// predicted ticks delta are larger.
        /// Good ranges: [0.075 - 0.2]
        /// </summary>
        public float CommandAgeCorrectionFraction;
        /// <summary>
        /// PredictionTick time scale min value, max be less then 1.0f. Default: 0.9f.
        /// Note: it is not mandatory to have the min-max symmetric.
        /// Good Range: (0.8 - 0.95)
        /// </summary>
        public float PredictionTimeScaleMin;
        /// <summary>
        /// PredictionTick time scale max value, max be greater then 1.0f. Default: 1.1f
        /// Note: it is not mandatory to have the min-max symmetric.
        /// Good Range: (1.05 - 1.2)
        /// </summary>
        public float PredictionTimeScaleMax;
    }
}
