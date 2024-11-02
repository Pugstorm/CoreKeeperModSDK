using System;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Editor.Bridge;
using Unity.Mathematics;
using Unity.Scenes.Editor;
using Unity.Transforms;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Editor
{
    [InitializeOnLoad]
    static class BindingRegistryLiveProperties
    {
        static readonly TypeIndex s_LocalTransformTypeIndex = TypeManager.GetTypeIndex<LocalTransform>();

        static BindingRegistryLiveProperties()
        {
            LivePropertyBridge.EnableLivePropertyFeatureGlobally(true);
            RegisterBindingsFromBuiltinTypes();

            foreach (var b in BindingRegistry.s_AuthoringToRuntimeBinding)
            {
                var binding = new BindingCache(b.Key);
                LivePropertyBridge.AddLivePropertyChanged(b.Key, binding.ShouldUpdateLiveProperties);
                LivePropertyBridge.AddLivePropertyOverride(b.Key, binding.UpdateLiveProperties);
            }
        }

        struct DiffingContext
        {
            public EntityManager LiveEntityManager;
            public EntityManager ConvertedEntityManager;
            public Entity        LiveEntity;
            public Entity        ConvertedEntity;

            public unsafe bool HasChanged(TypeIndex typeIndex)
            {
                var type = TypeManager.GetType(typeIndex);
                if (!LiveEntityManager.HasComponent(LiveEntity, type))
                    return false;
                if (!ConvertedEntityManager.HasComponent(ConvertedEntity, type))
                    return false;
                var liveData = LiveEntityManager.GetComponentDataRawRO(LiveEntity, typeIndex);
                var convertedData = ConvertedEntityManager.GetComponentDataRawRO(ConvertedEntity, typeIndex);

                int res = UnsafeUtility.MemCmp(liveData, convertedData, UnsafeUtility.SizeOf(type));
                return res != 0;
            }
        }

        internal class BindingCache
        {
            readonly Type m_authoringType;

            public BindingCache(Type authoringType)
            {
                m_authoringType = authoringType;
            }

            // Gets the diff context (Live/Converted entities, entity managers) for a given GameObject
            bool GetDiffingContext(GameObject obj, EntitySelectionProxy proxy, out DiffingContext diffingContext)
            {
                diffingContext = default;

                if (!EntitySelectionProxy.FindPrimaryEntity(obj, proxy, out var world, out var liveEntity))
                    return false;

                var liveConversionSystem = world.GetExistingSystemManaged<EditorSubSceneLiveConversionSystem>();
                if (liveConversionSystem == null)
                    return false;

                var entityManager = world.EntityManager;
                var convertedWorld = liveConversionSystem.GetConvertedWorldForEntity(liveEntity);
                if (convertedWorld == null)
                    return false;

                var convertedEntityManager = convertedWorld.EntityManager;
                var convertedEntity = convertedEntityManager.Debug.GetPrimaryEntityForAuthoringObject(obj);

                if (convertedEntity == Entity.Null)
                    return false;

                diffingContext.LiveEntityManager = entityManager;
                diffingContext.LiveEntity = liveEntity;
                diffingContext.ConvertedEntityManager = convertedEntityManager;
                diffingContext.ConvertedEntity = convertedEntity;

                return true;
            }

            // Returns true if the authoring components (unityObjects) needs to be live updated because their runtime value has changed since conversion.
            // Returns false if the authoring components are not registered in the binding registry,
            //  or if there is no conversion between the authoring/runtime components,
            //  or if the runtime values are not different from the authoring ones
            public bool ShouldUpdateLiveProperties(UnityEngine.Object[] authoringObjects)
            {
                foreach (var authoringObject in authoringObjects)
                {
                    var gameObject = ((Component)authoringObject).gameObject;
                    var authoringComponent = (Component) authoringObject;

                    if (!BindingRegistry.s_AuthoringToRuntimeBinding.ContainsKey(authoringComponent.GetType()))
                        return false;

                    if (GetDiffingContext(gameObject, Selection.activeObject as EntitySelectionProxy, out var diffingContext))
                    {
                        var runtimeDataBinding = BindingRegistry.s_AuthoringToRuntimeBinding[authoringComponent.GetType()];
                        if (runtimeDataBinding.Count > 0)
                        {
                            var runtimeTypeTypeIndex = runtimeDataBinding[0].ComponentTypeIndex;
                            var runtimeType = ComponentType.FromTypeIndex(runtimeTypeTypeIndex);

                            if (!diffingContext.LiveEntityManager.HasComponent(diffingContext.LiveEntity, runtimeType))
                            {
                                if (runtimeType != typeof(LocalTransform))
                                {
                                    // Detect missing/failed conversion between the authoring and runtime type
                                    Debug.LogError($"Can't update live properties on the authoring component {m_authoringType}." +
                                                   $"Because the runtime component {runtimeType} is missing on the primary entity. It looks like conversion didn't run on {m_authoringType}.");
                                    return false;
                                }
                            }

                            foreach (var data in runtimeDataBinding)
                            {
                                if (diffingContext.HasChanged(data.ComponentTypeIndex))
                                    return true;
                            }
                        }
                    }
                }
                return false;
            }

            // Updates the properties of the serializedObject of the main targetObject that have been registered in the binding registry with runtime values from the primary entity.
            // isLiveUpdate==true means live property will be set with runtime value.
            // isLiveUpdate==false means live property value has been changed directly, not by override, we need to update runtime value with it.
            public unsafe void UpdateLiveProperties(SerializedObject serializedObject, bool isLiveUpdate)
            {
                var component = serializedObject.targetObject as Component;
                if (component == null)
                    return;

                var gameObject = component.gameObject;
                if (gameObject == null)
                    return;

                if (!BindingRegistry.s_AuthoringToRuntimeBinding.ContainsKey(m_authoringType))
                    return;

                if (!EntitySelectionProxy.FindPrimaryEntity(gameObject, Selection.activeObject as EntitySelectionProxy, out var world, out var liveEntity))
                    return;

                var runtimeDataBinding = BindingRegistry.s_AuthoringToRuntimeBinding[m_authoringType];
                if (runtimeDataBinding.Count <= 0)
                    return;

                for (var i = 0; i != runtimeDataBinding.Count; i++)
                {
                    var binding = runtimeDataBinding[i];
                    var runtimeTypeIndex = runtimeDataBinding[i].ComponentTypeIndex;

                    // For entities that do not have LocalTransform components, just skip them.
                    if (runtimeTypeIndex == s_LocalTransformTypeIndex && !world.EntityManager.HasComponent<LocalTransform>(liveEntity))
                        continue;

                    var prop = serializedObject.FindProperty(binding.AuthoringFieldName);
                    if (prop == null)
                    {
                        Debug.LogWarning($"Skipping the update of the property field: {binding.AuthoringFieldName}. " +
                                         $"The serialized property to override can't be found on the component: {m_authoringType}. " +
                                         $"The {BindingRegistry.s_AuthoringToRuntimeBinding} binding cache might be out of sync.");
                        continue;
                    }

                    var ptr = (byte*) world.EntityManager.GetComponentDataRawRO(liveEntity, runtimeTypeIndex);
                    ptr += binding.FieldProperties.FieldOffset;

                    // TODO: Support more types
                    switch (prop.propertyType)
                    {
                        case SerializedPropertyType.Integer:
                            if (binding.FieldProperties.FieldType == typeof(int))
                                if (isLiveUpdate)
                                    prop.intValue = UnsafeUtility.AsRef<int>(ptr);
                                else
                                    UnsafeUtility.AsRef<int>(ptr) = prop.intValue;
                            break;
                        case SerializedPropertyType.Boolean:
                            if (binding.FieldProperties.FieldType == typeof(bool))
                                if (isLiveUpdate)
                                    prop.boolValue = UnsafeUtility.AsRef<bool>(ptr);
                                else
                                    UnsafeUtility.AsRef<bool>(ptr) = prop.boolValue;
                            break;
                        case SerializedPropertyType.Float:
                            if (binding.FieldProperties.FieldType == typeof(float))
                                if (isLiveUpdate)
                                    prop.floatValue = UnsafeUtility.AsRef<float>(ptr);
                                else
                                    UnsafeUtility.AsRef<float>(ptr) = prop.floatValue;
                            break;
                        case SerializedPropertyType.Quaternion:
                            if (binding.FieldProperties.FieldType == typeof(quaternion))
                            {
                                if (isLiveUpdate)
                                {
                                    var qm = UnsafeUtility.AsRef<quaternion>(ptr).value;
                                    prop.quaternionValue = new Quaternion(qm.x, qm.y, qm.z, qm.w);
                                }
                                else
                                {
                                    UnsafeUtility.AsRef<quaternion>(ptr) = prop.quaternionValue;
                                }
                            }

                            if (binding.FieldProperties.FieldType == typeof(float4))
                            {
                                var f = UnsafeUtility.AsRef<float4>(ptr);
                                if (isLiveUpdate)
                                {
                                    prop.quaternionValue = new Quaternion(f.x, f.y, f.z, f.w);
                                }
                                else
                                {
                                    var quaternionValue = prop.quaternionValue;
                                    f = new float4(quaternionValue.x, quaternionValue.y, quaternionValue.z, quaternionValue.w);
                                }
                            }
                            break;
                        case SerializedPropertyType.Vector3:
                            if (binding.FieldProperties.FieldType == typeof(float3))
                            {
                                var f = UnsafeUtility.AsRef<float3>(ptr);
                                if (isLiveUpdate)
                                {
                                    prop.vector3Value = new Vector3(f.x, f.y, f.z);
                                }
                                else
                                {
                                    Vector3 v = prop.vector3Value;
                                    f = new float3(v.x, v.y, v.z);
                                }
                            }
                            else if (binding.FieldProperties.FieldType == typeof(float))
                            {
                                if (isLiveUpdate)
                                {
                                    var f = UnsafeUtility.AsRef<float>(ptr);
                                    prop.vector3Value = new Vector3(f, f, f);
                                }
                                else
                                {
                                    UnsafeUtility.AsRef<float>(ptr) = prop.vector3Value.x;
                                }
                            }
                            else if (binding.FieldProperties.FieldType == typeof(quaternion))
                            {
                                var q = UnsafeUtility.AsRef<quaternion>(ptr).value;
                                if (isLiveUpdate)
                                {
                                    var qm = new Quaternion(q.x, q.y, q.z, q.w);
                                    prop.vector3Value = qm.eulerAngles;
                                }
                                else
                                {
                                    var qm = Quaternion.Euler(prop.vector3Value);
                                    q = new float4(qm.x, qm.y, qm.z, qm.w);
                                }
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException($"Can't update the property {binding.AuthoringFieldName}." +
                                                                  $"The serialize property type: {prop.propertyType} is not supported yet by the live properties override.");
                    }
                }
            }
        }

        static void RegisterBindingsFromBuiltinTypes()
        {
            BindingRegistry.Register(typeof(LocalTransform), "Position.x", typeof(Transform), "m_LocalPosition.x");
            BindingRegistry.Register(typeof(LocalTransform), "Position.y", typeof(Transform), "m_LocalPosition.y");
            BindingRegistry.Register(typeof(LocalTransform), "Position.z", typeof(Transform), "m_LocalPosition.z");

            BindingRegistry.Register(typeof(LocalTransform), "Scale", typeof(Transform), "m_LocalScale");

            BindingRegistry.Register(typeof(LocalTransform), "Rotation", typeof(Transform), "m_LocalRotation");
        }
    }
}
