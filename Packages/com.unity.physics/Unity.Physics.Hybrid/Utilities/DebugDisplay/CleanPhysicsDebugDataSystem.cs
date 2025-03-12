using Unity.Entities;
using Unity.Physics.Systems;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// A system which cleans physics debug display data from the previous frame.
    /// When using multiple physics worlds, in order for the debug display to work properly, you need to disable
    /// the update of this system in either main physics group (<see cref="PhysicsSystemGroup"/>)
    /// or in the custom physics group, whichever updates later in the loop.
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup), OrderFirst = true)]
    public partial struct CleanPhysicsDebugDataSystem_Default : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsDebugDisplayData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            DebugDisplay.DebugDisplay.Clear();
        }
    }

    /// <summary>
    /// A system which cleans physics debug display data from the previous frame while in edit mode.
    /// In case of using multiple worlds feature, in order for debug display to work properly
    /// on multiple worlds, you need to disable the update of this system in editor display physics group (<see cref="PhysicsDisplayDebugGroup"/>).
    /// </summary>
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup_Editor), OrderFirst = true)]
    public partial struct CleanPhysicsDebugDataSystem_Editor : ISystem
    {
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PhysicsDebugDisplayData>();
        }

        public void OnUpdate(ref SystemState state)
        {
            DebugDisplay.DebugDisplay.Clear();
        }
    }
}
