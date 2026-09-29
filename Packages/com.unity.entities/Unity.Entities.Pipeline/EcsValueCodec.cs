using System;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Newtonsoft.Json.Linq;

namespace Unity.Entities.Pipeline
{
    // Field-only JSON: never execute arbitrary component properties or traverse native storage.
    internal static class EcsValueCodec
    {
        internal static FieldInfo[] Fields(Type type) => type.GetFields(BindingFlags.Public | BindingFlags.Instance)
            .OrderBy(field => field.Name, StringComparer.Ordinal).ToArray();

        static bool FixedString(Type type) => type.Namespace == "Unity.Collections" &&
            type.Name.StartsWith("FixedString", StringComparison.Ordinal) && type.Name.EndsWith("Bytes", StringComparison.Ordinal);

        static void Supported(Type type, int depth)
        {
            if (depth > 16) throw new NotSupportedException("Value nesting exceeds 16 levels (possibly a reference cycle).");
            if (type.IsPointer || type == typeof(IntPtr) || type == typeof(UIntPtr) ||
                typeof(Delegate).IsAssignableFrom(type) || typeof(UnityEngine.Object).IsAssignableFrom(type) ||
                type.GetCustomAttributes(false).Any(a => a.GetType().Name == "NativeContainerAttribute") ||
                (type.Namespace == "Unity.Entities" && (type.Name.StartsWith("Blob", StringComparison.Ordinal) ||
                  type.Name.StartsWith("UnityObjectRef", StringComparison.Ordinal))) ||
                (type.Namespace == "Unity.Collections" && !FixedString(type)) ||
                (typeof(System.Collections.IEnumerable).IsAssignableFrom(type) && !type.IsArray && type != typeof(string) && !FixedString(type)))
                throw new NotSupportedException($"No JSON value codec for {type.FullName}.");
        }

        internal static JToken Encode(object value, Type type, int depth = 0)
        {
            Supported(type, depth);
            if (value == null) return JValue.CreateNull();
            if (type == typeof(Entity)) return new JValue(EcsTypeResolver.Format((Entity)value));
            if (FixedString(type)) return new JValue(value.ToString());
            if (type.IsEnum) return new JValue(value.ToString());
            if (type == typeof(long) || type == typeof(ulong)) return new JValue(Convert.ToString(value, CultureInfo.InvariantCulture));
            if (type.IsPrimitive || type == typeof(string) || type == typeof(decimal)) return new JValue(value);
            if (type.IsArray)
            {
                if (type.GetArrayRank() != 1) throw new NotSupportedException("Only one-dimensional managed arrays are supported.");
                var array = new JArray();
                foreach (var item in (Array)value) array.Add(Encode(item, type.GetElementType(), depth + 1));
                return array;
            }
            var result = new JObject();
            foreach (var field in Fields(type))
            {
                try { result[field.Name] = Encode(field.GetValue(value), field.FieldType, depth + 1); }
                catch (NotSupportedException error)
                {
                    result[field.Name] = new JObject { ["error"] = "unsupported", ["reason"] = error.Message };
                }
            }
            return result;
        }

