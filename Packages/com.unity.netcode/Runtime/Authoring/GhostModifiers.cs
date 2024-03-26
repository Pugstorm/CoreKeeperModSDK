// IMPORTANT NOTE: This file is shared with NetCode source generators
// NO UnityEngine, UnityEditor or other packages dll references are allowed here.
// IF YOU CHANGE THIS FILE, REMEMBER TO RECOMPILE THE SOURCE GENERATORS

using System;

namespace Unity.NetCode
{
    /// <summary>
    /// Assign to every <see cref="GhostInstance"/>, and denotes which Ghost prefab version this component is allowed to exist on.
    /// <example>Use this to disable rendering components on the Server version of the Ghost.</example>
    /// If you cannot change the ComponentType, use the `GhostAuthoringInspectionComponent` to manually override on a specific Ghost prefab.
    /// </summary>
    [Flags]
    public enum GhostPrefabType
    {
        /// <summary>Component will not be added to any Ghost prefab type.</summary>
        None = 0,
        /// <summary>Component will only be added to the <see cref="GhostMode.Interpolated"/> Client version.</summary>
        InterpolatedClient = 1,
        /// <summary>Component will only be added to the <see cref="GhostMode.Predicted"/> Client version.</summary>
        PredictedClient = 2,
        /// <summary>Component will only be added to Client versions.</summary>
        Client = 3,
        /// <summary>Component will only be added to the Server version.</summary>
        Server = 4,
        /// <summary>Component will only be added to the Server and PredictedClient versions.</summary>
        AllPredicted = 6,
        /// <summary>Component will be to all versions.</summary>
        All = 7
    }

    /// <summary>
    /// <para>An optimization: Set on each GhostComponent via the <see cref="GhostComponentAttribute"/> (or via a variant).</para>
    /// <para>When a Ghost is <see cref="GhostMode.OwnerPredicted"/>, OR its SupportedGhostModes is known at compile time,
    /// this flag will filter which types of clients will receive data updates.</para>
    /// <para>Maps to the <see cref="GhostMode"/> of each Ghost.</para>
    /// <para>Note that this optimization is <b>not</b> available to Ghosts that can have their <see cref="GhostMode"/>
    /// modified at runtime!</para>
    /// </summary>
    /// <remarks>
    /// <para>GhostSendType works for OwnerPredicted ghosts because:</para>
    /// <para>- The server <b>can</b> infer what GhostMode any given client will have an OwnerPredicted ghost in.
    /// It's as simple as: If Owner, then Predicting, otherwise Interpolating.</para>
    /// <para>- The server <b>cannot</b> infer what GhostMode a ghost supporting both Predicted and Interpolated can be in,
    /// as this can change at runtime (see <see cref="GhostPredictionSwitchingQueues"/>.
    /// Thus, the server snapshot serialization strategy must be identical for both.</para>
    /// <para>GhostSendType <i>also</i> works for Ghosts not using <see cref="GhostModeMask.All"/> because:</para>
    /// <para>- The server <b>can</b> infer what GhostMode any given client will have its ghost in, as it cannot change at runtime.</para>
    /// <para>Applies to all components (parents and children).</para>
    /// </remarks>
    /// <example>
    /// A velocity component may only be required on a client if the ghost is being predicted (to predict velocity and collisions correctly).
    /// Thus, use GhostSendType.Predicted on the Velocity component.
    /// </example>
    [Flags]
    public enum GhostSendType
    {
        /// <summary>The server will never replicate this component to any clients.
        /// Works similarly to <see cref="DontSerializeVariant"/> (and thus, redundant, if the DontSerializeVariant is in use).</summary>
        DontSend = 0,
        /// <summary>The server will only replicate this component to clients which are interpolating this Ghost. <see cref="GhostMode.Interpolated"/>).</summary>
        OnlyInterpolatedClients = 1,
        /// <summary>The server will only replicate this component to clients which are predicted this Ghost. <see cref="GhostMode.Predicted"/>).</summary>
        OnlyPredictedClients = 2,
        /// <summary>The server will always replicate this component. Default.</summary>
        AllClients = 3
    }

    /// <summary>
    /// <para><b>Meta-data of a <see cref="ICommandData"/> component, denoting whether or not the server should replicate the
    /// input commands back down to clients.
    /// Configure via <see cref="GhostComponentAttribute"/>.</b></para>
    /// <para>Docs for ICommandData:<inheritdoc cref="ICommandData"/></para>
    /// </summary>
    [Flags]
    public enum SendToOwnerType
    {
        /// <summary>Informs the server not not replicate this <see cref="ICommandData"/> back down to any clients.</summary>
        None = 0,
        /// <summary>Informs the server to replicate this <see cref="ICommandData"/> back to the owner, exclusively.</summary>
        SendToOwner = 1,
        /// <summary>Informs the server to replicate this <see cref="ICommandData"/> to all clients except the input "author"
        /// (i.e. the player who owns the ghost).</summary>
        SendToNonOwner = 2,
        /// <summary>Informs the server to replicate this <see cref="ICommandData"/> to all clients, including back to ourselves.</summary>
        All = 3,
    }

    /// <summary>Denotes how <see cref="GhostFieldAttribute"/> values are deserialized when received from snapshots.</summary>
    public enum SmoothingAction
    {
        /// <summary>The GhostField value will clamp to the latest snapshot value as it's available.</summary>
        Clamp = 0,

        /// <summary>Interpolate the GhostField value between the latest two processed snapshot values, and if no data is available for the next tick, clamp at the latest snapshot value.
        /// Tweak the <see cref="ClientTickRate"/> interpolation values if too jittery, or too delayed.</summary>
        Interpolate = 1 << 0,

        /// <summary>
        /// Interpolate the GhostField value between snapshot values, and if no data is available for the next tick, the next value is linearly extrapolated using the previous two snapshot values.
        /// Extrapolation is limited (i.e. clamped) via <see cref="ClientTickRate.MaxExtrapolationTimeSimTicks"/>.
        /// </summary>
        InterpolateAndExtrapolate = 3
    }
}
