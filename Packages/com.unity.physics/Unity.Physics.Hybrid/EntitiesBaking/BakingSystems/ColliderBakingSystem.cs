using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Profiling;
#if UNITY_2023_3_OR_NEWER
using UnityPhysicsMaterial = UnityEngine.PhysicsMaterial;
using UnityPhysicsMaterialCombine = UnityEngine.PhysicsMaterialCombine;
#else
using UnityPhysicsMaterial = UnityEngine.PhysicMaterial;
using UnityPhysicsMaterialCombine = UnityEngine.PhysicMaterialCombine;
#endif

namespace Unity.Physics.Authoring
{
    internal static class FindAncestorBuffer
    {
        public static readonly List<Rigidbody> s_RigidbodiesBuffer = new List<UnityEngine.Rigidbody>(16);
        public static readonly List<UnityEngine.Collider> s_CollidersBuffer = new List<UnityEngine.Collider>(16);
    }

    abstract class ColliderBaker<T> : BaseColliderBaker<T> where T : UnityEngine.Collider
    {
        static List<UnityEngine.Collider> colliderComponents = new List<UnityEngine.Collider>();

        static readonly IReadOnlyDictionary<UnityPhysicsMaterialCombine, Material.CombinePolicy> k_MaterialCombineLookup =
            new Dictionary<UnityPhysicsMaterialCombine, Material.CombinePolicy>
        {
            { UnityPhysicsMaterialCombine.Average, Material.CombinePolicy.ArithmeticMean },
            { UnityPhysicsMaterialCombine.Maximum, Material.CombinePolicy.Maximum },
            { UnityPhysicsMaterialCombine.Minimum, Material.CombinePolicy.Minimum }
        };

        static UnityPhysicsMaterial DefaultMaterial
        {
            get
            {
                if (s_DefaultMaterial == null)
                    s_DefaultMaterial = new UnityPhysicsMaterial { hideFlags = HideFlags.DontSave };
                return s_DefaultMaterial;
            }
        }
        static UnityPhysicsMaterial s_DefaultMaterial;

        Material ProduceMaterial(UnityEngine.Collider collider)
        {
            // n.b. need to manually opt in to collision events with legacy colliders if desired
            var material = new Material();
            if (collider.isTrigger)
            {
                material.CollisionResponse = CollisionResponsePolicy.RaiseTriggerEvents;
            }

            var physicsMaterial = collider.sharedMaterial;
            if (physicsMaterial == null)
                physicsMaterial = DefaultMaterial;
            else
                DependsOn(physicsMaterial);

            material.Friction = physicsMaterial.dynamicFriction;
            if (k_MaterialCombineLookup.TryGetValue(physicsMaterial.frictionCombine, out var combine))
                material.FrictionCombinePolicy = combine;
            else
                Debug.LogWarning(
                    $"{collider.name} uses {physicsMaterial.name}, which specifies non-convertible mode {physicsMaterial.frictionCombine} for {nameof(UnityPhysicsMaterial.frictionCombine)}.",
                    collider
                );

            material.Restitution = physicsMaterial.bounciness;
            if (k_MaterialCombineLookup.TryGetValue(physicsMaterial.bounceCombine, out combine))
                material.RestitutionCombinePolicy = combine;
            else
                Debug.LogWarning(
                    $"{collider.name} uses {physicsMaterial.name}, which specifies non-convertible mode {physicsMaterial.bounceCombine} for {nameof(UnityPhysicsMaterial.bounceCombine)}.",
                    collider
                );

            return material;
        }

        internal ShapeComputationDataBaking GenerateComputationDataGeneric(UnityEngine.Collider shape, GameObject body, ColliderInstanceBaking colliderInstance, bool forceUnique)
        {
            return new ShapeComputationDataBaking
            {
                Instance = colliderInstance,
                Material = ProduceMaterial(shape),
                CollisionFilter = ProduceCollisionFilter(shape, body),
                ForceUniqueIdentifier = forceUnique ? (uint)shape.GetInstanceID() : 0u
            };
        }

