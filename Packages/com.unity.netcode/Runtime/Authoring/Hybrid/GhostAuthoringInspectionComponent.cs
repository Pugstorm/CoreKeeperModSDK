using System;
using System.Collections.Generic;
using Unity.Entities;
using UnityEngine;
using UnityEngine.Serialization;

namespace Unity.NetCode
{
    /// <summary>
    /// <para>MonoBehaviour you may optionally add to any/all GameObjects in a Ghost Prefab, which allows inspecting of (and saving of) "Ghost Meta Data". E.g.</para>
    /// <para> - Override/Tweak some of the component replication properties, for both child and root entities.</para>
    /// <para> - Assign to each component which <see cref="GhostComponentVariationAttribute">variant</see> to use.</para>
    /// <seealso cref="GhostAuthoringComponent"/>
    /// </summary>
    [DisallowMultipleComponent]
    [HelpURL(Authoring.HelpURLs.GhostAuthoringInspetionComponent)]
    public class GhostAuthoringInspectionComponent : MonoBehaviour
    {
        // TODO: This doesn't support multi-edit.
        internal static bool forceBake;
        internal static bool forceRebuildInspector = true;
        internal static bool forceSave;

        /// <summary>
        /// List of all saved modifications that the user has applied to this entity.
        /// If not set, defaults to whatever Attribute values the user has setup on each <see cref="GhostInstance"/>.
        /// </summary>
        [FormerlySerializedAs("m_ComponentOverrides")]
        [SerializeField]
        internal ComponentOverride[] ComponentOverrides = Array.Empty<ComponentOverride>();

        ///<summary>Not the fastest way but on average is taking something like 10-50us or less to find the type,
        ///so seem reasonably fast even with tens of components per prefab.</summary>
        static Type FindTypeFromFullTypeNameInAllAssemblies(string fullName)
        {
            // TODO - Consider using the TypeManager.
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            {
                var type = a.GetType(fullName, false);
                if (type != null)
                    return type;
            }
            return default;
        }

        [ContextMenu("Force Re-Bake Prefab")]
        void ForceBake()
        {
            forceBake = true;
            forceRebuildInspector = true;
        }

        /// <summary>Notifies of all invalid overrides.</summary>
        internal void LogErrorIfComponentOverrideIsInvalid()
        {
            for (var i = 0; i < ComponentOverrides.Length; i++)
            {
                ref var mod = ref ComponentOverrides[i];
                var compType = FindTypeFromFullTypeNameInAllAssemblies(mod.FullTypeName);
                if (compType == null)
                {
                    Debug.LogError($"Ghost Prefab '{name}' has an invalid 'Component Override' targeting an unknown component type '{mod.FullTypeName}'. " +
                                   "If this type has been renamed, you will unfortunately need to manually re-add this override. If it has been deleted, simply re-commit this prefab.");
                }
            }
        }

        /// <remarks>Note that this operation is not saved. Ensure you call <see cref="SavePrefabOverride"/>.</remarks>
        internal ref ComponentOverride GetOrAddPrefabOverride(Type managedType, EntityGuid entityGuid, GhostPrefabType defaultPrefabType)
        {
            if (!gameObject || !this)
                throw new ArgumentException($"Attempting to GetOrAddPrefabOverride for entityGuid '{entityGuid}' to '{this}', but GameObject and/or InspectionComponent has been destroyed!");

            if (gameObject.GetInstanceID() != entityGuid.OriginatingId && !TryGetFirstMatchingGameObjectInChildren(gameObject.transform, entityGuid, out _))
            {
                throw new ArgumentException($"Attempting to GetOrAddPrefabOverride for entityGuid '{entityGuid}' to '{this}', but entityGuid does not match our gameObject, nor our children!");
            }

            if (TryFindExistingOverrideIndex(managedType, entityGuid, out var index))
            {
                return ref ComponentOverrides[index];
            }

            // Did not find, so add:
            ref var found = ref AddComponentOverrideRaw();
            found = new ComponentOverride
            {
                EntityIndex = entityGuid.b,
                FullTypeName = managedType.FullName,
            };
            found.Reset();
            found.PrefabType = defaultPrefabType;
            return ref found;
        }

