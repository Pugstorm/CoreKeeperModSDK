using System;
using Unity.Burst;
using Unity.Burst.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;

namespace Unity.Transforms
{
    /// <summary>
    /// Helper functions to manipulate and decompose transformation matrices.
    /// </summary>
    [BurstCompile]
    public static class TransformHelpers
    {
        // ------------------ Transformation Matrix Decomposition Helpers

        /// <summary>Computes the "forward" direction in a transformation matrix's reference frame.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector pointing in the "forward" direction of <paramref name="m"/>'s reference frame. By Unity's convention,
        /// this is positive Z axis. This vector does not necessarily have unit length.
        /// </returns>
        public static float3 Forward(in this float4x4 m) => new float3(m.c2.x, m.c2.y, m.c2.z);

        /// <summary>Computes the "back" direction in a transformation matrix's reference frame.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector pointing in the "back" direction of <paramref name="m"/>'s reference frame. By Unity's convention,
        /// this is negative Z axis. This vector does not necessarily have unit length.
        /// </returns>
        public static float3 Back(in this float4x4 m) => -Forward(m);

        /// <summary>Computes the "up" direction in a transformation matrix's reference frame.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector pointing in the "up" direction of <paramref name="m"/>'s reference frame. By Unity's convention,
        /// this is positive Y axis. This vector does not necessarily have unit length.
        /// </returns>
        public static float3 Up(in this float4x4 m) => new float3(m.c1.x, m.c1.y, m.c1.z);

        /// <summary>Computes the "down" direction in a transformation matrix's reference frame.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector pointing in the "down" direction of <paramref name="m"/>'s reference frame. By Unity's convention,
        /// this is negative Y axis. This vector does not necessarily have unit length.
        /// </returns>
        public static float3 Down(in this float4x4 m) => -Up(m);

        /// <summary>Computes the "right" direction in a transformation matrix's reference frame.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector pointing in the "right" direction of <paramref name="m"/>'s reference frame. By Unity's convention,
        /// this is positive X axis. This vector does not necessarily have unit length.
        /// </returns>
        public static float3 Right(in this float4x4 m) => new float3(m.c0.x, m.c0.y, m.c0.z);

        /// <summary>Computes the "left" direction in a transformation matrix's reference frame.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector pointing in the "left" direction of <paramref name="m"/>'s reference frame. By Unity's convention,
        /// this is negative X axis. This vector does not necessarily have unit length.
        /// </returns>
        public static float3 Left(in this float4x4 m) => -Right(m);

        /// <summary>Extracts the translation from a transformation matrix</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector containing the translation applied by the provided transformation matrix.
        /// </returns>
        public static float3 Translation(in this float4x4 m) => new float3(m.c3.x, m.c3.y, m.c3.z);

        /// <summary>Extracts the rotation from a transformation matrix</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A normalized quaternion containing the rotation applied by the provided transformation matrix.
        /// </returns>
        public static quaternion Rotation(in this float4x4 m) => new quaternion(math.orthonormalize(new float3x3(m)));

        /// <summary>Extracts the scale from a transformation matrix</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <returns>
        /// A vector containing the scale applied by the provided transformation matrix.
        /// </returns>
        public static float3 Scale(in this float4x4 m) =>
            new float3(math.length(m.c0.xyz), math.length(m.c1.xyz), math.length(m.c2.xyz));

        // ------------------ Coordinate system conversion

        /// <summary>Transforms a 3D point by a 4x4 transformation matrix.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <param name="p">A 3D position</param>
        /// <returns>
        /// A vector containing the transformed point.
        /// </returns>
        public static float3 TransformPoint(in this float4x4 m, in float3 p) => math.mul(m, new float4(p, 1)).xyz;

        /// <summary>Transforms a 3D direction by a 4x4 transformation matrix.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <param name="d">A vector representing a direction in 3D space. This vector does not need to be normalized.</param>
        /// <returns>
        /// A vector containing the transformed direction. This vector will not necessarily be unit-length.
        /// </returns>
        public static float3 TransformDirection(in this float4x4 m, in float3 d) => math.rotate(m, d);

        /// <summary>Transforms a 3D rotation by a 4x4 transformation matrix.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <param name="q">A quaternion representing a 3D rotation. This quaternion does not need to be normalized.</param>
        /// <returns>
        /// A quaternion containing the transformed rotation. This quaternion will normalized if the input quaternion is normalized.
        /// </returns>
        public static quaternion TransformRotation(in this float4x4 m, in quaternion q) =>
            math.mul(new quaternion(math.orthonormalize(new float3x3(m))), q);

