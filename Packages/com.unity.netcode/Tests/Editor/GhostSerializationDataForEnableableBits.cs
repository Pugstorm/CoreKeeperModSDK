using System;
using NUnit.Framework;
using Unity.Entities;
using UnityEngine;
// ReSharper disable InconsistentNaming
// ReSharper disable ParameterHidesMember

namespace Unity.NetCode.Tests
{
    public class GhostTypeConverter : TestNetCodeAuthoring.IConverter
    {
        public enum GhostTypes
        {
            EnableableComponent,
            MultipleEnableableComponent,
            EnableableBuffer,
            MultipleEnableableBuffer,
            ChildComponent,
            ChildBufferComponent,
            GhostGroup,
            // TODO: Support GhostGroupBuffers!
        }

        // TODO - Tests for ClientOnlyVariant.

        GhostTypes _type;
        private EnabledBitBakedValue _enabledBitBakedValue;
        public GhostTypeConverter(GhostTypes ghostType, EnabledBitBakedValue enabledBitBakedValue)
        {
            _type = ghostType;
            _enabledBitBakedValue = enabledBitBakedValue;
        }
        public void Bake(GameObject gameObject, IBaker baker)
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            switch (_type)
            {
                case GhostTypes.EnableableComponent:
                    baker.AddComponent(entity, new GhostOwner());
                    AddTestEnableableComponents(baker);
                    break;
                case GhostTypes.MultipleEnableableComponent:
                    baker.AddComponent(entity, new GhostOwner());
                    SetupMultipleEnableableComponents(baker);
                    break;
                case GhostTypes.EnableableBuffer:
                    baker.AddComponent(entity, new GhostOwner());
                    AddBufferWithLength<EnableableBuffer>(baker);
                    // TODO - Same tests for buffers.
                    break;
                case GhostTypes.MultipleEnableableBuffer:
                    baker.AddComponent(entity, new GhostOwner());
                    SetupMultipleEnableableBuffer(baker);
                    break;
                case GhostTypes.ChildComponent:
                    baker.AddComponent(entity, new GhostOwner());
                    AddTestEnableableComponents(baker);
                    var transform = baker.GetComponent<Transform>();
                    baker.DependsOn(transform.parent);
                    if (transform.parent == null)
                    {
                        baker.AddComponent(entity, new TopLevelGhostEntity());
                    }
                    else
                    {
                        baker.AddComponent<ChildOnlyComponent_1>(entity);
                        baker.AddComponent<ChildOnlyComponent_2>(entity);
                        baker.SetComponentEnabled<ChildOnlyComponent_1>(entity, BakedEnabledBitValue(_enabledBitBakedValue));
                        baker.SetComponentEnabled<ChildOnlyComponent_2>(entity, BakedEnabledBitValue(_enabledBitBakedValue));
                        AddComponentWithDefaultValue<ChildOnlyComponent_3>(baker);
                        AddComponentWithDefaultValue<ChildOnlyComponent_4>(baker);
                    }
                    break;
                case GhostTypes.ChildBufferComponent:
                    baker.AddComponent(entity, new GhostOwner());
                    AddBufferWithLength<EnableableBuffer>(baker);
                    if (gameObject.transform.parent == null)
                        baker.AddComponent(entity, new TopLevelGhostEntity());
                    break;
                case GhostTypes.GhostGroup:
                    baker.AddComponent(entity, new GhostOwner());
                    // Dependency on the name
                    baker.DependsOn(gameObject);
                    if (gameObject.name.StartsWith("ParentGhost"))
                    {
                        baker.AddBuffer<GhostGroup>(entity);
                        baker.AddComponent(entity, default(GhostGroupRoot));
                        AddTestEnableableComponents(baker);
                    }
                    else
                    {
                        baker.AddComponent(entity, default(GhostChildEntity));
                        AddTestEnableableComponents(baker);
                    }
                    break;
                default:
                    Assert.True(false);
                    break;
            }
        }