        internal ref ComponentOverride AddComponentOverrideRaw()
        {
            Array.Resize(ref ComponentOverrides, ComponentOverrides.Length + 1);
            return ref ComponentOverrides[ComponentOverrides.Length - 1];
        }

        /// <summary>Saves this component override. Attempts to remove it if it's default.</summary>
        internal void SavePrefabOverride(ref ComponentOverride componentOverride, string reason)
        {
            forceSave = true;

            // Remove the override entirely if its no longer overriding anything.
            if (!componentOverride.HasOverriden)
            {
                var index = FindExistingOverrideIndex(ref componentOverride);
                RemoveComponentOverrideByIndex(index);
            }
        }

        /// <summary>Replaces this element with the last, then resizes -1.</summary>
        /// <param name="index">Index to remove.</param>
        internal void RemoveComponentOverrideByIndex(int index)
        {
            if (ComponentOverrides.Length == 0) return;
            if (index < ComponentOverrides.Length - 1)
            {
                ComponentOverrides[index] = ComponentOverrides[ComponentOverrides.Length - 1];
            }
            Array.Resize(ref ComponentOverrides, ComponentOverrides.Length - 1);
        }

        int FindExistingOverrideIndex(ref ComponentOverride currentOverride)
        {
            for (int i = 0; i < ComponentOverrides.Length; i++)
            {
                if (string.Equals(ComponentOverrides[i].FullTypeName, currentOverride.FullTypeName, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
            throw new InvalidOperationException("Unable to find index of override, which should be impossible as we're passing currentOverride by ref!");
        }

        /// <summary>Does a depth first search to find an element in the transform hierarchy matching this EntityGuid.</summary>
        /// <param name="current">Root element to search from.</param>
        /// <param name="entityGuid">Query: First to match with this EntityGuid.</param>
        /// <param name="foundGameObject">First element matching the query. Will be set to null otherwise.</param>
        /// <returns>True if found.</returns>
        static bool TryGetFirstMatchingGameObjectInChildren(Transform current, EntityGuid entityGuid, out GameObject foundGameObject)
        {
            if (current.gameObject.GetInstanceID() == entityGuid.OriginatingId)
            {
                foundGameObject = current.gameObject;
                return true;
            }

            if (current.childCount == 0)
            {
                foundGameObject = null;
                return false;
            }

            for (int i = 0; i < current.childCount; i++)
            {
                var child = current.GetChild(i);
                if (TryGetFirstMatchingGameObjectInChildren(child, entityGuid, out foundGameObject))
                {
                    return true;
                }
            }
            foundGameObject = null;
            return false;
        }

        /// <summary>Finds all <see cref="GhostAuthoringInspectionComponent"/>'s on this Ghost Authoring Prefab (including in children), and adds all <see cref="ComponentOverrides"/> to a single list.</summary>
        /// <param name="ghostAuthoring">Root prefab to search from.</param>
        /// <param name="validate"></param>
        internal static List<(GameObject, ComponentOverride)> CollectAllComponentOverridesInInspectionComponents(GhostAuthoringComponent ghostAuthoring, bool validate)
        {
            var inspectionComponents = CollectAllInspectionComponents(ghostAuthoring);
            var allComponentOverrides = new List<(GameObject, ComponentOverride)>(inspectionComponents.Count * 4);
            foreach (var inspectionComponent in inspectionComponents)
            {
                if(validate)
                    inspectionComponent.LogErrorIfComponentOverrideIsInvalid();

                foreach (var componentOverride in inspectionComponent.ComponentOverrides)
                {
                    allComponentOverrides.Add((inspectionComponent.gameObject, componentOverride));
                }
            }

            return allComponentOverrides;
        }

        internal static List<GhostAuthoringInspectionComponent> CollectAllInspectionComponents(GhostAuthoringComponent ghostAuthoring)
        {
            var inspectionComponents = new List<GhostAuthoringInspectionComponent>(8);
            ghostAuthoring.gameObject.GetComponents(inspectionComponents);
            ghostAuthoring.GetComponentsInChildren(inspectionComponents);
            return inspectionComponents;
        }

        /// <summary>Saved override values.</summary>
        [Serializable]
        internal struct ComponentOverride : IComparer<ComponentOverride>, IComparable<ComponentOverride>
        {
            public const int NoOverride = -1;

            ///<summary>
            /// For sake of serialization we are using the type fullname because we can't rely on the TypeIndex for the component.
            /// StableTypeHash cannot be used either because layout or fields changes affect the hash too (so is not a good candidate for that).
            /// </summary>
            public string FullTypeName;

            ///<summary>The entity guid index reference.</summary>
            [FormerlySerializedAs("EntityGuid")] public ulong EntityIndex;

            ///<summary>Override what modes are available for that type. If `None`, this component is removed from the prefab/entity instance.</summary>
            /// <remarks>Note that <see cref="VariantHash"/> can clobber this value.</remarks>
            public GhostPrefabType PrefabType;

            ///<summary>Override which client type it will be sent to, if we're able to determine.</summary>
            [FormerlySerializedAs("OwnerPredictedSendType")]
            public GhostSendType SendTypeOptimization;

            ///<summary>Select which variant we would like to use. 0 means the default.</summary>
            public ulong VariantHash;

            /// <summary>Flag denoting that this ComponentOverride is known, and properly configured.</summary>
            [NonSerialized]public bool DidCorrectlyMap;

            public bool HasOverriden => IsPrefabTypeOverriden || IsSendTypeOptimizationOverriden || IsVariantOverriden;

            public bool IsPrefabTypeOverriden => (int)PrefabType != NoOverride;

            public bool IsSendTypeOptimizationOverriden => (int)SendTypeOptimization != NoOverride;

            public bool IsVariantOverriden => VariantHash != 0;

            public void Reset()
            {
                PrefabType = (GhostPrefabType)NoOverride;
                SendTypeOptimization = (GhostSendType)NoOverride;
                VariantHash = 0;
            }

            public override string ToString()
            {
                return $"ComponentOverride['{FullTypeName}', EntityIndex:'{EntityIndex}', prefabType:{PrefabType}, sto:{SendTypeOptimization}, variantH:{VariantHash}]";
            }

            public int Compare(ComponentOverride x, ComponentOverride y)
            {
                var fullTypeNameComparison = string.Compare(x.FullTypeName, y.FullTypeName, StringComparison.Ordinal);
                if (fullTypeNameComparison != 0) return fullTypeNameComparison;
                var entityGuidComparison = x.EntityIndex.CompareTo(y.EntityIndex);
                return entityGuidComparison != 0 ? entityGuidComparison : x.VariantHash.CompareTo(y.VariantHash);
            }

            public int CompareTo(ComponentOverride other)
            {
                return Compare(this, other);
            }
        }

        internal bool TryFindExistingOverrideIndex(Type managedType, in EntityGuid guid, out int index)
        {
            var managedTypeFullName = managedType.FullName;
            return TryFindExistingOverrideIndex(managedTypeFullName, guid.b, out index);
        }

        internal bool TryFindExistingOverrideIndex(string managedTypeFullName, in ulong entityGuid, out int index)
        {
            for (index = 0; index < ComponentOverrides.Length; index++)
            {
                ref var componentOverride = ref ComponentOverrides[index];
                if (componentOverride.EntityIndex == entityGuid && string.Equals(componentOverride.FullTypeName, managedTypeFullName, StringComparison.OrdinalIgnoreCase))
                {
                    componentOverride.DidCorrectlyMap = true;
                    return true;
                }
            }
            index = -1;
            return false;
        }
    }
}
