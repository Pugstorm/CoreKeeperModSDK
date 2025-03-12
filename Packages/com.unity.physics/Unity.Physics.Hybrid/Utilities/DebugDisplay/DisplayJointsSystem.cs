using Unity.Burst;
using Unity.Physics.Systems;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Transforms;
using static Unity.Physics.Math;

namespace Unity.Physics.Authoring
{
#if UNITY_EDITOR

    /// Job which draws every joint
    [BurstCompile]
    internal struct DisplayJointsJob : IJobParallelFor
    {
        const float k_Scale = 0.5f;

        [ReadOnly] private NativeArray<PhysicsJoint> Joints;
        [ReadOnly] private NativeArray<float4x4> WorldFromJointsA;
        [ReadOnly] private NativeArray<float4x4> WorldFromJointsB;

        public float3 Offset;

        public static void ScheduleJob(in EntityQuery jointsQuery, in EntityQuery bodyPairQuery, ref SystemState state, float3 offset)
        {
            var joints = jointsQuery.ToComponentDataArray<PhysicsJoint>(Allocator.TempJob);
            var bodyPairs = bodyPairQuery.ToComponentDataArray<PhysicsConstrainedBodyPair>(Allocator.TempJob);

            NativeList<float4x4> worldFromJointA = new NativeList<float4x4>(Allocator.TempJob);
            NativeList<float4x4> worldFromJointB = new NativeList<float4x4>(Allocator.TempJob);

            for (int i = 0; i < joints.Length; ++i)
            {
                var bodyPair = bodyPairs[i];
                var localToWorldA = bodyPair.EntityA != Entity.Null && state.EntityManager.HasComponent<LocalToWorld>(bodyPair.EntityA)
                    ? state.EntityManager.GetComponentData<LocalToWorld>(bodyPair.EntityA).Value
                    : float4x4.identity;
                var localToWorldB = bodyPair.EntityB != Entity.Null && state.EntityManager.HasComponent<LocalToWorld>(bodyPair.EntityB)
                    ? state.EntityManager.GetComponentData<LocalToWorld>(bodyPair.EntityB).Value
                    : float4x4.identity;

                var inverseScaleA = math.inverse(float4x4.Scale(localToWorldA.DecomposeScale()));
                var inverseScaleB = math.inverse(float4x4.Scale(localToWorldB.DecomposeScale()));

                var localToWorldANoScale = math.mul(localToWorldA, inverseScaleA);
                var localToWorldBNoScale = math.mul(localToWorldB, inverseScaleB);

                var joint = joints[i];
                float3x3 rotationA = new float3x3(joint.BodyAFromJoint.Axis, joint.BodyAFromJoint.PerpendicularAxis, math.cross(joint.BodyAFromJoint.Axis, joint.BodyAFromJoint.PerpendicularAxis));
                float3x3 rotationB = new float3x3(joint.BodyBFromJoint.Axis, joint.BodyBFromJoint.PerpendicularAxis, math.cross(joint.BodyBFromJoint.Axis, joint.BodyBFromJoint.PerpendicularAxis));
                var AFromJoint = new float4x4(rotationA, joint.BodyAFromJoint.Position);
                var BFromJoint = new float4x4(rotationB, joint.BodyBFromJoint.Position);

                worldFromJointA.Add(math.mul(localToWorldANoScale, AFromJoint));
                worldFromJointB.Add(math.mul(localToWorldBNoScale, BFromJoint));
            }

            state.Dependency = new DisplayJointsJob
            {
                Joints = joints,
                WorldFromJointsA = worldFromJointA.AsArray(),
                WorldFromJointsB = worldFromJointB.AsArray(),
                Offset = offset,
            }.Schedule(joints.Length, 16, state.Dependency);

            state.Dependency.Complete();

            worldFromJointA.Dispose();
            worldFromJointB.Dispose();

            joints.Dispose();
            bodyPairs.Dispose();
        }