        internal static object Decode(JToken token, Type type, bool requireAll = false, int depth = 0)
        {
            Supported(type, depth);
            if (token == null) throw new ArgumentException($"A value for {type.FullName} is required.");
            // Pipeline 0.7's schema describes arbitrary JSON slots as strings. Accept encoded
            // JSON for complex values as well as natural nested JSON, without reparsing actual strings.
            if (token.Type == JTokenType.String && type != typeof(string) && type != typeof(Entity) && !FixedString(type) &&
                !type.IsPrimitive && !type.IsEnum && type != typeof(decimal)) token = JToken.Parse(token.Value<string>());
            if (token.Type == JTokenType.Null)
            {
                if (type.IsValueType) throw new ArgumentException($"null is not valid for {type.FullName}; use 0:0 for Entity.Null.");
                return null;
            }
            if (type == typeof(Entity)) return EcsTypeResolver.ParseEntity(String(token));
            if (FixedString(type))
            {
                var text = String(token);
                var value = Activator.CreateInstance(type, text);
                if (value.ToString() != text) throw new ArgumentException($"Text does not fit in {type.Name}.");
                return value;
            }
            if (type.IsEnum)
            {
                if (token.Type != JTokenType.String && token.Type != JTokenType.Integer) throw new ArgumentException("Expected an enum name or integer.");
                return Enum.Parse(type, token.ToString(), false);
            }
            if (type == typeof(string)) return String(token);
            if (type == typeof(bool))
            {
                if (token.Type == JTokenType.String && bool.TryParse(token.Value<string>(), out var boolean)) return boolean;
                if (token.Type != JTokenType.Boolean) throw new ArgumentException("Expected a JSON boolean.");
                return token.Value<bool>();
            }
            if (type == typeof(char))
            {
                var text = String(token);
                if (text.Length != 1) throw new ArgumentException("Expected one character.");
                return text[0];
            }
            if (type.IsPrimitive || type == typeof(decimal))
            {
                if (token.Type != JTokenType.Integer && token.Type != JTokenType.Float && token.Type != JTokenType.String)
                    throw new ArgumentException($"Expected a number for {type.Name}.");
                var text = token.ToString();
                var floating = type == typeof(float) || type == typeof(double) || type == typeof(decimal);
                if (!floating && (text.Contains(".") || text.Contains("e") || text.Contains("E")))
                    throw new ArgumentException($"Expected an integer for {type.Name}.");
                return Convert.ChangeType(text, type, CultureInfo.InvariantCulture);
            }
            if (type.IsArray)
            {
                if (!(token is JArray array) || type.GetArrayRank() != 1) throw new ArgumentException("Expected a one-dimensional JSON array.");
                var value = Array.CreateInstance(type.GetElementType(), array.Count);
                for (var i = 0; i < array.Count; i++) value.SetValue(Decode(array[i], type.GetElementType(), requireAll, depth + 1), i);
                return value;
            }
            if (!(token is JObject obj)) throw new ArgumentException($"Expected an object for {type.FullName}.");
            var fields = Fields(type);
            foreach (var property in obj.Properties())
                if (!fields.Any(field => field.Name == property.Name)) throw new ArgumentException($"Unknown field {type.Name}.{property.Name}.");
            var instance = Activator.CreateInstance(type);
            foreach (var field in fields)
            {
                if (!obj.TryGetValue(field.Name, out var fieldValue))
                {
                    if (requireAll) throw new ArgumentException($"Missing field {type.Name}.{field.Name} in complete value.");
                    continue;
                }
                if (field.IsInitOnly) throw new ArgumentException($"Field {type.Name}.{field.Name} is readonly.");
                field.SetValue(instance, Decode(fieldValue, field.FieldType, requireAll, depth + 1));
            }
            return instance;
        }

        internal static string String(JToken value)
        {
            if (value?.Type != JTokenType.String) throw new ArgumentException("Expected a JSON string.");
            return value.Value<string>();
        }

        internal static FieldInfo[] Path(Type type, string path)
        {
            if (string.IsNullOrEmpty(path)) throw new ArgumentException("A nonempty field path is required.");
            var parts = path.Split('.');
            var fields = new FieldInfo[parts.Length];
            for (var i = 0; i < parts.Length; i++)
            {
                Supported(type, i);
                fields[i] = Fields(type).FirstOrDefault(field => field.Name == parts[i]);
                if (fields[i] == null) throw new ArgumentException($"Unknown field {type.FullName}.{parts[i]} in '{path}'.");
                type = fields[i].FieldType;
            }
            return fields;
        }

        internal static object Get(object instance, FieldInfo[] path)
        {
            foreach (var field in path)
            {
                if (instance == null) throw new ArgumentException($"Cannot read {field.Name} through a null reference.");
                instance = field.GetValue(instance);
            }
            return instance;
        }

        internal static object Patch(object value, Type type, JObject fields)
        {
            if (fields == null) throw new ArgumentException("fields is required.");
            // Convert all values before modifying even the detached copy.
            var edits = fields.Properties().Select(property =>
            {
                var path = Path(type, property.Name);
                if (path.Any(field => field.IsInitOnly)) throw new ArgumentException($"Readonly field in {property.Name}.");
                return (path, value: Decode(property.Value, path[path.Length - 1].FieldType));
            }).ToArray();
            foreach (var edit in edits) value = Set(value, edit.path, 0, edit.value);
            return value;
        }

        static object Set(object instance, FieldInfo[] path, int at, object value)
        {
            if (instance == null) throw new ArgumentException($"Cannot write {path[at].Name} through a null reference.");
            // Copy managed components/nested objects as well as boxed structs, matching a detached inspector edit.
            instance = typeof(object).GetMethod("MemberwiseClone", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(instance, null);
            var field = path[at];
            field.SetValue(instance, at == path.Length - 1 ? value : Set(field.GetValue(instance), path, at + 1, value));
            return instance;
        }

        internal static JToken Project(object value, Type type, string[] paths)
        {
            if (paths == null) return Encode(value, type);
            var result = new JObject();
            foreach (var name in paths)
            {
                var path = Path(type, name);
                result[name] = Encode(Get(value, path), path[path.Length - 1].FieldType);
            }
            return result;
        }
    }
}