        /// <summary>Transforms a 3D point by the inverse of a 4x4 transformation matrix.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <param name="p">A 3D position</param>
        /// <returns>
        /// A vector containing the transformed point.
        /// </returns>
        public static float3 InverseTransformPoint(in this float4x4 m, in float3 p) => math.mul(math.inverse(m), new float4(p, 1)).xyz;

        /// <summary>Transforms a 3D direction by the inverse of a 4x4 transformation matrix.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <param name="d">A vector representing a direction in 3D space. This vector does not need to be normalized.</param>
        /// <returns>
        /// A vector containing the transformed direction. This vector will not necessarily be unit-length.
        /// </returns>
        public static float3 InverseTransformDirection(in this float4x4 m, in float3 d) => math.rotate(math.inverse(m), d);

        /// <summary>Transforms a 3D rotation by the inverse of a 4x4 transformation matrix.</summary>
        /// <remarks>
        /// This method assumes that <paramref name="m"/> is an affine transformation matrix without shear.
        /// </remarks>
        /// <param name="m">A transformation matrix</param>
        /// <param name="q">A quaternion representing a 3D rotation. This quaternion does not need to be normalized.</param>
        /// <returns>
        /// A quaternion containing the transformed rotation. This quaternion will be normalized if the input quaternion is normalized.
        /// </returns>
        public static quaternion InverseTransformRotation(in this float4x4 m, in quaternion q) =>
            math.mul(new quaternion(math.orthonormalize(math.inverse(new float3x3(m)))), q);

        /// <summary>Computes a rotation so that "forward" points to the target.</summary>
        /// <param name="eyeWorldPosition">The 3D position of the viewer (the "eye"), in world-space.</param>
        /// <param name="targetWorldPosition">The 3D position the viewer wants to rotate to face, in world-space</param>
        /// <param name="worldUp">The direction in world-space that represents "up". When in doubt,
        /// <see cref="Unity.Mathematics.math.up()"/> is often a safe bet.</param>
        /// <remarks>
        /// Note that the viewer's existing orientation is ignored; the quaternion returned by this function should replace
        /// the viewer's rotation, not be added to it.
        /// </remarks>
        /// <returns>
        /// A quaternion containing the rotation which would cause a viewer at <paramref name="eyeWorldPosition"/> to face
        /// <paramref name="targetWorldPosition"/>, with the "up" direction in the resulting reference frame corresponding
        /// as closely as possible to <typeparam name="worldUp"></typeparam>.
        /// </returns>
        public static quaternion LookAtRotation(in float3 eyeWorldPosition, float3 targetWorldPosition, float3 worldUp)
        {
            return quaternion.LookRotationSafe(targetWorldPosition - eyeWorldPosition, worldUp);
        }