        CollisionFilter ProduceCollisionFilter(UnityEngine.Collider collider, GameObject body)
        {
            // Declaring the dependency on the GameObject with GetLayer, so the baker rebakes if the layer changes
            var layer = GetLayer(collider);

            // create filter and assign layer of this collider
            var filter = new CollisionFilter {BelongsTo = 1u << layer};

            uint includeMask = 0u;
            // incorporate global layer collision matrix
            for (var i = 0; i < 32; ++i)
            {
                includeMask |= UnityEngine.Physics.GetIgnoreLayerCollision(layer, i) ? 0 : 1u << i;
            }

            // Now incorporate the layer overrides.
            // The exclude layers take precedence over the include layers.

            includeMask |= (uint)collider.includeLayers.value;
            var excludeMask = (uint)collider.excludeLayers.value;

            // obtain rigid body if any, and incorporate its layer overrides
            var rigidBody = body.GetComponent<Rigidbody>();
            if (rigidBody)
            {
                includeMask |= (uint)rigidBody.includeLayers.value;
                excludeMask |= (uint)rigidBody.excludeLayers.value;
            }

            // apply exclude mask to include mask and set the final result
            includeMask &= ~excludeMask;

            filter.CollidesWith = includeMask;

            return filter;
        }

        bool ShouldConvertShape(T collider)
        {
            return collider.enabled;
        }

        GameObject GetPrimaryBody(GameObject shape, out bool hasBodyComponent, out bool isStaticBody)
        {
            var rb = FindFirstEnabledAncestor(shape, FindAncestorBuffer.s_RigidbodiesBuffer);

            hasBodyComponent = rb != null;
            isStaticBody = false;

            if (rb != null)
                return rb.gameObject;

            // for implicit static shape, first see if it is part of static optimized hierarchy
            isStaticBody = FindTopmostStaticEnabledAncestor(shape, out var topStatic);
            if (topStatic != null)
                return topStatic;

            // otherwise, find topmost enabled Collider or PhysicsShapeAuthoring
            return FindTopmostEnabledAncestor(shape, FindAncestorBuffer.s_CollidersBuffer);
        }

        protected abstract ShapeComputationDataBaking GenerateComputationData(T shapeData, GameObject body, Transform bodyTransform, ColliderInstanceBaking colliderInstance, Entity colliderEntity, bool isUnique);

        ShapeComputationDataBaking GetInputDataFromAuthoringComponent(T shape, Entity colliderEntity)
        {
            GameObject shapeGameObject = shape.gameObject;
            var body = GetPrimaryBody(shapeGameObject, out bool hasBodyComponent, out bool isStaticBody);
            var child = shapeGameObject;
            var shapeInstanceID = shape.GetInstanceID();

            var bodyEntity = GetEntity(body, TransformUsageFlags.Dynamic);

            // prepare the static root
            if (isStaticBody)
            {
                var staticRootMarker = CreateAdditionalEntity(TransformUsageFlags.Dynamic, true, "StaticRootBakeMarker");
                AddComponent(staticRootMarker, new BakeStaticRoot() { Body = bodyEntity, ConvertedBodyInstanceID = body.transform.GetInstanceID() });
            }

            // Track dependencies to the transforms
            Transform shapeTransform = GetComponent<Transform>(shape);
            Transform bodyTransform = GetComponent<Transform>(body);
            var instance = new ColliderInstanceBaking
            {
                AuthoringComponentId = shapeInstanceID,
                BodyEntity = bodyEntity,
                ShapeEntity = GetEntity(shapeGameObject, TransformUsageFlags.Dynamic),
                ChildEntity = GetEntity(child, TransformUsageFlags.Dynamic),
                BodyFromShape = ColliderInstanceBaking.GetCompoundFromChild(shapeTransform, bodyTransform),
            };

            ForceUniqueColliderAuthoring forceUniqueComponent = body.GetComponent<ForceUniqueColliderAuthoring>();
            bool isForceUnique = forceUniqueComponent != null;

            var data = GenerateComputationData(shape, body, bodyTransform, instance, colliderEntity, isForceUnique);

            data.Instance.ConvertedAuthoringInstanceID = shapeInstanceID;
            data.Instance.ConvertedBodyInstanceID = bodyTransform.GetInstanceID();

            // The root colliders with no body in the parent hierarchy needs a PhysicsWorldIndex
            if (!hasBodyComponent && body == shapeGameObject)
            {
                GetComponents(colliderComponents);

                // We need to check that there are no other colliders in the same object, if so, only the first one should do this, otherwise there will be 2 bakers adding this to the entity
                // This will be needed to trigger BuildCompoundColliderBakingSystem
                // If they are legacy Colliders and PhysicsShapeAuthoring in the same object, the PhysicsShapeAuthoring will add this
                if (colliderComponents.Count > 0 && colliderComponents[0].GetInstanceID() == shapeInstanceID)
                {
                    var entity = GetEntity(TransformUsageFlags.Dynamic);

                    // Rigid Body bakes always add the PhysicsWorldIndex component
                    AddSharedComponent(entity, new PhysicsWorldIndex());

                    AddComponent(entity, new PhysicsCompoundData()
                    {
                        AssociateBlobToBody = false,
                        ConvertedBodyInstanceID = shapeInstanceID,
                        Hash = default,
                    });

                    AddComponent<PhysicsRootBaked>(entity);
                    AddComponent<PhysicsCollider>(entity);

                    PostProcessTransform(bodyTransform);
                }
            }

            return data;
        }

