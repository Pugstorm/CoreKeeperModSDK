using System.Linq;
using NUnit.Framework;
using Unity.Entities.Tests;
using Unity.Scenes.Editor.Tests;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities.Hybrid.Tests.Baking
{
    class BindingRegistryTests : BakingSystemFixtureBase
    {
        private BakingSystem m_BakingSystem;
        private TestLiveConversionSettings m_Settings;

        [SetUp]
        public override void Setup()
        {
            m_Settings.Setup(true);
            base.Setup();

            m_BakingSystem = World.GetOrCreateSystemManaged<BakingSystem>();
            m_BakingSystem.PrepareForBaking(MakeDefaultSettings(), default);

            m_Manager = World.EntityManager;
        }

        [TearDown]
        public override void TearDown()
        {
            base.TearDown();
            m_BakingSystem = null;
            m_Settings.TearDown();
        }

        [Test]
        public void ManualConversion_BindingRegistry()
        {
            var go = CreateGameObject();
            go.AddComponent<BindingRegistryManualTestAuthoring>();

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            var entity = m_BakingSystem.GetEntity(go);

            var component = m_Manager.GetComponentData<BindingRegistryManualTestComponent>(entity);
            Assert.AreEqual(true, component.BindBool);
            Assert.AreEqual(5, component.BindInt);
            Assert.AreEqual(10.0f, component.BindFloat);

            Assert.IsTrue(BindingRegistry.HasBindings(typeof(BindingRegistryManualTestComponent)));
        }

        private static TestCaseData[] s_ExpectedBindings =
        {
            new TestCaseData(typeof(BindingRegistryBoolComponentAuthoring), typeof(BindingRegistryBoolComponent),
                new []
                {
                    (nameof(BindingRegistryBoolComponent.Bool1), nameof(BindingRegistryBoolComponentAuthoring.Bool1))
                }).SetName("Binding boolean authoring field to boolean component field"),
            new TestCaseData(typeof(BindingRegistryBoolComponentAuthoring), typeof(BindingRegistryBoolComponent),
                new []
                {
                    ($"{nameof(BindingRegistryBoolComponent.Bool2)}.x", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool2)}.x"),
                    ($"{nameof(BindingRegistryBoolComponent.Bool2)}.y", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool2)}.y")
                }).SetName("Binding bool2 authoring field to bool2 component field"),
            new TestCaseData(typeof(BindingRegistryBoolComponentAuthoring), typeof(BindingRegistryBoolComponent),
                new []
                {
                    ($"{nameof(BindingRegistryBoolComponent.Bool3)}.x", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool3)}.x"),
                    ($"{nameof(BindingRegistryBoolComponent.Bool3)}.y", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool3)}.y"),
                    ($"{nameof(BindingRegistryBoolComponent.Bool3)}.z", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool3)}.z")
                }).SetName("Binding bool3 authoring field to bool3 component field"),
            new TestCaseData(typeof(BindingRegistryBoolComponentAuthoring), typeof(BindingRegistryBoolComponent),
                new []
                {
                    ($"{nameof(BindingRegistryBoolComponent.Bool4)}.x", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool4)}.x"),
                    ($"{nameof(BindingRegistryBoolComponent.Bool4)}.y", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool4)}.y"),
                    ($"{nameof(BindingRegistryBoolComponent.Bool4)}.z", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool4)}.z"),
                    ($"{nameof(BindingRegistryBoolComponent.Bool4)}.w", $"{nameof(BindingRegistryBoolComponentAuthoring.Bool4)}.w")
                }).SetName("Binding bool4 authoring field to bool4 component field"),
            new TestCaseData(typeof(BindingRegistryIntComponentAuthoring), typeof(BindingRegistryIntComponent),
                new []
                {
                    (nameof(BindingRegistryIntComponent.Int1), nameof(BindingRegistryIntComponentAuthoring.Int1))
                }).SetName("Binding int authoring field to int component field"),
            new TestCaseData(typeof(BindingRegistryIntComponentAuthoring), typeof(BindingRegistryIntComponent),
                new []
                {
                    ($"{nameof(BindingRegistryIntComponent.Int2)}.x", $"{nameof(BindingRegistryIntComponentAuthoring.Int2)}.x"),
                    ($"{nameof(BindingRegistryIntComponent.Int2)}.y", $"{nameof(BindingRegistryIntComponentAuthoring.Int2)}.y")
                }).SetName("Binding int2 authoring field to int2 component field"),
            new TestCaseData(typeof(BindingRegistryIntComponentAuthoring), typeof(BindingRegistryIntComponent),
                new []
                {
                    ($"{nameof(BindingRegistryIntComponent.Int3)}.x", $"{nameof(BindingRegistryIntComponentAuthoring.Int3)}.x"),
                    ($"{nameof(BindingRegistryIntComponent.Int3)}.y", $"{nameof(BindingRegistryIntComponentAuthoring.Int3)}.y"),
                    ($"{nameof(BindingRegistryIntComponent.Int3)}.z", $"{nameof(BindingRegistryIntComponentAuthoring.Int3)}.z")
                }).SetName("Binding int3 authoring field to int3 component field"),
            new TestCaseData(typeof(BindingRegistryIntComponentAuthoring), typeof(BindingRegistryIntComponent),
                new []
                {
                    ($"{nameof(BindingRegistryIntComponent.Int4)}.x", $"{nameof(BindingRegistryIntComponentAuthoring.Int4)}.x"),
                    ($"{nameof(BindingRegistryIntComponent.Int4)}.y", $"{nameof(BindingRegistryIntComponentAuthoring.Int4)}.y"),
                    ($"{nameof(BindingRegistryIntComponent.Int4)}.z", $"{nameof(BindingRegistryIntComponentAuthoring.Int4)}.z"),
                    ($"{nameof(BindingRegistryIntComponent.Int4)}.w", $"{nameof(BindingRegistryIntComponentAuthoring.Int4)}.w")
                }).SetName("Binding int4 authoring field to int4 component field"),
            new TestCaseData(typeof(BindingRegistryFloatComponentAuthoring), typeof(BindingRegistryFloatComponent),
                new []
                {
                    (nameof(BindingRegistryFloatComponent.Float1), nameof(BindingRegistryFloatComponentAuthoring.Float1))
                }).SetName("Binding float authoring field to float component field"),
            new TestCaseData(typeof(BindingRegistryFloatComponentAuthoring), typeof(BindingRegistryFloatComponent),
                new []
                {
                    ($"{nameof(BindingRegistryFloatComponent.Float2)}.x", $"{nameof(BindingRegistryFloatComponentAuthoring.Float2)}.x"),
                    ($"{nameof(BindingRegistryFloatComponent.Float2)}.y", $"{nameof(BindingRegistryFloatComponentAuthoring.Float2)}.y")
                }).SetName("Binding float2 authoring field to float2 component field"),
            new TestCaseData(typeof(BindingRegistryFloatComponentAuthoring), typeof(BindingRegistryFloatComponent),
                new []
                {
                    ($"{nameof(BindingRegistryFloatComponent.Float3)}.x", $"{nameof(BindingRegistryFloatComponentAuthoring.Float3)}.x"),
                    ($"{nameof(BindingRegistryFloatComponent.Float3)}.y", $"{nameof(BindingRegistryFloatComponentAuthoring.Float3)}.y"),
                    ($"{nameof(BindingRegistryFloatComponent.Float3)}.z", $"{nameof(BindingRegistryFloatComponentAuthoring.Float3)}.z")
                }).SetName("Binding float3 authoring field to float3 component field"),
            new TestCaseData(typeof(BindingRegistryFloatComponentAuthoring), typeof(BindingRegistryFloatComponent),
                new []
                {
                    ($"{nameof(BindingRegistryFloatComponent.Float4)}.x", $"{nameof(BindingRegistryFloatComponentAuthoring.Float4)}.x"),
                    ($"{nameof(BindingRegistryFloatComponent.Float4)}.y", $"{nameof(BindingRegistryFloatComponentAuthoring.Float4)}.y"),
                    ($"{nameof(BindingRegistryFloatComponent.Float4)}.z", $"{nameof(BindingRegistryFloatComponentAuthoring.Float4)}.z"),
                    ($"{nameof(BindingRegistryFloatComponent.Float4)}.w", $"{nameof(BindingRegistryFloatComponentAuthoring.Float4)}.w")
                }).SetName("Binding float4 authoring field to float4 component field"),
            new TestCaseData(typeof(BindingRegistryColorAuthoring), typeof(BindingRegistryColorComponent),
                new []
                {
                    ($"{nameof(BindingRegistryColorComponent.BindColor)}.x", $"{nameof(BindingRegistryColorAuthoring.Color)}.r"),
                    ($"{nameof(BindingRegistryColorComponent.BindColor)}.y", $"{nameof(BindingRegistryColorAuthoring.Color)}.g"),
                    ($"{nameof(BindingRegistryColorComponent.BindColor)}.z", $"{nameof(BindingRegistryColorAuthoring.Color)}.b"),
                    ($"{nameof(BindingRegistryColorComponent.BindColor)}.w", $"{nameof(BindingRegistryColorAuthoring.Color)}.a")
                }).SetName("Binding Color to float4"),
            new TestCaseData(typeof(BindingRegistryVectorAuthoring), typeof(BindingRegistryVectorComponent),
                new []
                {
                    ($"{nameof(BindingRegistryVectorComponent.BindFloat4)}.x", $"{nameof(BindingRegistryVectorAuthoring.Vector4)}.x"),
                    ($"{nameof(BindingRegistryVectorComponent.BindFloat4)}.y", $"{nameof(BindingRegistryVectorAuthoring.Vector4)}.y"),
                    ($"{nameof(BindingRegistryVectorComponent.BindFloat4)}.z", $"{nameof(BindingRegistryVectorAuthoring.Vector4)}.z"),
                    ($"{nameof(BindingRegistryVectorComponent.BindFloat4)}.w", $"{nameof(BindingRegistryVectorAuthoring.Vector4)}.w")
                }).SetName("Binding Vector4 to float4"),
            new TestCaseData(typeof(BindingRegistrySeparateFieldsAuthoring), typeof(BindingRegistrySeparateFieldsComponent),
                new []
                {
                    ($"{nameof(BindingRegistrySeparateFieldsComponent.BindFloat2)}.x", nameof(BindingRegistrySeparateFieldsAuthoring.FloatField1)),
                    ($"{nameof(BindingRegistrySeparateFieldsComponent.BindFloat2)}.y", nameof(BindingRegistrySeparateFieldsAuthoring.FloatField2)),

                }).SetName("Binding separate authoring fields to float2"),
            new TestCaseData(typeof(BindingRegistryNestedFieldsAuthoring), typeof(BindingRegistryNestedFieldsComponent),
                new []
                {
                    ($"{nameof(BindingRegistryNestedFieldsComponent.BindFloat4)}.x", $"{nameof(BindingRegistryNestedFieldsAuthoring.NestedData)}.{nameof(BindingRegistryNestedFieldsAuthoring.NestedStruct.Float)}"),
                    ($"{nameof(BindingRegistryNestedFieldsComponent.BindFloat4)}.y", $"{nameof(BindingRegistryNestedFieldsAuthoring.NestedData)}.{nameof(BindingRegistryNestedFieldsAuthoring.NestedStruct.Float3)}.x"),
                    ($"{nameof(BindingRegistryNestedFieldsComponent.BindFloat4)}.z", $"{nameof(BindingRegistryNestedFieldsAuthoring.NestedData)}.{nameof(BindingRegistryNestedFieldsAuthoring.NestedStruct.Float3)}.y"),
                    ($"{nameof(BindingRegistryNestedFieldsComponent.BindFloat4)}.w", $"{nameof(BindingRegistryNestedFieldsAuthoring.NestedData)}.{nameof(BindingRegistryNestedFieldsAuthoring.NestedStruct.Float3)}.z")
                }).SetName("Binding nested authoring fields to float4")
        };

        [TestCaseSource(nameof(s_ExpectedBindings))]
        public void TestBindings_BindingRegistry(System.Type authoringType, System.Type runtimeType, (string Key, string Value)[] expectedBindings)
        {
            var go = CreateGameObject();
            go.AddComponent(authoringType);

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            var entity = m_BakingSystem.GetEntity(go);

            Assert.IsTrue(m_Manager.HasComponent(entity, runtimeType), $"{runtimeType.Name} was not added to the entity during conversion");

            foreach (var kvp in expectedBindings)
            {
                var binding = BindingRegistry.GetBinding(runtimeType, kvp.Key);
                Assert.NotNull(binding, $"The field {kvp.Key} could not be found in the BindingRegistry.");
                Assert.AreEqual(authoringType, binding.Item1);
                Assert.AreEqual(kvp.Value, binding.Item2);
            }
        }

        struct BindingRegistryBufferElement : IBufferElementData
        {
            public int Int1;
        }

        [Test]
        public void GetBinding_NotAssignableFromTypeThrows_BindingRegistry()
        {
            var go = CreateGameObject();
            go.AddComponent<BindingRegistrySeparateFieldsAuthoring>();

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            var entity = m_BakingSystem.GetEntity(go);

            Assert.IsTrue(m_Manager.HasComponent<BindingRegistrySeparateFieldsComponent>(entity), "BindingRegistryFieldTestComponent was not added to the entity during conversion");

            Assert.Throws<UnityEngine.Assertions.AssertionException>( () =>BindingRegistry.GetBinding(typeof(BindingRegistryBufferElement), nameof(BindingRegistryBufferElement.Int1)));
        }

        [Test]
        public void GetBinding_FieldNotFoundReturnsNull_BindingRegistry()
        {
            var go = CreateGameObject();
            go.AddComponent<BindingRegistrySeparateFieldsAuthoring>();

            BakingUtility.BakeGameObjects(World, new[] {go}, m_BakingSystem.BakingSettings);
            var entity = m_BakingSystem.GetEntity(go);

            Assert.IsTrue(m_Manager.HasComponent<BindingRegistrySeparateFieldsComponent>(entity), "BindingRegistryFieldTestComponent was not added to the entity during conversion");

            var floatXBinding = BindingRegistry.GetBinding(typeof(BindingRegistrySeparateFieldsComponent), "BindFloat.x");
            Assert.Null(floatXBinding.Item1);

            Assert.AreEqual("", floatXBinding.Item2);
        }

        [Test]
        public void GetReverseBindings_BindingRegistry()
        {
            // Check manual binding
            var runtimeBindings = BindingRegistry.GetReverseBindings(typeof(BindingRegistryManualTestAuthoring));
            Assertions.Assert.AreEqual(runtimeBindings.Count, 3);
            var data = runtimeBindings.Where(x => x.AuthoringFieldName == "FloatField").ToArray();
            Assert.AreEqual( 1, data.Length);
            Assert.AreEqual(0, data[0].FieldProperties.FieldOffset);
            Assert.AreEqual(typeof(float), data[0].FieldProperties.FieldType);

            // Check generic authoring component binding with multiple runtime fields
            runtimeBindings = BindingRegistry.GetReverseBindings(typeof(BindingRegistryIntComponentAuthoring));
            Assertions.Assert.AreEqual(runtimeBindings.Count, 10);
            data = runtimeBindings.Where(x => x.AuthoringFieldName == "Int2.x").ToArray();
            Assert.AreEqual( 1, data.Length);
            Assert.AreEqual(4, data[0].FieldProperties.FieldOffset);
            Assert.AreEqual(typeof(int), data[0].FieldProperties.FieldType);

            runtimeBindings = BindingRegistry.GetReverseBindings(typeof(BindingRegistryBoolComponentAuthoring));
            Assertions.Assert.AreEqual(10, runtimeBindings.Count);
            data = runtimeBindings.Where(x => x.AuthoringFieldName == "Bool2.x").ToArray();
            Assert.AreEqual(1, data.Length);
            Assert.AreEqual(1, data[0].FieldProperties.FieldOffset);
            Assert.AreEqual(typeof(bool), data[0].FieldProperties.FieldType);

            runtimeBindings = BindingRegistry.GetReverseBindings(typeof(BindingRegistryFloatComponentAuthoring));
            Assertions.Assert.AreEqual(10, runtimeBindings.Count);
            data = runtimeBindings.Where(x => x.AuthoringFieldName == "Float2.x").ToArray();
            Assert.AreEqual(1, data.Length);
            Assert.AreEqual(4, data[0].FieldProperties.FieldOffset);
            Assert.AreEqual(typeof(float), data[0].FieldProperties.FieldType);
        }
    }
}
