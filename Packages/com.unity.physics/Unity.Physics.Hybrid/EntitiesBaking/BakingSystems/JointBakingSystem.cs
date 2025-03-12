using System.Runtime.CompilerServices;
using UnityEngine.Assertions;
using Unity.Collections;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using FloatRange = Unity.Physics.Math.FloatRange;

namespace Unity.Physics.Authoring
{
    internal abstract class BaseJointBaker<T> : Baker<T> where T : UnityEngine.Component
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static float3 GetScaledLocalAnchorPosition(in Transform bodyTransform, in float3 anchorPosition)
        {
            // account for scale in the body transform if any
            var kScaleEpsilon = 0.0001f;
            if (math.lengthsq((float3)bodyTransform.lossyScale - new float3(1f)) > kScaleEpsilon)
            {
                var localToWorld = bodyTransform.localToWorldMatrix;
                var rigidBodyTransform = Math.DecomposeRigidBodyTransform(localToWorld);

                // extract local skew matrix if non-identity world scale detected to re-position the local joint anchor position
                var skewMatrix = math.mul(math.inverse(new float4x4(rigidBodyTransform)), localToWorld);
                return math.mul(skewMatrix, new float4(anchorPosition, 1)).xyz;
            }
            // else:

            return anchorPosition;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        protected static quaternion GetJointFrameOrientation(float3 axis, float3 secondaryAxis)
        {
            // classic Unity uses a different approach than BodyFrame.ValidateAxes() for ortho-normalizing degenerate inputs
            // ortho-normalizing here ensures behavior is consistent with classic Unity
            var a = (Vector3)axis;
            var p = (Vector3)secondaryAxis;
            Vector3.OrthoNormalize(ref a, ref p);
            return new BodyFrame { Axis = a, PerpendicularAxis = p }.AsRigidTransform().rot;
        }

        protected bool3 GetAxesWithMotionType(
            ConfigurableJointMotion motionType,
            ConfigurableJointMotion x, ConfigurableJointMotion y, ConfigurableJointMotion z
        ) => new bool3(x == motionType, y == motionType, z == motionType);

        protected void ConvertSpringDamperSettings(float inSpringConstant, float inDampingCoefficient, float inConstrainedMass,
            out float outSpringFrequency, out float outDampingRatio)
        {
            // If stiffness and damping coefficient are both zero, then we treat the constraint as locked by
            // using the default spring settings for a stiff spring and zero damping for a rapid response.

            if (inSpringConstant > 0.0f || inDampingCoefficient > 0.0f)
            {
                // If the spring coefficient is zero, we can not calculate the damping ratio. So, we enforce a minimum spring stiffness in this case.
                var springConstant = inSpringConstant == 0.0f ? 1e-3f : inSpringConstant;
                outSpringFrequency = CalculateSpringFrequencyFromSpringConstant(springConstant, inConstrainedMass);
                outDampingRatio = CalculateDampingRatio(springConstant, inDampingCoefficient, inConstrainedMass);
            }
            else
            {
                // default spring-damper (stiff)
                outSpringFrequency = Constraint.DefaultSpringFrequency;
                outDampingRatio = Constraint.DefaultDampingRatio;
            }
        }

        protected float CalculateSpringFrequencyFromSpringConstant(float springConstant, float mass = 1.0f)
        {
            if (springConstant < Math.Constants.Eps) return 0.0f;

            return math.sqrt(springConstant / mass) * Math.Constants.OneOverTau;
        }

        protected float CalculateDampingRatio(float springConstant, float dampingCoefficient, float mass = 1.0f)
        {
            if (dampingCoefficient < Math.Constants.Eps) return 0.0f;

            var tmp = springConstant * mass;
            if (tmp < Math.Constants.Eps)
            {
                // Can not compute damping ratio. Just use damping coefficient as an approximation.
                return dampingCoefficient;
            }

            return dampingCoefficient / (2 * math.sqrt(tmp)); // damping coefficient / critical damping coefficient
        }

        protected PhysicsConstrainedBodyPair GetConstrainedBodyPair(in UnityEngine.Joint joint) =>
            new PhysicsConstrainedBodyPair(
                GetEntity(joint.gameObject, TransformUsageFlags.Dynamic),
                joint.connectedBody == null ? Entity.Null : GetEntity(joint.connectedBody, TransformUsageFlags.Dynamic),
                joint.enableCollision
            );

        protected float GetConstrainedBodyMass(in UnityEngine.Joint joint)
        {
            var rigidBody = GetComponent<Rigidbody>(joint.gameObject);
            var connectedBody = joint.connectedBody;
            float mass = 0;

            if (rigidBody != null && !rigidBody.isKinematic)
            {
                mass += rigidBody.mass;
            }

            if (connectedBody != null && !connectedBody.isKinematic)
            {
                mass += connectedBody.mass;
            }

            return mass > 0 ? mass : 1.0f;
        }

        protected struct CombinedJoint
        {
            public PhysicsJoint LinearJoint;
            public PhysicsJoint AngularJoint;
        }

        private void SetupBodyFrames(quaternion jointFrameOrientation, UnityEngine.Joint joint, ref BodyFrame bodyAFromJoint, ref BodyFrame bodyBFromJoint)
        {
            RigidTransform worldFromBodyA = Math.DecomposeRigidBodyTransform(joint.transform.localToWorldMatrix);
            RigidTransform worldFromBodyB = joint.connectedBody == null
                ? RigidTransform.identity
                : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            var anchorPos = GetScaledLocalAnchorPosition(joint.transform, joint.anchor);
            var worldFromJointA = math.mul(
                new RigidTransform(joint.transform.rotation, joint.transform.position),
                new RigidTransform(jointFrameOrientation, anchorPos)
            );
            bodyAFromJoint = new BodyFrame(math.mul(math.inverse(worldFromBodyA), worldFromJointA));

            var connectedEntity = GetEntity(joint.connectedBody, TransformUsageFlags.Dynamic);
            var isConnectedBodyConverted =
                joint.connectedBody == null || connectedEntity != Entity.Null;

            RigidTransform bFromA = isConnectedBodyConverted ? math.mul(math.inverse(worldFromBodyB), worldFromBodyA) : worldFromBodyA;
            RigidTransform bFromBSource =
                isConnectedBodyConverted ? RigidTransform.identity : worldFromBodyB;

            float3 connectedAnchorPos = joint.connectedBody
                ? GetScaledLocalAnchorPosition(joint.connectedBody.transform, joint.connectedAnchor)
                : joint.connectedAnchor;
            bodyBFromJoint = new BodyFrame
            {
                Axis = math.mul(bFromA.rot, bodyAFromJoint.Axis),
                PerpendicularAxis = math.mul(bFromA.rot, bodyAFromJoint.PerpendicularAxis),
                Position = math.mul(bFromBSource, new float4(connectedAnchorPos, 1f)).xyz
            };
        }

        protected CombinedJoint CreateConfigurableJoint(
            quaternion jointFrameOrientation,
            UnityEngine.Joint joint, bool3 linearLocks, bool3 linearLimited, SoftJointLimit linearLimit, SoftJointLimitSpring linearSpring, bool3 angularFree, bool3 angularLocks,
            bool3 angularLimited, SoftJointLimit lowAngularXLimit, SoftJointLimit highAngularXLimit, SoftJointLimitSpring angularXLimitSpring, SoftJointLimit angularYLimit,
            SoftJointLimit angularZLimit, SoftJointLimitSpring angularYZLimitSpring)
        {
            var angularConstraints = new FixedList512Bytes<Constraint>();
            var linearConstraints = new FixedList512Bytes<Constraint>();

            var constrainedMass = GetConstrainedBodyMass(joint);

            if (angularLimited[0])
            {
                ConvertSpringDamperSettings(angularXLimitSpring.spring, angularXLimitSpring.damper, constrainedMass, out var springFrequency, out var dampingRatio);

                Constraint constraint = Constraint.Twist(
                    0,
                    math.radians(new FloatRange(-highAngularXLimit.limit, -lowAngularXLimit.limit).Sorted()),
                    joint.breakTorque * Time.fixedDeltaTime,
                    springFrequency,
                    dampingRatio);
                angularConstraints.Add(constraint);
            }

            if (angularLimited[1])
            {
                ConvertSpringDamperSettings(angularYZLimitSpring.spring, angularYZLimitSpring.damper, constrainedMass, out var springFrequency, out var dampingRatio);

                Constraint constraint = Constraint.Twist(
                    1,
                    math.radians(new FloatRange(-angularYLimit.limit, angularYLimit.limit).Sorted()),
                    joint.breakTorque * Time.fixedDeltaTime,
                    springFrequency,
                    dampingRatio);

                angularConstraints.Add(constraint);
            }

            if (angularLimited[2])
            {
                ConvertSpringDamperSettings(angularYZLimitSpring.spring, angularYZLimitSpring.damper, constrainedMass, out var springFrequency, out var dampingRatio);

                Constraint constraint = Constraint.Twist(
                    2,
                    math.radians(new FloatRange(-angularZLimit.limit, angularZLimit.limit).Sorted()),
                    joint.breakTorque * Time.fixedDeltaTime,
                    springFrequency,
                    dampingRatio);

                angularConstraints.Add(constraint);
            }

            if (math.any(linearLimited))
            {
                ConvertSpringDamperSettings(linearSpring.spring, linearSpring.damper, constrainedMass, out var springFrequency, out var dampingRatio);

                linearConstraints.Add(new Constraint
                {
                    ConstrainedAxes = linearLimited,
                    Type = ConstraintType.Linear,
                    Min = 0f,
                    Max = linearLimit.limit,  // allow movement up to limit from anchor
                    SpringFrequency = springFrequency,
                    DampingRatio = dampingRatio,
                    MaxImpulse = joint.breakForce * Time.fixedDeltaTime,
                });
            }

            if (math.any(linearLocks))
            {
                linearConstraints.Add(new Constraint
                {
                    ConstrainedAxes = linearLocks,
                    Type = ConstraintType.Linear,
                    Min = linearLimit.limit,    // lock at distance from anchor
                    Max = linearLimit.limit,
                    SpringFrequency = Constraint.DefaultSpringFrequency, // default spring-damper (stiff)
                    DampingRatio = Constraint.DefaultDampingRatio, // default spring-damper (stiff)
                    MaxImpulse = joint.breakForce * Time.fixedDeltaTime,
                });
            }

            if (math.any(angularLocks))
            {
                angularConstraints.Add(new Constraint
                {
                    ConstrainedAxes = angularLocks,
                    Type = ConstraintType.Angular,
                    Min = 0,
                    Max = 0,
                    SpringFrequency = Constraint.DefaultSpringFrequency, // default spring-damper (stiff)
                    DampingRatio = Constraint.DefaultDampingRatio, // default spring-damper (stiff)
                    MaxImpulse = joint.breakTorque * Time.fixedDeltaTime,
                });
            }
            var bodyAFromJoint = new BodyFrame {};
            var bodyBFromJoint = new BodyFrame {};
            SetupBodyFrames(jointFrameOrientation, joint, ref bodyAFromJoint, ref bodyBFromJoint);

            var combinedJoint = new CombinedJoint();
            combinedJoint.LinearJoint.SetConstraints(linearConstraints);
            combinedJoint.LinearJoint.BodyAFromJoint = bodyAFromJoint;
            combinedJoint.LinearJoint.BodyBFromJoint = bodyBFromJoint;
            combinedJoint.AngularJoint.SetConstraints(angularConstraints);
            combinedJoint.AngularJoint.BodyAFromJoint = bodyAFromJoint;
            combinedJoint.AngularJoint.BodyBFromJoint = bodyBFromJoint;

            return combinedJoint;
        }

        protected PhysicsJoint ConvertMotor(quaternion jointFrameOrientation, UnityEngine.Joint joint, JacobianType motorType, float target, float maxImpulseForMotor)
        {
            var bodyAFromJoint = new BodyFrame {};
            var bodyBFromJoint = new BodyFrame {};
            SetupBodyFrames(jointFrameOrientation, joint, ref bodyAFromJoint, ref bodyBFromJoint);

            // In PhysX Configurable Joint:
            // X Motion and Angular X Motion: applied to Axis (not necessarily the x-axis)
            // Y Motion and Angular Y Motion: applied to Secondary Axis (not necessarily the y-axis)
            // Z Motion and Angular Z Motion: applied to axis perpendicular to Axis & Secondary Axis
            // Therefore when setting Target fields in PhysX, the Target.x is aligned to whatever the Axis field is set
            // and Target.y is aligned to the Secondary Axis > need to convert the target to be aligns to Axis
            // Targets are set local to bodyA as the offset of the final target relative to  the starting bodyA position
            var jointData = new PhysicsJoint();

            switch (motorType)
            {
                case JacobianType.PositionMotor:
                    jointData = PhysicsJoint.CreatePositionMotor(bodyAFromJoint, bodyBFromJoint, target, maxImpulseForMotor);
                    break;
                case JacobianType.LinearVelocityMotor:
                    jointData = PhysicsJoint.CreateLinearVelocityMotor(bodyAFromJoint, bodyBFromJoint, target, maxImpulseForMotor);
                    break;
                case JacobianType.RotationMotor:
                    jointData = PhysicsJoint.CreateRotationalMotor(bodyAFromJoint, bodyBFromJoint, target, maxImpulseForMotor);
                    break;
                case JacobianType.AngularVelocityMotor:
                    jointData = PhysicsJoint.CreateAngularVelocityMotor(bodyAFromJoint, bodyBFromJoint, target, maxImpulseForMotor);
                    break;
                default:
                    SafetyChecks.ThrowNotImplementedException();
                    break;
            }

            return jointData;
        }

        protected uint GetWorldIndex(UnityEngine.Component c)
        {
            // World Indices are not supported in current built-in physics implementation, which makes it unavailable with legacy baking.
            return 0;
        }

        protected Entity CreateJointEntity(uint worldIndex, PhysicsConstrainedBodyPair constrainedBodyPair, PhysicsJoint joint)
        {
            using (var joints = new NativeArray<PhysicsJoint>(1, Allocator.Temp) { [0] = joint })
            using (var jointEntities = new NativeList<Entity>(1, Allocator.Temp))
            {
                CreateJointEntities(worldIndex, constrainedBodyPair, joints, jointEntities);
                return jointEntities[0];
            }
        }

        protected void CreateJointEntities(uint worldIndex, PhysicsConstrainedBodyPair constrainedBodyPair, NativeArray<PhysicsJoint> joints, NativeList<Entity> newJointEntities = default)
        {
            if (!joints.IsCreated || joints.Length == 0)
                return;

            if (newJointEntities.IsCreated)
                newJointEntities.Clear();
            else
                newJointEntities = new NativeList<Entity>(joints.Length, Allocator.Temp);

            // create all new joints
            var multipleJoints = joints.Length > 1;

            foreach (var joint in joints)
            {
                var jointEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                AddSharedComponent(jointEntity, new PhysicsWorldIndex(worldIndex));

                AddComponent(jointEntity, constrainedBodyPair);
                AddComponent(jointEntity, joint);

                newJointEntities.Add(jointEntity);
            }

            if (multipleJoints)
            {
                // set companion buffers for new joints
                for (var i = 0; i < joints.Length; ++i)
                {
                    var companions = AddBuffer<PhysicsJointCompanion>(newJointEntities[i]);
                    for (var j = 0; j < joints.Length; ++j)
                    {
                        if (i == j)
                            continue;
                        companions.Add(new PhysicsJointCompanion {JointEntity = newJointEntities[j]});
                    }
                }
            }
        }
    }

