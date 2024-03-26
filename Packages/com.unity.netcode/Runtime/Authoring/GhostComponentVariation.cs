using System;

namespace Unity.NetCode
{
    /// <summary>
    /// <para>Generate a serialization variant for a component using the <seealso cref="GhostFieldAttribute"/> annotations
    /// present in variant declaration.
    /// The component variant can be assigned at authoring time using the GhostAuthoringComponent editor.</para>
    /// <para>Note: This is incompatible with any type implementing <see cref="DontSupportPrefabOverridesAttribute"/>.</para>
    /// <remarks>
    /// When declaring a variant, all fields that should be serialized must be declared. Any missing field or new field
    /// not present in the original struct will not be serialized.
    /// </remarks>
    /// </summary>
    [AttributeUsage(AttributeTargets.Struct)]
    public class GhostComponentVariationAttribute : Attribute
    {
        /// <summary>Type that this variant is overriding.  Assigned at construction time.</summary>
        public readonly Type ComponentType;

        /// <summary>
        /// User friendly, readable name for the variant. Used mainly for UI and logging purposes.
        /// If not assigned at construction time, the annotated class name will be used instead.
        /// "Default", "ClientOnly" and "DontSerialize" are not valid names and will be treated as null.
        /// </summary>
        /// <example>"Translation - 2D"</example>
        public string DisplayName { get; internal set; }

        /// <summary>
        /// The unique hash for the component variation.
        /// The hash is computed at compile time (assigned to the generated serialization class) and
        /// then assigned at runtime to the attribute when all the variant are
        /// registered by the <see cref="GhostComponentSerializerCollectionSystemGroup"/>.
        /// The hash itself is then used at both edit and runtime to identify the variation used for each component.
        /// </summary>
        public ulong VariantHash { get; internal set; }

        /// <summary>
        /// True if editor-only. Hides it in the user-facing dropdown.
        /// If true; we'll set this variant as the default in the editor, assuming we're unable to find a "proper" default.
        /// </summary>
        public bool IsTestVariant { get; internal set; }

        /// <summary>
        /// Initialize and declare the variant for a given component type.
        /// We can't constrain on a specific interface, thus, the validation is done at compile time in the constructor (for now).
        /// </summary>
        /// <param name="componentType"><see cref="ComponentType"/></param>
        /// <param name="displayName"><see cref="DisplayName"/></param>
        /// <param name="isTestVariant"><see cref="IsTestVariant"/></param>
        public GhostComponentVariationAttribute(Type componentType, string displayName = null, bool isTestVariant = false)
        {
            if (string.Equals(displayName, GhostVariantsUtility.k_DefaultVariantName, StringComparison.OrdinalIgnoreCase)
                || string.Equals(displayName, GhostVariantsUtility.k_DontSerializeVariant, StringComparison.OrdinalIgnoreCase)
                || string.Equals(displayName, GhostVariantsUtility.k_ClientOnlyVariant, StringComparison.OrdinalIgnoreCase))
                displayName = null;

            ComponentType = componentType;
            DisplayName = displayName;
            IsTestVariant = isTestVariant;
        }
    }
}
