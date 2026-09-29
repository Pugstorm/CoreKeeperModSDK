using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Unity.Entities.Pipeline
{
    // Accept natural JSON, plus JSON strings for clients following Pipeline 0.7's
    // string schema for JObject/JArray. No global serializer configuration is changed.
    public sealed class EcsJsonPayloadConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) => objectType == typeof(JObject) || objectType == typeof(JArray);
        public override bool CanWrite => false;
        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            var token = JToken.Load(reader);
            if (token.Type == JTokenType.Null) return null;
            if (token.Type == JTokenType.String) token = JToken.Parse(token.Value<string>());
            if (!objectType.IsInstanceOfType(token)) throw new JsonSerializationException($"Expected {objectType.Name} or a JSON string containing it.");
            return token;
        }
        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer) => throw new NotSupportedException();
    }
}
