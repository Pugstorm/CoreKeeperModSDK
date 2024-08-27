using System;
using System.Collections.Generic;
using Unity.Entities;
using Unity.Collections;
#if ENABLE_UNITY_COLLECTIONS_CHECKS
using System.Reflection;
#endif

namespace Unity.NetCode
{
    /// <summary>
    /// <para>DefaultVariantSystemBase is an abstract base class that should be used to update the default variants in
    /// <see cref="GhostComponentSerializerCollectionData"/>, which contains what serialization variant to use
    /// (<see cref="GhostComponentVariationAttribute"/>) for certain type.
    /// A concrete implementation must implement the <see cref="RegisterDefaultVariants"/> method and add to the dictionary
    /// the desired type-variant pairs.</para>
    /// <para>The system must (and will be) created in both runtime and baking worlds. During baking, in particular,
    /// the <see cref="GhostComponentSerializerCollectionSystemGroup" /> is used by the `GhostAuthoringBakingSystem` to configure the ghost
    /// prefabs meta-data with the defaults values.</para>
    /// <para>The abstract base class already has the correct flags / update in world attributes set.
    /// It is not necessary for the concrete implementation to specify the flags, nor the <see cref="WorldSystemFilterAttribute"/>.</para>
    /// <para><b>CREATION FLOW </b></para>
    /// <para>
    /// All the default variant systems <b>must</b> be created after the <see cref="GhostComponentSerializerCollectionSystemGroup"/> (that is responsible
    /// to create the the default ghost variant mapping singleton). The `DefaultVariantSystemBase` already has the the correct <see cref="CreateAfterAttribute"/>
    /// set, and it is not necessary for the sub-class to add the explicitly add/set this creation order again.
    /// </para>
    /// </summary>
    /// <remarks>You may have multiple derived systems. They'll all be read from, and conflicts will output errors at bake time, and the latest values will be used.</remarks>
    [WorldSystemFilter(WorldSystemFilterFlags.ServerSimulation | WorldSystemFilterFlags.ClientSimulation |
                       WorldSystemFilterFlags.ThinClientSimulation | WorldSystemFilterFlags.BakingSystem)]
    [CreateAfter(typeof(GhostComponentSerializerCollectionSystemGroup))]
    [CreateBefore(typeof(DefaultVariantSystemGroup))]
    [UpdateInGroup(typeof(DefaultVariantSystemGroup))]
    public abstract partial class DefaultVariantSystemBase : SystemBase
    {
        /// <summary>When defining default variants for a type, you must denote whether or not this variant will be applied to both parents and children.</summary>
        public readonly struct Rule
        {
            /// <summary>The variant to use for all top-level (i.e. root/parent level) entities.</summary>
            /// <remarks>Parent entities default to send (i.e. serialize all "Ghost Fields" using the settings defined in the <see cref="GhostFieldAttribute"/>).</remarks>
            public readonly System.Type VariantForParents;

            /// <summary>The variant to use for all child entities.</summary>
            /// <remarks>Child entities default to <see cref="DontSerializeVariant"/> for performance reasons.</remarks>
            public readonly System.Type VariantForChildren;

            /// <summary>This rule will only add the variant to parent entities with this component type.
            /// Children with this component will remain <see cref="DontSerializeVariant"/> (which is the default for children).
            /// <b>This is the recommended approach.</b></summary>
            /// <param name="variantForParentOnly"></param>
            /// <returns></returns>
            public static Rule OnlyParents(Type variantForParentOnly) => new Rule(variantForParentOnly, default);

            /// <summary>This rule will add the same variant to all entities with this component type (i.e. both parent and children a.k.a. regardless of hierarchy).
            /// <b>Note: It is not recommended to serialize child entities as it is relatively slow to serialize them!</b></summary>
            /// <param name="variantForBoth"></param>
            /// <returns></returns>
            public static Rule ForAll(Type variantForBoth) => new Rule(variantForBoth, variantForBoth);

            /// <summary>This rule will add one variant for parents, and another variant for children, by default.
            /// <b>Note: It is not recommended to serialize child entities as it is relatively slow to serialize them!</b></summary>
            /// <param name="variantForParents"></param>
            /// <param name="variantForChildren"></param>
            /// <returns></returns>
            public static Rule Unique(Type variantForParents, Type variantForChildren) => new Rule(variantForParents, variantForChildren);

            /// <summary>This rule will only add this variant to child entities with this component.
            /// The parent entities with this component will use the default serializer.
            /// <b>Note: It is not recommended to serialize child entities as it is relatively slow to serialize them!</b></summary>
            /// <param name="variantForChildrenOnly"></param>
            /// <returns></returns>
            public static Rule OnlyChildren(Type variantForChildrenOnly) => new Rule(default, variantForChildrenOnly);

            /// <summary>Use the static builder methods instead!</summary>
            /// <param name="variantForParents"><inheritdoc cref="VariantForParents"/></param>
            /// <param name="variantForChildren"><inheritdoc cref="VariantForChildren"/></param>
            private Rule(Type variantForParents, Type variantForChildren)
            {
                VariantForParents = variantForParents;
                VariantForChildren = variantForChildren;
            }

            /// <summary>
            /// The Rule string representation. Print the parent and child variant types.
            /// </summary>
            /// <returns></returns>
            public override string ToString() => $"Rule[parents: `{VariantForParents}`, children: `{VariantForChildren}`]";

            /// <summary>
            /// Compare two rules ana check if their parent and child types are identical.
            /// </summary>
            /// <param name="other"></param>
            /// <returns></returns>
            public bool Equals(Rule other) => VariantForParents == other.VariantForParents && VariantForChildren == other.VariantForChildren;

            /// <summary>Unique HashCode if Variant fields are set.</summary>
            /// <returns></returns>
            public override int GetHashCode()
            {
                unchecked
                {
                    return ((VariantForParents != null ? VariantForParents.GetHashCode() : 0) * 397) ^ (VariantForChildren != null ? VariantForChildren.GetHashCode() : 0);
                }
            }

            internal HashRule CreateHashRule(ComponentType componentType) => new HashRule(TryGetHashElseZero(componentType, VariantForParents), TryGetHashElseZero(componentType, VariantForChildren));

            static ulong TryGetHashElseZero(ComponentType componentType, Type variantType)
            {
                if (variantType == null)
                    return 0;
                if (variantType == typeof(DontSerializeVariant))
                    return GhostVariantsUtility.DontSerializeHash;
                if (variantType == typeof(ClientOnlyVariant))
                    return GhostVariantsUtility.ClientOnlyHash;
                if (variantType == typeof(ServerOnlyVariant))
                    return GhostVariantsUtility.ServerOnlyHash;
                return GhostVariantsUtility.UncheckedVariantHash(variantType.FullName, componentType);
            }
        }

