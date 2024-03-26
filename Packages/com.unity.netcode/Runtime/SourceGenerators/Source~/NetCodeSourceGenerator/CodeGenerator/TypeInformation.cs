using System.Collections.Generic;
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;

namespace Unity.NetCode.Generators
{
    public enum GenTypeKind
    {
        Invalid,
        Primitive,
        Enum,
        Struct,
    }

    public enum ComponentType
    {
        Unknown = 0,
        Component,
        HybridComponent,
        Buffer,
        Rpc,
        CommandData,
        Input
    }

    // This is used internally in SG but needs to be kept in sync with the runtime netcode class in
    // Runtime/Authoring/GhostComponentAttribute.cs
    internal class GhostComponentAttribute
    {
        public GhostPrefabType PrefabType;
        public GhostSendType SendTypeOptimization;
        public SendToOwnerType OwnerSendType;
        public bool SendDataForChildEntity;

        public GhostComponentAttribute()
        {
            PrefabType = GhostPrefabType.All;
            SendTypeOptimization = GhostSendType.AllClients;
            OwnerSendType = SendToOwnerType.All;
            SendDataForChildEntity = false;
        }
    }

    /// <summary>
    /// A type descriptor, completely independent from roslyn types, used to generate serialization code for
    /// both ghosts and commands
    /// </summary>
    internal class TypeInformation
    {
#pragma warning disable 649
        public string Namespace;
        public string TypeFullName;
        //Only valid for type that support a different type of backend, like Enums. Return empty otherwise
        public string UnderlyingTypeName;
        //Only valid for field. Empty or null in all other cases
        public string FieldName;
        //Only valid for field. Empty or null in all other cases
        public string FieldTypeName;
        //Only valid for field. Empty or null in all other cases
        public string DeclaringTypeFullName;
        public GenTypeKind Kind;
        //This is valid for the root type and always NotApplicable for the members
        public ComponentType ComponentType;
        //Children can inherit and set attribute if they are set in the mask (by default: all)
        public TypeAttribute.AttributeFlags AttributeMask = TypeAttribute.AttributeFlags.All;
        public TypeAttribute Attribute;
        //Only applicable to root
        public GhostComponentAttribute GhostAttribute;
        public string Parent;
        public bool CanBatchPredict;
        public ITypeSymbol Symbol;
#pragma warning restore 649
        //The syntax tree and text span location of the type
        public Location Location;

        public List<TypeInformation> GhostFields = new List<TypeInformation>();
        public bool ShouldSerializeEnabledBit;
        public bool HasDontSupportPrefabOverridesAttribute;
        public bool IsTestVariant;

        public TypeDescription Description => new TypeDescription
        {
            TypeFullName = TypeFullName,
            Key = Kind == GenTypeKind.Enum ? UnderlyingTypeName : TypeFullName,
            Attribute = Attribute
        };

        public bool IsValid => Kind != GenTypeKind.Invalid;

        public override string ToString()
        {
            return $"{TypeFullName} (quantized={Attribute.quantization} composite={Attribute.aggregateChangeMask} smoothing={Attribute.smoothing} subtype={Attribute.subtype})";
        }
    }
}
