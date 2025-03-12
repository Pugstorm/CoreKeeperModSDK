#if UNITY_EDITOR
#if UNITY_ANDROID && !UNITY_64
#define UNITY_ANDROID_ARM7V
#endif

using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.Profiling;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// Baking system for colliders: Stage 2
    /// This system is responsible for adding new BlobAssetReference<Collider> to the BlobAssetStore. It chains several jobs/steps:
    /// 1) Obtain a MeshDataArray that contains all meshes that have a PhysicsMeshAuthoringData component
    /// 2a) MeshHashesJobHandleJob: Consumes output of Step 1 to calculate and update the hash data of the meshes
    /// (only for convex and mesh colliders)
    /// 2b) BasicHashesJobHandleJob: Performs the same type of work as MeshHashesJobHandleJob, but for all other collider types
    /// 3) DeduplicateJobHandleJob: Checks if a blob hash has already been created in the BlobAssetStore and flags it if it is new
    /// 4a) MeshBlobsJob: Creates a BlobAssetReference<Collider> for each mesh collider and convex collider that was
    /// flagged by the DeduplicateJobHandleJob as requiring recalculation (to be added to the BlobAssetStore).
    /// Job returns an array of ColliderBlobBakingData
    /// 4b) BasicBlobsJobHandleJob: creates a BlobAssetReference<Collider> for each non-mesh collider that was flagged
    /// by the DeduplicateJobHandleJob as requiring recalculation (to be added to the BlobAssetStore) and then updates
    /// the array of ColliderBlobBakingData
    /// 5) Update the BlobComputationContext with the new blobs. This consumes the array of ColliderBlobBakingData
    /// </summary>
    [RequireMatchingQueriesForUpdate]
    [UpdateAfter(typeof(BeginColliderBakingSystem))]
    [UpdateBefore(typeof(BuildCompoundCollidersBakingSystem))]
    [WorldSystemFilter(WorldSystemFilterFlags.BakingSystem)]
    [BurstCompile]
    public partial class BaseShapeBakingSystem : SystemBase
    {
        BeginColliderBakingSystem m_BeginColliderBakingSystem;
        List<UnityEngine.Mesh> meshArray;

        static readonly ProfilerMarker BuffersAcquire = new ProfilerMarker("Buffers Acquire");
        static readonly ProfilerMarker MeshCreate = new ProfilerMarker("Mesh Create");
        static readonly ProfilerMarker ConvexCreate = new ProfilerMarker("Convex Create");

        internal struct ColliderBlobBakingData
        {
            public Entities.Hash128 Hash;
            public BlobAssetReference<Collider> ColliderBlobAsset;
        }

        internal BlobAssetComputationContext<int, Collider> BlobComputationContext =>
            m_BeginColliderBakingSystem.BlobComputationContext;

        EntityQuery m_ShapeQuery;
        EntityQuery m_MeshBlobQuery;

        protected override void OnCreate()
        {
            base.OnCreate();
            m_ShapeQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PhysicsColliderAuthoringData>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            });
            m_BeginColliderBakingSystem = World.GetOrCreateSystemManaged<BeginColliderBakingSystem>();
            meshArray = new List<UnityEngine.Mesh>();

            m_MeshBlobQuery = GetEntityQuery(new EntityQueryDesc
            {
                All = new[] { ComponentType.ReadOnly<PhysicsColliderAuthoringData>(), ComponentType.ReadOnly<PhysicsMeshAuthoringData>() },
                Options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities
            });
        }

        /// <summary>
        /// This job calculates and updates the hash data for only meshes. The hash data needs updated in
        /// ShapeComputationalData.Instance where Instance is a member of ColliderInstanceBaking
        /// </summary>
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
#if !UNITY_ANDROID_ARM7V
        [BurstCompile]
