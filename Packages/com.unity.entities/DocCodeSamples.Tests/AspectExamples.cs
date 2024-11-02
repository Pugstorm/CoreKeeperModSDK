using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using Unity.Burst;

namespace Doc.CodeSamples.Tests
{
    #region aspect-example
    struct CannonBall : IComponentData
    {
        public float3 Speed;
    }

    // Aspects must be declared as a readonly partial struct
    readonly partial struct CannonBallAspect : IAspect
    {
        // An Entity field in an Aspect gives access to the Entity itself.
        // This is required for registering commands in an EntityCommandBuffer for example.
        public readonly Entity Self;

        // Aspects can contain other aspects.

        // A RefRW field provides read write access to a component. If the aspect is taken as an "in"
        // parameter, the field behaves as if it was a RefRO and throws exceptions on write attempts.
        readonly RefRW<LocalTransform> Transform;
        readonly RefRW<CannonBall> CannonBall;

        // Properties like this aren't mandatory. The Transform field can be public instead.
        // But they improve readability by avoiding chains of "aspect.aspect.aspect.component.value.value".
        public float3 Position
        {
            get => Transform.ValueRO.Position;
            set => Transform.ValueRW.Position = value;
        }

        public float3 Speed
        {
            get => CannonBall.ValueRO.Speed;
            set => CannonBall.ValueRW.Speed = value;
        }
    }
    #endregion

    #region aspect-iterate
    public partial struct MySystem : ISystem
    {
        public void OnUpdate(ref SystemState state)
        {
            foreach (var cannonball in SystemAPI.Query<CannonBallAspect>())
            {
                // use cannonball aspect here
            }
        }
    }
    #endregion
}
