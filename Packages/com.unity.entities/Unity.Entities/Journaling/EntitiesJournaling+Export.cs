#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Serialization.Json;

namespace Unity.Entities
{
    partial class EntitiesJournaling
    {
        class JsonAdapters :
            IJsonAdapter<FixedString32Bytes>,
            IJsonAdapter<FixedString64Bytes>,
            IJsonAdapter<FixedString128Bytes>,
            IJsonAdapter<FixedString512Bytes>,
            IJsonAdapter<FixedString4096Bytes>,
            IJsonAdapter<Hash128>,
            IJsonAdapter<EntityGuid>
        {
            void IJsonAdapter<FixedString32Bytes>.Serialize(in JsonSerializationContext<FixedString32Bytes> context, FixedString32Bytes value)
                => context.Writer.WriteValueLiteral(value.ToString().SingleQuote());

            FixedString32Bytes IJsonAdapter<FixedString32Bytes>.Deserialize(in JsonDeserializationContext<FixedString32Bytes> context)
                => throw new NotImplementedException();

            void IJsonAdapter<FixedString64Bytes>.Serialize(in JsonSerializationContext<FixedString64Bytes> context, FixedString64Bytes value)
                => context.Writer.WriteValueLiteral(value.ToString().SingleQuote());

            FixedString64Bytes IJsonAdapter<FixedString64Bytes>.Deserialize(in JsonDeserializationContext<FixedString64Bytes> context)
                => throw new NotImplementedException();

            void IJsonAdapter<FixedString128Bytes>.Serialize(in JsonSerializationContext<FixedString128Bytes> context, FixedString128Bytes value)
                => context.Writer.WriteValueLiteral(value.ToString().SingleQuote());

            FixedString128Bytes IJsonAdapter<FixedString128Bytes>.Deserialize(in JsonDeserializationContext<FixedString128Bytes> context)
                => throw new NotImplementedException();

            void IJsonAdapter<FixedString512Bytes>.Serialize(in JsonSerializationContext<FixedString512Bytes> context, FixedString512Bytes value)
                => context.Writer.WriteValueLiteral(value.ToString().SingleQuote());

            FixedString512Bytes IJsonAdapter<FixedString512Bytes>.Deserialize(in JsonDeserializationContext<FixedString512Bytes> context)
                => throw new NotImplementedException();

            void IJsonAdapter<FixedString4096Bytes>.Serialize(in JsonSerializationContext<FixedString4096Bytes> context, FixedString4096Bytes value)
                => context.Writer.WriteValueLiteral(value.ToString().SingleQuote());

            FixedString4096Bytes IJsonAdapter<FixedString4096Bytes>.Deserialize(in JsonDeserializationContext<FixedString4096Bytes> context)
                => throw new NotImplementedException();

            void IJsonAdapter<Hash128>.Serialize(in JsonSerializationContext<Hash128> context, Hash128 value)
                => context.Writer.WriteValueLiteral(value.ToString());

            Hash128 IJsonAdapter<Hash128>.Deserialize(in JsonDeserializationContext<Hash128> context)
                => throw new NotImplementedException();

            void IJsonAdapter<EntityGuid>.Serialize(in JsonSerializationContext<EntityGuid> context, EntityGuid value)
                => context.Writer.WriteValueLiteral(value.ToString());

            EntityGuid IJsonAdapter<EntityGuid>.Deserialize(in JsonDeserializationContext<EntityGuid> context)
                => throw new NotImplementedException();
        }

        /// <summary>
        /// Export journaling data as CSV.
        /// </summary>
        /// <returns>An enumerable of strings, one per CSV data row.</returns>
        public static IEnumerable<string> ExportToCSV()
        {
            if (!s_Initialized)
                yield break;

            // Export header
            yield return $"{nameof(RecordView.Index)},{nameof(RecordView.RecordType)},{nameof(RecordView.FrameIndex)},{nameof(RecordView.World)},{nameof(RecordView.ExecutingSystem)},{nameof(RecordView.OriginSystem)},{nameof(RecordView.Entities)},{nameof(RecordView.ComponentTypes)},{nameof(RecordView.Data)}";

            // Setup json export parameters
            var jsonAdapters = new List<IJsonAdapter>
            {
                new JsonAdapters(),
            };
            var jsonSerializationParameters = new JsonSerializationParameters
            {
                DisableRootAdapters = true,
                DisableSerializedReferences = true,
                RequiresThreadSafety = false,
                Simplified = true,
                Minified = true,
                UserDefinedAdapters = jsonAdapters
            };

            // Export each record
            var records = GetRecords(Ordering.Ascending);
            foreach (var record in records)
            {
                var world = record.World.Name.ToCSV();
                var executingSystem = record.ExecutingSystem.Name.ToCSV();
                var originSystem = record.OriginSystem.Name.ToCSV();
                var sortedEntities = record.Entities.Select(e => e.Name).OrderBy(e => e);
                var entities = string.Join(";", sortedEntities).ToCSV();
                var sortedComponentTypes = record.ComponentTypes.Select(t => t.Name).OrderBy(c => c);
                var componentTypes = string.Join(";", sortedComponentTypes).ToCSV();
                var data = string.Empty;
                switch (record.RecordType)
                {
                    case RecordType.SystemAdded:
                    case RecordType.SystemRemoved:
                        if (TryGetRecordDataAsSystemView(record, out var systemView))
                        {
                            data = systemView.Name.ToCSV();
                        }
                        break;

                    case RecordType.SetComponentData:
                    case RecordType.SetSharedComponentData:
                    case RecordType.GetComponentDataRW:
                        if (TryGetRecordDataAsComponentDataArrayBoxed(record, out var componentDataArray))
                        {
                            jsonSerializationParameters.SerializedType = componentDataArray.GetType();
                            data = JsonSerialization.ToJson(componentDataArray, jsonSerializationParameters).ToCSV();
                        }
                        break;
                }
                yield return string.Join(",", record.Index, record.RecordType, record.FrameIndex, world, executingSystem, originSystem, entities, componentTypes, data);
            }
        }

        static string ToCSV(this string value)
        {
            var result = value.Replace('\"', '\'');
            return result.Contains(' ') || result.Contains(',') ? result.DoubleQuote() : result;
        }

        static string SingleQuote(this string value)
        {
            return "\'" + value + "\'";
        }

        static string DoubleQuote(this string value)
        {
            return "\"" + value + "\"";
        }
    }
}
#endif