    class CharacterBaker : BaseJointBaker<CharacterJoint>
    {
        void ConvertCharacterJoint(CharacterJoint joint)
        {
            var linearLocks = new bool3(true);
            var linearLimited = new bool3(false);

            var angularFree = new bool3(false);
            var angularLocks = new bool3(false);
            var angularLimited = new bool3(true);

            var jointFrameOrientation = GetJointFrameOrientation(joint.axis, joint.swingAxis);
            var jointData = CreateConfigurableJoint(jointFrameOrientation, joint, linearLocks, linearLimited,
                new SoftJointLimit { limit = 0f, bounciness = 0f },
                new SoftJointLimitSpring { spring = 0f, damper = 0f },
                angularFree, angularLocks, angularLimited,
                joint.lowTwistLimit, joint.highTwistLimit, joint.twistLimitSpring,
                joint.swing1Limit, joint.swing2Limit, joint.swingLimitSpring);

            var worldIndex = GetWorldIndex(joint);
            using (var joints = new NativeArray<PhysicsJoint>(2, Allocator.Temp) { [0] = jointData.LinearJoint, [1] = jointData.AngularJoint })
            using (var jointEntities = new NativeList<Entity>(2, Allocator.Temp))
            {
                CreateJointEntities(worldIndex, GetConstrainedBodyPair(joint), joints, jointEntities);
            }
        }

