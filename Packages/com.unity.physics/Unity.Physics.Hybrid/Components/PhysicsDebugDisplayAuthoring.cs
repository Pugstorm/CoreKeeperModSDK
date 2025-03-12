using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Display modes for the physics debug displays.
    /// <para>PreIntegration displays the state of the body before the simulation.</para>
    /// <para>PostIntegration displays the state of the body when the simulation results have been integrated.</para>
    /// </summary>
    public enum PhysicsDebugDisplayMode : byte
    {
        PreIntegration,
        PostIntegration
    }

    /// <summary>
    /// Physics Debug Display Data.<para/>
    ///
    /// Component data containing physics debug display settings.
    /// </summary>
    public struct PhysicsDebugDisplayData : IComponentData
    {
        /// <summary>
        /// Enable or disable collider debug display.
        /// </summary>
        public int DrawColliders;

        /// <summary>
        /// Enable or disable debug display of collider edges.
        /// </summary>
        public int DrawColliderEdges;

        /// <summary>
        /// Enable or disable debug display of the colliders' axis-aligned bounding boxes.
        /// </summary>
        public int DrawColliderAabbs;

        /// <summary>
        /// Enable or disable debug display of the colliders' bounding volume used during the Broadphase.
        /// </summary>
        public int DrawBroadphase;

        /// <summary>
        /// Enable or disable debug display of the mass properties of rigid bodies.
        /// </summary>
        public int DrawMassProperties;

        /// <summary>
        /// Enable or disable debug display of the contacts detected in the Narrowphase.
        /// </summary>
        public int DrawContacts;

        /// <summary>
        /// Enable or disable the collision events debug display.
        /// </summary>
        public int DrawCollisionEvents;

        /// <summary>
        /// Enable or disable the trigger events debug display.
        /// </summary>
        public int DrawTriggerEvents;

        /// <summary>
        /// Enable or disable the joints debug display.
        /// </summary>
        public int DrawJoints;

        /// <summary>
        /// Display mode for colliders.
        /// </summary>
        public PhysicsDebugDisplayMode ColliderDisplayMode;

        /// <summary>
        /// Display mode for collider edges.
        /// </summary>
        public PhysicsDebugDisplayMode ColliderEdgesDisplayMode;

        /// <summary>
        /// Display mode for the colliders' axis-aligned bounding boxes.
        /// </summary>
        public PhysicsDebugDisplayMode ColliderAabbDisplayMode;

        public float3 Offset;
    }

    /// <summary>
    /// Physics Debug Display Authoring.<para/>
    ///
    /// GameObject component containing physics debug display settings.
    /// </summary>
    [AddComponentMenu("Entities/Physics/Physics Debug Display")]
    [DisallowMultipleComponent]
    [HelpURL(HelpURLs.PhysicsDebugDisplayAuthoring)]
    public sealed class PhysicsDebugDisplayAuthoring : MonoBehaviour
    {
        PhysicsDebugDisplayAuthoring() {}

        /// <summary>
        /// Enables the debug display of the colliders.
        /// </summary>
        [Tooltip("Enables the debug display of the colliders.")]
        public bool DrawColliders = false;

        /// <summary>
        /// Enables the debug display of the colliders' edges.
        /// </summary>
        [Tooltip("Enables the debug display of the colliders' edges.")]
        public bool DrawColliderEdges = false;

        /// <summary>
        /// Enables the debug display of the colliders' axis-aligned bounding boxes.
        /// </summary>
        [Tooltip("Enables the debug display of the colliders' axis-aligned bounding boxes.")]
        public bool DrawColliderAabbs = false;

        /// <summary>
        /// Enables the debug display of the rigid body mass properties.
        /// </summary>
        [Tooltip("Enables the debug display of the rigid body mass properties.")]
        public bool DrawMassProperties = false;

        /// <summary>
        /// Enables the debug display of the colliders' bounding volumes used during the Broadphase.
        /// </summary>
        [Tooltip("Enables the debug display of the colliders' bounding volumes used during the Broadphase.")]
        public bool DrawBroadphase = false;

        /// <summary>
        /// Enables the debug display of the contacts detected in the Narrowphase.
        /// </summary>
        [Tooltip("Enables the debug display of the contacts detected in the Narrowphase.")]
        public bool DrawContacts = false;

        /// <summary>
        /// Enables the debug display of the collision events.
        /// </summary>
        [Tooltip("Enables the debug display of the collision events.")]
        public bool DrawCollisionEvents = false;

        /// <summary>
        /// Enables the debug display of the trigger events.
        /// </summary>
        [Tooltip("Enables the debug display of the trigger events.")]
        public bool DrawTriggerEvents = false;

        /// <summary>
        /// Enables the debug display of the joints.
        /// </summary>
        [Tooltip("Enables the debug display of the joints.")]
        public bool DrawJoints = false;

        /// <summary>
        /// The debug display mode for colliders.
        /// </summary>
        [Tooltip("The debug display mode for colliders.\n\n" +
            "Pre Integration displays the state of the data at the beginning of the simulation step, before rigid bodies have been integrated.\n" +
            "Post Integration displays the state of the data at the end of the simulation step, after rigid bodies have been integrated.")]
        public PhysicsDebugDisplayMode ColliderDisplayMode = PhysicsDebugDisplayMode.PreIntegration;

        /// <summary>
        /// The debug display mode for collider edges.
        /// </summary>
        [Tooltip("The debug display mode for collider edges.\n\n" +
            "- Pre Integration displays the state of the data at the beginning of the simulation step, before rigid bodies have been integrated.\n" +
            "- Post Integration displays the state of the data at the end of the simulation step, after rigid bodies have been integrated.")]
        public PhysicsDebugDisplayMode ColliderEdgesDisplayMode = PhysicsDebugDisplayMode.PreIntegration;

        /// <summary>
        /// The debug display mode of the colliders' axis-aligned bounding boxes.
        /// </summary>
        [Tooltip("The debug display mode for the colliders' axis-aligned bounding boxes.\n\n" +
            "- Pre Integration displays the state of the data at the beginning of the simulation step, before rigid bodies have been integrated.\n" +
            "- Post Integration displays the state of the data at the end of the simulation step, after rigid bodies have been integrated.")]
        public PhysicsDebugDisplayMode ColliderAabbDisplayMode = PhysicsDebugDisplayMode.PreIntegration;

        internal PhysicsDebugDisplayData AsComponent => new PhysicsDebugDisplayData
        {
            DrawColliders = DrawColliders ? 1 : 0,
            DrawColliderEdges = DrawColliderEdges ? 1 : 0,
            DrawColliderAabbs = DrawColliderAabbs ? 1 : 0,
            DrawBroadphase = DrawBroadphase ? 1 : 0,
            DrawMassProperties = DrawMassProperties ? 1 : 0,
            DrawContacts = DrawContacts ? 1 : 0,
            DrawCollisionEvents = DrawCollisionEvents ? 1 : 0,
            DrawTriggerEvents = DrawTriggerEvents ? 1 : 0,
            DrawJoints = DrawJoints ? 1 : 0,
            ColliderDisplayMode = ColliderDisplayMode,
            ColliderEdgesDisplayMode = ColliderEdgesDisplayMode,
            ColliderAabbDisplayMode = ColliderAabbDisplayMode
        };
    }

    /// <summary>
    /// Baker for the <see cref="PhysicsDebugDisplayAuthoring>"/>
    /// </summary>
    internal class PhysicsDebugDisplayDataBaker : Baker<PhysicsDebugDisplayAuthoring>
    {
        /// <summary>
        /// Baking method to create the <see cref="PhysicsDebugDisplayAuthoring"/> component data.
        /// </summary>
        public override void Bake(PhysicsDebugDisplayAuthoring authoring)
        {
            var entity = GetEntity(TransformUsageFlags.None);
            AddComponent(entity, authoring.AsComponent);
        }
    }
}
