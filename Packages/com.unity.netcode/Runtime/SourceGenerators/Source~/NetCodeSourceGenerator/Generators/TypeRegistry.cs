using System;
using System.Collections.Generic;
using System.Linq;

namespace Unity.NetCode.Generators
{
    internal struct TypeDescription
    {
        public string TypeFullName;
        public string Key;
        public TypeAttribute Attribute;

        public override bool Equals(object obj)
        {
            if (obj is TypeDescription)
            {
                var other = (TypeDescription)obj;
                var otherQuantization = other.Attribute.quantization > 0;
                var ourQuantization = Attribute.quantization > 0;

                var mask = (uint)TypeAttribute.AttributeFlags.Interpolated;
                if (other.Key == Key &&
                    other.Attribute.subtype == Attribute.subtype &&
                    (other.Attribute.smoothing & mask) == (Attribute.smoothing & mask) &&
                    ourQuantization == otherQuantization)
                    return true;
            }
            return false;
        }

        public override string ToString()
        {
            return $"Type:{TypeFullName} Key:{Key} (quantized={Attribute.quantization} composite={Attribute.aggregateChangeMask} smoothing={Attribute.smoothing} subtype={Attribute.subtype})";
        }

        public override int GetHashCode()
        {
            return Key.GetHashCode() ^ Attribute.GetHashCode();
        }
    }

    internal class TypeTemplate
    {
        public bool SupportsQuantization;
        public bool Composite;
        public bool SupportCommand = true;
        public string TemplatePath;
        public string TemplateOverridePath;

        public override string ToString()
        {
            return
                $"IsComposite({Composite}), SupportQuantization({SupportsQuantization})\nTemplatePath: {TemplatePath}\nTemplateOverridePath: {TemplateOverridePath}\n";
        }
    }

    internal struct TypeAttribute
    {
        public int subtype;
        public int quantization;
        public uint smoothing;
        public float maxSmoothingDist;
        public bool aggregateChangeMask;

        public static TypeAttribute Empty()
        {
            return new TypeAttribute
            {
                aggregateChangeMask = false,
                smoothing = (uint)SmoothingAction.Clamp,
                quantization = -1,
                subtype = 0,
                maxSmoothingDist = 0
            };
        }

        [Flags]
        public enum AttributeFlags
        {
            None = 0,
            Interpolated = 1 << 0,
            InterpolatedAndExtrapolated = Interpolated | 1 << 1,
            Quantized = 1 << 2,
            Composite = 1 << 3,

            All = InterpolatedAndExtrapolated | Quantized | Composite
        }

        public override int GetHashCode()
        {
            bool isquantized = quantization > 0;
            return isquantized.GetHashCode() ^ subtype.GetHashCode() ^
                   (int) ((smoothing & (uint) AttributeFlags.Interpolated));
        }
    }

    internal class TypeRegistry
    {
        public Dictionary<TypeDescription, TypeTemplate> Templates = new Dictionary<TypeDescription, TypeTemplate>(16);

        public TypeRegistry(IEnumerable<TypeRegistryEntry> types)
        {
            AddRange(types);
        }

        public void AddRange(IEnumerable<TypeRegistryEntry> types)
        {
            foreach (var entry in types)
            {
                AddTemplateEntry(entry);
            }
        }

        public void AddTemplateEntry(in TypeRegistryEntry entry)
        {
            var typeDescription = new TypeDescription
            {
                TypeFullName = entry.Type,
                Key = entry.Type,
                Attribute = new TypeAttribute
                {
                    subtype = entry.SubType,
                    quantization = entry.Quantized ? 1 : -1,
                    smoothing = (uint)entry.Smoothing,
                    aggregateChangeMask = entry.Composite
                }
            };
            var template = new TypeTemplate
            {
                SupportsQuantization = entry.Quantized,
                Composite = entry.Composite,
                SupportCommand = entry.SupportCommand,
                TemplatePath = entry.Template,
                TemplateOverridePath = entry.TemplateOverride
            };
            Templates.Add(typeDescription, template);
        }

        public string FormatAllKnownTypes()
        {
            return $"[{Templates.Count}:{string.Join(",", Templates.Keys)}]";
        }

        public string FormatAllKnownSubTypes()
        {
            var aggregate = string.Join(",", Templates
                .Where(x => x.Key.Attribute.subtype != 0)
                .Select(x => $"[{x.Key.Attribute.subtype}: {x.Key} at {x.Value.TemplatePath}]"));
            return $"[{Templates.Count}:{aggregate}]";
        }
    }
}