        public override void Bake(CharacterJoint authoring)
        {
            ConvertCharacterJoint(authoring);
        }
    }

    class ConfigurableBaker : BaseJointBaker<ConfigurableJoint>
    {
        void ConvertConfigurableJoint(UnityEngine.ConfigurableJoint joint)
        {
            var linearLocks =
                GetAxesWithMotionType(ConfigurableJointMotion.Locked, joint.xMotion, joint.yMotion, joint.zMotion);
            var linearLimited =
                GetAxesWithMotionType(ConfigurableJointMotion.Limited, joint.xMotion, joint.yMotion, joint.zMotion);
            var angularFree =
                GetAxesWithMotionType(ConfigurableJointMotion.Free, joint.angularXMotion, joint.angularYMotion, joint.angularZMotion);
            var angularLocks =
                GetAxesWithMotionType(ConfigurableJointMotion.Locked, joint.angularXMotion, joint.angularYMotion, joint.angularZMotion);
            var angularLimited =
                GetAxesWithMotionType(ConfigurableJointMotion.Limited, joint.angularXMotion, joint.angularYMotion, joint.angularZMotion);

            var jointFrameOrientation = GetJointFrameOrientation(joint.axis, joint.secondaryAxis);

            bool requiredDoF_forLinearMotors = math.any(linearLocks) && math.all(angularLocks) && math.all(!linearLimited);
            bool requiredDoF_forAngularMotors = math.all(linearLocks) && math.any(angularLocks) && math.all(!linearLimited);

            // Enforcing motor authoring on X Drive (primary Axis aligned) only. If either of these values are zero, then it will not be flagged be a position motor.
            bool3 isPositionMotor;
            isPositionMotor.x = joint.xDrive.positionSpring * joint.xDrive.maximumForce > 0;
            isPositionMotor.y = false;
            isPositionMotor.z = false;

            // Enforcing motor authoring on X Drive (primary Axis aligned) only. If either of these values are zero, then it will not be flagged be a velocity motor
            bool3 isLinearVelocityMotor;
            isLinearVelocityMotor.x = joint.xDrive.positionDamper * joint.xDrive.maximumForce > 0;
            isLinearVelocityMotor.y = false;
            isLinearVelocityMotor.z = false;

            var maxForceForLinearMotor = new float3(joint.xDrive.maximumForce, joint.yDrive.maximumForce, joint.zDrive.maximumForce);

            // Enforcing motor authoring on Angular X Drive (primary Axis aligned) only. If either of these values are zero, then it will not be flagged be a rotation motor
            bool3 isRotationMotor;
            isRotationMotor.x = joint.angularXDrive.positionSpring * joint.angularXDrive.maximumForce > 0;
            isRotationMotor.y = false;
            isRotationMotor.z = false;

            // Enforcing motor authoring on Angular X Drive (primary Axis aligned) only. If either of these values are zero, then it will not be flagged be an angular velocity motor
            bool3 isAngularVelocityMotor;
            isAngularVelocityMotor.x = joint.angularXDrive.positionDamper * joint.angularXDrive.maximumForce > 0;
            isAngularVelocityMotor.y = false;
            isAngularVelocityMotor.z = false;

            var maxForceForAngularMotor = new float3(joint.angularXDrive.maximumForce, joint.angularYZDrive.maximumForce, joint.angularYZDrive.maximumForce);

            PhysicsJoint motorJointData;
            uint worldIndex;
            bool isMotor = false;
            if (math.any(isPositionMotor))
            {
                isMotor = true;
                // To simplify conversion require: X Motion = Free. Y&Z Motion = Locked. Angular X/Y/Z = Locked. Axis field determines rotation
                if (requiredDoF_forLinearMotors)
                {
                    Assert.IsTrue(linearLocks.y && linearLocks.z,
                        $"Configurable Joint Baking Failed for {joint.name}: Baking Motor as Position Motor requires that both Y Drive = Locked, Z Drive = Locked");
                    var maxImpulseForLinearMotor = math.length((int3)isPositionMotor * maxForceForLinearMotor) * Time.fixedDeltaTime;
                    float3 targetVector = (int3)isPositionMotor * (float3)joint.targetPosition;
                    var target = targetVector.x * -1; //need -1: PhysX sets target as offset from the target, whereas ECS Physics targets target as where to go

                    motorJointData = ConvertMotor(jointFrameOrientation, joint, JacobianType.PositionMotor, target, maxImpulseForLinearMotor);
                    motorJointData.SetImpulseEventThresholdAllConstraints(
                        joint.breakForce * Time.fixedDeltaTime,
                        joint.breakTorque * Time.fixedDeltaTime);

                    worldIndex = GetWorldIndex(joint);
                    CreateJointEntity(worldIndex, GetConstrainedBodyPair(joint), motorJointData);
                }
            }

            if (math.any(isLinearVelocityMotor))
            {
                isMotor = true;
                // To simplify conversion require: X Motion = Free. Y&Z Motion = Locked. Angular X/Y/Z = Locked. Axis field determines rotation
                if (requiredDoF_forLinearMotors)
                {
                    Assert.IsTrue(linearLocks.y && linearLocks.z,
                        $"Configurable Joint Baking Failed for {joint.name}: Baking Motor as Linear Velocity Motor requires that both Y Drive = Locked, Z Drive = Locked");

                    var maxImpulseForLinearMotor = math.length((int3)isLinearVelocityMotor * maxForceForLinearMotor) * Time.fixedDeltaTime;
                    float3 targetVector = (int3)isLinearVelocityMotor * (float3)joint.targetVelocity;
                    var target = targetVector.x * -1; //need -1: PhysX sets target as offset from the target, whereas ECS Physics targets target as where to go

                    motorJointData = ConvertMotor(jointFrameOrientation, joint, JacobianType.LinearVelocityMotor, target, maxImpulseForLinearMotor);
                    motorJointData.SetImpulseEventThresholdAllConstraints(
                        joint.breakForce * Time.fixedDeltaTime,
                        joint.breakTorque * Time.fixedDeltaTime);

                    worldIndex = GetWorldIndex(joint);
                    CreateJointEntity(worldIndex, GetConstrainedBodyPair(joint), motorJointData);
                }
            }

            if (math.any(isRotationMotor))
            {
                isMotor = true;
                // To simplify conversion require: Angular X Motion = Free. Angular Y&Z = Locked. X/Y/Z Motion = Locked.  Axis field determines rotation
                if (requiredDoF_forAngularMotors)
                {
                    Assert.IsTrue(angularLocks.y && angularLocks.z,
                        $"Configurable Joint Baking Failed for {joint.name}: Baking Motor as Rotation Motor requires that both Angular Y Drive = Locked, Angular Z Drive = Locked");

                    var maxImpulseForAngularMotor = math.length((int3)isRotationMotor * maxForceForAngularMotor) * Time.fixedDeltaTime;
                    float3 targetVector = (int3)isRotationMotor * (float3)joint.targetRotation.eulerAngles; //in degrees, targetRotation a Quaternion
                    var target = math.radians(targetVector.x) * -1; //need -1: PhysX sets target as offset from the target, whereas ECS Physics targets target as where to go

                    motorJointData = ConvertMotor(jointFrameOrientation, joint, JacobianType.RotationMotor, target, maxImpulseForAngularMotor);
                    motorJointData.SetImpulseEventThresholdAllConstraints(
                        joint.breakForce * Time.fixedDeltaTime,
                        joint.breakTorque * Time.fixedDeltaTime);

                    worldIndex = GetWorldIndex(joint);
                    CreateJointEntity(worldIndex, GetConstrainedBodyPair(joint), motorJointData);
                }
            }

            if (math.any(isAngularVelocityMotor))
            {
                isMotor = true;
                // To simplify conversion require: Angular X Motion = Free. Angular Y&Z = Locked. X/Y/Z Motion = Locked.  Axis field determines rotation
                if (requiredDoF_forAngularMotors)
                {
                    Assert.IsTrue(angularLocks.y && angularLocks.z,
                        $"Configurable Joint Baking Failed for {joint.name}: Baking Motor as Angular Velocity Motor requires that both Angular Y Drive = Locked, Angular Z Drive = Locked");

                    var maxImpulseForAngularMotor = math.length((int3)isAngularVelocityMotor * maxForceForAngularMotor) * Time.fixedDeltaTime;
                    float3 targetVector = (int3)isAngularVelocityMotor * (float3)joint.targetAngularVelocity; //target in radian/s
                    var target = targetVector.x;

                    motorJointData = ConvertMotor(jointFrameOrientation, joint, JacobianType.AngularVelocityMotor, target, maxImpulseForAngularMotor);
                    motorJointData.SetImpulseEventThresholdAllConstraints(
                        joint.breakForce * Time.fixedDeltaTime,
                        joint.breakTorque * Time.fixedDeltaTime);

                    worldIndex = GetWorldIndex(joint);
                    CreateJointEntity(worldIndex, GetConstrainedBodyPair(joint), motorJointData);
                }
            }

            if (!isMotor)
            {
                var jointData = CreateConfigurableJoint(jointFrameOrientation, joint, linearLocks, linearLimited,
                    joint.linearLimit, joint.linearLimitSpring, angularFree, angularLocks, angularLimited,
                    joint.lowAngularXLimit, joint.highAngularXLimit, joint.angularXLimitSpring, joint.angularYLimit,
                    joint.angularZLimit, joint.angularYZLimitSpring);

                worldIndex = GetWorldIndex(joint);
                using (var joints = new NativeArray<PhysicsJoint>(2, Allocator.Temp) { [0] = jointData.LinearJoint, [1] = jointData.AngularJoint })
                using (var jointEntities = new NativeList<Entity>(2, Allocator.Temp))
                {
                    CreateJointEntities(worldIndex, GetConstrainedBodyPair(joint), joints, jointEntities);
                }
            }
        }