        /// <summary>Hash version of <see cref="Rule"/> to allow it to be BurstCompatible.</summary>
        internal readonly struct HashRule
        {
            /// <summary>Hash version of <see cref="Rule.VariantForParents"/>.</summary>
            public readonly ulong VariantForParents;
            /// <summary>Hash version of <see cref="Rule.VariantForChildren"/>.</summary>
            public readonly ulong VariantForChildren;

            public HashRule(ulong variantForParents, ulong variantForChildren)
            {
                VariantForParents = variantForParents;
                VariantForChildren = variantForChildren;
            }

            public override string ToString() => $"HashRule[parent: `{VariantForParents}`, children: `{VariantForChildren}`]";

            public bool Equals(HashRule other) => VariantForParents == other.VariantForParents && VariantForChildren == other.VariantForChildren;

        }

        protected sealed override void OnCreate()
        {
            //A dictionary of ComponentType -> Type is not sufficient to guarantee correctness.
            //Some sanity check here are necessary
            var defaultVariants = new Dictionary<ComponentType, Rule>();
            RegisterDefaultVariants(defaultVariants);

            var ghostComponentSerializerCollection = World.GetExistingSystemManaged<GhostComponentSerializerCollectionSystemGroup>();

#if ENABLE_UNITY_COLLECTIONS_CHECKS
            var cache = ghostComponentSerializerCollection.ghostComponentSerializerCollectionDataCache;
            cache.ThrowIfNotInRegistrationPhase($"register `DefaultVariantSystemBase` child system `{GetType().Name}` in '{World.Name}'");
#endif
            var variantRules = ghostComponentSerializerCollection.DefaultVariantRules;
            foreach (var rule in defaultVariants)
                variantRules.SetDefaultVariant(rule.Key, rule.Value, this);
            Enabled = false;
        }

