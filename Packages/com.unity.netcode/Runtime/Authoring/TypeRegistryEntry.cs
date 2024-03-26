namespace Unity.NetCode.Generators
{
    /// <summary>
    /// Used to configure the serialization/deserialization code-generation for a specific type (primitive or struct) and
    /// combination of <see cref="GhostFieldAttribute"/> quantized, smooting and sub-type flags.
    /// The tuple [<see cref="Type"/>, <see cref="Quantized"/>, <see cref="Smoothing"/>, <see cref="SubType"/>] is mapped to
    /// a template file that contains the code to use to serialize/deserialize this specific type.
    /// It is possible so to register for each individual type multiple serialization rules, that can be selected using the
    /// <see cref="GhostFieldAttribute"/>.
    /// For example, the default float type (subtype 0) has 4 different serialization rules:
    /// <para>(float, unquantized, Clamp, 0)</para>
    /// <para>(float, unquantized, InterpolateAndExtrapolate, 0)</para>
    /// <para>(float, quantized, Clamp, 0)</para>
    /// <para>(float, quantized, InterpolateAndExtrapolate)</para>
    /// </summary>
    public class TypeRegistryEntry
    {
        /// <summary>
        /// Mandatory, the qualified typename of the type (namespace + type name).
        /// </summary>
        public string Type;
        /// <summary>
        /// Mandatory, the template file to use. Must be relative path to the Asset or Package folder.
        /// </summary>
        public string Template;
        /// <summary>
        /// Optional, the template file to use to overrides/change the serializaton code present in the base <see cref="Template"/> file.
        /// Must be relative path to the Asset or Package folder.
        /// </summary>
        public string TemplateOverride;
#pragma warning disable 649
        /// <summary>
        /// The sub-type value for this specific type=template combination. This is used to map the
        /// [type, Quantized, Smooting, Suptype] tuple specified by the <see cref="GhostFieldAttribute"/>
        /// properties to the correct serializer type.
        /// </summary>
        public int SubType;
#pragma warning restore 649
        /// <summary>
        /// The smoothing supported by this template and type combination.
        /// </summary>
        public SmoothingAction Smoothing;
        /// <summary>
        /// floating point number can be serialized in two ways:
        /// <para>- as a full 32bit raw value</para>
        /// <para>- as a fixed-point number, with a given precision (see <see cref="GhostFieldAttribute.Quantization"/>)</para>
        /// The use of quantization requires special handling by the code-generation and in particular the code in the template file
        /// must uses certain rules.
        /// You should set this flag to true if the type-template combination should be used for quantized types.
        /// </summary>
        public bool Quantized;
        /// <summary>
        /// State if the type, template pairs can be used when serializing commands.
        /// </summary>
        public bool SupportCommand;
        /// <summary>
        /// State if the type, template pair is a composite type. Must be used only for structs that contains multiple fields
        /// of the same type (ex: float3). Whan a type is configured as composite,
        /// the <see cref="Template"/> model is used recursively on all the fields to generate the serialization code, without
        /// the need to crate a specific template for the struct itself.
        /// </summary>
        public bool Composite;
    }
}