        public override void Bake(T authoring)
        {
            var shapeBakingData = new PhysicsColliderAuthoringData();

            // First pass
            Profiler.BeginSample("Collect Inputs from Authoring Components");

            if (ShouldConvertShape(authoring))
            {
                // We can have multiple Colliders of the same type on the same game object, so instead of adding the components to the baking entity
                // we add the components to an additional entity. These new entities will be processed by the baking system
                var colliderEntity = CreateAdditionalEntity(TransformUsageFlags.None, true);
                shapeBakingData.ShapeComputationalData = GetInputDataFromAuthoringComponent(authoring, colliderEntity);
                AddComponent(colliderEntity, shapeBakingData);

                // The data will be filled in by the BaseShapeBakingSystem, but we add it here so it gets reverted from the entity if the collider component is deleted
                AddComponent(colliderEntity, new PhysicsColliderBakedData()
                {
                    BodyEntity = shapeBakingData.ShapeComputationalData.Instance.BodyEntity,
                    BodyFromShape = shapeBakingData.ShapeComputationalData.Instance.BodyFromShape,
                    ChildEntity = shapeBakingData.ShapeComputationalData.Instance.ChildEntity,
                    // It is a leaf if the Shape Entity equals Body Entity
                    IsLeafEntityBody = (shapeBakingData.ShapeComputationalData.Instance.ShapeEntity.Equals(shapeBakingData.ShapeComputationalData.Instance.BodyEntity))
                });
            }

            Profiler.EndSample();
        }
    }

    class BoxBaker : ColliderBaker<UnityEngine.BoxCollider>
    {
        protected override ShapeComputationDataBaking GenerateComputationData(UnityEngine.BoxCollider shape, GameObject body, Transform bodyTransform, ColliderInstanceBaking colliderInstance, Entity colliderEntity, bool forceUnique)
        {
            var res = GenerateComputationDataGeneric(shape, body, colliderInstance, forceUnique);
            res.ShapeType = ShapeType.Box;

            var shapeLocalToWorld = shape.transform.localToWorldMatrix;
            var bakeToShape = GetColliderBakeMatrix(shapeLocalToWorld, bodyTransform.localToWorldMatrix);

            var center = math.transform(bakeToShape, shape.center);

            var geometry = new BoxGeometry
            {
                Center = center,
                Orientation = bakeToShape.rotation
            };

            geometry.Size = math.abs(shape.size * (float3)bakeToShape.lossyScale);

            geometry.BevelRadius = math.min(ConvexHullGenerationParameters.Default.BevelRadius, math.cmin(geometry.Size) * 0.5f);

            res.BoxProperties = geometry;

            return res;
        }
    }

    class SphereBaker : ColliderBaker<UnityEngine.SphereCollider>
    {
        protected override ShapeComputationDataBaking GenerateComputationData(UnityEngine.SphereCollider shape, GameObject body, Transform bodyTransform, ColliderInstanceBaking colliderInstance, Entity colliderEntity, bool forceUnique)
        {
            var res = GenerateComputationDataGeneric(shape, body, colliderInstance, forceUnique);
            res.ShapeType = ShapeType.Sphere;

            var shapeLocalToWorld = shape.transform.localToWorldMatrix;
            var bakeToShape = GetColliderBakeMatrix(shapeLocalToWorld, bodyTransform.localToWorldMatrix);

            var center = math.transform(bakeToShape, shape.center);
            var radius = shape.radius * math.cmax(math.abs(bakeToShape.lossyScale));

            res.SphereProperties = new SphereGeometry { Center = center, Radius = radius };

            return res;
        }
    }