        /// <returns>Item1 is the ComponentType. Item2 is the VariantType (or null if same as ComponentType).</returns>
        internal static ValueTuple<Type, Type>[] FetchAllTestComponentTypesRequiringSendRuleOverride()
        {
            return new[]
            {
                (typeof(EnableableComponent), null),
                (typeof(EnableableFlagComponent), null),
                (typeof(EnableableComponentWithNonGhostField), null),
                (typeof(ReplicatedFieldWithNonReplicatedEnableableComponent), null),
                (typeof(ReplicatedEnableableComponentWithNonReplicatedField), null),
                (typeof(ComponentWithReplicatedVariant), typeof(ComponentWithVariantVariation)),
                (typeof(ComponentWithDontSendChildrenVariant), typeof(ComponentWithDontSendChildrenVariantVariation)),
                (typeof(ComponentWithNonReplicatedVariant), typeof(ComponentWithNonReplicatedVariantVariation)),
                // Skipped as never replicated. (typeof(NeverReplicatedEnableableFlagComponent), null),

                // GhostComponent:
                (typeof(SendForChildren_OnlyPredictedGhosts_SendToOwner_EnableableComponent), null),
                (typeof(SendForChildren_OnlyInterpolatedGhosts_SendToOwner_EnableableComponent), null),
                (typeof(SendForChildren_OnlyPredictedGhosts_SendToNonOwner_EnableableComponent), null),
                (typeof(SendForChildren_OnlyInterpolatedGhosts_SendToNonOwner_EnableableComponent), null),
                (typeof(DontSendForChildren_OnlyPredictedGhosts_SendToOwner_EnableableComponent), null),
                (typeof(DontSendForChildren_OnlyInterpolatedGhosts_SendToOwner_EnableableComponent), null),
                (typeof(DontSendForChildren_OnlyPredictedGhosts_SendToNonOwner_EnableableComponent), null),
                (typeof(DontSendForChildren_OnlyInterpolatedGhosts_SendToNonOwner_EnableableComponent), null),

                (typeof(ChildOnlyComponent_1), null),
                (typeof(ChildOnlyComponent_2), null),
                (typeof(ChildOnlyComponent_3), null),
                (typeof(ChildOnlyComponent_4), null),

                (typeof(EnableableComponent_0), null),
                (typeof(EnableableComponent_1), null),
                (typeof(EnableableComponent_2), null),
                (typeof(EnableableComponent_3), null),
                (typeof(EnableableComponent_4), null),
                (typeof(EnableableComponent_5), null),
                (typeof(EnableableComponent_6), null),
                (typeof(EnableableComponent_7), null),
                (typeof(EnableableComponent_8), null),
                (typeof(EnableableComponent_9), null),
                (typeof(EnableableComponent_10), null),
                (typeof(EnableableComponent_11), null),
                (typeof(EnableableComponent_12), null),
                (typeof(EnableableComponent_13), null),
                (typeof(EnableableComponent_14), null),
                (typeof(EnableableComponent_15), null),
                (typeof(EnableableComponent_16), null),
                (typeof(EnableableComponent_17), null),
                (typeof(EnableableComponent_18), null),
                (typeof(EnableableComponent_19), null),
                (typeof(EnableableComponent_20), null),
                (typeof(EnableableComponent_21), null),
                (typeof(EnableableComponent_22), null),
                (typeof(EnableableComponent_23), null),
                (typeof(EnableableComponent_24), null),
                (typeof(EnableableComponent_25), null),
                (typeof(EnableableComponent_26), null),
                (typeof(EnableableComponent_27), null),
                (typeof(EnableableComponent_28), null),
                (typeof(EnableableComponent_29), null),
                (typeof(EnableableComponent_30), null),
                (typeof(EnableableComponent_31), null),
                (typeof(EnableableComponent_32), null),

                (typeof(EnableableBuffer), null),
                (typeof(EnableableBuffer_0), null),
                (typeof(EnableableBuffer_1), null),
                (typeof(EnableableBuffer_2), null),
                (typeof(EnableableBuffer_3), null),
                (typeof(EnableableBuffer_4), null),
                (typeof(EnableableBuffer_5), null),
                (typeof(EnableableBuffer_6), null),
                (typeof(EnableableBuffer_7), null),
                (typeof(EnableableBuffer_8), null),
                (typeof(EnableableBuffer_9), null),
                (typeof(EnableableBuffer_10), null),
                (typeof(EnableableBuffer_11), null),
                (typeof(EnableableBuffer_12), null),
                (typeof(EnableableBuffer_13), null),
                (typeof(EnableableBuffer_14), null),
                (typeof(EnableableBuffer_15), null),
                (typeof(EnableableBuffer_16), null),
                (typeof(EnableableBuffer_17), null),
                (typeof(EnableableBuffer_18), null),
                (typeof(EnableableBuffer_19), null),
                (typeof(EnableableBuffer_20), null),
                (typeof(EnableableBuffer_21), null),
                (typeof(EnableableBuffer_22), null),
                (typeof(EnableableBuffer_23), null),
                (typeof(EnableableBuffer_24), null),
                (typeof(EnableableBuffer_25), null),
                (typeof(EnableableBuffer_26), null),
                (typeof(EnableableBuffer_27), null),
                (typeof(EnableableBuffer_28), null),
                (typeof(EnableableBuffer_29), null),
                (typeof(EnableableBuffer_30), null),
                (typeof(EnableableBuffer_31), null),
                (typeof(EnableableBuffer_32), null),
            };
        }

