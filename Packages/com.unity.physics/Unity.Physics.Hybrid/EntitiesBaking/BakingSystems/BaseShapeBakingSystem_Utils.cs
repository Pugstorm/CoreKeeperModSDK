#if UNITY_EDITOR

using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Mathematics;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Physics.Authoring
{
    public partial class BaseShapeBakingSystem
    {
        static Aabb RotatedBoxAabb(float3 center, float3 size, quaternion orientation)
        {
            var extents = 0.5f * size;
            var aabb = new Aabb { Min = float.MaxValue, Max = float.MinValue };
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(-1f, -1f, -1f)))); // 000
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(-1f, -1f,  1f)))); // 001
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(-1f,  1f, -1f)))); // 010
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(-1f,  1f,  1f)))); // 011
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(1f, -1f, -1f))));  // 100
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(1f, -1f,  1f))));  // 101
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(1f,  1f, -1f))));  // 110
            aabb.Include(center + math.mul(orientation, math.mul(extents, new float3(1f,  1f,  1f))));  // 111
            return aabb;
        }

        static Aabb RotatedCylinderAabb(float3 center, float height, float radius, quaternion orientation)
        {
            var cylinder = new Aabb
            {
                Min = center + new float3(2f * radius, -height, 2f * radius),
                Max = center + new float3(2f * radius, height, 2f * radius)
            };
            return RotatedBoxAabb(cylinder.Center, cylinder.Extents, orientation);
        }

        static Hash128 CalculatePhysicsShapeHash(ref ShapeComputationDataBaking shapeData)
        {
            var material = shapeData.Material;
            var collisionFilter = shapeData.CollisionFilter;
            var shapeType = shapeData.ShapeType;
            Hash128 output;

            var bytes = new NativeList<byte>(Allocator.Temp);
            bytes.Append(ref shapeType);
            bytes.Append(ref shapeData.ForceUniqueIdentifier);
            bytes.Append(ref collisionFilter);
            bytes.Append(ref material);

            switch (shapeType)
            {
                case ShapeType.Box:
                {
                    var p = shapeData.BoxProperties;
                    bytes.Append(ref p);
                    var aabb = RotatedBoxAabb(p.Center, p.Size, shapeData.BoxProperties.Orientation);
                    HashableShapeInputs.GetQuantizedTransformations(shapeData.BodyFromShape, aabb, out var transformations);
                    bytes.Append(ref transformations);
                    break;
                }
                case ShapeType.Capsule:
                {
                    var p = shapeData.CapsuleProperties;
                    bytes.Append(ref p);
                    var v0 = p.Vertex0;
                    var v1 = p.Vertex1;
                    var r = p.Radius;
                    var aabb = RotatedCylinderAabb(p.GetCenter(), p.GetHeight(), r, quaternion.LookRotationSafe(v0 - v1, math.up()));
                    HashableShapeInputs.GetQuantizedTransformations(shapeData.BodyFromShape, aabb, out var transformations);
                    bytes.Append(ref transformations);
                    break;
                }
                case ShapeType.Cylinder:
                {
                    var p = shapeData.CylinderProperties;
                    bytes.Append(ref p);
                    var aabb = RotatedCylinderAabb(p.Center, p.Height, p.Radius, shapeData.CylinderProperties.Orientation);
                    HashableShapeInputs.GetQuantizedTransformations(shapeData.BodyFromShape, aabb, out var transformations);
                    bytes.Append(ref transformations);
                    break;
                }
                case ShapeType.Plane:
                {
                    var v = shapeData.PlaneVertices;
                    bytes.Append(ref v);
                    var planeCenter = math.lerp(v.c0, v.c2, 0.5f);
                    var planeSize = math.abs(v.c0 - v.c2);
                    var aabb = RotatedBoxAabb(planeCenter, planeSize, quaternion.LookRotationSafe(v.c1 - v.c2, math.cross(v.c1 - v.c0, v.c2 - v.c1)));
                    HashableShapeInputs.GetQuantizedTransformations(shapeData.BodyFromShape, aabb, out var transformations);
                    bytes.Append(ref transformations);
                    break;
                }
                case ShapeType.Sphere:
                {
                    var p = shapeData.SphereProperties;
                    bytes.Append(ref p);
                    var aabb = new Aabb { Min = p.Center - new float3(p.Radius), Max = p.Center + new float3(p.Radius) };
                    HashableShapeInputs.GetQuantizedTransformations(shapeData.BodyFromShape, aabb, out var transformations);
                    bytes.Append(ref transformations);
                    break;
                }
                case ShapeType.ConvexHull:
                {
                    output = shapeData.Instance.Hash; // precomputed when gathering inputs
                    return output;
                }
                case ShapeType.Mesh:
                {
                    output = shapeData.Instance.Hash; // precomputed when gathering inputs
                    return output;
                }
            }

            unsafe
            {
                output = HashUtility.Hash128(bytes.GetUnsafeReadOnlyPtr(), bytes.Length);
            }

            bytes.Dispose();
            return output;
        }

        static BlobAssetReference<Collider> CalculateBasicBlobAsset(in ShapeComputationDataBaking shapeData)
        {
            switch (shapeData.ShapeType)
            {
                case ShapeType.Box:
                {
                    return BoxCollider.CreateInternal(
                        shapeData.BoxProperties, shapeData.CollisionFilter, shapeData.Material, shapeData.ForceUniqueIdentifier
                    );
                }
                case ShapeType.Capsule:
                {
                    return CapsuleCollider.CreateInternal(
                        shapeData.CapsuleProperties, shapeData.CollisionFilter, shapeData.Material, shapeData.ForceUniqueIdentifier
                    );
                }
                case ShapeType.Cylinder:
                {
                    return CylinderCollider.CreateInternal(
                        shapeData.CylinderProperties, shapeData.CollisionFilter, shapeData.Material, shapeData.ForceUniqueIdentifier
                    );
                }
                case ShapeType.Plane:
                {
                    var v = shapeData.PlaneVertices;
                    return PolygonCollider.CreateQuadInternal(
                        v.c0, v.c1, v.c2, v.c3, shapeData.CollisionFilter, shapeData.Material, shapeData.ForceUniqueIdentifier
                    );
                }
                case ShapeType.Sphere:
                {
                    return SphereCollider.CreateInternal(
                        shapeData.SphereProperties, shapeData.CollisionFilter, shapeData.Material, shapeData.ForceUniqueIdentifier
                    );
                }
                // Note : Mesh and Hull are not computed here as they are in a separated set of jobs
                default:
                    return default;
            }
        }
    }
}
#endif