        public override void Bake(UnityEngine.ConfigurableJoint authoring)
        {
            ConvertConfigurableJoint(authoring);
        }
    }

    class FixedBaker : BaseJointBaker<FixedJoint>
    {
        void ConvertFixedJoint(FixedJoint joint)
        {
            var worldFromJointA = math.mul(
                new RigidTransform(joint.transform.rotation, joint.transform.position),
                new RigidTransform(quaternion.identity, joint.anchor)
            );

            RigidTransform worldFromBodyA = Math.DecomposeRigidBodyTransform(joint.transform.localToWorldMatrix);
            var connectedEntity = GetEntity(joint.connectedBody, TransformUsageFlags.Dynamic);
            RigidTransform worldFromBodyB = connectedEntity == Entity.Null
                ? RigidTransform.identity
                : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            var bodyAFromJoint = new BodyFrame(math.mul(math.inverse(worldFromBodyA), worldFromJointA));
            var bodyBFromJoint = new BodyFrame(math.mul(math.inverse(worldFromBodyB), worldFromJointA));

            var jointData = PhysicsJoint.CreateFixed(bodyAFromJoint, bodyBFromJoint);
            jointData.SetImpulseEventThresholdAllConstraints(joint.breakForce * Time.fixedDeltaTime, joint.breakTorque * Time.fixedDeltaTime);

            var worldIndex = GetWorldIndex(joint);
            Entity entity = CreateJointEntity(worldIndex, GetConstrainedBodyPair(joint), jointData);
        }

