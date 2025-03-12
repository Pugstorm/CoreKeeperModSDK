using Unity.Entities;

namespace Unity.Physics.GraphicsIntegration
{
    /// <summary>
    /// Stores a direct association to another Entity holding a graphical representation of a physics
    /// shape referenced by this Entity. 
    /// </summary>
    /// <remarks>
    /// This component is usually added where the edit time structure has separate 
    /// hierarchies for the graphics and physical representations. For example, a node
    /// that held a PhysicsShape component but no MeshRenderer component.
    /// </remarks>
    public struct PhysicsRenderEntity : IComponentData
    {
        /// <summary>   An Entity containing the graphical representation of a physics shape. </summary>
        public Entity Entity;
    }
}
