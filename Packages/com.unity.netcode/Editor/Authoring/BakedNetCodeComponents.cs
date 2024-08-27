using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Unity.Entities;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.NetCode.Editor
{
    /// <summary>Internal structs used by the GhostComponentInspector to store post-conversion (i.e. Baked) data.</summary>
    class BakedResult
    {
        public Dictionary<GameObject, BakedGameObjectResult> GameObjectResults;
        public GhostAuthoringComponent GhostAuthoring;

        public BakedGameObjectResult GetInspectionResult(GhostAuthoringInspectionComponent inspection)
        {
            foreach (var kvp in GameObjectResults)
            {
                if (kvp.Value.SourceInspection == inspection)
                    return kvp.Value;
            }
            return null;
        }
    }

    class BakedGameObjectResult
    {
        public BakedResult AuthoringRoot;
        public GameObject SourceGameObject;
        [CanBeNull] public GhostAuthoringInspectionComponent SourceInspection;
        public GhostAuthoringComponent RootAuthoring => AuthoringRoot.GhostAuthoring;
        public string SourcePrefabPath;
        public List<BakedEntityResult> BakedEntities;
        public int NumComponents;
    }

    /// <inheritdoc cref="BakedGameObjectResult"/>
    class BakedEntityResult
    {
        public BakedGameObjectResult GoParent;
        public Entity Entity;
        public EntityGuid Guid;
        public string EntityName;
        public int EntityIndex;
        public bool IsPrimaryEntity => EntityIndex == 0;
        public List<BakedComponentItem> BakedComponents;
        public bool IsLinkedEntity;
        public bool IsRoot => !IsLinkedEntity && GoParent.SourceGameObject == GoParent.RootAuthoring.gameObject && IsPrimaryEntity;
    }

    /// <inheritdoc cref="BakedGameObjectResult"/>
    class BakedComponentItem
    {
        public BakedEntityResult EntityParent;
        public string fullname;
        public Type managedType;
        /// <summary>Determined by the ComponentOverride (fallback is <see cref="defaultSerializationStrategy"/>).</summary>
        public ComponentTypeSerializationStrategy serializationStrategy;
        /// <summary>Cache the default variant so we can mark it up as such in the Inspection UI.</summary>
        public ComponentTypeSerializationStrategy defaultSerializationStrategy;
        /// <summary>Lists all strategies available to this baked component.</summary>
        public ComponentTypeSerializationStrategy[] availableSerializationStrategies;
        public string[] availableSerializationStrategyDisplayNames;

        public int entityIndex;
        public EntityGuid entityGuid => EntityParent.Guid;
        public bool anyVariantIsSerialized;
        public SendToOwnerType sendToOwnerType;

        public GhostPrefabType PrefabType => HasPrefabOverride() && GetPrefabOverride().IsPrefabTypeOverriden
            ? GetPrefabOverride().PrefabType
            : serializationStrategy.PrefabType;

        public GhostSendType SendTypeOptimization =>
            HasPrefabOverride() && GetPrefabOverride().IsSendTypeOptimizationOverriden
                ? GetPrefabOverride().SendTypeOptimization
                : serializationStrategy.SendTypeOptimization;

        public ulong VariantHash
        {
            get
            {
                if (HasPrefabOverride())
                {
                    ref var componentOverride = ref GetPrefabOverride();
                    if (componentOverride.IsVariantOverriden)
                        return componentOverride.VariantHash;
                }
                return 0;
            }
        }

        /// <summary>
        /// Denotes if this type supports user modification of <see cref="ComponentTypeSerializationStrategy"/>.
        /// We obviously support it "implicitly" if we have multiple variant types.
        /// </summary>
        public bool DoesAllowVariantModification => serializationStrategy.HasDontSupportPrefabOverridesAttribute == 0 && serializationStrategy.IsInput == 0;

        /// <summary>
        /// Denotes if this type supports user modification of <see cref="SendTypeOptimization"/>.
        /// </summary>
        public bool DoesAllowSendTypeOptimizationModification => serializationStrategy.HasDontSupportPrefabOverridesAttribute == 0 && anyVariantIsSerialized && !serializationStrategy.IsDontSerializeVariant && EntityParent.GoParent.RootAuthoring.SupportsSendTypeOptimization && serializationStrategy.IsInput == 0;

        /// <summary>
        /// Denotes if this type supports user modification of <see cref="GhostAuthoringInspectionComponent.ComponentOverride.PrefabType"/>.
        /// </summary>
        public bool DoesAllowPrefabTypeModification => serializationStrategy.HasDontSupportPrefabOverridesAttribute == 0 && serializationStrategy.IsInput == 0;

        /// <summary>I.e. Implicitly supports prefab overrides.</summary>
        internal bool HasMultipleVariants => availableSerializationStrategies.Length > 1;

        internal bool HasMultipleVariantsExcludingDontSerializeVariant => HasMultipleVariants && availableSerializationStrategies.Count(x => !x.IsDontSerializeVariant) > 1;

        /// <summary>Returns by ref. Throws if not found. Use <see cref="HasPrefabOverride"/>.</summary>
        public ref GhostAuthoringInspectionComponent.ComponentOverride GetPrefabOverride()
        {
            if (EntityParent.GoParent.SourceInspection.TryFindExistingOverrideIndex(managedType, entityGuid, out var index))
                return ref EntityParent.GoParent.SourceInspection.ComponentOverrides[index];
            throw new InvalidOperationException($"No override created for '{fullname}'! '{serializationStrategy.ToFixedString()}', EntityGuid: {entityGuid.ToString()}!");
        }

        /// <summary>Returns true if this Inspection Component has a prefab override for this Baked Component Type.</summary>
        public bool HasPrefabOverride()
        {
            return EntityParent.GoParent.SourceInspection != null && EntityParent.GoParent.SourceInspection.TryFindExistingOverrideIndex(managedType, entityGuid, out _);
        }

        /// <summary>Returns the current override if it exists, or a new one, by ref.</summary>
        public ref GhostAuthoringInspectionComponent.ComponentOverride GetOrAddPrefabOverride()
        {
            var defaultPrefabType = (GhostPrefabType)GhostAuthoringInspectionComponent.ComponentOverride.NoOverride;
            EntityParent.GoParent.SourceInspection.GetOrAddPrefabOverride(managedType, entityGuid, defaultPrefabType);
            return ref GetPrefabOverride();
        }

        /// <summary>
        /// Called during initialization and whenever a variant is modified by the user.
        /// Ensures we actually save any custom variants if we need to.
        /// </summary>
        public void SaveVariant(bool warnIfChosenIsNotAlreadySaved, bool allowSettingDefaultToRevertOverride)
        {
            if (serializationStrategy.Hash != 0 && !VariantIsTheDefault && !HasPrefabOverride())
            {
                if(warnIfChosenIsNotAlreadySaved)
                    Debug.LogError($"Discovered on ghost '{EntityParent.GoParent.SourceGameObject.name}' that in-use variant ({serializationStrategy}) was not saved as a prefabOverride! Fixed.");

                GetOrAddPrefabOverride();
            }

            if (HasPrefabOverride())
            {
                ref var @override = ref GetPrefabOverride();
                var hash = (!@override.IsVariantOverriden || allowSettingDefaultToRevertOverride) && VariantIsTheDefault ? 0 : serializationStrategy.Hash;
                if (@override.VariantHash != hash)
                {
                    @override.VariantHash = hash;
                    EntityParent.GoParent.SourceInspection.SavePrefabOverride(ref @override, $"Confirmed Variant on {fullname} is {serializationStrategy}");
                }
            }
        }

        internal bool VariantIsTheDefault => serializationStrategy.Hash == defaultSerializationStrategy.Hash;

        /// <remarks>Note that this is an "override" action. Reverting to default is a different action.</remarks>
        public void TogglePrefabType(GhostPrefabType type)
        {
            var newValue = PrefabType ^ type;
            ref var @override = ref GetOrAddPrefabOverride();
            @override.PrefabType = newValue;
            EntityParent.GoParent.SourceInspection.SavePrefabOverride(ref @override, $"Toggled GhostPrefabType.{type} on {fullname}, set type flag to GhostPrefabType.{newValue}");
        }

        /// <remarks>Note that this is an "override" action. Reverting to default is a different action.</remarks>
        public void SetSendTypeOptimization(GhostSendType newValue)
        {
            ref var @override = ref GetOrAddPrefabOverride();
            @override.SendTypeOptimization = newValue;
            EntityParent.GoParent.SourceInspection.SavePrefabOverride(ref @override, $"Set GhostSendType.{newValue} on {fullname}, set value to GhostSendType.{newValue}");
        }

        public void RemoveEntirePrefabOverride(DropdownMenuAction action)
        {
            if (HasPrefabOverride())
            {
                serializationStrategy = defaultSerializationStrategy;
                ref var @override = ref GetPrefabOverride();
                @override.Reset();
                SaveVariant(false, true);
                EntityParent.GoParent.SourceInspection.SavePrefabOverride(ref @override, $"Removed entire prefab override on {fullname}");
            }
            else GhostAuthoringInspectionComponent.forceSave = true;
        }

        public void ResetPrefabTypeToDefault(DropdownMenuAction action)
        {
            if (HasPrefabOverride())
            {
                ref var @override = ref GetPrefabOverride();
                @override.PrefabType = (GhostPrefabType) GhostAuthoringInspectionComponent.ComponentOverride.NoOverride;
                EntityParent.GoParent.SourceInspection.SavePrefabOverride(ref @override, $"Reset PrefabType on {fullname}");
            }
        }

        public void ResetSendTypeToDefault(DropdownMenuAction action)
        {
            if (HasPrefabOverride())
            {
                ref var @override = ref GetPrefabOverride();
                @override.SendTypeOptimization = (GhostSendType) GhostAuthoringInspectionComponent.ComponentOverride.NoOverride;
                EntityParent.GoParent.SourceInspection.SavePrefabOverride(ref @override, $"Reset SendTypeOptimization on {fullname}");
            }
        }

        public void ResetVariantToDefault()
        {
            if (HasPrefabOverride())
            {
                serializationStrategy = defaultSerializationStrategy;
                SaveVariant(false, true);
            }
        }

        public override string ToString() => $"BakedComponentItem[{fullname} with {serializationStrategy}, {availableSerializationStrategies.Length} variants available, entityGuid: {entityGuid}]";
    }
}