#endif
        partial struct MeshHashesJobHandleJob : IJobEntity
        {
            [ReadOnly] public UnityEngine.Mesh.MeshDataArray meshDataArray;
#if !UNITY_ANDROID_ARM7V
            [BurstCompile]
#endif
            private void Execute(ref PhysicsColliderAuthoringData colliderData, ref PhysicsColliderBakedData generatedData, in PhysicsMeshAuthoringData meshData)
            {
                var hash = CalculateMeshHashes(ref colliderData.ShapeComputationalData, meshData, meshDataArray);
                generatedData.Hash = hash;

                // Setting the hash internally inside the ShapeComputationalData
                var shapeData = colliderData.ShapeComputationalData;
                var instance = shapeData.Instance;
                instance.Hash = hash;
                shapeData.Instance = instance;
                colliderData.ShapeComputationalData = shapeData;
            }
        }

        /// <summary>
        /// This job calculates and updates the hash data for every other collider type than meshes (everything not
        /// covered by MeshHashesJobHandleJob). The hash data needs updated in
        /// ShapeComputationalData.Instance where Instance is a member of ColliderInstanceBaking
        /// </summary>
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
#if !UNITY_ANDROID_ARM7V
        [BurstCompile]
#endif
        [WithNone(typeof(PhysicsMeshAuthoringData))]
        partial struct BasicHashesJobHandleJob : IJobEntity
        {
#if !UNITY_ANDROID_ARM7V
            [BurstCompile]
#endif
            private void Execute(ref PhysicsColliderAuthoringData colliderData, ref PhysicsColliderBakedData generatedData)
            {
                // Calculating the hash
                var hash = CalculatePhysicsShapeHash(ref colliderData.ShapeComputationalData);
                generatedData.Hash = hash;

                // Setting the hash internally inside the ShapeComputationalData
                var shapeData = colliderData.ShapeComputationalData;
                var instance = shapeData.Instance;
                instance.Hash = hash;
                shapeData.Instance = instance;
                colliderData.ShapeComputationalData = shapeData;
            }
        }

        /// <summary>
        /// This job checks if a blob hash has already been created in the BlobAssetStore. If it is new, then the blob
        /// is flagged to be recalculated.
        /// </summary>
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        partial struct DeduplicateJobHandleJob : IJobEntity
        {
            public BlobAssetComputationContext<int, Collider> localBlobComputationContext;
            public NativeArray<int> count;
            private void Execute(ref PhysicsColliderAuthoringData colliderData)
            {
                // Associate the blob hash to the GO Instance ID
                var hash = colliderData.ShapeComputationalData.Instance.Hash;

                if (localBlobComputationContext.NeedToComputeBlobAsset(hash))
                {
                    localBlobComputationContext.AddBlobAssetToCompute(hash, 0);

                    colliderData.BlobIndex = count[0];
                    colliderData.RecalculateBlob = true;
                    ++count[0];
                }
                else
                {
                    colliderData.RecalculateBlob = false;
                }
            }
        }

        /// <summary>
        /// This job consumes the output of the DeduplicateJobHandleJob. For each blob that was flagged to be
        /// recalculated, use the ShapeComputationalData to set the BlobAssetReferences<Collider> (note that this step
        /// calls CreateInternal for each of the collider types and is what adds the blob to the BlobAssetStore.
        /// This job returns a NativeArray of ColliderBlobBakingData which consists of the Hash and BlobAssetReference.
        /// Note: job is not run for mesh colliders or convex colliders
        /// </summary>
        [WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities)]
        [WithNone(typeof(PhysicsMeshAuthoringData))]
        partial struct BasicBlobsJobHandleJob : IJobEntity
        {
            [NativeDisableParallelForRestriction]
            public NativeArray<ColliderBlobBakingData> generatedDataArray;
            private void Execute(ref PhysicsColliderAuthoringData colliderData)
            {
                // Calculate the blob assets if needed
                if (colliderData.RecalculateBlob)
                {
                    var shapeData = colliderData.ShapeComputationalData;
                    var collider = CalculateBasicBlobAsset(shapeData);
                    generatedDataArray[colliderData.BlobIndex] = new ColliderBlobBakingData()
                    {
                        Hash = colliderData.ShapeComputationalData.Instance.Hash,
                        ColliderBlobAsset = collider
                    };
                }
            }
        }

        protected override void OnUpdate()
        {
            int shapeCount = m_ShapeQuery.CalculateEntityCount();
            if (shapeCount == 0)
                return;

            // -----------------------------------------------------------------
            // 1) Obtain all meshes in colliders
            Profiler.BeginSample("Get Meshes");

            // Collect meshes doing only one call to the engine
            // It would be better to use a native container, but AcquireReadOnlyMeshData doesn't accept it
            meshArray.Clear();
            int meshCount = 0;

            foreach (var meshBakingData in SystemAPI.Query<RefRW<PhysicsMeshAuthoringData>>().WithOptions(EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities))
            {
                meshArray.Add(meshBakingData.ValueRW.Mesh.Value);
                meshBakingData.ValueRW.MeshArrayIndex = meshCount;
                ++meshCount;
            }

            var meshDataArray = UnityEditor.MeshUtility.AcquireReadOnlyMeshData(meshArray);
            Profiler.EndSample();

            // -----------------------------------------------------------------
            // 2) Calculate the hashes for all the colliders
            Profiler.BeginSample("Generate Hashes for Inputs");

            // 2a) Convex Hull and Mesh Colliders
            JobHandle meshHashesJobHandle = new MeshHashesJobHandleJob
            {
                meshDataArray = meshDataArray
            }.ScheduleParallel(default(JobHandle));

            // 2b) Basic (all other) Colliders
            JobHandle basicHashesJobHandle = new BasicHashesJobHandleJob().ScheduleParallel(meshHashesJobHandle); //AMS: QUESTION: is the dependency really on meshHashesJobHandle??

            Profiler.EndSample(); //Generate Hashes for Inputs

            var hashesJobHandle = JobHandle.CombineDependencies(meshHashesJobHandle, basicHashesJobHandle);

            // -----------------------------------------------------------------
            // 3) Deduplicate blobs to calculate, so we calculate it only once
            Profiler.BeginSample("Determine New Colliders to Create");

            var localBlobComputationContext = BlobComputationContext;

            NativeArray<int> count = new NativeArray<int>(1, Allocator.TempJob);
            NativeArray<ColliderBlobBakingData> generatedDataArray = new NativeArray<ColliderBlobBakingData>(shapeCount, Allocator.TempJob);

            JobHandle deduplicateJobHandle = new DeduplicateJobHandleJob
            {
                localBlobComputationContext = localBlobComputationContext,
                count = count
            }.Schedule(hashesJobHandle);

            Profiler.EndSample(); //Determine New Colliders to Create

            // -----------------------------------------------------------------
            // 4) Calculate blobs
            Profiler.BeginSample("Create New Colliders");
            Profiler.BeginSample("Hull and Meshes");

            var colliderDataArray = m_MeshBlobQuery.ToComponentDataListAsync<PhysicsColliderAuthoringData>(Allocator.TempJob, deduplicateJobHandle, out JobHandle copyColliderData);
            var meshBakingDataArray = m_MeshBlobQuery.ToComponentDataListAsync<PhysicsMeshAuthoringData>(Allocator.TempJob, deduplicateJobHandle, out JobHandle copyMeshData);

            var job = new MeshBlobsJob
            {
                ColliderDataArray = colliderDataArray.AsDeferredJobArray(),
                MeshBakingDataArray = meshBakingDataArray.AsDeferredJobArray(),
                meshDataArray = meshDataArray,
                generatedDataArray = generatedDataArray,

                BuffersAcquire = BuffersAcquire,
                MeshCreate =  MeshCreate,
                ConvexCreate = ConvexCreate
            };
            var meshBlobsJobHandle = job.Schedule(meshDataArray.Length, 1, JobHandle.CombineDependencies(copyColliderData, copyMeshData));

            Profiler.EndSample(); //Hull and Meshes

            Profiler.BeginSample("Basic Colliders");

            JobHandle basicBlobsJobHandle = new BasicBlobsJobHandleJob
            {
                generatedDataArray = generatedDataArray
            }.ScheduleParallel(meshBlobsJobHandle);

            Profiler.EndSample(); //Basic Colliders
            Profiler.EndSample(); //Create New Colliders

            var blobJobHandle = JobHandle.CombineDependencies(meshBlobsJobHandle, basicBlobsJobHandle);
            blobJobHandle.Complete();

            // -----------------------------------------------------------------
            // 5) Update BlobComputationContext with the new blobs
            Profiler.BeginSample("Store Blobs in Context");

            for (int blobIndex = 0; blobIndex < count[0]; ++blobIndex)
            {
                var entry = generatedDataArray[blobIndex];
                localBlobComputationContext.AddComputedBlobAsset(entry.Hash, entry.ColliderBlobAsset);
            }
            Profiler.EndSample();

            generatedDataArray.Dispose();
            meshDataArray.Dispose();
            count.Dispose();
            colliderDataArray.Dispose();
            meshBakingDataArray.Dispose();
        }

        static Hash128 CalculateMeshHashes(ref ShapeComputationDataBaking res, PhysicsMeshAuthoringData physicsMeshData, UnityEngine.Mesh.MeshDataArray meshDataArray)
        {
            // Access the mesh vertices
            MeshUtilities.AppendMeshPropertiesToNativeBuffers(meshDataArray[physicsMeshData.MeshArrayIndex], !physicsMeshData.Convex, out var pointCloud, out var triangles);

            // Hash Calculation
            return HashableShapeInputs.GetHash128(
                res.ForceUniqueIdentifier,
                res.ConvexHullProperties.GenerationParameters,
                res.Material,
                res.CollisionFilter,
                physicsMeshData.BakeFromShape,
                physicsMeshData.ChildToShape,
                physicsMeshData.MeshBounds,
                pointCloud,
                triangles
            );
        }
    }

    /// <summary>
    /// This job creates a BlobAssetReferences<Collider> for each mesh collider and convex collider that were flagged by
    /// the DeduplicateJobHandleJob as requiring recalculation (to be added to the BlobAssetStore).
    /// Job returns an array of BaseShapeBakingSystem.ColliderBlobBakingData
    /// </summary>
    [BurstCompile]
    struct MeshBlobsJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<PhysicsColliderAuthoringData> ColliderDataArray;
        [ReadOnly] public NativeArray<PhysicsMeshAuthoringData> MeshBakingDataArray;
        [ReadOnly] public UnityEngine.Mesh.MeshDataArray meshDataArray;

        [NativeDisableParallelForRestriction]
        public NativeArray<BaseShapeBakingSystem.ColliderBlobBakingData> generatedDataArray;

        public ProfilerMarker BuffersAcquire;
        public ProfilerMarker MeshCreate;
        public ProfilerMarker ConvexCreate;

        public void Execute(int index)
        {
            var colliderData = ColliderDataArray[index];
            var meshBakingData = MeshBakingDataArray[index];

            if (colliderData.RecalculateBlob)
            {
                BuffersAcquire.Begin();
                MeshUtilities.AppendMeshPropertiesToNativeBuffers(meshDataArray[meshBakingData.MeshArrayIndex], !meshBakingData.Convex, out var pointCloud, out var triangles);
                var compoundMatrix = math.mul(meshBakingData.BakeFromShape, meshBakingData.ChildToShape);
                for (int i = 0; i < pointCloud.Length; ++i)
                    pointCloud[i] = math.mul(compoundMatrix, new float4(pointCloud[i], 1f)).xyz;
                BuffersAcquire.End();

                if (meshBakingData.Convex)
                {
                    ConvexCreate.Begin();
                    // Create the blob for Convex meshArray
                    var colliderBlobAsset = ConvexCollider.CreateInternal(pointCloud,
                        colliderData.ShapeComputationalData.ConvexHullProperties.GenerationParameters,
                        colliderData.ShapeComputationalData.ConvexHullProperties.Filter,
                        colliderData.ShapeComputationalData.ConvexHullProperties.Material,
                        colliderData.ShapeComputationalData.ForceUniqueIdentifier);
                    generatedDataArray[colliderData.BlobIndex] = new BaseShapeBakingSystem.ColliderBlobBakingData()
                    {
                        Hash = colliderData.ShapeComputationalData.Instance.Hash,
                        ColliderBlobAsset = colliderBlobAsset
                    };
                    ConvexCreate.End();
                }
                else if (pointCloud.Length != 0 && triangles.Length != 0)
                {
                    MeshCreate.Begin();
                    // Create the blob for mesh colliders
                    var colliderBlobAsset = MeshCollider.CreateInternal(pointCloud, triangles,
                        colliderData.ShapeComputationalData.MeshProperties.Filter,
                        colliderData.ShapeComputationalData.MeshProperties.Material,
                        colliderData.ShapeComputationalData.ForceUniqueIdentifier);
                    generatedDataArray[colliderData.BlobIndex] = new BaseShapeBakingSystem.ColliderBlobBakingData()
                    {
                        Hash = colliderData.ShapeComputationalData.Instance.Hash,
                        ColliderBlobAsset = colliderBlobAsset
                    };
                    MeshCreate.End();
                }

                pointCloud.Dispose();
                if (triangles.IsCreated)
                    triangles.Dispose();
            }
        }
    }
}
#endif