        public override void Bake(FixedJoint authoring)
        {
            ConvertFixedJoint(authoring);
        }
    }

    class HingeBaker : BaseJointBaker<HingeJoint>
    {
        void ConvertHingeJoint(HingeJoint joint)
        {
            RigidTransform worldFromBodyA = Math.DecomposeRigidBodyTransform(joint.transform.localToWorldMatrix);
            RigidTransform worldFromBodyB = joint.connectedBody == null
                ? RigidTransform.identity
                : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            Math.CalculatePerpendicularNormalized(joint.axis, out float3 perpendicularA, out _);

            // calculate local joint anchor position in body A space
            var anchorPos = GetScaledLocalAnchorPosition(joint.transform, joint.anchor);
            var bodyAFromJoint = new BodyFrame
            {
                Axis = joint.axis,
                PerpendicularAxis = perpendicularA,
                Position = anchorPos
            };

            var connectedEntity = GetEntity(joint.connectedBody, TransformUsageFlags.Dynamic);
            var isConnectedBodyConverted =
                joint.connectedBody == null || connectedEntity != Entity.Null;

            RigidTransform bFromA = isConnectedBodyConverted ? math.mul(math.inverse(worldFromBodyB), worldFromBodyA) : worldFromBodyA;
            RigidTransform bFromBSource =
                isConnectedBodyConverted ? RigidTransform.identity : worldFromBodyB;

            // calculate local joint anchor position in body B space
            float3 connectedAnchorPos = joint.connectedBody ? GetScaledLocalAnchorPosition(joint.connectedBody.transform, joint.connectedAnchor) : joint.connectedAnchor;
            var bodyBFromJoint = new BodyFrame
            {
                Axis = math.mul(bFromA.rot, joint.axis),
                PerpendicularAxis = math.mul(bFromA.rot, perpendicularA),
                Position = math.mul(bFromBSource, new float4(connectedAnchorPos, 1)).xyz
            };

            var limits = math.radians(new FloatRange(joint.limits.min, joint.limits.max).Sorted());

            // Create different types of joints based on: are there limits, is there a motor?
            PhysicsJoint jointData;
            if (joint.useSpring && !joint.useMotor) //a rotational motor if Use Spring = T, Spring: Spring, Damper, Target Position are set
            {
                var maxImpulseOfMotor = joint.breakTorque * Time.fixedDeltaTime;
                var target = math.radians(joint.spring.targetPosition);
                jointData = PhysicsJoint.CreateRotationalMotor(bodyAFromJoint, bodyBFromJoint, target, maxImpulseOfMotor);
            }
            else if (joint.useMotor) //an angular velocity motor if use Motor = T, Motor: Target Velocity, Force are set
            {
                var targetSpeedInRadians = math.radians(joint.motor.targetVelocity);
                var maxImpulseOfMotor = joint.motor.force * Time.fixedDeltaTime;
                jointData = PhysicsJoint.CreateAngularVelocityMotor(bodyAFromJoint, bodyBFromJoint, targetSpeedInRadians, maxImpulseOfMotor);
            }
            else //non-motorized
            {
                jointData = joint.useLimits
                    ? PhysicsJoint.CreateLimitedHinge(bodyAFromJoint, bodyBFromJoint, limits)
                    : PhysicsJoint.CreateHinge(bodyAFromJoint, bodyBFromJoint);
            }

            jointData.SetImpulseEventThresholdAllConstraints(joint.breakForce * Time.fixedDeltaTime, joint.breakTorque * Time.fixedDeltaTime);

            var worldIndex = GetWorldIndex(joint);
            Entity entity = CreateJointEntity(worldIndex, GetConstrainedBodyPair(joint), jointData);
        }