    class CapsuleBaker : ColliderBaker<UnityEngine.CapsuleCollider>
    {
        protected override ShapeComputationDataBaking GenerateComputationData(UnityEngine.CapsuleCollider shape, GameObject body, Transform bodyTransform, ColliderInstanceBaking colliderInstance, Entity colliderEntity, bool forceUnique)
        {
            var res = GenerateComputationDataGeneric(shape, body, colliderInstance, forceUnique);

            res.ShapeType = ShapeType.Capsule;

            var bakeToShape = GetColliderBakeMatrix(shape.transform.localToWorldMatrix, bodyTransform.localToWorldMatrix);
            var lossyScale = math.abs(bakeToShape.lossyScale);

            // the capsule axis corresponds to the local axis specified by the direction index.
            var capsuleAxis = new float3 { [shape.direction] = 1f };

            // the baked radius is the user-specified shape radius times the maximum of the scale of the two axes orthogonal to the capsule axis.
            var radius = shape.radius * math.cmax(new float3(lossyScale) { [shape.direction] = 0f });

            // the capsule vertex offset points from the center of the capsule to the top of the capsule's cylindrical center part.
            var vertexOffset = capsuleAxis * (0.5f * shape.height * lossyScale[shape.direction] - radius);

            // finish baking the vertex offset by rotating it with the bake matrix
            vertexOffset = math.rotate(bakeToShape.rotation, vertexOffset);

            // bake the capsule's center
            var center = math.transform(bakeToShape, shape.center);

            // the capsule's two baked vertices are the baked center plus/minus the baked vertex offset
            var v0 = center + vertexOffset;
            var v1 = center - vertexOffset;

            res.CapsuleProperties = new CapsuleGeometry { Vertex0 = v0, Vertex1 = v1, Radius = radius };

            return res;
        }
    }

    class MeshBaker : ColliderBaker<UnityEngine.MeshCollider>
    {
        protected override ShapeComputationDataBaking GenerateComputationData(UnityEngine.MeshCollider shape, GameObject body, Transform bodyTransform, ColliderInstanceBaking colliderInstance, Entity colliderEntity, bool forceUnique)
        {
            UnityEngine.Mesh mesh = shape.sharedMesh;
            if (mesh == null)
            {
                throw new InvalidOperationException(
                    $"No {nameof(UnityEngine.MeshCollider.sharedMesh)} assigned to {typeof(MeshCollider)} on {shape.name}."
                );
            }

            if (!mesh.IsValidForConversion(shape.gameObject))
            {
                throw new InvalidOperationException(
                    $"Mesh '{mesh}' assigned to {typeof(MeshCollider)} on {shape.name} is not readable. Ensure that you have enabled Read/Write on its import settings."
                );
            }

            // No need to check for null mesh as this has been checked earlier in the function
            DependsOn(mesh);

            var res = GenerateComputationDataGeneric(shape, body, colliderInstance, forceUnique);

            if (shape.convex)
            {
                res.ShapeType = ShapeType.ConvexHull;
                res.ConvexHullProperties.Material = res.Material;
                res.ConvexHullProperties.Filter = res.CollisionFilter;
                res.ConvexHullProperties.GenerationParameters = ConvexHullGenerationParameters.Default;
            }
            else
            {
                res.ShapeType = ShapeType.Mesh;
                res.MeshProperties.Material = res.Material;
                res.MeshProperties.Filter = res.CollisionFilter;
                res.ConvexHullProperties.GenerationParameters = default;
            }

            var meshBakingData = new PhysicsMeshAuthoringData()
            {
                Convex = shape.convex,
                Mesh = mesh,
                BakeFromShape = GetColliderBakeMatrix(shape.transform.localToWorldMatrix, bodyTransform.localToWorldMatrix),
                MeshBounds = mesh.bounds,
                ChildToShape = float4x4.identity
            };
            AddComponent(colliderEntity, meshBakingData);

            return res;
        }
    }
}