        /// <summary>Returns true if the component was baked with the component enabled, and vice-versa.</summary>
        internal static bool BakedEnabledBitValue(EnabledBitBakedValue enabledBitBakedValue)
        {
            switch (enabledBitBakedValue)
            {
                //case EnabledBitBakedValue.StartEnabledAndWriteImmediately:
                case EnabledBitBakedValue.StartEnabledAndWaitForClientSpawn:
                    return true;
                case EnabledBitBakedValue.StartDisabledAndWriteImmediately:
                case EnabledBitBakedValue.StartDisabledAndWaitForClientSpawn:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        /// <summary>Returns true if this configuration waits for replication before checking values.</summary>
        internal static bool WaitForClientEntitiesToSpawn(EnabledBitBakedValue enabledBitBakedValue)
        {
            switch (enabledBitBakedValue)
            {
                case EnabledBitBakedValue.StartDisabledAndWaitForClientSpawn:
                case EnabledBitBakedValue.StartEnabledAndWaitForClientSpawn:
                    return true;
                case EnabledBitBakedValue.StartDisabledAndWriteImmediately:
                //case EnabledBitBakedValue.StartEnabledAndWriteImmediately:
                    return false;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        void AddTestEnableableComponents(IBaker baker)
        {
            AddComponentWithDefaultValue<EnableableComponent>(baker);
            AddComponentWithDefaultValue<EnableableComponentWithNonGhostField>(baker);
            AddComponent<EnableableFlagComponent>(baker);
            AddComponentWithDefaultValue<ReplicatedFieldWithNonReplicatedEnableableComponent>(baker);
            AddComponentWithDefaultValue<ReplicatedEnableableComponentWithNonReplicatedField>(baker);
            AddComponent<NeverReplicatedEnableableFlagComponent>(baker);
            AddComponentWithDefaultValue<ComponentWithReplicatedVariant>(baker);
            AddComponentWithDefaultValue<ComponentWithDontSendChildrenVariant>(baker);
            AddComponentWithDefaultValue<ComponentWithNonReplicatedVariant>(baker);

            // FIXME: GhostComponentAttribute coverage.
            AddComponentWithDefaultValue<SendForChildren_OnlyPredictedGhosts_SendToOwner_EnableableComponent>(baker);
            AddComponentWithDefaultValue<SendForChildren_OnlyInterpolatedGhosts_SendToOwner_EnableableComponent>(baker);
            AddComponentWithDefaultValue<SendForChildren_OnlyPredictedGhosts_SendToNonOwner_EnableableComponent>(baker);
            AddComponentWithDefaultValue<SendForChildren_OnlyInterpolatedGhosts_SendToNonOwner_EnableableComponent>(baker);
            AddComponentWithDefaultValue<DontSendForChildren_OnlyPredictedGhosts_SendToOwner_EnableableComponent>(baker);
            AddComponentWithDefaultValue<DontSendForChildren_OnlyInterpolatedGhosts_SendToOwner_EnableableComponent>(baker);
            AddComponentWithDefaultValue<DontSendForChildren_OnlyPredictedGhosts_SendToNonOwner_EnableableComponent>(baker);
            AddComponentWithDefaultValue<DontSendForChildren_OnlyInterpolatedGhosts_SendToNonOwner_EnableableComponent>(baker);
        }

        void SetupMultipleEnableableComponents(IBaker baker)
        {
            AddComponentWithDefaultValue<EnableableComponent_0>(baker);
            AddComponentWithDefaultValue<EnableableComponent_1>(baker);
            AddComponentWithDefaultValue<EnableableComponent_2>(baker);
            AddComponentWithDefaultValue<EnableableComponent_3>(baker);
            AddComponentWithDefaultValue<EnableableComponent_4>(baker);
            AddComponentWithDefaultValue<EnableableComponent_5>(baker);
            AddComponentWithDefaultValue<EnableableComponent_6>(baker);
            AddComponentWithDefaultValue<EnableableComponent_7>(baker);
            AddComponentWithDefaultValue<EnableableComponent_8>(baker);
            AddComponentWithDefaultValue<EnableableComponent_9>(baker);
            AddComponentWithDefaultValue<EnableableComponent_10>(baker);
            AddComponentWithDefaultValue<EnableableComponent_11>(baker);
            AddComponentWithDefaultValue<EnableableComponent_12>(baker);
            AddComponentWithDefaultValue<EnableableComponent_13>(baker);
            AddComponentWithDefaultValue<EnableableComponent_14>(baker);
            AddComponentWithDefaultValue<EnableableComponent_15>(baker);
            AddComponentWithDefaultValue<EnableableComponent_16>(baker);
            AddComponentWithDefaultValue<EnableableComponent_17>(baker);
            AddComponentWithDefaultValue<EnableableComponent_18>(baker);
            AddComponentWithDefaultValue<EnableableComponent_19>(baker);
            AddComponentWithDefaultValue<EnableableComponent_20>(baker);
            AddComponentWithDefaultValue<EnableableComponent_21>(baker);
            AddComponentWithDefaultValue<EnableableComponent_22>(baker);
            AddComponentWithDefaultValue<EnableableComponent_23>(baker);
            AddComponentWithDefaultValue<EnableableComponent_24>(baker);
            AddComponentWithDefaultValue<EnableableComponent_25>(baker);
            AddComponentWithDefaultValue<EnableableComponent_26>(baker);
            AddComponentWithDefaultValue<EnableableComponent_27>(baker);
            AddComponentWithDefaultValue<EnableableComponent_28>(baker);
            AddComponentWithDefaultValue<EnableableComponent_29>(baker);
            AddComponentWithDefaultValue<EnableableComponent_30>(baker);
            AddComponentWithDefaultValue<EnableableComponent_31>(baker);
            AddComponentWithDefaultValue<EnableableComponent_32>(baker);
        }

        void AddComponentWithDefaultValue<T>(IBaker baker) where T : unmanaged, IComponentData, IComponentValue, IEnableableComponent
        {
            var def = default(T);
            def.SetValue(GhostSerializationTestsForEnableableBits.kDefaultValueIfNotReplicated);
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent(entity, def);
            baker.SetComponentEnabled<T>(entity, BakedEnabledBitValue(_enabledBitBakedValue));
        }

        void AddComponent<T>(IBaker baker) where T : unmanaged, IEnableableComponent, IComponentData
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            baker.AddComponent<T>(entity);
            baker.SetComponentEnabled<T>(entity, BakedEnabledBitValue(_enabledBitBakedValue));
        }

        void AddBufferWithLength<T>(IBaker baker)
            where T : unmanaged, IBufferElementData, IComponentValue, IEnableableComponent
        {
            var entity = baker.GetEntity(TransformUsageFlags.Dynamic);
            var enableableBuffers = baker.AddBuffer<T>(entity);
            enableableBuffers.Length = GhostSerializationTestsForEnableableBits.kBakedBufferSize;
            for (var index = 0; index < enableableBuffers.Length; index++)
            {
                var bufferElementData = enableableBuffers[index];
                bufferElementData.SetValue(GhostSerializationTestsForEnableableBits.kDefaultValueIfNotReplicated);
                enableableBuffers[index] = bufferElementData;
            }
            baker.SetComponentEnabled<T>(entity, BakedEnabledBitValue(_enabledBitBakedValue));
        }

        void SetupMultipleEnableableBuffer(IBaker baker)
        {
            AddBufferWithLength<EnableableBuffer_0>(baker);
            AddBufferWithLength<EnableableBuffer_1>(baker);
            AddBufferWithLength<EnableableBuffer_2>(baker);
            AddBufferWithLength<EnableableBuffer_3>(baker);
            AddBufferWithLength<EnableableBuffer_4>(baker);
            AddBufferWithLength<EnableableBuffer_5>(baker);
            AddBufferWithLength<EnableableBuffer_6>(baker);
            AddBufferWithLength<EnableableBuffer_7>(baker);
            AddBufferWithLength<EnableableBuffer_8>(baker);
            AddBufferWithLength<EnableableBuffer_9>(baker);
            AddBufferWithLength<EnableableBuffer_10>(baker);
            AddBufferWithLength<EnableableBuffer_11>(baker);
            AddBufferWithLength<EnableableBuffer_12>(baker);
            AddBufferWithLength<EnableableBuffer_13>(baker);
            AddBufferWithLength<EnableableBuffer_14>(baker);
            AddBufferWithLength<EnableableBuffer_15>(baker);
            AddBufferWithLength<EnableableBuffer_16>(baker);
            AddBufferWithLength<EnableableBuffer_17>(baker);
            AddBufferWithLength<EnableableBuffer_18>(baker);
            AddBufferWithLength<EnableableBuffer_19>(baker);
            AddBufferWithLength<EnableableBuffer_20>(baker);
            AddBufferWithLength<EnableableBuffer_21>(baker);
            AddBufferWithLength<EnableableBuffer_22>(baker);
            AddBufferWithLength<EnableableBuffer_23>(baker);
            AddBufferWithLength<EnableableBuffer_24>(baker);
            AddBufferWithLength<EnableableBuffer_25>(baker);
            AddBufferWithLength<EnableableBuffer_26>(baker);
            AddBufferWithLength<EnableableBuffer_27>(baker);
            AddBufferWithLength<EnableableBuffer_28>(baker);
            AddBufferWithLength<EnableableBuffer_29>(baker);
            AddBufferWithLength<EnableableBuffer_30>(baker);
            AddBufferWithLength<EnableableBuffer_31>(baker);
            AddBufferWithLength<EnableableBuffer_32>(baker);
        }
    }

    public interface IComponentValue
    {
        void SetValue(int value);
        int GetValue();
    }

    [GhostComponent(SendDataForChildEntity = true)] // We test this attribute flag too.
    [GhostEnabledBit]
    public struct EnableableBuffer : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    [GhostComponent(SendDataForChildEntity = true)] // We test this attribute flag too.
    [GhostEnabledBit]
    public struct EnableableComponent : IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    /// <summary>Enable flag SHOULD BE replicated.</summary>
    [GhostComponent(SendDataForChildEntity = true)]
    [GhostEnabledBit]
    public struct EnableableFlagComponent : IComponentData, IEnableableComponent
    {
    }

    [GhostComponent(SendDataForChildEntity = true)] // We test this attribute flag too.
    [GhostEnabledBit]
    public struct EnableableComponentWithNonGhostField : IComponentData, IEnableableComponent, IComponentValue
    {
        public int nonGhostField1;
        [GhostField] public int value;
        public int nonGhostField2;

        public void SetValue(int value)
        {
            nonGhostField1 = GhostSerializationTestsForEnableableBits.kDefaultValueForNonGhostFields;
            nonGhostField2 = GhostSerializationTestsForEnableableBits.kDefaultValueForNonGhostFields;
            this.value = value;
        }

        public int GetValue()
        {
            GhostSerializationTestsForEnableableBits.EnsureNonGhostFieldValueIsNotClobbered(nonGhostField1);
            GhostSerializationTestsForEnableableBits.EnsureNonGhostFieldValueIsNotClobbered(nonGhostField2);
            return value;
        }
    }

    /// <summary>Enable flag should NOT BE replicated.</summary>
    public struct NeverReplicatedEnableableFlagComponent : IComponentData, IEnableableComponent
    {
    }

    /// <summary>Enable flag should NOT BE replicated, but the field A SHOULD BE.</summary>
    [GhostComponent(SendDataForChildEntity = true)]
    public struct ReplicatedFieldWithNonReplicatedEnableableComponent : IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField]
        public int value;

        public void SetValue(int value) => this.value = value;

        public int GetValue() => value;
    }