        public override void Bake(HingeJoint authoring)
        {
            ConvertHingeJoint(authoring);
        }
    }

    class SpringBaker : BaseJointBaker<SpringJoint>
    {
        void ConvertSpringJoint(SpringJoint joint)
        {
            // calculate local joint anchor positions in body A and body B space:

            var jointAnchorA = GetScaledLocalAnchorPosition(joint.transform, joint.anchor);
            float3 jointAnchorB = joint.connectedBody ? GetScaledLocalAnchorPosition(joint.connectedBody.transform, joint.connectedAnchor) : joint.connectedAnchor;

            var connectedEntity = GetEntity(joint.connectedBody, TransformUsageFlags.Dynamic);

            var isConnectedBodyConverted =
                joint.connectedBody == null || connectedEntity != Entity.Null;

            RigidTransform bFromBSource =
                isConnectedBodyConverted ? RigidTransform.identity : Math.DecomposeRigidBodyTransform(joint.connectedBody.transform.localToWorldMatrix);

            jointAnchorB = math.mul(bFromBSource, new float4(jointAnchorB, 1)).xyz;

            // create the joint:

            var distanceRange = new FloatRange(joint.minDistance, joint.maxDistance).Sorted();
            var mass = GetConstrainedBodyMass(joint);
            var impulseEventThreshold = joint.breakForce * Time.fixedDeltaTime;
            var springFrequency = CalculateSpringFrequencyFromSpringConstant(joint.spring, GetConstrainedBodyMass(joint));
            var dampingRatio = CalculateDampingRatio(joint.spring, joint.damper, mass);

            var jointData = PhysicsJoint.CreateLimitedDistance(jointAnchorA, jointAnchorB, distanceRange, impulseEventThreshold, springFrequency, dampingRatio);

            var worldIndex = GetWorldIndex(joint);
            Entity entity = CreateJointEntity(worldIndex, GetConstrainedBodyPair(joint), jointData);
        }

        public override void Bake(UnityEngine.SpringJoint authoring)
        {
            ConvertSpringJoint(authoring);
        }
    }
}
