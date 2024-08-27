using System;
using System.Diagnostics;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.NetCode
{
    /// <summary>
    /// The ClientServerTickRate singleton is used to configure the client and server simulation time step,
    /// server packet send rate and other related settings.
    /// The singleton entity is automatically created for the clients in the <see cref="Unity.NetCode.NetworkStreamReceiveSystem"/>
    /// first update if not present.
    /// On the server, by countrary, the entity is never automatically created and it is up to the user to create the singletong instance if
    /// they need to.
    /// <remarks>
    /// This behaviour is asymmetric because the client need to have this singleton data synced with the server one. It is like
    /// this for compatibility reason and It may be changed in the future.
    /// </remarks>
    /// In order to configure these settings you can either:
    /// <list>
    /// <item>- Create the entity in a custom <see cref="Unity.NetCode.ClientServerBootstrap"/> after the worlds has been created.</item>
    /// <item>- On a system, in either the OnCreate or OnUpdate.</item>
    /// </list>
    /// It is not mandatory to set all the fields to a proper value when creating the singleton. It is sufficient to change only the relevant setting, and call the <see cref="ResolveDefaults"/> method to
    /// configure the fields that does not have a value set.
    /// <example>
    /// class MyCustomClientServerBootstrap : ClientServerBootstrap
    /// {
    ///    override public void Initialize(string defaultWorld)
    ///    {
    ///        base.Initialise(defaultWorld);
    ///        var customTickRate = new ClientServerTickRate();
    ///        //run at 30hz
    ///        customTickRate.simulationTickRate = 30;
    ///        customTickRate.ResolveDefault();
    ///        foreach(var world in World.All)
    ///        {
    ///            if(world.IsServer())
    ///            {
    ///               //In this case we only create on the server, but we can do the same also for the client world
    ///               var tickRateEntity = world.EntityManager.CreateSingleton(new ClientServerTickRate
    ///               {
    ///                   SimulationTickRate = 30;
    ///               });
    ///            }
    ///        }
    ///    }
    /// }
    /// </example>
    /// The <see cref="ClientServerTickRate"/> settings are synced as part of the of the initial client connection handshake.
    /// (<see cref="Unity.NetCode.ClientServerTickRateRefreshRequest"/> data).
    /// The ClientServerTickRate should also be used to customise other server only timing settings, such as
    /// <list type="bullet">
    /// <item>the maximum number of tick per frame</item>
    /// <item>the maximum number of tick per frame</item>
    /// <item>tick batching (<see cref="MaxSimulationStepBatchSize"/> and others.</item>
    /// </list>
    /// See the individual fields documentation for more information.
    /// </summary>
    /// <remarks>
    /// <list>
    /// <item>
    /// Once the client is connected, changes to the <see cref="ClientServerTickRate"/> are not replicated. If you change the settings are runtime, the same change must
    /// be done on both client and server.
    /// </item>
    /// <item>
    /// The ClientServerTickRate <b>should never be added to sub-scene with a baker</b>. In case you want to setup the ClientServerTickRate
    /// based on some scene settings, we suggest to implement your own component and change the ClientServerTickRate inside a system in
    /// your game.
    /// </item>
    /// </list>
    /// </remarks>
    [Serializable]
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

        /// <summary>
        /// Multiplier used to calculate the tick rate/frequency for the <see cref="PredictedFixedStepSimulationSystemGroup"/>.
        /// The group rate must be an integer multiple of the <see cref="SimulationTickRate"/>.
        /// Default value is 1, meaning that the <see cref="PredictedFixedStepSimulationSystemGroup"/> run at the same frequency
        /// of the prediction loop.
        /// The calculated frequency is 1.0/(SimulationTickRate*PredictedFixedStepSimulationTickRatio)
        /// </summary>
        public int PredictedFixedStepSimulationTickRatio;

        /// <summary>1f / <see cref="SimulationTickRate"/>. Think of this as the netcode version of `fixedDeltaTime`.</summary>
        public float SimulationFixedTimeStep => 1f / SimulationTickRate;

        /// <summary>
        /// The fixed time used to run the physics simulation. Is always an integer multiple of the SimulationFixedTimeStep. <br/>
        /// The value is equal to 1f / (<see cref="SimulationTickRate"/> * <see cref="PredictedFixedStepSimulationTickRatio"/>).
        /// </summary>
        public float PredictedFixedStepSimulationTimeStep => 1f / (PredictedFixedStepSimulationTickRatio*SimulationTickRate);

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
        /// On the client, Netcode attempts to align its own fixed step with the render refresh rate, with the goal of
        /// reducing Partial ticks, and increasing stability.
        /// Defaults to 5 (5%), which is applied each way: I.e. If you're within 5% of the last full tick, or if you're
        /// within 5% of the next full tick, we'll clamp.
        /// -1 is 'turn clamping off', 0 is 'use default'.
        /// Max value is 50 (i.e. 50% each way, leading to full clamping, as it's applied in both directions).
        /// </summary>
        /// <remarks>High values will lead to more aggressive alignment, which may be perceivable (as we'll need to shift time further).</remarks>
        public int ClampPartialTicksThreshold { get; set; }

        /// <summary>
        /// Set all the properties that hasn't been changed by the user or that have invalid ranges to a proper default value.
        /// In particular this guarantee that both <see cref="NetworkTickRate"/> and <see cref="SimulationTickRate"/> are never 0.
        /// </summary>
        public void ResolveDefaults()
        {
            if (SimulationTickRate <= 0)
                SimulationTickRate = 60;
            if (PredictedFixedStepSimulationTickRatio <= 0)
                PredictedFixedStepSimulationTickRatio = 1;
            if (NetworkTickRate <= 0)
                NetworkTickRate = SimulationTickRate;
            if (NetworkTickRate > SimulationTickRate)
                NetworkTickRate = SimulationTickRate;
            if (MaxSimulationStepsPerFrame <= 0)
                MaxSimulationStepsPerFrame = 1;
            if (MaxSimulationStepBatchSize <= 0)
                MaxSimulationStepBatchSize = 4;
            if (ClampPartialTicksThreshold == 0)
                ClampPartialTicksThreshold = 5;
        }

        [Conditional("ENABLE_UNITY_COLLECTIONS_CHECKS")]
        internal readonly void Validate()
        {
            if (SimulationTickRate <= 0)
                throw new ArgumentException($"The {nameof(SimulationTickRate)} must be always > 0");
            if (PredictedFixedStepSimulationTickRatio <= 0)
                throw new ArgumentException($"The {nameof(PredictedFixedStepSimulationTickRatio)} must be always > 0");
            if (NetworkTickRate <= 0)
                throw new ArgumentException($"The {nameof(NetworkTickRate)} must be always > 0");
            if (NetworkTickRate > SimulationTickRate)
                throw new ArgumentException($"The {nameof(NetworkTickRate)} must be always less or equal");
            if (MaxSimulationStepsPerFrame <= 0)
                throw new ArgumentException($"The {nameof(MaxSimulationStepsPerFrame)} must be always > 0");
            if (MaxSimulationStepBatchSize <= 0)
                throw new ArgumentException($"The {nameof(MaxSimulationStepBatchSize)} must be always > 0");
            if (ClampPartialTicksThreshold > 50)
                throw new ArgumentException($"The {nameof(ClampPartialTicksThreshold)} must always be within [-1, 50]");
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
        /// The ratio between the <see cref="PredictedFixedStepSimulationSystemGroup"/> and the <see cref="SimulationTickRate"/>.
        /// </summary>
        public int PredictedFixedStepSimulationTickRatio;
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

        public void ApplyTo(ref ClientServerTickRate tickRate)
        {
            tickRate.MaxSimulationStepsPerFrame = MaxSimulationStepsPerFrame;
            tickRate.NetworkTickRate = NetworkTickRate;
            tickRate.SimulationTickRate = SimulationTickRate;
            tickRate.MaxSimulationStepBatchSize = MaxSimulationStepBatchSize;
            tickRate.PredictedFixedStepSimulationTickRatio = PredictedFixedStepSimulationTickRatio;
        }
    }

    /// <summary>
    /// Create a ClientTickRate singleton in the client world (either at runtime or by loading it from sub-scene)
    /// to configure all the network time synchronization, interpolation delay, prediction batching and other setting for the client.
    /// See the individual fields for more information about the individual properties.
    /// </summary>
    [Serializable]
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
