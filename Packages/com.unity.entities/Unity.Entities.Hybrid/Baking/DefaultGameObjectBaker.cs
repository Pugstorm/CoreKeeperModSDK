using Unity.Transforms;

namespace Unity.Entities
{
    /// <summary>
    /// Default baker for GameObject instances.
    /// </summary>
    /// <remarks>
    /// The baker ensures that the authoring GameObject's static and active state is applied on the baked primary entity.
    /// </remarks>
    class DefaultGameObjectBaker : GameObjectBaker
    {
        public override void Bake(UnityEngine.GameObject authoring)
        {
            // Force the object to be used to preserve the GameObject hierarchy.
            //AddTransformUsageFlags(TransformUsageFlags.None);

            if (IsStatic())
                AddComponent(GetEntityWithoutDependency(),new Static());

            if (!IsActive())
                AddComponent(GetEntityWithoutDependency(),new Disabled());
        }
    }
}
