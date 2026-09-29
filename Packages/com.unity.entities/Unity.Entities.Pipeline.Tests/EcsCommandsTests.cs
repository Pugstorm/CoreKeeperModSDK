using System;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using Unity.Collections;

namespace Unity.Entities.Pipeline.Tests
{
    public struct CliTestValue : IComponentData { public int Value; public Entity Target; }
    public struct CliTestEnabled : IComponentData, IEnableableComponent { }
    public struct CliTestBuffer : IBufferElementData, IEnableableComponent { public int Value; public Entity Target; }
    public struct CliTestGroup : ISharedComponentData { public int Value; }
    public struct CliTestGroup2 : ISharedComponentData { public int Value; }
    public struct CliTestChunk : IComponentData { public int Value; }
    public sealed class CliTestManaged : IComponentData { public int Value; }
    public struct CliTestCleanup : ICleanupComponentData { public int Value; }
    [WriteGroup(typeof(CliTestValue))]
    public struct CliTestWriter : IComponentData { }

    [DisableAutoCreation]
    public partial class CliTestAdvanceVersionSystem : SystemBase
    {
        protected override void OnUpdate() { }
    }

    public class EcsCommandsTests
    {
        World m_World;
        EntityManager Manager => m_World.EntityManager;

        [SetUp]
        public void SetUp() => m_World = new World("EcsPipelineTests-" + Guid.NewGuid().ToString("N"));

        [TearDown]
        public void TearDown() { if (m_World.IsCreated) m_World.Dispose(); }

        static string Ref(Entity entity) => $"{entity.Index}:{entity.Version}";
        JObject Query(EcsQueryArgs args) => JObject.FromObject(EcsCommands.Query(m_World.Name, args));
        JObject Read(Entity entity, params EcsSelection[] components) => JObject.FromObject(EcsCommands.Read(m_World.Name,
            new EcsReadArgs { Entities = new[] { Ref(entity) }, Components = components }));
        EcsWriteResponse Write(params EcsOperation[] operations) => EcsWriteCommands.Write(m_World.Name, new EcsWriteArgs { Operations = operations });

        [Test]
        public void NativeEnableableSelectionAndOrDescriptionsAgree()
        {
            var enabled = Manager.CreateEntity(typeof(CliTestValue), typeof(CliTestEnabled));
            var disabled = Manager.CreateEntity(typeof(CliTestValue), typeof(CliTestEnabled));
            Manager.SetComponentEnabled<CliTestEnabled>(disabled, false);
            var absent = Manager.CreateEntity(typeof(CliTestValue));
            foreach (var kind in new[] { "all", "any", "none", "disabled", "absent", "present" })
            {
                var args = JsonConvert.DeserializeObject<EcsQueryArgs>("{\"" + kind + "\":[\"CliTestEnabled\"]}");
                var desc = new EntityQueryDesc();
                var component = new[] { ComponentType.ReadOnly<CliTestEnabled>() };
                switch (kind)
                {
                    case "all": desc.All = component; break;
                    case "any": desc.Any = component; break;
                    case "none": desc.None = component; break;
                    case "disabled": desc.Disabled = component; break;
                    case "absent": desc.Absent = component; break;
                    case "present": desc.Present = component; break;
                }
                using var native = Manager.CreateEntityQuery(desc);
                using var entities = native.ToEntityArray(Allocator.Temp);
                var expected = entities.ToArray().Select(Ref).OrderBy(x => x).ToArray();
                var actual = Query(args)["entities"].Select(e => (string)e["entity"]).OrderBy(x => x).ToArray();
                CollectionAssert.AreEqual(expected, actual, kind);
            }
            var union = Query(new EcsQueryArgs { Descriptions = new[]
            {
                new EcsDescription { All = new JToken[] { "CliTestEnabled" } },
                new EcsDescription { Disabled = new JToken[] { "CliTestEnabled" } }
            } });
            // Compare the fork's actual multi-description enable-mask behavior, rather than
            // assuming it is equivalent to concatenating two separately executed queries.
            using var nativeUnion = Manager.CreateEntityQuery(
                new EntityQueryDesc { All = new[] { ComponentType.ReadOnly<CliTestEnabled>() } },
                new EntityQueryDesc { Disabled = new[] { ComponentType.ReadOnly<CliTestEnabled>() } });
            using var nativeUnionEntities = nativeUnion.ToEntityArray(Allocator.Temp);
            CollectionAssert.AreEquivalent(nativeUnionEntities.ToArray().Select(Ref),
                union["entities"].Select(e => (string)e["entity"]));
            Assert.AreEqual(2, (int)Query(new EcsQueryArgs { All = new JToken[] { "CliTestEnabled" }, Options = new[] { "IgnoreComponentEnabledState" } })["total"]);
        }

