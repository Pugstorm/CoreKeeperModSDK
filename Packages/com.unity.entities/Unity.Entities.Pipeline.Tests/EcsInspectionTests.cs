using System;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Pipeline.Tests
{
    public struct CliTestNested { public int Value; public int Unchanged; }
    public struct CliTestSerialization : IComponentData
    {
        public CliTestNested Nested;
        public long Signed;
        public ulong Unsigned;
        public FixedString64Bytes Text;
        public Entity Target;
        public int Computed => throw new InvalidOperationException("Inspection must not execute properties.");
    }
    public struct CliTestUnsupported : IComponentData { public int Value; public IntPtr Pointer; }

    // These are EditMode integration tests using real, isolated Worlds, not mocked EntityManagers.
    // Commands are synchronous, so NUnit [Test] is sufficient; no coroutine/Play Mode is needed.
    public class EcsInspectionTests
    {
        World m_World;
        EntityManager Manager => m_World.EntityManager;

        [SetUp]
        public void SetUp() => m_World = new World("EcsInspectionTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() { if (m_World != null && m_World.IsCreated) m_World.Dispose(); }

        static string Ref(Entity entity) => $"{entity.Index}:{entity.Version}";
        JObject Read(EcsReadArgs request) => JObject.FromObject(EcsCommands.Read(m_World.Name, request));
        JObject Query(EcsQueryArgs query) => JObject.FromObject(EcsCommands.Query(m_World.Name, query));
        EcsWriteResponse Write(params EcsOperation[] operations) => EcsWriteCommands.Write(m_World.Name,
            new EcsWriteArgs { Operations = operations });

        [Test]
        public void WorldDiscoveryIncludesCreatedWorldAndReadsOnlyTheNamedWorld()
        {
            using var other = new World(m_World.Name + "-other");
            var first = Manager.CreateEntity(typeof(CliTestValue));
            var second = other.EntityManager.CreateEntity(typeof(CliTestValue));
            Manager.SetComponentData(first, new CliTestValue { Value = 10 });
            other.EntityManager.SetComponentData(second, new CliTestValue { Value = 20 });
            var worlds = JObject.FromObject(EcsCommands.Worlds())["worlds"].Select(w => (string)w["name"]).ToArray();
            CollectionAssert.Contains(worlds, m_World.Name);
            CollectionAssert.Contains(worlds, other.Name);
            var request = new EcsReadArgs { Entities = new[] { Ref(second) }, Components = new[] { new EcsSelection { Type = "CliTestValue" } } };
            var result = JObject.FromObject(EcsCommands.Read(other.Name, request));
            Assert.AreEqual(20, (int)result["entities"][0]["components"][0]["value"]["Value"]);
            Assert.AreEqual(10, Manager.GetComponentData<CliTestValue>(first).Value);
            Assert.Throws<ArgumentException>(() => EcsCommands.Read(m_World.Name + "-missing", request));
        }

        [Test]
        public void InventoryReadDoesNotSerializeValuesAndIncludesDisabledTags()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue), typeof(CliTestEnabled), typeof(CliTestBuffer));
            Manager.SetComponentEnabled<CliTestEnabled>(entity, false);
            var result = Read(new EcsReadArgs { Entities = new[] { Ref(entity) } });
            var row = result["entities"][0];
            var tag = row["components"].Single(c => (string)c["type"] == "CliTestEnabled");
            CollectionAssert.AreEquivalent(new[] { "type", "enabled" }, ((JObject)tag).Properties().Select(p => p.Name));
            Assert.IsFalse((bool)tag["enabled"]);
            Assert.IsTrue((bool)result["complete"]);
            Assert.IsNull(row["complete"]);
            Assert.IsNull(row["status"]);
            Assert.IsTrue(row["components"].All(c => c["value"] == null && c["elements"] == null));
        }

        [Test]
        public void BatchReadDistinguishesMissingEntityMissingComponentAndEmptyBuffer()
        {
            var emptyBuffer = Manager.CreateEntity(typeof(CliTestBuffer));
            var missingBuffer = Manager.CreateEntity(typeof(CliTestValue));
            var stale = new Entity { Index = emptyBuffer.Index, Version = emptyBuffer.Version + 1 };
            var result = Read(new EcsReadArgs { Entities = new[] { Ref(emptyBuffer), Ref(missingBuffer), Ref(stale) },
                Components = new[] { new EcsSelection { Type = "CliTestBuffer" } } });
            Assert.IsFalse((bool)result["complete"]);
            Assert.AreEqual(0, (int)result["entities"][0]["components"][0]["length"]);
            Assert.IsNull(result["entities"][0]["components"][0]["error"]);
            Assert.AreEqual("missingComponent", (string)result["entities"][1]["components"][0]["error"]);
            Assert.AreEqual("missingEntity", (string)result["entities"][2]["error"]);
        }

        [Test]
        public void NestedWritesPreserveOtherFieldsAndRoundTripLargeIntegersAndFixedStrings()
        {
            var entity = Manager.CreateEntity(typeof(CliTestSerialization));
            Manager.SetComponentData(entity, new CliTestSerialization { Nested = new CliTestNested { Unchanged = 17 } });
            var result = Write(new EcsOperation { Op = "set", Entity = Ref(entity), Type = "CliTestSerialization", Fields = new JObject
            {
                ["Nested.Value"] = 42, ["Signed"] = long.MinValue.ToString(CultureInfo.InvariantCulture),
                ["Unsigned"] = ulong.MaxValue.ToString(CultureInfo.InvariantCulture), ["Text"] = "hello", ["Target"] = "0:0"
            } });
            Assert.IsTrue(result.Success, result.Error);
            var data = Manager.GetComponentData<CliTestSerialization>(entity);
            Assert.AreEqual(17, data.Nested.Unchanged);
            Assert.AreEqual(42, data.Nested.Value);
            Assert.AreEqual(long.MinValue, data.Signed);
            Assert.AreEqual(ulong.MaxValue, data.Unsigned);
            Assert.AreEqual("hello", data.Text.ToString());
            var read = Read(new EcsReadArgs { Entities = new[] { Ref(entity) }, AllValues = true });
            var value = read["entities"][0]["components"].Single(c => (string)c["type"] == "CliTestSerialization")["value"];
            Assert.AreEqual(JTokenType.String, value["Unsigned"].Type);
            Assert.AreEqual(ulong.MaxValue.ToString(CultureInfo.InvariantCulture), (string)value["Unsigned"]);
            Assert.AreEqual("0:0", (string)value["Target"]);
            Assert.IsNull(value["Computed"]);
            Assert.IsTrue((bool)read["complete"]);
        }

        [Test]
        public void UnsupportedFieldsRetainSupportedDataAndMarkTheReadIncomplete()
        {
            var entity = Manager.CreateEntity(typeof(CliTestUnsupported));
            Manager.SetComponentData(entity, new CliTestUnsupported { Value = 5 });
            var result = Read(new EcsReadArgs { Entities = new[] { Ref(entity) }, AllValues = true });
            var component = result["entities"][0]["components"].Single(c => (string)c["type"] == "CliTestUnsupported");
            Assert.IsNull(component["error"]);
            Assert.AreEqual(5, (int)component["value"]["Value"]);
            Assert.AreEqual("unsupported", (string)component["value"]["Pointer"]["error"]);
            Assert.IsFalse((bool)result["complete"]);
            var query = Query(new EcsQueryArgs { All = new JToken[] { "CliTestUnsupported" },
                Select = new[] { new EcsSelection { Type = "CliTestUnsupported" } } });
            Assert.IsFalse((bool)query["complete"]);
        }

        [Test]
        public void ValueReadsUseShortNamesAndOnlyMarkChunkStorage()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue), typeof(CliTestChunk));
            Manager.SetComponentData(entity, new CliTestChunk { Value = 3 });
            Manager.AddChunkComponentData<CliTestChunk>(entity);
            Manager.SetChunkComponentData(Manager.GetChunk(entity), new CliTestChunk { Value = 7 });
            var selections = new[] { new EcsSelection { Type = typeof(CliTestChunk).AssemblyQualifiedName },
                new EcsSelection { Type = "CliTestChunk", Storage = "chunk" } };
            var read = Read(new EcsReadArgs { Entities = new[] { Ref(entity) }, Components = selections });
            var query = Query(new EcsQueryArgs { All = new JToken[] { "CliTestValue" }, Select = selections });
            foreach (var result in new[] { read, query })
            {
                Assert.IsTrue((bool)result["complete"]);
                var row = result["entities"][0];
                CollectionAssert.AreEquivalent(new[] { "entity", "name", "components" }, ((JObject)row).Properties().Select(p => p.Name));
                var component = (JObject)row["components"][0];
                CollectionAssert.AreEquivalent(new[] { "type", "value" }, component.Properties().Select(p => p.Name));
                Assert.AreEqual("CliTestChunk", (string)component["type"]);
                Assert.AreEqual(3, (int)component["value"]["Value"]);
                var chunk = (JObject)row["components"][1];
                CollectionAssert.AreEquivalent(new[] { "type", "storage", "value" }, chunk.Properties().Select(p => p.Name));
                Assert.AreEqual("CliTestChunk", (string)chunk["type"]);
                Assert.AreEqual("chunk", (string)chunk["storage"]);
                Assert.AreEqual(7, (int)chunk["value"]["Value"]);
            }
            var metadata = JObject.FromObject(EcsCommands.Types(type: "CliTestEnabled"))["component"];
            Assert.IsTrue((bool)metadata["enableable"]);
            Assert.AreEqual("tag", (string)metadata["category"]);
        }

        [TestCase("eq", 1)]
        [TestCase("ne", 2)]
        [TestCase("lt", 1)]
        [TestCase("lte", 2)]
        [TestCase("gt", 1)]
        [TestCase("gte", 2)]
        public void ValueComparisonsExcludeEntitiesWithoutTheComponent(string op, int expected)
        {
            for (var i = 0; i < 3; i++)
            {
                var entity = Manager.CreateEntity(typeof(CliTestValue));
                Manager.SetComponentData(entity, new CliTestValue { Value = i });
            }
            Manager.CreateEntity(typeof(CliTestEnabled));
            var result = Query(new EcsQueryArgs { Where = new[] { new EcsPredicate { Type = "CliTestValue", Field = "Value", Op = op, Value = 1 } } });
            Assert.AreEqual(expected, (int)result["total"]);
        }

        [Test]
        public void OverlappingOrDescriptionsAreDeduplicatedBeforePagination()
        {
            for (var i = 0; i < 4; i++) Manager.CreateEntity(typeof(CliTestValue), typeof(CliTestEnabled));
            var query = new EcsQueryArgs { Descriptions = new[]
            {
                new EcsDescription { All = new JToken[] { "CliTestValue" } },
                new EcsDescription { All = new JToken[] { "CliTestEnabled" } }
            }, Limit = 2 };
            var first = Query(query);
            query.Offset = 2;
            var second = Query(query);
            Assert.AreEqual(4, (int)first["total"]);
            Assert.AreEqual(4, (int)second["total"]);
            Assert.IsTrue((bool)first["hasMore"]);
            Assert.IsFalse((bool)second["hasMore"]);
            Assert.AreEqual(4, first["entities"].Concat(second["entities"]).Select(e => (string)e["entity"]).Distinct().Count());
        }

        [Test]
        public void InvalidFieldPathsAndIncompleteSharedFiltersFailEvenWithoutMatches()
        {
            Assert.Throws<ArgumentException>(() => Query(new EcsQueryArgs { All = new JToken[] { "CliTestValue" },
                Where = new[] { new EcsPredicate { Type = "CliTestValue", Field = "Typo", Op = "eq", Value = 1 } } }));
            Assert.Throws<ArgumentException>(() => Query(new EcsQueryArgs { All = new JToken[] { "CliTestGroup" },
                Shared = new[] { new EcsSharedFilter { Type = "CliTestGroup", Value = new JObject() } } }));
            Assert.Throws<ArgumentException>(() => Query(new EcsQueryArgs { Descriptions = new[]
            {
                new EcsDescription { All = new JToken[] { "CliTestEnabled" }, Options = new[] { "IgnoreComponentEnabledState" } },
                new EcsDescription { All = new JToken[] { "CliTestValue" } }
            } }));
        }

        [TestCase("bufferSet", 2, 0)]
        [TestCase("bufferInsert", 3, 0)]
        [TestCase("bufferRemove", 1, 2)]
        [TestCase("bufferResize", 0, -1)]
        public void InvalidBufferEditsLeaveContentsUnchanged(string op, int index, int size)
        {
            var entity = Manager.CreateEntity(typeof(CliTestBuffer));
            Manager.GetBuffer<CliTestBuffer>(entity).Add(new CliTestBuffer { Value = 11 });
            Manager.GetBuffer<CliTestBuffer>(entity).Add(new CliTestBuffer { Value = 22 });
            var result = Write(new EcsOperation { Op = op, Entity = Ref(entity), Type = "CliTestBuffer", Index = index, Count = size, Length = size,
                Fields = new JObject { ["Value"] = 99 }, Elements = new JArray(new JObject { ["Value"] = 99 }) });
            Assert.IsFalse(result.Success);
            var actual = Manager.GetBuffer<CliTestBuffer>(entity, true);
            Assert.AreEqual(2, actual.Length);
            Assert.AreEqual(11, actual[0].Value);
            Assert.AreEqual(22, actual[1].Value);
        }

        [Test]
        public void FailedNumericConversionDoesNotWriteOtherFields()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue));
            Manager.SetComponentData(entity, new CliTestValue { Value = 7 });
            var result = Write(new EcsOperation { Op = "set", Entity = Ref(entity), Type = "CliTestValue",
                Fields = new JObject { ["Target"] = "999:1", ["Value"] = "2147483648" } });
            Assert.IsFalse(result.Success);
            var actual = Manager.GetComponentData<CliTestValue>(entity);
            Assert.AreEqual(7, actual.Value);
            Assert.AreEqual(Entity.Null, actual.Target);
        }

        [Test]
        public void EntityEnabledAffectsNativeQueryMembershipButNotDirectReads()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue));
            Assert.IsTrue(Write(new EcsOperation { Op = "entityEnabled", Entity = Ref(entity), Value = false }).Success);
            Assert.IsTrue(Manager.HasComponent<Disabled>(entity));
            Assert.AreEqual(0, (int)Query(new EcsQueryArgs { All = new JToken[] { "CliTestValue" } })["total"]);
            Assert.IsTrue((bool)Read(new EcsReadArgs { Entities = new[] { Ref(entity) } })["complete"]);
            Assert.IsTrue(Write(new EcsOperation { Op = "entityEnabled", Entity = Ref(entity), Value = true },
                new EcsOperation { Op = "name", Entity = Ref(entity), Name = "Renamed" }).Success);
            var result = Query(new EcsQueryArgs { All = new JToken[] { "CliTestValue" } });
            Assert.AreEqual(1, (int)result["total"]);
            Assert.AreEqual("Renamed", (string)result["entities"][0]["name"]);
        }
    }
}