        public void Execute(int iJoint)
        {
            // Color palette
            var colorA = Unity.DebugDisplay.ColorIndex.Cyan;
            var colorB = Unity.DebugDisplay.ColorIndex.Magenta;
            var colorError = Unity.DebugDisplay.ColorIndex.Red;
            var colorRange = Unity.DebugDisplay.ColorIndex.Yellow;

            PhysicsJoint joint = Joints[iJoint];
            var worldFromJointA = WorldFromJointsA[iJoint];
            var worldFromJointB = WorldFromJointsB[iJoint];
            float3 pivotA = new float3(worldFromJointA.c3.xyz);
            float3 pivotB = new float3(worldFromJointB.c3.xyz);

            float3x3 rotationA = new float3x3(worldFromJointA.c0.xyz, worldFromJointA.c1.xyz, math.cross(worldFromJointA.c0.xyz, worldFromJointA.c1.xyz));
            float3x3 rotationB = new float3x3(worldFromJointB.c0.xyz, worldFromJointB.c1.xyz, math.cross(worldFromJointB.c0.xyz, worldFromJointB.c1.xyz));

            var constraints = joint.GetConstraints();
            {
                for (var i = 0; i < constraints.Length; i++)
                {
                    Constraint constraint = constraints[i];
                    switch (constraint.Type)
                    {
                        case ConstraintType.Linear:
                            float3 diff = pivotA - pivotB;

                            // Draw the feature on B and find the range for A
                            float3 rangeOrigin;
                            float3 rangeDirection;
                            float rangeDistance;
                            switch (constraint.Dimension)
                            {
                                case 0:
                                    continue;
                                case 1:
                                    float3 normal = rotationB[constraint.ConstrainedAxis1D];
                                    PhysicsDebugDisplaySystem.Plane(pivotB, normal * k_Scale, colorB, Offset);
                                    rangeDistance = math.dot(normal, diff);
                                    rangeOrigin = pivotA - normal * rangeDistance;
                                    rangeDirection = normal;
                                    break;
                                case 2:
                                    float3 direction = rotationB[constraint.FreeAxis2D];
                                    PhysicsDebugDisplaySystem.Line(pivotB - direction * k_Scale, pivotB + direction * k_Scale,
                                        colorB, Offset);
                                    float dot = math.dot(direction, diff);
                                    rangeOrigin = pivotB + direction * dot;
                                    rangeDirection = diff - direction * dot;
                                    rangeDistance = math.length(rangeDirection);
                                    rangeDirection = math.select(rangeDirection / rangeDistance, float3.zero,
                                        rangeDistance < 1e-5);
                                    break;
                                case 3:
                                    PhysicsDebugDisplaySystem.Point(pivotB, k_Scale, colorB, Offset);
                                    rangeOrigin = pivotB;
                                    rangeDistance = math.length(diff);
                                    rangeDirection = math.select(diff / rangeDistance, float3.zero,
                                        rangeDistance < 1e-5);
                                    break;
                                default:
                                    SafetyChecks.ThrowNotImplementedException();
                                    return;
                            }

                            // Draw the pivot on A
                            PhysicsDebugDisplaySystem.Point(pivotA, k_Scale, colorA, Offset);

                            // Draw error
                            float3 rangeA = rangeOrigin + rangeDistance * rangeDirection;
                            float3 rangeMin = rangeOrigin + constraint.Min * rangeDirection;
                            float3 rangeMax = rangeOrigin + constraint.Max * rangeDirection;
                            if (rangeDistance < constraint.Min)
                            {
                                PhysicsDebugDisplaySystem.Line(rangeA, rangeMin, colorError, Offset);
                            }
                            else if (rangeDistance > constraint.Max)
                            {
                                PhysicsDebugDisplaySystem.Line(rangeA, rangeMax, colorError, Offset);
                            }

                            if (math.length(rangeA - pivotA) > 1e-5f)
                            {
                                PhysicsDebugDisplaySystem.Line(rangeA, pivotA, colorError, Offset);
                            }

                            // Draw the range
                            if (constraint.Min != constraint.Max)
                            {
                                PhysicsDebugDisplaySystem.Line(rangeMin, rangeMax, colorRange, Offset);
                            }

                            break;
                        case ConstraintType.Angular:
                            switch (constraint.Dimension)
                            {
                                case 0:
                                    continue;
                                case 1:
                                    // Get the limited axis and perpendicular in joint space
                                    int constrainedAxis = constraint.ConstrainedAxis1D;
                                    float3 axisInWorld = rotationA[constrainedAxis];
                                    float3 perpendicularInWorld =
                                        rotationA[(constrainedAxis + 1) % 3] * k_Scale;

                                    // Draw the angle of A
                                    PhysicsDebugDisplaySystem.Line(pivotA, pivotA + perpendicularInWorld, colorA, Offset);

                                    // Calculate the relative angle
                                    float angle;
                                    {
                                        float3x3 jointBFromA = math.mul(math.inverse(rotationA),
                                            rotationA);
                                        angle = CalculateTwistAngle(new quaternion(jointBFromA), constrainedAxis);
                                    }

                                    // Draw the range in B
                                    float3 axis = rotationA[constraint.ConstrainedAxis1D];
                                    PhysicsDebugDisplaySystem.Arc(pivotB, axis,
                                        math.mul(quaternion.AxisAngle(axis, constraint.Min - angle),
                                            perpendicularInWorld), constraint.Max - constraint.Min, colorB, Offset);

                                    break;
                                case 2:
                                    // Get axes in world space
                                    int axisIndex = constraint.FreeAxis2D;
                                    float3 axisA = rotationA[axisIndex];
                                    float3 axisB = rotationB[axisIndex];

                                    // Draw the cones in B
                                    if (constraint.Min == 0.0f)
                                    {
                                        PhysicsDebugDisplaySystem.Line(pivotB, pivotB + axisB * k_Scale, colorB, Offset);
                                    }
                                    else
                                    {
                                        PhysicsDebugDisplaySystem.Cone(pivotB, axisB * k_Scale, constraint.Min, colorB, Offset);
                                    }

                                    if (constraint.Max != constraint.Min)
                                    {
                                        PhysicsDebugDisplaySystem.Cone(pivotB, axisB * k_Scale, constraint.Max, colorB, Offset);
                                    }

                                    // Draw the axis in A
                                    PhysicsDebugDisplaySystem.Arrow(pivotA, axisA * k_Scale, colorA, Offset);

                                    break;
                                case 3:
                                    // TODO - no idea how to visualize this if the limits are nonzero :)
                                    break;
                                default:
                                    SafetyChecks.ThrowNotImplementedException();
                                    return;
                            }
                            break;
                        case ConstraintType.LinearVelocityMotor:
                            //TODO: implement debug draw for motors
                            break;
                        case ConstraintType.AngularVelocityMotor:
                            //TODO: implement debug draw for motors
                            break;
                        case ConstraintType.PositionMotor:
                            //TODO: implement debug draw for motors
                            break;
                        case ConstraintType.RotationMotor:
                            //TODO: implement debug draw for motors
                            break;
                        default:
                            SafetyChecks.ThrowNotImplementedException();
                            return;
                    }
                }
            }
        }
    }

