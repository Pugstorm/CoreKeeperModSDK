using System;
using NUnit.Framework;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Physics.Authoring;
using Unity.Physics.Extensions;
using UnityEngine;

namespace Unity.Physics.Tests.Authoring
{
    [TestFixture(typeof(BoxCollider), typeof(SphereCollider))]
    [TestFixture(typeof(CylinderCollider), typeof(BoxCollider))]
    [TestFixture(typeof(CapsuleCollider), typeof(CylinderCollider))]
    [TestFixture(typeof(SphereCollider), typeof(MeshCollider))]
    [TestFixture(typeof(MeshCollider), typeof(CompoundCollider))]
    [TestFixture(typeof(ConvexCollider), typeof(TerrainCollider))]
    [TestFixture(typeof(TerrainCollider), typeof(MeshCollider))]
    [TestFixture(typeof(CompoundCollider), typeof(CapsuleCollider))]

    unsafe class BlobAssetReferenceColliderExtentions_UnitTests<T, InvalidCast> where T : unmanaged, ICollider where InvalidCast : unmanaged, ICollider
    {
        public static BlobAssetReference<Collider> MakeBox()
        {
            return BoxCollider.Create(new BoxGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.identity,
                Size = new float3(1.0f, 1.0f, 1.0f),
                BevelRadius = 0.0f
            });
        }

        public static BlobAssetReference<Collider> MakeCapsule()
        {
            return CapsuleCollider.Create(new CapsuleGeometry { Vertex0 = math.up(), Vertex1 = -math.up(), Radius = 1.0f });
        }

        public static BlobAssetReference<Collider> MakeCylinder()
        {
            return CylinderCollider.Create(new CylinderGeometry
            {
                Center = float3.zero,
                Orientation = quaternion.AxisAngle(new float3(1.0f, 0.0f, 0.0f), 45.0f),
                Height = 2f,
                Radius = 0.25f,
                BevelRadius = 0.05f,
                SideCount = 8
            });
        }

        public static BlobAssetReference<Collider> MakeSphere()
        {
            return SphereCollider.Create(new SphereGeometry { Center = float3.zero, Radius = 1.0f });
        }

        public static BlobAssetReference<Collider> MakeMesh()
        {
            unsafe
            {
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                UnityEngine.Mesh mesh = go.GetComponent<MeshFilter>().sharedMesh;

                var vertexBuff = mesh.vertices;
                var indexBuff = mesh.triangles;
                var verts = new NativeArray<float3>(vertexBuff.Length, Allocator.Temp);
                var tris = new NativeArray<int3>(indexBuff.Length / 3, Allocator.Temp);

                fixed(int* indexPtr = indexBuff)
                UnsafeUtility.MemCpy(tris.GetUnsafePtr(), indexPtr, UnsafeUtility.SizeOf<int>() * indexBuff.Length);

                UnityEngine.Object.DestroyImmediate(go);
                return MeshCollider.Create(verts, tris);
            }
        }

        public static BlobAssetReference<Collider> MakeConvex()
        {
            float3[] testPoints =
            {
                new float3(1.45f, 8.67f, 3.45f),
                new float3(8.75f, 1.23f, 6.44f),
                new float3(100.34f, 5.33f, -2.55f),
                new float3(8.76f, 4.56f, -4.54f),
                new float3(9.75f, -0.45f, -8.99f),
                new float3(7.66f, 3.44f, 0.0f)
            };

            return ConvexCollider.Create(new NativeArray<float3>(testPoints, Allocator.Temp), new ConvexHullGenerationParameters { BevelRadius = 0.125f });
        }

        public static BlobAssetReference<Collider> MakeTerrain()
        {
            return TerrainCollider.Create(new NativeArray<float>(16, Allocator.Temp), new int2(4, 4), new float3(1.0f, 1.0f, 1.0f), TerrainCollider.CollisionMethod.VertexSamples);
        }