        /// <summary>
        /// Synchronously compute a local-to-world transformation matrix for an entity, using the current values
        /// of LocalTransform and PostTransformMatrix for the target entity and its hierarchy ancestors.
        /// </summary>
        /// <remarks>
        /// This method is intended for the relatively uncommon cases where an entity's accurate world-space
        /// transformation matrix is needed immediately. For example:
        /// - When performing a raycast from an entity which may be part of an entity hierarchy, such as the wheel of a
        ///   car object. The ray origin must be in world-space, but the entity's <see cref="LocalTransform"/> may be
        ///   relative to its parent.
        /// - When one entity's transform needs to "track" another in world-space, and the targeting entity and/or the
        ///   targeted entity are in a transform hierarchy.
        /// - When an entity's transform is modified in the <see cref="LateSimulationSystemGroup"/> (after the
        ///   <see cref="TransformSystemGroup"/> has updated, but before the <see cref="PresentationSystemGroup"/> runs),
        ///   this method can be used to compute a new <see cref="LocalToWorld"/> value for the affected entity.
        ///
        /// For an entity that is not part of an entity hierarchy, the <see cref="LocalTransform"/> component already
        /// stores the world-space transform (since the two spaces are identical). In his case, reading <see cref="LocalTransform"/>
        /// directly is more efficient than calling this method.
        ///
        /// The <see cref="LocalToWorld"/> component also contains a world-space transformation matrix. However,
        /// this value may be out of date or invalid; it is only updated when the <see cref="TransformSystemGroup"/> runs).
        /// It may also contain additional offsets applied for graphical smoothing purposes.
        /// Therefore, while the <see cref="LocalToWorld"/> component may be useful as a fast approximation when its latency
        /// is acceptable, it should not be relied one when an accurate, up-to-date world transform is needed for
        /// simulation purposes.
        ///
        /// This method's running time scales with the depth of the target <paramref name="entity"/> is in its hierarchy.
        /// </remarks>
        /// <param name="entity">The entity whose world-space transform should be computed.</param>
        /// <param name="outputMatrix">If successful, the output world-space transformation matrix will be stored here.</param>
        /// <param name="localTransformLookup">Required to access the current transform values for <paramref name="entity"/>
        /// and its ancestors in the transform hierarchy. This method only requires read-only access.</param>
        /// <param name="parentLookup">Required to access the current parent for <paramref name="entity"/>
        /// and its ancestors in the transform hierarchy. This method only requires read-only access.</param>
        /// <param name="scaleLookup">Required to access the current non-uniform scale values for <paramref name="entity"/>
        /// and its ancestors in the transform hierarchy. This method only requires read-only access.</param>
        /// <exception cref="InvalidOperationException">Thrown if <paramref name="entity"/> or one if its ancestors are invalid,
        /// or are missing the required <see cref="LocalTransform"/> component.</exception>
        [BurstCompile]
        [GenerateTestsForBurstCompatibility]
        public static unsafe void ComputeWorldTransformMatrix(in Entity entity, out float4x4 outputMatrix,
            ref ComponentLookup<LocalTransform> localTransformLookup,
            ref ComponentLookup<Parent> parentLookup,
            ref ComponentLookup<PostTransformMatrix> scaleLookup)
        {
            // We expect most hierarchies to be shallow enough to fit in a small fixed list, which avoids the need for
            // a memory allocation. But we fall back on a larger allocation if necessary, for correctness.
            const int fixedListCapacity = 16;
            var entities = stackalloc Entity[fixedListCapacity];
            NativeList<Entity> entitiesDynamicList = default;
            bool fitsInFixedList = true;
            bool hasNonUniformScale = scaleLookup.HasComponent(entity);
            entities[0] = entity;
            int entityCount = 1;
            Entity e = entity;
            while (parentLookup.TryGetComponent(e, out Parent parent))
            {
                if (Hint.Unlikely(fitsInFixedList && entityCount == fixedListCapacity))
                {
                    fitsInFixedList = false;
                    entitiesDynamicList = new NativeList<Entity>(2*fixedListCapacity, Allocator.TempJob);
                    for (int i = 0; i < entityCount; ++i)
                        entitiesDynamicList.AddNoResize(entities[i]);
                }

                e = parent.Value;
                if (Hint.Likely(fitsInFixedList))
                    entities[entityCount] = e;
                else
                    entitiesDynamicList.Add(e);
                entityCount += 1;
                if (Hint.Likely(!hasNonUniformScale))
                    hasNonUniformScale = scaleLookup.HasComponent(e);
            }
            // If the entities didn't fit in the fixed list, update to the dynamic list's final base pointer
            if (Hint.Unlikely(!fitsInFixedList))
                entities = entitiesDynamicList.GetUnsafeReadOnlyPtr();

            if (Hint.Unlikely(hasNonUniformScale))
            {
                outputMatrix = float4x4.identity;
                for (int i = entityCount - 1; i >= 0; --i)
                {
                    e = entities[i];
                    if (Hint.Likely(localTransformLookup.TryGetComponent(e, out LocalTransform localTransform)))
                    {
                        outputMatrix = math.mul(outputMatrix, localTransform.ToMatrix());
                        if (Hint.Unlikely(scaleLookup.TryGetComponent(e, out PostTransformMatrix nonUniformScale)))
                        {
                            outputMatrix = math.mul(outputMatrix, nonUniformScale.Value);
                        }
                    }
                    else
                    {
                        if (Hint.Unlikely((entitiesDynamicList.IsCreated)))
                            entitiesDynamicList.Dispose();
                        throw new InvalidOperationException(
                            $"Entity {e} does not have the required LocalTransform component");
                    }
                }
            }
            else
            {
                outputMatrix = float4x4.identity;
                for (int i = entityCount - 1; i >= 0; --i)
                {
                    e = entities[i];
                    if (Hint.Likely(localTransformLookup.TryGetComponent(e, out LocalTransform localTransform)))
                    {
                        outputMatrix = math.mul(outputMatrix, localTransform.ToMatrix());
                    }
                    else
                    {
                        if (Hint.Unlikely((entitiesDynamicList.IsCreated)))
                            entitiesDynamicList.Dispose();
                        throw new InvalidOperationException(
                            $"Entity {e} does not have the required LocalTransform component");
                    }
                }
            }

            if (Hint.Unlikely((entitiesDynamicList.IsCreated)))
                entitiesDynamicList.Dispose();
        }
    }
}
