using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Unity.Pipeline.Commands;

namespace Unity.Entities.Pipeline
{
    [Serializable]
    public class EcsDescription : IStructuredCommandInput
    {
        [CliArg("all", "Required component names, or selector JSON {type,access,storage}; encoded JSON strings also accepted")]
        [JsonProperty("all")] public JToken[] All;
        [JsonProperty("any")] public JToken[] Any;
        [JsonProperty("none")] public JToken[] None;
        [JsonProperty("disabled")] public JToken[] Disabled;
        [JsonProperty("absent")] public JToken[] Absent;
        [JsonProperty("present")] public JToken[] Present;
        [JsonProperty("options")] public string[] Options;
    }

    [Serializable]
    public sealed class EcsQueryArgs : EcsDescription, IStructuredCommandInput
    {
        [JsonProperty("descriptions")] public EcsDescription[] Descriptions;
        [CliArg("shared", "Up to two native shared equality filters; types must be required by every description")]
        [JsonProperty("shared")] public EcsSharedFilter[] Shared;
        [JsonProperty("where")] public EcsPredicate[] Where;
        [JsonProperty("name")] public EcsNameFilter Name;
        [JsonProperty("select")] public EcsSelection[] Select;
        [JsonProperty("offset")] public int Offset;
        [JsonProperty("limit")] public int Limit = 50;
    }

    [Serializable]
    public sealed class EcsSharedFilter : IStructuredCommandInput
    {
        [CliArg("type", "Registered shared component name", Required = true)]
        [JsonProperty("type")] public string Type;
        [CliArg("value", "Complete shared value as an object or encoded JSON string", Required = true)]
        [JsonProperty("value"), JsonConverter(typeof(EcsJsonPayloadConverter))] public JObject Value;
    }

    [Serializable]
    public sealed class EcsPredicate : IStructuredCommandInput
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("field")] public string Field;
        [JsonProperty("op")] public string Op;
        [JsonProperty("value")] public JToken Value;
        [JsonProperty("storage")] public string Storage;
    }

    [Serializable]
    public sealed class EcsNameFilter : IStructuredCommandInput
    {
        [JsonProperty("equals")] public string EqualsName;
        [JsonProperty("contains")] public string Contains;
    }

    [Serializable]
    public sealed class EcsSelection : IStructuredCommandInput
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("storage")] public string Storage;
        [JsonProperty("fields")] public string[] Fields;
        [JsonProperty("offset")] public int Offset;
        [JsonProperty("limit")] public int Limit = 32;
    }

    [Serializable]
    public sealed class EcsReadArgs : IStructuredCommandInput
    {
        [CliArg("entities", "Entity references in index:version format", Required = true)]
        [JsonProperty("entities")] public string[] Entities;
        [JsonProperty("components")] public EcsSelection[] Components;
        [JsonProperty("allValues")] public bool AllValues;
    }

    [Serializable]
    public sealed class EcsWriteArgs : IStructuredCommandInput
    {
        [CliArg("operations", "Sequential direct edits; stops on failure without rollback", Required = true)]
        [JsonProperty("operations")] public EcsOperation[] Operations;
    }

    [Serializable]
    public sealed class EcsOperation : IStructuredCommandInput
    {
        [CliArg("op", "set, bufferSet, bufferReplace, bufferResize, bufferInsert, bufferRemove, add, remove, enabled, entityEnabled, create, instantiate, destroy, name", Required = true)]
        [JsonProperty("op")] public string Op;
        [JsonProperty("entity")] public string Entity;
        [JsonProperty("type")] public string Type;
        [JsonProperty("storage")] public string Storage;
        [CliArg("fields", "Field-path/value object or encoded JSON string; omitted fields are unchanged")]
        [JsonProperty("fields"), JsonConverter(typeof(EcsJsonPayloadConverter))] public JObject Fields;
        [JsonProperty("value")] public JToken Value;
        [JsonProperty("elements"), JsonConverter(typeof(EcsJsonPayloadConverter))] public JArray Elements;
        [JsonProperty("index")] public int Index;
        [JsonProperty("count")] public int Count = 1;
        [JsonProperty("length")] public int Length;
        [JsonProperty("components")] public EcsInitialComponent[] Components;
        [JsonProperty("name")] public string Name;
    }

    [Serializable]
    public sealed class EcsInitialComponent : IStructuredCommandInput
    {
        [JsonProperty("type")] public string Type;
        [JsonProperty("storage")] public string Storage;
        [JsonProperty("value")] public JToken Value;
        [JsonProperty("elements"), JsonConverter(typeof(EcsJsonPayloadConverter))] public JArray Elements;
    }
}