        protected sealed override void OnUpdate()
        {
        }

        /// <summary>
        /// Implement this method by adding to the <param name="defaultVariants"></param> mapping your
        /// default type->variant <seealso cref="Rule"/>
        /// </summary>
        /// <param name="defaultVariants"></param>
        protected abstract void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants);
    }

    /// <summary>
    /// Store the default component type -> ghost variant mapping (see <see cref="GhostComponentVariationAttribute"/>).
    /// Used by systems implementing the abstract <see cref="DefaultVariantSystemBase"/>.
    /// </summary>
    internal class GhostVariantRules
    {
        public struct RuleAssignment
        {
            public DefaultVariantSystemBase.Rule Rule;
            public SystemBase LastSystem;
        }
        private NativeHashMap<ComponentType, DefaultVariantSystemBase.HashRule> DefaultVariants;

#if ENABLE_UNITY_COLLECTIONS_CHECKS || NETCODE_DEBUG
        //Used for debug purpose, track the latest assigned rule by each system. That help tracking
        //down who is overwriting the the default rule, in case multiple systems responsible for assigning the default variants exists
        //in the project.
        private readonly Dictionary<ComponentType, RuleAssignment> DefaultVariantsManaged;
#endif

        public GhostVariantRules(NativeHashMap<ComponentType, DefaultVariantSystemBase.HashRule> defaultVariants)
        {
            DefaultVariants = defaultVariants;
#if ENABLE_UNITY_COLLECTIONS_CHECKS || NETCODE_DEBUG
            DefaultVariantsManaged = new Dictionary<ComponentType, RuleAssignment>(32);
#endif
        }

        /// <summary>
        /// Set the current <see cref="GhostComponentVariationAttribute"/> variant to use by default
        /// for the given component type.
        /// <para>If an entry for the component is already preent, the new <paramref name="rule"/> will overwrite the current
        /// assignment </para>
        /// </summary>
        /// <param name="componentType">The component type for which you want to specify the variant to use.</param>
        /// <param name="rule">The rule to assign.</param>
        /// <param name="currentSystem">The system that want to assign the rule. Used almost for debugging purpose</param>
        /// <returns></returns>
        public bool TrySetDefaultVariant(ComponentType componentType, DefaultVariantSystemBase.Rule rule, SystemBase currentSystem)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ValidateVariantRule(componentType, rule, currentSystem);
#endif
            var added = DefaultVariants.TryAdd(componentType, rule.CreateHashRule(componentType));
#if ENABLE_UNITY_COLLECTIONS_CHECKS || NETCODE_DEBUG
            if (added)
                DefaultVariantsManaged[componentType] = new RuleAssignment { Rule = rule, LastSystem = currentSystem };
#endif
            return added;
        }

        /// <summary>
        /// Will set the current <see cref="GhostComponentVariationAttribute"/> variant to use by default
        /// for the given component type if a rule for the <paramref name="componentType"/> is not already present.
        /// </summary>
        /// <param name="componentType">The component type for which you want to specify the variant to use.</param>
        /// <param name="rule">The rule to assign.</param>
        /// <param name="currentSystem">The system that want to assign the rule. Used almost for debugging purpose</param>
        /// <returns></returns>
        public void SetDefaultVariant(ComponentType componentType, DefaultVariantSystemBase.Rule rule, SystemBase currentSystem)
        {
#if ENABLE_UNITY_COLLECTIONS_CHECKS
            ValidateVariantRule(componentType, rule, currentSystem);
#endif
            var newRuleHash = rule.CreateHashRule(componentType);
#if ENABLE_UNITY_COLLECTIONS_CHECKS || NETCODE_DEBUG
            if (DefaultVariantsManaged.TryGetValue(componentType, out var existingRule))
            {
                var rulesAreTheSame = existingRule.Rule.Equals(rule);
                if (!rulesAreTheSame)
                {
                    UnityEngine.Debug.Log($"`Overriding the default variant rule for type `{componentType.ToFixedString()}` with '{rule}' ('{newRuleHash}'). Previous rule was " +
                                          $"('{existingRule.Rule}' ('{existingRule.Rule.CreateHashRule(componentType)}'), setup by {TypeManager.GetSystemName(existingRule.LastSystem.GetType())}." +
                                          $"In your implementation of DefaultVariantSystemBase use [CreateBefore(typeof({TypeManager.GetSystemName(existingRule.LastSystem.GetType())}))] to resolve this issue.");
                }
            }
            DefaultVariantsManaged[componentType] = new RuleAssignment{Rule = rule, LastSystem = currentSystem};
#endif
            DefaultVariants[componentType] = newRuleHash;
        }