    /// <summary>Enable flag SHOULD BE replicated, but the field B should NOT BE.</summary>
    [GhostComponent(SendDataForChildEntity = true)]
    [GhostEnabledBit]
    public struct ReplicatedEnableableComponentWithNonReplicatedField : IComponentData, IEnableableComponent, IComponentValue
    {
        public int value;

        public void SetValue(int value) => this.value = value;

        public int GetValue() => value;
    }

    public struct ComponentWithReplicatedVariant : IComponentData, IEnableableComponent, IComponentValue
    {
        public int value;

        public void SetValue(int value) => this.value = value;

        public int GetValue() => value;
    }

    // As this is the only variant, it becomes the default variant.
    [GhostComponentVariation(typeof(ComponentWithReplicatedVariant))]
    [GhostComponent(SendDataForChildEntity = true)]
    [GhostEnabledBit]
    public struct ComponentWithVariantVariation
    {
        [GhostField]
        public int value;
    }

    [GhostComponent(SendDataForChildEntity = true)] // Testing this as well, as this should be clobbered by the Variant.
    public struct ComponentWithDontSendChildrenVariant  : IComponentData, IEnableableComponent, IComponentValue
    {
        public int value;

        public void SetValue(int value) => this.value = value;

        public int GetValue() => value;
    }