        [Test]
        public void SharedFiltersUseNativeEqualityAndValueFiltersRunBeforePaging()
        {
            for (var i = 0; i < 6; i++)
            {
                var entity = Manager.CreateEntity(typeof(CliTestValue));
                Manager.SetComponentData(entity, new CliTestValue { Value = i });
                Manager.AddSharedComponentManaged(entity, new CliTestGroup { Value = i % 2 });
                Manager.AddSharedComponentManaged(entity, new CliTestGroup2 { Value = 7 });
            }
            var result = Query(new EcsQueryArgs
            {
                All = new JToken[] { "CliTestValue", "CliTestGroup", "CliTestGroup2" },
                Shared = new[] { new EcsSharedFilter { Type = "CliTestGroup", Value = new JObject { ["Value"] = 1 } },
                    new EcsSharedFilter { Type = "CliTestGroup2", Value = new JObject { ["Value"] = 7 } } },
                Where = new[] { new EcsPredicate { Type = "CliTestValue", Field = "Value", Op = "gt", Value = 2 } },
                Select = new[] { new EcsSelection { Type = "CliTestValue" } }, Limit = 1
            });
            Assert.AreEqual(2, (int)result["total"]);
            Assert.AreEqual(1, result["entities"].Count());
            Assert.IsTrue((bool)result["hasMore"]);
            Assert.Greater((int)result["entities"][0]["components"][0]["value"]["Value"], 2);
        }

