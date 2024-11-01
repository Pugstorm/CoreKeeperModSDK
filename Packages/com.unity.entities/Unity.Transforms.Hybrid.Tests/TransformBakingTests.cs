using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Unity.Entities.Hybrid.Baking;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

namespace Unity.Entities.Tests.Conversion
{
    class TransformBakingTests
    {
        #region Setup

        private Scene _scene;
        private World _destinationWorld;
        private EntityManager _entityManager;
        private TestWithObjects _objects = default;

        internal enum ConversionType
        {
            ConvertHierarchy,
            ConvertScene
        }

        [OneTimeSetUp]
        public void SetUpOnce()
        {
            _scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            _destinationWorld = new World("TestWorld");
            _entityManager = _destinationWorld.EntityManager;
        }

        [OneTimeTearDown]
        public void TearDownOnce()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
            _destinationWorld.Dispose();
            _destinationWorld = null;
        }

        [SetUp]
        public void SetUp()
        {
            _objects.SetUp();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _scene.GetRootGameObjects())
                Object.DestroyImmediate(go);
            _objects.TearDown();
            _entityManager.DestroyEntity(_entityManager.UniversalQuery);

            AssignTransformUsageBaker.Flags.Clear();
            AssignTransformUsageBaker.AdditionalFlags.Clear();
            AssignTransformUsageBaker.AdditionalCount = 0;
        }

        BakingSettings MakeDefaultSettings() => new BakingSettings
        {
            BakingFlags = BakingUtility.BakingFlags.AssignName
        };

        private void Convert(GameObject root, ConversionType conversion, BakingSettings settings, params Type[] additionalBakers)
        {
            // These tests fully replace existing bakers, this means we also replace the StaticAndActiveBaker which currently forces all transforms to be dynamic so that nothing is stripped by default.
            var replaceExistingBakers = true;
            using var overrideBakers = new BakerDataUtility.OverrideBakers(replaceExistingBakers, additionalBakers);

            if (settings == null)
            {
                settings = MakeDefaultSettings();
                if (conversion == ConversionType.ConvertScene)
                    settings.BakingFlags |= BakingUtility.BakingFlags.AddEntityGUID;
            }

            bool localBlobAssetStore = !settings.BlobAssetStore.IsCreated;
            if (localBlobAssetStore)
            {
                settings.BlobAssetStore = new BlobAssetStore(128);
            }

            if (conversion == ConversionType.ConvertScene)
            {
                SceneManager.MoveGameObjectToScene(root, _scene);
                BakingUtility.BakeScene(_destinationWorld, _scene, settings, false, null);
            }
            else
                BakingUtility.BakeGameObjects(_destinationWorld, new[]{root}, settings);

            if (localBlobAssetStore)
            {
                settings.BlobAssetStore.Dispose();
            }
        }

        private void Convert(GameObject root, ConversionType conversion, params Type[] additionalBakers)
        {
            Convert(root, conversion, null, additionalBakers);
        }

        #endregion setup

        [DisableAutoCreation]
        class AssignTransformUsageBaker : Baker<UnityEngine.Transform>
        {
            internal static readonly Dictionary<GameObject, TransformUsageFlags> Flags = new Dictionary<GameObject, TransformUsageFlags>();
            internal static readonly Dictionary<GameObject, TransformUsageFlags> AdditionalFlags = new Dictionary<GameObject, TransformUsageFlags>();
            internal static int AdditionalCount = 0;
            public override void Bake(UnityEngine.Transform authoring)
            {
                if (Flags.TryGetValue(authoring.gameObject, out var flags))
                    GetEntity(authoring, flags);

                for (int index = 0; index < AdditionalCount; ++index)
                {
                    if (AdditionalFlags.TryGetValue(authoring.gameObject, out flags))
                        CreateAdditionalEntity(flags);
                }
            }
        }

        public enum ConvertGameObjectParent
        {
            None,
            Renderable,
            RenderableNonUniformScale,
            Dynamic,
            ManualOverride
        }

        [Flags]
        public enum ExpectedConvertedTransformResults
        {
            Nothing                     = 0,
            HasLocalToWorld             = 1,
            HasLocalTransform           = 1 << 1,
            HasPostTransformMatrix      = 1 << 2,
            HasParent                   = 1 << 3,
            HasWorldSpaceData           = 1 << 4,
            HasNonUniformScale          = 1 << 5,
            HasValidRuntimeParent       = 1 << 6
        }

        public struct ConvertGameObjectInput
        {
            public ConvertGameObjectParent parentConfig;
            public TransformUsageFlags flags;
            public bool nonUniformScale;

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                switch (parentConfig)
                {
                    case ConvertGameObjectParent.None:
                        sb.Append("NoParent");
                        break;
                    case ConvertGameObjectParent.Renderable:
                        sb.Append("ParentRenderable");
                        break;
                    case ConvertGameObjectParent.RenderableNonUniformScale:
                        sb.Append("ParentRenderableNonUniScl");
                        break;
                    case ConvertGameObjectParent.Dynamic:
                        sb.Append("ParentDynamic");
                        break;
                    case ConvertGameObjectParent.ManualOverride:
                        sb.Append("ParentManual");
                        break;
                }
                sb.Append("_");
                if (flags == TransformUsageFlags.None)
                {
                    sb.Append("None");
                }
                if ((flags & TransformUsageFlags.Renderable) != 0)
                {
                    sb.Append("Renderable");
                }
                if ((flags & TransformUsageFlags.Dynamic) != 0)
                {
                    sb.Append("Dynamic");
                }
                if ((flags & TransformUsageFlags.WorldSpace) != 0)
                {
                    sb.Append("World");
                }
                if ((flags & TransformUsageFlags.NonUniformScale) != 0)
                {
                    sb.Append("NonUniScl");
                }
                if ((flags & TransformUsageFlags.ManualOverride) != 0)
                {
                    sb.Append("Manual");
                }
                sb.Append("_");
                if (nonUniformScale)
                {
                    sb.Append("NonUniScl");
                }
                else
                {
                    sb.Append("UniScl");
                }
                return sb.ToString();
            }
        }

        public static ExpectedConvertedTransformResults CalculateExpected(ConvertGameObjectInput input)
        {
            bool dynamic = (input.flags & TransformUsageFlags.Dynamic) != 0;
            bool worldSpace = (input.flags & TransformUsageFlags.WorldSpace) != 0;
            bool nonUniformScale = (input.flags & TransformUsageFlags.NonUniformScale) != 0;
            bool renderable = (input.flags & TransformUsageFlags.Renderable) != 0 || dynamic || worldSpace ||
                              nonUniformScale;
            bool manualOverrideParent = (input.parentConfig == ConvertGameObjectParent.ManualOverride);
            bool dynamicParent = (input.parentConfig == ConvertGameObjectParent.Dynamic) || manualOverrideParent;
            bool nonUniformScaleParent = (input.parentConfig == ConvertGameObjectParent.RenderableNonUniformScale);

            //return ConvertSingleGameObjectResults.Nothing;

            ExpectedConvertedTransformResults expected = new ExpectedConvertedTransformResults();

            if ((input.flags & TransformUsageFlags.ManualOverride) != 0)
                return ExpectedConvertedTransformResults.Nothing;

            // if(map.WorldSpace == 1 && map.Reparentable == 0) {
            if(worldSpace)
            {
                // if(component == 'LocalTransfom') return map.Dynamic;
                if (dynamic)
                {
                    expected |= ExpectedConvertedTransformResults.HasLocalTransform;
                }
                //if(component == 'Parent') return 0;
            }
            else
            {
                //if(component == 'LocalTransfom') return map.Renderable & (map.Reparentable | map.Dynamic | map.DynamicParent);
                if (renderable && (dynamic || dynamicParent))
                {
                    expected |= ExpectedConvertedTransformResults.HasLocalTransform;
                }
                //if(component == 'Parent') return map.Renderable & (map.Reparentable | map.DynamicParent);
                if (renderable && dynamicParent)
                {
                    expected |= ExpectedConvertedTransformResults.HasParent;
                }
            }

            // if(component == 'LocalToWorld') return map.Renderable;
            if (renderable)
                expected |= ExpectedConvertedTransformResults.HasLocalToWorld;

            if ((input.flags & TransformUsageFlags.WorldSpace) != 0 || !dynamicParent)
                expected |= ExpectedConvertedTransformResults.HasWorldSpaceData;

            // If it actually has non uniform scale data
            if (input.nonUniformScale || ((expected & ExpectedConvertedTransformResults.HasWorldSpaceData) != 0 && nonUniformScaleParent))
                expected |= ExpectedConvertedTransformResults.HasNonUniformScale;

            // if(component == 'PostTransformMatrix') return map.NonUniformScale;
            if (nonUniformScale || (((expected & ExpectedConvertedTransformResults.HasNonUniformScale) != 0) && ((expected & ExpectedConvertedTransformResults.HasLocalTransform) != 0)))                  //  TODO: Review if (input.nonUniformScale && dynamic) or (input.nonUniformScale && renderable)
                expected |= ExpectedConvertedTransformResults.HasPostTransformMatrix | ExpectedConvertedTransformResults.HasNonUniformScale;

            if (!worldSpace && dynamicParent && renderable)
                expected |= ExpectedConvertedTransformResults.HasValidRuntimeParent;

            return expected;
        }

        public static ExpectedConvertedTransformResults CalculateExpectedNoneIntermediate(ConvertGameObjectInput input, ExpectedConvertedTransformResults leafChild)
        {
            if ((leafChild & ExpectedConvertedTransformResults.HasParent) != 0 &&
                (leafChild & ExpectedConvertedTransformResults.HasValidRuntimeParent) != 0)
            {
                // I need to be promoted
                ExpectedConvertedTransformResults expected = new ExpectedConvertedTransformResults();
                expected |= ExpectedConvertedTransformResults.HasParent |
                            ExpectedConvertedTransformResults.HasValidRuntimeParent |
                            ExpectedConvertedTransformResults.HasLocalTransform |
                            ExpectedConvertedTransformResults.HasLocalToWorld;

                if (input.nonUniformScale)
                {
                    expected |= ExpectedConvertedTransformResults.HasPostTransformMatrix | ExpectedConvertedTransformResults.HasNonUniformScale;
                }
                return expected;
            }

            // Handle manual override
            // Parent is Dynamic or Manual
            // Child is Manual or it has a value but not WorldSpace (!None && !WorldSpace)
            if ((input.parentConfig == ConvertGameObjectParent.Dynamic || input.parentConfig == ConvertGameObjectParent.ManualOverride) &&
                (((input.flags & TransformUsageFlags.ManualOverride) != 0) || ((input.flags != TransformUsageFlags.None) && ((input.flags & TransformUsageFlags.WorldSpace) == 0))))
            {
                // I need to be promoted
                ExpectedConvertedTransformResults expected = new ExpectedConvertedTransformResults();
                expected |= ExpectedConvertedTransformResults.HasParent |
                            ExpectedConvertedTransformResults.HasValidRuntimeParent |
                            ExpectedConvertedTransformResults.HasLocalTransform |
                            ExpectedConvertedTransformResults.HasLocalToWorld;

                if (input.nonUniformScale)
                {
                    expected |= ExpectedConvertedTransformResults.HasPostTransformMatrix | ExpectedConvertedTransformResults.HasNonUniformScale;
                }
                return expected;
            }

            return ExpectedConvertedTransformResults.Nothing;
        }

        public static bool Has(ExpectedConvertedTransformResults expectedDescription, ExpectedConvertedTransformResults flag)
        {
            return (expectedDescription & flag) != 0;
        }

        const float k_Tolerance = 0.001f;

        static bool AreEqual(float v1, float v2)
        {
            return math.abs(v1 - v2) <= k_Tolerance;
        }

        static bool AreEqual(in float3 v1, in float3 v2)
        {
            return math.abs(v1.x - v2.x) <= k_Tolerance &&
                   math.abs(v1.y - v2.y) <= k_Tolerance &&
                   math.abs(v1.z - v2.z) <= k_Tolerance;
        }

        static bool AreEqual(in quaternion v1, in quaternion v2)
        {
            return AreEqual(v1.value, v2.value);
        }

        static bool AreEqual(in float4 v1, in float4 v2)
        {
            return math.abs(v1.x - v2.x) <= k_Tolerance &&
                   math.abs(v1.y - v2.y) <= k_Tolerance &&
                   math.abs(v1.w - v2.w) <= k_Tolerance &&
                   math.abs(v1.z - v2.z) <= k_Tolerance;
        }

        static bool AreEqual(in float4x4 v1, in float4x4 v2)
        {
            return AreEqual(v1.c0, v2.c0) &&
                   AreEqual(v1.c1, v2.c1) &&
                   AreEqual(v1.c2, v2.c2) &&
                   AreEqual(v1.c3, v2.c3);
        }

        [TestCaseSource(nameof(Convert2GameObjectTestCases))]
        public void Convert2GameObjectHierarchy(ConvertGameObjectInput input, ExpectedConvertedTransformResults expectedDescription)
        {
            ConvertGameObjectParent parentConfig = input.parentConfig;

            Generate2GameObjectHierarchyScenario(input, out GameObject main, out GameObject root, out TransformUsageFlags mainFlags, out TransformUsageFlags parentFlags);
            if (parentConfig != ConvertGameObjectParent.None)
            {
                AssignTransformUsageBaker.Flags[root] = parentFlags;
            }
            AssignTransformUsageBaker.Flags[main] = mainFlags;
            AssignTransformUsageBaker.AdditionalCount = 0;
            Convert(root, ConversionType.ConvertHierarchy, typeof(AssignTransformUsageBaker));

            var entity = _destinationWorld.GetExistingSystemManaged<BakingSystem>().GetEntity(main);

            var authoring = _entityManager.GetComponentData<TransformAuthoring>(entity);
            var transform = main.transform;

            Entity parentEntity = Entity.Null;
            if (parentConfig != ConvertGameObjectParent.None)
            {
                parentEntity = _destinationWorld.GetExistingSystemManaged<BakingSystem>().GetEntity(root);
            }
            VerifyBakedTransformData(expectedDescription, transform, authoring, entity, parentEntity);
        }

        public static IEnumerable Convert2GameObjectTestCases()
        {
            var parentConfigs = Enum.GetValues(typeof(ConvertGameObjectParent)).Cast<ConvertGameObjectParent>();
            foreach (var parentConfig in parentConfigs)
            {
                uint upperFlagLimit = ((uint)TransformUsageFlags.ManualOverride << 1) - 1;
                for (uint flagsValue = 0; flagsValue < upperFlagLimit; ++flagsValue)
                {
                    var input = new ConvertGameObjectInput
                    {
                        parentConfig = parentConfig,
                        flags = (TransformUsageFlags)flagsValue,
                        nonUniformScale = false
                    };
                    yield return new TestCaseData(input, CalculateExpected(input) ).SetName($"ConvertHierarchy_{input}");

                    input.nonUniformScale = true;
                    yield return new TestCaseData(input, CalculateExpected(input) ).SetName($"ConvertHierarchy_{input}");
                }
            }
        }

        public void Generate2GameObjectHierarchyScenario(ConvertGameObjectInput input, out GameObject main, out GameObject root, out TransformUsageFlags mainFlags, out TransformUsageFlags parentFlags)
        {
            ConvertGameObjectParent parentConfig = input.parentConfig;
            TransformUsageFlags flag = input.flags;
            bool nonUniformScaleSetup = input.nonUniformScale;

            parentFlags = TransformUsageFlags.None;

            main = _objects.CreateGameObject();
            main.transform.localPosition = new Vector3(1, 2, 3);
            main.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);

            // Setup the scaled based on what we expect
            if (nonUniformScaleSetup)
            {
                main.transform.localScale = new Vector3(2f, 4f, 6f);
            }
            else
            {
                main.transform.localScale = new Vector3(2f, 2f, 2f);
            }
            mainFlags = flag;

            if (parentConfig != ConvertGameObjectParent.None)
            {
                // Do the parent part and attach
                var parent = _objects.CreateGameObject();
                parent.transform.localPosition = new Vector3(4, 5, 6);
                parent.transform.localRotation = Quaternion.Euler(30f, 0f, -30f);
                if (parentConfig != ConvertGameObjectParent.RenderableNonUniformScale)
                {
                    parent.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
                }
                else
                {
                    parent.transform.localScale = new Vector3(0.5f, 0.25f, 0.1f);
                }
                main.transform.SetParent(parent.transform, false);
                switch (parentConfig)
                {
                    case ConvertGameObjectParent.RenderableNonUniformScale:
                    case ConvertGameObjectParent.Renderable:
                        parentFlags = TransformUsageFlags.Renderable;
                        break;
                    case ConvertGameObjectParent.Dynamic:
                        parentFlags = TransformUsageFlags.Dynamic;
                        break;
                    case ConvertGameObjectParent.ManualOverride:
                        parentFlags = TransformUsageFlags.ManualOverride;
                        break;
                }
                root = parent;
            }
            else
            {
                root = main;
            }
        }

        [TestCaseSource(nameof(ConvertGameObjectAdditionalTestCases))]
        public void ConvertGameObjectAdditional(ConvertGameObjectInput input, ExpectedConvertedTransformResults expectedDescription)
        {
            ConvertGameObjectParent parentConfig = input.parentConfig;

            GenerateGameObjectAdditionalScenario(input, out GameObject root, out TransformUsageFlags additionalEntitiesFlags, out TransformUsageFlags parentFlags);
            AssignTransformUsageBaker.Flags[root] = parentFlags;
            AssignTransformUsageBaker.AdditionalFlags[root] = additionalEntitiesFlags;
            AssignTransformUsageBaker.AdditionalCount = 1;
            Convert(root, ConversionType.ConvertHierarchy, typeof(AssignTransformUsageBaker));

            var bakingSystem = _destinationWorld.GetExistingSystemManaged<BakingSystem>();
            var parentEntity = bakingSystem.GetEntity(root);

            // Access the additional entity
            var buffer = _entityManager.GetBuffer<AdditionalEntitiesBakingData>(parentEntity);
            Assert.AreEqual(1, buffer.Length);
            var entity = buffer[0].Value;

            var authoring = _entityManager.GetComponentData<TransformAuthoring>(entity);
            var transform = root.transform;

            VerifyBakedTransformData(expectedDescription, transform, authoring, entity, parentEntity, true);
        }

        public static IEnumerable ConvertGameObjectAdditionalTestCases()
        {
            var parentConfigs = Enum.GetValues(typeof(ConvertGameObjectParent)).Cast<ConvertGameObjectParent>();
            foreach (var parentConfig in parentConfigs)
            {
                uint upperFlagLimit = ((uint)TransformUsageFlags.ManualOverride << 1) - 1;
                for (uint flagsValue = 0; flagsValue < upperFlagLimit; ++flagsValue)
                {
                    var input = new ConvertGameObjectInput
                    {
                        parentConfig = parentConfig,
                        flags = (TransformUsageFlags)flagsValue,
                        nonUniformScale = false
                    };
                    yield return new TestCaseData(input, CalculateExpected(input) ).SetName($"ConvertAdditional_{input}");
                }
            }
        }

        public void GenerateGameObjectAdditionalScenario(ConvertGameObjectInput input, out GameObject root, out TransformUsageFlags additionalEntitiesFlags, out TransformUsageFlags parentFlags)
        {
            ConvertGameObjectParent parentConfig = input.parentConfig;
            TransformUsageFlags flag = input.flags;

            additionalEntitiesFlags = flag;

            // Do the parent part and attach
            root = _objects.CreateGameObject();
            root.transform.localPosition = new Vector3(4, 5, 6);
            root.transform.localRotation = Quaternion.Euler(30f, 0f, -30f);
            if (parentConfig != ConvertGameObjectParent.RenderableNonUniformScale)
            {
                root.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            else
            {
                root.transform.localScale = new Vector3(0.5f, 0.25f, 0.1f);
            }

            parentFlags = TransformUsageFlags.None;
            switch (parentConfig)
            {
                case ConvertGameObjectParent.RenderableNonUniformScale:
                case ConvertGameObjectParent.Renderable:
                    parentFlags = TransformUsageFlags.Renderable;
                    break;
                case ConvertGameObjectParent.Dynamic:
                    parentFlags = TransformUsageFlags.Dynamic;
                    break;
                case ConvertGameObjectParent.ManualOverride:
                    parentFlags = TransformUsageFlags.ManualOverride;
                    break;
            }
        }

        [TestCaseSource(nameof(ConvertNoneIntermediateGameObjectTestCases))]
        public void ConvertNoneIntermediateGameObject(ConvertGameObjectInput input, ExpectedConvertedTransformResults expectedDescription, ExpectedConvertedTransformResults expectedIntermediateDescription)
        {
            ConvertGameObjectParent parentConfig = input.parentConfig;
            int intermediateCount = 2;

            var hierarchyArray = GenerateNoneIntermediateGameObjectScenario(input, intermediateCount, out TransformUsageFlags rootFlags, out TransformUsageFlags intermediateFlags, out TransformUsageFlags childFlags);

            AssignTransformUsageBaker.Flags[hierarchyArray[0]] = rootFlags;
            for (int index = 1; index < hierarchyArray.Length - 1; ++index)
            {
                AssignTransformUsageBaker.Flags[hierarchyArray[index]] = intermediateFlags;
            }
            AssignTransformUsageBaker.Flags[hierarchyArray[hierarchyArray.Length - 1]] = childFlags;
            AssignTransformUsageBaker.AdditionalCount = 0;
            Convert(hierarchyArray[0], ConversionType.ConvertHierarchy, typeof(AssignTransformUsageBaker));

            var bakingSystem = _destinationWorld.GetExistingSystemManaged<BakingSystem>();

            // Check the child
            GameObject leafGameObject = hierarchyArray[hierarchyArray.Length - 1];
            var entityLeaf = bakingSystem.GetEntity(leafGameObject);

            var authoringLeaf = _entityManager.GetComponentData<TransformAuthoring>(entityLeaf);
            var transformLeaf = leafGameObject.transform;

            var parentEntity = bakingSystem.GetEntity(hierarchyArray[hierarchyArray.Length - 2]);
            VerifyBakedTransformData(expectedDescription, transformLeaf, authoringLeaf, entityLeaf, parentEntity);

            // Check the intermediate nodes
            for (int index = 0; index < intermediateCount; ++index)
            {
                GameObject intermediateGameObject = hierarchyArray[hierarchyArray.Length - 2 - index];
                var entityIntermediate = bakingSystem.GetEntity(intermediateGameObject);

                var authoringIntermediate = _entityManager.GetComponentData<TransformAuthoring>(entityIntermediate);
                var transformIntermediate = intermediateGameObject.transform;

                var parentEntityIntermediate = bakingSystem.GetEntity(hierarchyArray[hierarchyArray.Length - 3 - index]);
                VerifyBakedTransformData(expectedIntermediateDescription, transformIntermediate, authoringIntermediate, entityIntermediate, parentEntityIntermediate);
            }
        }

        public static IEnumerable ConvertNoneIntermediateGameObjectTestCases()
        {
            var parentConfigs = Enum.GetValues(typeof(ConvertGameObjectParent)).Cast<ConvertGameObjectParent>();
            foreach (var parentConfig in parentConfigs)
            {
                uint upperFlagLimit = ((uint)TransformUsageFlags.ManualOverride << 1) - 1;
                for (uint flagsValue = 0; flagsValue < upperFlagLimit; ++flagsValue)
                {
                    var input = new ConvertGameObjectInput
                    {
                        parentConfig = parentConfig,
                        flags = (TransformUsageFlags)flagsValue,
                        nonUniformScale = false
                    };

                    var leafExpect = CalculateExpected(input);
                    var intermediateExpect = CalculateExpectedNoneIntermediate(input, leafExpect);
                    yield return new TestCaseData(input, leafExpect, intermediateExpect).SetName($"ConvertNoneIntermediate_{input}");

                    input.nonUniformScale = true;
                    leafExpect = CalculateExpected(input);
                    intermediateExpect = CalculateExpectedNoneIntermediate(input, leafExpect);
                    yield return new TestCaseData(input, leafExpect, intermediateExpect).SetName($"ConvertNoneIntermediate_{input}");
                }
            }
        }

        public GameObject[] GenerateNoneIntermediateGameObjectScenario(ConvertGameObjectInput input, int intermediateCount, out TransformUsageFlags rootFlags, out TransformUsageFlags intermediateFlags, out TransformUsageFlags childFlags)
        {
            GameObject[] returnArray = new GameObject[intermediateCount + 2];
            int arrayIndex = returnArray.Length - 1;

            ConvertGameObjectParent parentConfig = input.parentConfig;
            TransformUsageFlags flag = input.flags;
            bool nonUniformScaleSetup = input.nonUniformScale;

            rootFlags = TransformUsageFlags.None;

            var child = _objects.CreateGameObject();
            child.transform.localPosition = new Vector3(1, 2, 3);
            child.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);

            // Setup the scaled based on what we expect
            if (nonUniformScaleSetup)
            {
                child.transform.localScale = new Vector3(2f, 4f, 6f);
            }
            else
            {
                child.transform.localScale = new Vector3(2f, 2f, 2f);
            }
            childFlags = flag;
            var previous = child;
            returnArray[arrayIndex--] = previous;

            // Create Intermediate
            intermediateFlags = TransformUsageFlags.None;
            for (int index = 0; index < intermediateCount; ++index)
            {
                var intermediate = _objects.CreateGameObject();
                intermediate.transform.localPosition = new Vector3(1, 2, 3);
                intermediate.transform.localRotation = Quaternion.Euler(45f, 45f, 45f);

                // Setup the scaled based on what we expect
                if (nonUniformScaleSetup)
                {
                    intermediate.transform.localScale = new Vector3(2f, 4f, 6f);
                }
                else
                {
                    intermediate.transform.localScale = new Vector3(2f, 2f, 2f);
                }
                previous.transform.SetParent(intermediate.transform, false);
                previous = intermediate;
                returnArray[arrayIndex--] = previous;
            }

            // Do the parent part and attach
            var root = _objects.CreateGameObject();
            root.transform.localPosition = new Vector3(4, 5, 6);
            root.transform.localRotation = Quaternion.Euler(30f, 0f, -30f);
            if (parentConfig != ConvertGameObjectParent.RenderableNonUniformScale)
            {
                root.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            }
            else
            {
                root.transform.localScale = new Vector3(0.5f, 0.25f, 0.1f);
            }
            previous.transform.SetParent(root.transform, false);
            switch (parentConfig)
            {
                case ConvertGameObjectParent.RenderableNonUniformScale:
                case ConvertGameObjectParent.Renderable:
                    rootFlags = TransformUsageFlags.Renderable;
                    break;
                case ConvertGameObjectParent.Dynamic:
                    rootFlags = TransformUsageFlags.Dynamic;
                    break;
                case ConvertGameObjectParent.ManualOverride:
                    rootFlags = TransformUsageFlags.ManualOverride;
                    break;
            }
            returnArray[arrayIndex--] = root;

            return returnArray;
        }

        public void VerifyBakedTransformData(ExpectedConvertedTransformResults expectedDescription, Transform transform, TransformAuthoring authoring, Entity entity, Entity parentEntity, bool isAdditionalEntity = false)
        {
            // Check TransformAuthoring values
            float3 localPositionRef;
            quaternion localRotationRef;
            float3 localScaleRef;
            if (!isAdditionalEntity)
            {
                localPositionRef = transform.localPosition;
                localRotationRef = transform.localRotation;
                localScaleRef = transform.localScale;
            }
            else
            {
                localPositionRef = float3.zero;
                localRotationRef = quaternion.identity;
                localScaleRef = new float3(1f,1f,1f);
            }

            Assert.IsTrue(AreEqual((float3) localPositionRef, authoring.LocalPosition));
            Assert.IsTrue(AreEqual((quaternion) localRotationRef, authoring.LocalRotation));
            Assert.IsTrue(AreEqual((float3) localScaleRef, authoring.LocalScale));

            Assert.IsTrue(AreEqual((float3) transform.position, authoring.Position));
            Assert.IsTrue(AreEqual((quaternion) transform.rotation, authoring.Rotation));
            Assert.IsTrue(AreEqual((float4x4) transform.localToWorldMatrix, authoring.LocalToWorld));

            Assert.AreEqual(parentEntity, authoring.AuthoringParent);

            if (Has(expectedDescription, ExpectedConvertedTransformResults.HasValidRuntimeParent))
            {
                Assert.AreEqual(parentEntity, authoring.RuntimeParent);
            }
            else
            {
                Assert.AreEqual(default(Entity), authoring.RuntimeParent);
            }

            // Check Entity Components and Values
            bool expectsLocalToWorld = Has(expectedDescription, ExpectedConvertedTransformResults.HasLocalToWorld);
            Assert.AreEqual(expectsLocalToWorld, _entityManager.HasComponent<LocalToWorld>(entity));
            if (expectsLocalToWorld)
            {
                // Check the values are the expected ones
                var data = _entityManager.GetComponentData<LocalToWorld>(entity);
                Assert.IsTrue(AreEqual(authoring.LocalToWorld, data.Value));
            }

            bool expectsLocalTransform = Has(expectedDescription, ExpectedConvertedTransformResults.HasLocalTransform);
            Assert.AreEqual(expectsLocalTransform, _entityManager.HasComponent<LocalTransform>(entity));
            if (expectsLocalTransform)
            {
                // Check the values are the expected ones
                var data = _entityManager.GetComponentData<LocalTransform>(entity);
                if (Has(expectedDescription, ExpectedConvertedTransformResults.HasWorldSpaceData))
                {
                    Assert.IsTrue(AreEqual(authoring.Position, data.Position));
                    Assert.IsTrue(AreEqual(authoring.Rotation, data.Rotation));

                    if (Has(expectedDescription, ExpectedConvertedTransformResults.HasNonUniformScale))
                    {
                        Assert.IsTrue(AreEqual(1f, data.Scale));
                    }
                    else
                    {
                        Assert.IsTrue(AreEqual(((float3)transform.lossyScale).x, data.Scale));
                    }
                }
                else
                {
                    Assert.IsTrue(AreEqual( authoring.LocalPosition, data.Position));
                    Assert.IsTrue(AreEqual(authoring.LocalRotation, data.Rotation));
                    if (Has(expectedDescription, ExpectedConvertedTransformResults.HasNonUniformScale))
                    {
                        Assert.IsTrue(AreEqual(1f, data.Scale));
                    }
                    else
                    {
                        Assert.IsTrue(AreEqual(localScaleRef.x, data.Scale));
                    }
                }
            }

            bool expectsPostTransformMatrix = Has(expectedDescription, ExpectedConvertedTransformResults.HasPostTransformMatrix);
            Assert.AreEqual(expectsPostTransformMatrix, _entityManager.HasComponent<PostTransformMatrix>(entity));
            if (expectsPostTransformMatrix)
            {
                // Check the values are the expected ones
                var data = _entityManager.GetComponentData<PostTransformMatrix>(entity);
                if (!Has(expectedDescription, ExpectedConvertedTransformResults.HasNonUniformScale))
                {
                    Assert.IsTrue(AreEqual(float4x4.identity, data.Value));
                }
                else
                {
                    if (Has(expectedDescription, ExpectedConvertedTransformResults.HasWorldSpaceData))
                    {
                        Assert.IsTrue(AreEqual(float4x4.Scale(transform.lossyScale), data.Value));
                    }
                    else
                    {
                        Assert.IsTrue(AreEqual(float4x4.Scale(localScaleRef), data.Value));
                    }
                }
            }

            bool expectsParent = Has(expectedDescription, ExpectedConvertedTransformResults.HasParent);
            Assert.AreEqual(expectsParent, _entityManager.HasComponent<Parent>(entity));
            if (expectsParent)
            {
                // Check the values are the expected ones
                var data = _entityManager.GetComponentData<Parent>(entity);
                if (Has(expectedDescription, ExpectedConvertedTransformResults.HasValidRuntimeParent))
                {
                    Assert.AreEqual(authoring.RuntimeParent, data.Value);
                }
                else
                {
                    Assert.AreEqual(default(Entity), data.Value);
                }
            }
        }
    }
}