    // Creates DisplayJointsJobs
    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Default)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup))]
    [BurstCompile]
    internal partial struct DisplayJointsSystem_Default : ISystem
    {
        private EntityQuery JointsQuery;
        private EntityQuery BodyPairQuery;
        public void OnCreate(ref SystemState state)
        {
            JointsQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsJoint>());
            BodyPairQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsConstrainedBodyPair>());
            state.RequireForUpdate(JointsQuery);
            state.RequireForUpdate(BodyPairQuery);
            state.RequireForUpdate<PhysicsDebugDisplayData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawJoints == 0)
                return;

            DisplayJointsJob.ScheduleJob(JointsQuery, BodyPairQuery, ref state, debugDisplay.Offset);
        }
    }

    [RequireMatchingQueriesForUpdate]
    [WorldSystemFilter(WorldSystemFilterFlags.Editor)]
    [UpdateInGroup(typeof(PhysicsDebugDisplayGroup_Editor))]
    [BurstCompile]
    internal partial struct DisplayJointsSystem_Editor : ISystem
    {
        private EntityQuery JointsQuery;
        private EntityQuery BodyPairQuery;
        public void OnCreate(ref SystemState state)
        {
            JointsQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsJoint>());
            BodyPairQuery = state.GetEntityQuery(ComponentType.ReadOnly<PhysicsConstrainedBodyPair>());
            state.RequireForUpdate(JointsQuery);
            state.RequireForUpdate(BodyPairQuery);
            state.RequireForUpdate<PhysicsDebugDisplayData>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            if (!SystemAPI.TryGetSingleton(out PhysicsDebugDisplayData debugDisplay) || debugDisplay.DrawJoints == 0)
                return;

            DisplayJointsJob.ScheduleJob(JointsQuery, BodyPairQuery, ref state, debugDisplay.Offset);
        }
    }
#endif
}