        [Test]
        public void NamesAreIncludedAndProjectionsDoNotChangeMembership()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue));
            Manager.SetName(entity, "Test projectile");
            var result = Query(new EcsQueryArgs { All = new JToken[] { "CliTestValue" }, Name = new EcsNameFilter { Contains = "projectile" },
                Select = new[] { new EcsSelection { Type = "CliTestEnabled" } } });
            Assert.AreEqual(1, (int)result["total"]);
            Assert.AreEqual("Test projectile", (string)result["entities"][0]["name"]);
            Assert.AreEqual("missingComponent", (string)result["entities"][0]["components"][0]["error"]);
        }

        [Test]
        public void ReferencesAndBuffersRoundTripAndResizeInitializesNewElements()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue), typeof(CliTestBuffer));
            var target = Manager.CreateEntity();
            var response = Write(
                new EcsOperation { Op = "set", Entity = Ref(entity), Type = "CliTestValue", Fields = new JObject { ["Target"] = Ref(target) } },
                new EcsOperation { Op = "bufferReplace", Entity = Ref(entity), Type = "CliTestBuffer", Elements = JArray.Parse("[{\"Value\":4,\"Target\":\"0:0\"}]") },
                new EcsOperation { Op = "bufferResize", Entity = Ref(entity), Type = "CliTestBuffer", Length = 3 },
                new EcsOperation { Op = "bufferSet", Entity = Ref(entity), Type = "CliTestBuffer", Index = 2, Fields = new JObject { ["Target"] = "999999:5" } },
                new EcsOperation { Op = "enabled", Entity = Ref(entity), Type = "CliTestBuffer", Value = false });
            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual(target, Manager.GetComponentData<CliTestValue>(entity).Target);
            var buffer = Manager.GetBuffer<CliTestBuffer>(entity, true);
            Assert.AreEqual(3, buffer.Length);
            Assert.AreEqual(0, buffer[1].Value);
            Assert.AreEqual(Entity.Null, buffer[1].Target);
            Assert.IsFalse(Manager.IsComponentEnabled<CliTestBuffer>(entity));
            var read = Read(entity, new EcsSelection { Type = "CliTestValue" }, new EcsSelection { Type = "CliTestBuffer", Offset = 2, Limit = 1 });
            Assert.AreEqual(Ref(target), (string)read["entities"][0]["components"][0]["value"]["Target"]);
            Assert.AreEqual("999999:5", (string)read["entities"][0]["components"][1]["elements"][0]["value"]["Target"]);
        }

        [Test]
        public void StructuralSharedManagedAndChunkEditsUseNormalSetters()
        {
            var entity = Manager.CreateEntity();
            var response = Write(
                new EcsOperation { Op = "add", Entity = Ref(entity), Type = "CliTestGroup", Value = new JObject { ["Value"] = 4 } },
                new EcsOperation { Op = "set", Entity = Ref(entity), Type = "CliTestGroup", Fields = new JObject { ["Value"] = 8 } },
                new EcsOperation { Op = "add", Entity = Ref(entity), Type = "CliTestManaged", Value = new JObject { ["Value"] = 12 } },
                new EcsOperation { Op = "set", Entity = Ref(entity), Type = "CliTestManaged", Fields = new JObject { ["Value"] = 13 } },
                // Set chunk data after structural moves: it belongs to the destination chunk.
                new EcsOperation { Op = "add", Entity = Ref(entity), Type = "CliTestChunk", Storage = "chunk", Value = new JObject { ["Value"] = 11 } });
            Assert.IsTrue(response.Success, response.Error);
            Assert.AreEqual(8, Manager.GetSharedComponentManaged<CliTestGroup>(entity).Value);
            Assert.AreEqual(11, Manager.GetChunkComponentData<CliTestChunk>(entity).Value);
            Assert.AreEqual(13, Manager.GetComponentData<CliTestManaged>(entity).Value);
            response = Write(new EcsOperation { Op = "remove", Entity = Ref(entity), Type = "CliTestChunk", Storage = "chunk" });
            Assert.IsTrue(response.Success, response.Error);
            Assert.IsFalse(Manager.HasComponent(entity, ComponentType.ChunkComponent<CliTestChunk>()));
        }

        [Test]
        public void FailureStopsLaterOperationsAndPreservesEarlierWrites()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue));
            var response = Write(
                new EcsOperation { Op = "set", Entity = Ref(entity), Type = "CliTestValue", Fields = new JObject { ["Value"] = 4 } },
                new EcsOperation { Op = "set", Entity = Ref(entity), Type = "CliTestValue", Fields = new JObject { ["Value"] = 7, ["Typo"] = 1 } },
                new EcsOperation { Op = "destroy", Entity = Ref(entity) });
            Assert.IsFalse(response.Success);
            Assert.AreEqual(1, response.FailedOperation);
            Assert.AreEqual(2, response.Operations.Count);
            Assert.AreEqual(4, Manager.GetComponentData<CliTestValue>(entity).Value);
        }

        [Test]
        public void DuplicateWorldNamesAndMalformedEntitiesFailClearly()
        {
            using (var duplicate = new World(m_World.Name))
                Assert.Throws<ArgumentException>(() => EcsCommands.Query(m_World.Name, new EcsQueryArgs()));
            Assert.Throws<ArgumentException>(() => EcsCommands.Read(m_World.Name, new EcsReadArgs { Entities = new[] { "not-an-entity" } }));
            var entity = Manager.CreateEntity();
            Manager.DestroyEntity(entity);
            var result = Read(entity);
            Assert.AreEqual("missingEntity", (string)result["entities"][0]["error"]);
        }

        [Test]
        public void QueryValidationDoesNotDependOnUnitySafetyDefines()
        {
            Assert.Throws<ArgumentException>(() => Query(new EcsQueryArgs { All = new JToken[] { "CliTestValue" }, None = new JToken[] { "CliTestValue" } }));
            Assert.Throws<ArgumentException>(() => Query(new EcsQueryArgs { Disabled = new JToken[] { "CliTestValue" } }));
            Assert.Throws<ArgumentException>(() => Query(new EcsQueryArgs { Shared = new[] { new EcsSharedFilter { Type = "CliTestGroup", Value = new JObject { ["Value"] = 1 } } } }));
        }

        [Test]
        public void CreateCloneDestroyAndCleanupFollowEntityManagerSemantics()
        {
            var created = Write(new EcsOperation { Op = "create", Name = "Created through CLI", Components = new[]
            {
                new EcsInitialComponent { Type = "CliTestValue", Value = new JObject { ["Value"] = 7 } },
                new EcsInitialComponent { Type = "CliTestCleanup" }
            } });
            Assert.IsTrue(created.Success, created.Error);
            var reference = (string)created.Operations[0]["entity"];
            var parts = reference.Split(':');
            var entity = new Entity { Index = int.Parse(parts[0]), Version = int.Parse(parts[1]) };
            var clone = Write(new EcsOperation { Op = "instantiate", Entity = reference });
            Assert.IsTrue(clone.Success, clone.Error);
            Assert.AreNotEqual(reference, (string)clone.Operations[0]["entity"]);
            var destroyed = Write(new EcsOperation { Op = "destroy", Entity = reference });
            Assert.IsTrue(destroyed.Success, destroyed.Error);
            Assert.IsTrue(Manager.Exists(entity));
            Assert.IsTrue(Manager.HasComponent<CliTestCleanup>(entity));
            Assert.IsFalse(Manager.HasComponent<CliTestValue>(entity));
            Assert.IsTrue(Write(new EcsOperation { Op = "remove", Entity = reference, Type = "CliTestCleanup" }).Success);
            Assert.IsFalse(Manager.Exists(entity));
        }

        [Test]
        public void BufferInsertRemoveAndReadDoNotObtainWriteAccess()
        {
            var entity = Manager.CreateEntity(typeof(CliTestBuffer));
            var response = Write(
                new EcsOperation { Op = "bufferInsert", Entity = Ref(entity), Type = "CliTestBuffer", Index = 0,
                    Elements = JArray.Parse("[{\"Value\":1},{\"Value\":2},{\"Value\":3}]") },
                new EcsOperation { Op = "bufferRemove", Entity = Ref(entity), Type = "CliTestBuffer", Index = 1, Count = 1 });
            Assert.IsTrue(response.Success, response.Error);
            var buffer = Manager.GetBuffer<CliTestBuffer>(entity, true);
            Assert.AreEqual(2, buffer.Length);
            Assert.AreEqual(3, buffer[1].Value);
            var advanceVersion = m_World.GetOrCreateSystemManaged<CliTestAdvanceVersionSystem>();
            var handle = Manager.GetBufferTypeHandle<CliTestBuffer>(true);
            var version = Manager.GetChunk(entity).GetChangeVersion(ref handle);
            advanceVersion.Update();
            Assert.AreNotEqual(version, Manager.GlobalSystemVersion, "A writable read must have a newer version available to stamp.");
            handle = Manager.GetBufferTypeHandle<CliTestBuffer>(true);
            Read(entity, new EcsSelection { Type = "CliTestBuffer" });
            Assert.AreEqual(version, Manager.GetChunk(entity).GetChangeVersion(ref handle));
        }

        [Test]
        public void DynamicJsonAcceptsNaturalAndSchemaStringForms()
        {
            var entity = Manager.CreateEntity(typeof(CliTestValue));
            var natural = new JObject { ["operations"] = new JArray(new JObject
            {
                ["op"] = "set", ["entity"] = Ref(entity), ["type"] = "CliTestValue", ["fields"] = new JObject { ["Value"] = 18 }
            }) };
            var encoded = (JObject)natural.DeepClone();
            encoded["operations"][0]["fields"] = "{\"Value\":19}";
            Assert.IsTrue(EcsWriteCommands.Write(m_World.Name, natural.ToObject<EcsWriteArgs>()).Success);
            Assert.AreEqual(18, Manager.GetComponentData<CliTestValue>(entity).Value);
            Assert.IsTrue(EcsWriteCommands.Write(m_World.Name, encoded.ToObject<EcsWriteArgs>()).Success);
            Assert.AreEqual(19, Manager.GetComponentData<CliTestValue>(entity).Value);
        }

        [Test]
        public void SpecialEntityOptionsAndWriteGroupsMatchNativeQueries()
        {
            Manager.CreateEntity(typeof(CliTestValue));
            Manager.CreateEntity(typeof(CliTestValue), typeof(Prefab));
            Manager.CreateEntity(typeof(CliTestValue), typeof(Disabled));
            Manager.CreateEntity(typeof(CliTestValue), typeof(CliTestWriter));
            var options = EntityQueryOptions.IncludePrefab | EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.FilterWriteGroup;
            using var native = Manager.CreateEntityQuery(new EntityQueryDesc { All = new[] { ComponentType.ReadWrite<CliTestValue>() }, Options = options });
            using var entities = native.ToEntityArray(Allocator.Temp);
            var actual = Query(new EcsQueryArgs
            {
                All = new JToken[] { new JObject { ["type"] = "CliTestValue", ["access"] = "readWrite" } },
                Options = new[] { "IncludePrefab", "IncludeDisabledEntities", "FilterWriteGroup" }
            })["entities"].Select(e => (string)e["entity"]).OrderBy(x => x).ToArray();
            CollectionAssert.AreEqual(entities.ToArray().Select(Ref).OrderBy(x => x).ToArray(), actual);
        }
    }
}