        public static BlobAssetReference<Collider> MakeCompound()
        {
            using var box = MakeBox();
            var children = new NativeArray<CompoundCollider.ColliderBlobInstance>(4, Allocator.Temp)
            {
                [0] = new CompoundCollider.ColliderBlobInstance { Collider = box, CompoundFromChild = math.mul(RigidTransform.identity, new RigidTransform(quaternion.identity, new float3(+0.5f, +0.5f, +0.5f))) },
                [1] = new CompoundCollider.ColliderBlobInstance { Collider = box, CompoundFromChild = math.mul(RigidTransform.identity, new RigidTransform(quaternion.identity, new float3(-0.5f, +0.5f, +0.5f))) },
                [2] = new CompoundCollider.ColliderBlobInstance { Collider = box, CompoundFromChild = math.mul(RigidTransform.identity, new RigidTransform(quaternion.identity, new float3(+0.5f, -0.5f, +0.5f))) },
                [3] = new CompoundCollider.ColliderBlobInstance { Collider = box, CompoundFromChild = math.mul(RigidTransform.identity, new RigidTransform(quaternion.identity, new float3(+0.5f, +0.5f, -0.5f))) },
            };

            return CompoundCollider.Create(children);
        }

        //not burstable
        public static BlobAssetReference<Collider> MakeCollider<ColliderT>() where ColliderT : ICollider
        {
            switch (default(ColliderT))
            {
                case BoxCollider col:
                    return MakeBox();
                case CapsuleCollider col:
                    return MakeCapsule();
                case CylinderCollider col:
                    return MakeCylinder();
                case SphereCollider col:
                    return MakeSphere();
                case MeshCollider col:
                    return MakeMesh();
                case ConvexCollider col:
                    return MakeConvex();
                case TerrainCollider col:
                    return MakeTerrain();
                case CompoundCollider col:
                    return MakeCompound();
            }

            throw new Exception("Unhandled collider type.");
        }

        [Test]
        public void As_WithCorrectTypeDoesNotThrow()
        {
            using (var col = MakeCollider<T>())
            {
                Assert.DoesNotThrow(() => { col.As<T>(); });
            }
        }

        [Test]
        public void As_WithCorrectTypeCollierPtrMatches()
        {
            using (var col = MakeCollider<T>())
            {
                ref var castedCol = ref col.As<T>();
                fixed(void* ptr = &castedCol)
                Assert.AreEqual((ulong)ptr, (ulong)col.GetUnsafePtr());
            }
        }

        [Test]
        public void As_WithInvalidCastTypeThrows()
        {
            using (var col = MakeCollider<T>())
            {
                Assert.Throws(typeof(Exception), () => { col.As<InvalidCast>(); });
            }
        }

        [Test]
        public void AsPtr_WithCorrectTypeDoesNotThrow()
        {
            using (var col = MakeCollider<T>())
            {
                Assert.DoesNotThrow(() => { col.AsPtr<T>(); });
            }
        }

        [Test]
        public void AsPtr_WithCorrectTypeCollierPtrMatches()
        {
            using (var col = MakeCollider<T>())
            {
                Assert.AreEqual((ulong)col.AsPtr<T>(), (ulong)col.GetUnsafePtr());
            }
        }

        [Test]
        public void AsPtr_UsingSpecializationDoesNotThrow()
        {
            using (var col = MakeCollider<T>())
            {
                Assert.DoesNotThrow(() => { col.AsPtr(); });
            }
        }

        [Test]
        public void AsPtr_WithInvalidCastTypeThrows()
        {
            using (var col = MakeCollider<T>())
            {
                Assert.Throws(typeof(Exception), () => { col.AsPtr<InvalidCast>(); });
            }
        }

        [Test]
        public void AsComponent_ComponentColliderPtrMatchesBlobPtr()
        {
            using (var col = MakeCollider<T>())
            {
                var cmp = col.AsComponent();

                Assert.AreEqual((ulong)cmp.ColliderPtr, (ulong)col.GetUnsafePtr());
            }
        }
    }

    class BlobAssetReferenceColliderExtensions_CompileTests
    {
        [BurstCompile]
        protected struct TestParalelForJobBox : IJobParallelFor
        {
            public void Execute(int index)
            {
                var col = BoxCollider.Create(new BoxGeometry
                {
                    Center = float3.zero,
                    Orientation = quaternion.identity,
                    Size = new float3(1.0f, 1.0f, 1.0f),
                    BevelRadius = 0.0f
                });

                try
                {
                    var cvx = col.As<ConvexCollider>();
                }
                finally
                {
                    col.Dispose();
                }
            }
        }
        [Test]
        public void As_WithinIParalelForJob_CanCompileSuccessfully()
        {
            Assert.DoesNotThrow(() => { var j = default(TestParalelForJobBox); j.Execute(-1); });
        }
    }
}