    // As this is the only variant, it becomes the default variant.
    [GhostComponentVariation(typeof(ComponentWithDontSendChildrenVariant))]
    [GhostComponent(SendDataForChildEntity = false)]
    [GhostEnabledBit]
    public struct ComponentWithDontSendChildrenVariantVariation
    {
        [GhostField]
        public int value;
    }

    [GhostEnabledBit]
    public struct ComponentWithNonReplicatedVariant : IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField]
        public int value;

        public void SetValue(int value) => this.value = value;

        public int GetValue() => value;
    }

    // As this is the only variant, it becomes the default variant.
    [GhostComponentVariation(typeof(ComponentWithNonReplicatedVariant))]
    public struct ComponentWithNonReplicatedVariantVariation
    {
        public int value;
    }

    // Test child-only components:
    [GhostComponent(SendDataForChildEntity = true)]
    [GhostEnabledBit]
    public struct ChildOnlyComponent_1 : IComponentData, IEnableableComponent
    {
    }
    [GhostComponent(SendDataForChildEntity = false)]
    public struct ChildOnlyComponent_2 : IComponentData, IEnableableComponent
    {
    }
    [GhostComponent(SendDataForChildEntity = true)]
    [GhostEnabledBit]
    public struct ChildOnlyComponent_3 : IComponentData, IComponentValue, IEnableableComponent
    {
        public int nonGhostField1;
        [GhostField]
        public int value;
        public int nonGhostField2;
        public void SetValue(int value)
        {
            nonGhostField1 = GhostSerializationTestsForEnableableBits.kDefaultValueForNonGhostFields;
            nonGhostField2 = GhostSerializationTestsForEnableableBits.kDefaultValueForNonGhostFields;
            this.value = value;
        }

        public int GetValue()
        {
            GhostSerializationTestsForEnableableBits.EnsureNonGhostFieldValueIsNotClobbered(nonGhostField1);
            GhostSerializationTestsForEnableableBits.EnsureNonGhostFieldValueIsNotClobbered(nonGhostField2);
            return value;
        }
    }
    [GhostComponent(SendDataForChildEntity = false)]
    public struct ChildOnlyComponent_4 : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }

    // FIXME: GhostComponentAttribute coverage, test children equivalents of this.
    // FIXME: GhostComponentAttribute coverage, test SendData = false too.

    // Test components with lots of GhostComponentAttribute modifications (note: PrefabType stripping is tested elsewhere):
    [GhostComponent(SendDataForChildEntity = true, SendTypeOptimization = GhostSendType.OnlyPredictedClients, OwnerSendType = SendToOwnerType.SendToOwner)]
    [GhostEnabledBit]
    public struct SendForChildren_OnlyPredictedGhosts_SendToOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }
    [GhostComponent(SendDataForChildEntity = true, SendTypeOptimization = GhostSendType.OnlyInterpolatedClients, OwnerSendType = SendToOwnerType.SendToOwner)]
    [GhostEnabledBit]
    public struct SendForChildren_OnlyInterpolatedGhosts_SendToOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }
    [GhostComponent(SendDataForChildEntity = true, SendTypeOptimization = GhostSendType.OnlyPredictedClients, OwnerSendType = SendToOwnerType.SendToNonOwner)]
    [GhostEnabledBit]
    public struct SendForChildren_OnlyPredictedGhosts_SendToNonOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }
    [GhostComponent(SendDataForChildEntity = true, SendTypeOptimization = GhostSendType.OnlyInterpolatedClients, OwnerSendType = SendToOwnerType.SendToNonOwner)]
    [GhostEnabledBit]
    public struct SendForChildren_OnlyInterpolatedGhosts_SendToNonOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }
    // ----
    [GhostComponent(SendDataForChildEntity = false, SendTypeOptimization = GhostSendType.OnlyPredictedClients, OwnerSendType = SendToOwnerType.SendToOwner)]
    [GhostEnabledBit]
    public struct DontSendForChildren_OnlyPredictedGhosts_SendToOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }
    [GhostComponent(SendDataForChildEntity = false, SendTypeOptimization = GhostSendType.OnlyInterpolatedClients, OwnerSendType = SendToOwnerType.SendToOwner)]
    [GhostEnabledBit]
    public struct DontSendForChildren_OnlyInterpolatedGhosts_SendToOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }
    [GhostComponent(SendDataForChildEntity = false, SendTypeOptimization = GhostSendType.OnlyPredictedClients, OwnerSendType = SendToOwnerType.SendToNonOwner)]
    [GhostEnabledBit]
    public struct DontSendForChildren_OnlyPredictedGhosts_SendToNonOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }
    [GhostComponent(SendDataForChildEntity = false, SendTypeOptimization = GhostSendType.OnlyInterpolatedClients, OwnerSendType = SendToOwnerType.SendToNonOwner)]
    [GhostEnabledBit]
    public struct DontSendForChildren_OnlyInterpolatedGhosts_SendToNonOwner_EnableableComponent : IComponentData, IComponentValue, IEnableableComponent
    {
        [GhostField]
        public int value;
        public void SetValue(int value) => this.value = value;
        public int GetValue() => value;
    }

    ////////////////////////////////////////////////////////////////////////////

    [GhostEnabledBit]
    public struct EnableableComponent_0: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_1: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_2: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_3: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_4: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_5: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    [GhostEnabledBit]
    public struct EnableableComponent_6: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_7: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_8: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_9: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_10: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_11: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    [GhostEnabledBit]
    public struct EnableableComponent_12: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_13: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_14: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_15: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    [GhostEnabledBit]
    public struct EnableableComponent_16: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_17: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_18: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_19: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_20: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_21: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    [GhostEnabledBit]
    public struct EnableableComponent_22: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_23: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_24: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_25: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    [GhostEnabledBit]
    public struct EnableableComponent_26: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_27: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_28: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_29: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_30: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_31: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableComponent_32: IComponentData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;

        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_0 : IBufferElementData, IEnableableComponent, IComponentValue
    {
#pragma warning disable CS0414
        private int nonGhostField1;
        [GhostField] public int value;
        private int nonGhostField2;
#pragma warning restore CS0414

        public void SetValue(int value)
        {
            nonGhostField1 = GhostSerializationTestsForEnableableBits.kDefaultValueForNonGhostFields;
            nonGhostField2 = GhostSerializationTestsForEnableableBits.kDefaultValueForNonGhostFields;
            this.value = value;
        }

        public int GetValue()
        {
            // There is nothing to validate for private fields on buffers.
            // Every time the buffer gets updated, these fields become undefined (NOT default).
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_1 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_2 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_3 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_4 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_5 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_6 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_7 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_8 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_9 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_10 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_11 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_12 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_13 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_14 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_15 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_16 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_17 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_18 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_19 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_20 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_21 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_22 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_23 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_24 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_25 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_26 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_27 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_28 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_29 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_30 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_31 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }
    [GhostEnabledBit]
    public struct EnableableBuffer_32 : IBufferElementData, IEnableableComponent, IComponentValue
    {
        [GhostField] public int value;
        public void SetValue(int value)
        {
            this.value = value;
        }

        public int GetValue()
        {
            return value;
        }
    }

    ////////////////////////////////////////////////////////////////////////////
}
