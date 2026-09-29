using System;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;
using Unity.Pipeline.Models;
using UnityEngine.Scripting;

namespace Unity.Entities.Pipeline
{
    // Keep operation outcomes on the response itself: Pipeline may omit Result on a failure.
    public sealed class EcsWriteResponse : CommandExecutionResponse
    {
        [JsonProperty("world")] public string WorldName;
        [JsonProperty("operations")] public JArray Operations = new JArray();
        [JsonProperty("failedOperation")] public int? FailedOperation;
    }

    [Preserve]
    public static class EcsWriteCommands
    {
        [CliCommand("ecs_write", "Direct runtime ECS edits, like the entity inspector. Operations execute in order without rollback; failure reports completed operations. Supports component/shared fields, index:version references, buffer edits, add/remove, enable bits, create/instantiate/destroy and names. No automatic retry.", Tags = new[] { "ecs/write" })]
        public static EcsWriteResponse Write(
            [CliArg("world", "Exact unique World.Name", Required = true)] string world,
            [CliArg("request", "Ordered operations; see Entities Documentation~/ecs-pipeline.md for operation fields", Required = true)] EcsWriteArgs request)
        {
            if (request?.Operations == null || request.Operations.Length == 0) throw new ArgumentException("operations must contain at least one operation.");
            var manager = EcsTypeResolver.World(world).EntityManager;
            manager.CompleteAllTrackedJobs();
            var response = new EcsWriteResponse { Success = true, WorldName = world };
            for (var i = 0; i < request.Operations.Length; i++)
            {
                var operation = request.Operations[i];
                Entity? affected = null;
                try
                {
                    if (operation == null) throw new ArgumentException("Null operation.");
                    var result = Execute(manager, operation, ref affected);
                    result["operation"] = i; result["op"] = operation.Op; result["status"] = "ok";
                    response.Operations.Add(result);
                }
                catch (Exception error)
                {
                    while (error is TargetInvocationException && error.InnerException != null) error = error.InnerException;
                    response.Success = false;
                    response.Error = error.Message;
                    response.FailedOperation = i;
                    response.Operations.Add(new JObject
                    {
                        ["operation"] = i, ["op"] = operation?.Op, ["status"] = "failed", ["error"] = error.Message,
                        ["entity"] = affected.HasValue ? EcsTypeResolver.Format(affected.Value) : operation?.Entity,
                        ["note"] = "Earlier operations remain applied. This operation may have partially applied; no rollback was performed."
                    });
                    break;
                }
            }
            return response;
        }

        static JObject Execute(EntityManager manager, EcsOperation operation, ref Entity? affected)
        {
            if (operation.Op == "create")
            {
                // Prepare supplied values before creating an entity; native failures still have no rollback guarantee.
                var components = operation.Components ?? Array.Empty<EcsInitialComponent>();
                var prepared = components.Select(c => Prepare(c ?? throw new ArgumentException("Null initial component."))).ToArray();
                if (prepared.Select(p => p.component.TypeIndex).Distinct().Count() != prepared.Length) throw new ArgumentException("Duplicate initial component type.");
                if (operation.Name != null && !EcsCommands.NamesAvailable) throw new NotSupportedException("Debug-name storage is disabled.");
                var created = manager.CreateEntity();
                affected = created;
                foreach (var entry in prepared) entry.access.Add(manager, created, entry.component.IsChunkComponent, entry.value);
                if (operation.Name != null) manager.SetName(created, operation.Name);
                return EcsCommands.Identity(manager, created);
            }

            var entity = EcsTypeResolver.Existing(manager, operation.Entity);
            affected = entity;
            switch (operation.Op)
            {
                case "instantiate":
                    var clone = manager.Instantiate(entity);
                    affected = clone;
                    return EcsCommands.Identity(manager, clone);
                case "destroy":
                    var identity = EcsCommands.Identity(manager, entity);
                    manager.DestroyEntity(entity);
                    identity["existsAfter"] = manager.Exists(entity);
                    return identity;
                case "name":
                    if (!EcsCommands.NamesAvailable) throw new NotSupportedException("Debug-name storage is disabled.");
                    if (operation.Name == null) throw new ArgumentException("name is required.");
                    manager.SetName(entity, operation.Name);
                    return EcsCommands.Identity(manager, entity);
                case "entityEnabled":
                    manager.SetEnabled(entity, (bool)EcsValueCodec.Decode(operation.Value, typeof(bool)));
                    return EcsCommands.Identity(manager, entity);
            }

            var type = EcsTypeResolver.Resolve(operation.Type);
            var component = EcsTypeResolver.Component(type, operation.Storage);
            if (operation.Op == "add")
            {
                if (manager.HasComponent(entity, component)) throw new ArgumentException($"{type.FullName} is already present.");
                var prepared = Prepare(new EcsInitialComponent { Type = operation.Type, Storage = operation.Storage, Value = operation.Value, Elements = operation.Elements });
                prepared.access.Add(manager, entity, component.IsChunkComponent, prepared.value);
                return EcsCommands.Identity(manager, entity);
            }
            if (!manager.HasComponent(entity, component)) throw new ArgumentException($"Entity {operation.Entity} does not have {type.FullName} ({operation.Storage ?? "entity"} storage).");
            switch (operation.Op)
            {
                case "remove": manager.RemoveComponent(entity, component); break;
                case "enabled":
                    if (!component.IsEnableable || component.IsChunkComponent) throw new ArgumentException("enabled requires an enableable entity component or buffer.");
                    manager.SetComponentEnabled(entity, component, (bool)EcsValueCodec.Decode(operation.Value, typeof(bool)));
                    break;
                case "set":
                    if (component.IsBuffer) throw new ArgumentException("Use bufferSet or bufferReplace for buffers.");
                    var access = EcsAccess.For(type);
                    access.Write(manager, entity, component.IsChunkComponent,
                        EcsValueCodec.Patch(access.Read(manager, entity, component.IsChunkComponent), type, operation.Fields));
                    break;
                case "bufferSet": case "bufferReplace": case "bufferResize": case "bufferInsert": case "bufferRemove":
                    if (!component.IsBuffer) throw new ArgumentException("A buffer type is required.");
                    EcsAccess.For(type).EditBuffer(manager, entity, operation);
                    break;
                default: throw new ArgumentException($"Unknown operation '{operation.Op}'.");
            }
            return EcsCommands.Identity(manager, entity);
        }

        static (ComponentType component, EcsAccess access, object value) Prepare(EcsInitialComponent input)
        {
            var type = EcsTypeResolver.Resolve(input.Type);
            var component = EcsTypeResolver.Component(type, input.Storage);
            object value = null;
            if (component.IsBuffer)
            {
                if (input.Value != null) throw new ArgumentException("Use elements to initialize a buffer.");
                var elements = input.Elements ?? new JArray();
                var values = Array.CreateInstance(type, elements.Count);
                for (var i = 0; i < elements.Count; i++) values.SetValue(EcsValueCodec.Decode(elements[i], type), i);
                value = values;
            }
            else
            {
                if (input.Elements != null) throw new ArgumentException("elements requires a buffer type.");
                if (input.Value != null) value = EcsValueCodec.Decode(input.Value, type);
            }
            return (component, EcsAccess.For(type), value);
        }
    }
}