#if ENABLE_UNITY_COLLECTIONS_CHECKS
        void ValidateVariantRule(ComponentType componentType, DefaultVariantSystemBase.Rule rule, ComponentSystemBase systemBase)
        {
            if (rule.VariantForParents == default && rule.VariantForChildren == default)
                throw new System.ArgumentException($"`{componentType}` has an invalid default variant rule ({rule}) defined in `{TypeManager.GetSystemName(systemBase.GetType())}` (in '{systemBase.World.Name}'), as both are `null`!");

            var managedType = componentType.GetManagedType();
            if (typeof(InputBufferData<>).IsAssignableFrom(managedType))
                throw new System.ArgumentException($"`{managedType}` is of type `IInputBufferData`, which must get its default variants from the `IInputComponentData` that it is code-generated from. Replace this dictionary entry ({rule}) with the `IInputComponentData` type in system `{TypeManager.GetSystemName(systemBase.GetType())}`, in '{systemBase.World.Name}'!");

            ValidateUserDefinedDefaultVariantRule(componentType, rule.VariantForParents, systemBase);
            ValidateUserDefinedDefaultVariantRule(componentType, rule.VariantForChildren, systemBase);
        }

        void ValidateUserDefinedDefaultVariantRule(ComponentType componentType, Type variantType, ComponentSystemBase systemBase)
        {
            // Nothing to validate if the variant is the "default serializer".
            if (variantType == default || variantType == componentType.GetManagedType())
                return;

            var isInput = typeof(ICommandData).IsAssignableFrom(componentType.GetManagedType());
            if (variantType == typeof(ClientOnlyVariant) || variantType == typeof(ServerOnlyVariant) || variantType == typeof(DontSerializeVariant))
            {
                if (isInput)
                    throw new System.ArgumentException($"System `{GetType().FullName}` is attempting to set a default variant for an `ICommandData` type: `{componentType}`, but the type of the variant is `{variantType.FullName}`! Ensure you use a serialized variant with `GhostPrefabType.All`!");
                return;
            }

            var variantAttr = variantType.GetCustomAttribute<GhostComponentVariationAttribute>();
            if (variantAttr == null)
                throw new System.ArgumentException($"Invalid type registered as default variant. GhostComponentVariationAttribute not found for type `{variantType.FullName}`, cannot use it as the default variant for `{componentType}`! Defined in system `{TypeManager.GetSystemName(systemBase.GetType())}`!");

            var managedType = componentType.GetManagedType();
            if (variantAttr.ComponentType != managedType)
                throw new System.ArgumentException($"`{variantType.FullName}` is not a variation of component `{componentType}`, cannot use it as a default variant in system `{TypeManager.GetSystemName(systemBase.GetType())}`!");
        }
#endif
    }
}
