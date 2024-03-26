using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using Unity.Collections;
using Unity.Entities;
using UnityEngine;

namespace Unity.NetCode.Tests
{
    /// <summary>
    /// Used to test different baked values.
    /// </summary>
    public enum EnabledBitBakedValue
    {
        // No need to test this as it's covered by other tests.
        ///// <summary>Bake the component as ENABLED, then write to it on Server on the first frame.</summary>
        // StartEnabledAndWriteImmediately = 0,

        /// <summary>Bake the component as DISABLED, then write to it on Server on the first frame.</summary>
        StartDisabledAndWriteImmediately = 1,
        /// <summary>Bake the component as ENABLED, wait for the ghost to be created, then validate the baked value is replicated. Then continue the test by modify it.</summary>
        StartEnabledAndWaitForClientSpawn = 3,
        /// <summary>Bake the component as DISABLED, wait for the ghost to be created, then validate the baked value is replicated. Then continue the test by modify it.</summary>
        StartDisabledAndWaitForClientSpawn = 4,
    }

    public class GhostSerializationTestsForEnableableBits
    {
        float frameTime = 1.0f / 60.0f;

        void TickMultipleFrames(int numTicksToProperlyReplicate)
        {
            for (int i = 0; i < numTicksToProperlyReplicate; ++i)
            {
                m_TestWorld.Tick(frameTime);
            }
        }


        private int GetNumTicksToReplicateGhostTypes(GhostTypeConverter.GhostTypes ghostTypes)
        {
            switch (ghostTypes)
            {
                case GhostTypeConverter.GhostTypes.EnableableComponent:
                    return 6;
                case GhostTypeConverter.GhostTypes.MultipleEnableableComponent:
                    return 11;
                case GhostTypeConverter.GhostTypes.EnableableBuffer:
                    return 11;
                case GhostTypeConverter.GhostTypes.MultipleEnableableBuffer:
                    return 11;
                case GhostTypeConverter.GhostTypes.ChildComponent:
                    return 6;
                case GhostTypeConverter.GhostTypes.ChildBufferComponent:
                    return 6;
                case GhostTypeConverter.GhostTypes.GhostGroup:
                    return 6;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ghostTypes), ghostTypes, null);
            }
        }

        void SetLinkedBufferValues<T>(int value, bool enabled)
            where T : unmanaged, IBufferElementData, IEnableableComponent, IComponentValue
        {
            foreach (var serverEntity in m_ServerEntities)
            {
                var serverEntityGroup = m_TestWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEntity, true);
                Assert.AreEqual(2, serverEntityGroup.Length);

                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(serverEntityGroup[0].Value, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(serverEntityGroup[0].Value), $"{typeof(T)} is set correctly on server, linked[0]");

                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(serverEntityGroup[1].Value, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(serverEntityGroup[1].Value), $"{typeof(T)} is set correctly on server, linked[1]");

                SetupBuffer(m_TestWorld.ServerWorld.EntityManager.GetBuffer<T>(serverEntityGroup[0].Value));
                SetupBuffer(m_TestWorld.ServerWorld.EntityManager.GetBuffer<T>(serverEntityGroup[1].Value));

                void SetupBuffer(DynamicBuffer<T> buffer)
                {
                    buffer.ResizeUninitialized(kWrittenServerBufferSize);
                    for (int i = 0; i < kWrittenServerBufferSize; ++i)
                    {
                        var newValue = new T();
                        newValue.SetValue((i + 1) * 1000 + value);
                        buffer[i] = newValue;
                    }
                }
            }
        }

        void SetGhostGroupValues<T>(int value, bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            SetGhostGroupEnabled<T>(enabled);
            for (int i = 0; i < m_ServerEntities.Length; i += 2)
            {
                var rootEntity = m_ServerEntities[i];
                var childEntity = m_ServerEntities[i + 1];
                T newValue = default;
                newValue.SetValue(value);
                m_TestWorld.ServerWorld.EntityManager.SetComponentData(rootEntity, newValue);
                m_TestWorld.ServerWorld.EntityManager.SetComponentData(childEntity, newValue);
            }
        }

        void SetGhostGroupEnabled<T>(bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            for (int i = 0; i < m_ServerEntities.Length; i += 2)
            {
                var rootEntity = m_ServerEntities[i];
                var childEntity = m_ServerEntities[i + 1];

                Assert.True(m_TestWorld.ServerWorld.EntityManager.HasComponent<GhostGroupRoot>(rootEntity));
                Assert.True(m_TestWorld.ServerWorld.EntityManager.HasComponent<GhostChildEntity>(childEntity));

                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(rootEntity, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(rootEntity), $"{typeof(T)} is set correctly on server, root entity");

                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(childEntity, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(childEntity), $"{typeof(T)} is set correctly on server, child entity");
            }
        }

        void SetLinkedComponentValues<T>(int value, bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            SetLinkedComponentEnabled<T>(enabled);
            foreach (var entity in m_ServerEntities)
            {
                var serverEntityGroup = m_TestWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(entity, true);
                T newValue = default;
                newValue.SetValue(value);
                m_TestWorld.ServerWorld.EntityManager.SetComponentData(serverEntityGroup[0].Value, newValue);
                m_TestWorld.ServerWorld.EntityManager.SetComponentData(serverEntityGroup[1].Value, newValue);
            }
        }

        void SetLinkedComponentEnabled<T>(bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            foreach (var entity in m_ServerEntities)
            {
                var serverEntityGroup = m_TestWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(entity, true);
                Assert.AreEqual(2, serverEntityGroup.Length);

                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(serverEntityGroup[0].Value, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(serverEntityGroup[0].Value), $"{typeof(T)} is set correctly on server, linked[0]");

                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(serverEntityGroup[1].Value, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(serverEntityGroup[1].Value), $"{typeof(T)} is set correctly on server, linked[1]");
            }
        }

        void SetLinkedComponentEnabledOnlyOnChildren<T>(bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            for (var i = 0; i < m_ServerEntities.Length; i++)
            {
                var entity = m_ServerEntities[i];
                var serverEntityGroup = m_TestWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(entity, true);
                Assert.AreEqual(2, serverEntityGroup.Length);

                var childEntity = serverEntityGroup[1].Value;
                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(childEntity, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(childEntity), $"{typeof(T)} enabled state is set correctly on server child [{i}]!");
            }
        }
        void SetLinkedComponentValueOnlyOnChildren<T>(int value, bool enabled)
            where T : unmanaged, IComponentData, IComponentValue, IEnableableComponent
        {
            SetLinkedComponentEnabledOnlyOnChildren<T>(enabled);
            for (var i = 0; i < m_ServerEntities.Length; i++)
            {
                var entity = m_ServerEntities[i];
                var serverEntityGroup = m_TestWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(entity, true);
                Assert.AreEqual(2, serverEntityGroup.Length);


                T newValue = default;
                newValue.SetValue(value);
                var childEntity = serverEntityGroup[1].Value;
                m_TestWorld.ServerWorld.EntityManager.SetComponentData(childEntity, newValue);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(childEntity), $"{typeof(T)} value is set correctly on server child [{i}]!");
            }
        }

        void SetComponentValues<T>(int value, bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            SetComponentEnabled<T>(enabled);
            foreach (var entity in m_ServerEntities)
            {
                T newValue = default;
                newValue.SetValue(value);
                m_TestWorld.ServerWorld.EntityManager.SetComponentData(entity, newValue);
            }
        }

        void SetComponentEnabled<T>(bool enabled)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            foreach (var entity in m_ServerEntities)
            {
                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(entity, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(entity), $"{typeof(T)} is set correctly on server");
            }
        }

        void SetBufferValues<T>(int value, bool enabled) where T : unmanaged, IBufferElementData, IEnableableComponent, IComponentValue
        {
            foreach (var entity in m_ServerEntities)
            {
                m_TestWorld.ServerWorld.EntityManager.SetComponentEnabled<T>(entity, enabled);
                Assert.AreEqual(enabled, m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(entity), $"{typeof(T)} buffer is set correctly on server");

                var serverBuffer = m_TestWorld.ServerWorld.EntityManager.GetBuffer<T>(entity);

                serverBuffer.ResizeUninitialized(kWrittenServerBufferSize);
                for (int i = 0; i < kWrittenServerBufferSize; ++i)
                {
                    var newValue = new T();
                    newValue.SetValue((i + 1) * 1000 + value);

                    serverBuffer[i] = newValue;
                }
                Assert.True(m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(entity) == enabled);
            }
        }

        private void VerifyGhostGroupValues<T>(bool expectValueReplicated, bool expectEnabledReplicated)
            where T : unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            VerifyGhostGroupEnabledBits<T>(expectValueReplicated, expectEnabledReplicated);

            var rootType = ComponentType.ReadOnly<GhostGroupRoot>();
            var childType = ComponentType.ReadOnly<GhostChildEntity>();

            using var rootQuery = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(rootType);
            using var childQuery = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(childType);
            using (var clientRootEntities = rootQuery.ToEntityArray(Allocator.Temp))
            using (var clientChildEntities = childQuery.ToEntityArray(Allocator.Temp))
            {
                for (int i = 0; i < clientRootEntities.Length; i++)
                {
                    var clientGroupRootEntity = clientRootEntities[i];
                    var clientMemberEntity = clientChildEntities[i];


                    var clientGroupRootValue = m_TestWorld.ClientWorlds[0].EntityManager.GetComponentData<T>(clientGroupRootEntity).GetValue();
                    var clientGroupMemberValue = m_TestWorld.ClientWorlds[0].EntityManager.GetComponentData<T>(clientMemberEntity).GetValue();
                    if (expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true)) // Ghost groups are root entities, by definition.
                    {
                        Assert.AreEqual(m_ExpectedValueIfReplicated, clientGroupRootValue, $"[{typeof(T)}] Expect \"group root\" entity value IS replicated when `{m_SendForChildrenTestCase}`!");
                        Assert.AreEqual(m_ExpectedValueIfReplicated, clientGroupMemberValue, $"[{typeof(T)}] Expect \"group member\" entity value when `{m_SendForChildrenTestCase}`!");
                    }
                    else
                    {
                        Assert.AreEqual(kDefaultValueIfNotReplicated, clientGroupRootValue, $"[{typeof(T)}] Expect \"group root\" entity value is NOT replicated when `{m_SendForChildrenTestCase}`!");
                        Assert.AreEqual(kDefaultValueIfNotReplicated, clientGroupMemberValue, $"[{typeof(T)}] Expect \"group member\" entity value is NOT replicated when `{m_SendForChildrenTestCase}`!");
                    }
                }
            }

            ValidateChangeMaskForComponent<T>(expectEnabledReplicated | expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true), true);
        }

        private void VerifyGhostGroupEnabledBits<T>(bool expectValueReplicated, bool expectEnabledReplicated)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            var rootType = ComponentType.ReadOnly<GhostGroupRoot>();
            var childType = ComponentType.ReadOnly<GhostChildEntity>();

            using var rootQuery = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(rootType);
            using var childQuery = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(childType);
            using var clientRootEntities = rootQuery.ToEntityArray(Allocator.Temp);
            using var clientChildEntities = childQuery.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(clientRootEntities.Length, clientChildEntities.Length);
            Assert.AreEqual(m_ServerEntities.Length, clientChildEntities.Length + clientRootEntities.Length,  $"[{typeof(T)}] Expect client group has entities!");

            for (int i = 0; i < clientRootEntities.Length; i++)
            {
                var clientGroupRootEntity = clientRootEntities[i];
                var clientGroupMemberEntity = clientChildEntities[i];

                var rootEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientGroupRootEntity);
                var memberEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientGroupMemberEntity);
                if (expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true)) // Ghost groups are root entities, by definition.
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, rootEnabled, $"[{typeof(T)}] Expect \"group root\" entity enabled IS replicated when `{m_SendForChildrenTestCase}`!");
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, memberEnabled, $"[{typeof(T)}] Expect \"group member\" entity enabled IS replicated when `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, rootEnabled, $"[{typeof(T)}] Expect \"group root\" entity enabled NOT replicated when `{m_SendForChildrenTestCase}`!");
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, memberEnabled, $"[{typeof(T)}] Expect \"group member\" entity enabled NOT replicated when `{m_SendForChildrenTestCase}`!");
                }
            }

            ValidateChangeMaskForComponent<T>(expectEnabledReplicated | expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true), true);
        }

        private void VerifyLinkedBufferValues<T>(bool expectValuesReplicated, bool expectEnabledReplicated)
            where T : unmanaged, IBufferElementData, IEnableableComponent, IComponentValue
        {
            var type = ComponentType.ReadOnly<TopLevelGhostEntity>();
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(type);

            using var clientEntities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(m_ServerEntities.Length, clientEntities.Length,  $"[{typeof(T)}] Expect client has entities!");

            for (int i = 0; i < clientEntities.Length; i++)
            {
                var serverEntity = m_ServerEntities[i];
                var clientEntity = clientEntities[i];


                Assert.IsTrue(m_TestWorld.ClientWorlds[0].EntityManager.HasComponent<LinkedEntityGroup>(clientEntity), "Has client linked group!");

                var serverEntityGroup = m_TestWorld.ServerWorld.EntityManager.GetBuffer<LinkedEntityGroup>(serverEntity, true);
                var clientEntityGroup = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntity, true);
                Assert.AreEqual(2, clientEntityGroup.Length, "client linked group, expecting parent + child");

                var clientParentEntityComponentEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntityGroup[0].Value);
                var clientChildEntityComponentEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntityGroup[1].Value);

                if (expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, clientParentEntityComponentEnabled, $"[{typeof(T)}] Expect client parent entity component enabled bit IS replicated when `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, clientParentEntityComponentEnabled, $"[{typeof(T)}] Expect client parent entity component enabled bit NOT replicated when `{m_SendForChildrenTestCase}`!");
                }

                var serverParentBuffer = m_TestWorld.ServerWorld.EntityManager.GetBuffer<T>(serverEntityGroup[0].Value, true);
                var serverChildBuffer = m_TestWorld.ServerWorld.EntityManager.GetBuffer<T>(serverEntityGroup[1].Value, true);
                var clientParentBuffer = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<T>(clientEntityGroup[0].Value, true);
                var clientChildBuffer = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<T>(clientEntityGroup[1].Value, true);

                Assert.AreEqual(m_ExpectedServerBufferSize, serverParentBuffer.Length, $"[{typeof(T)}] Expect server parent buffer length!");
                Assert.AreEqual(m_ExpectedServerBufferSize, serverChildBuffer.Length, $"[{typeof(T)}] Expect server child buffer length!");

                // Root:
                if (expectValuesReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedServerBufferSize, clientParentBuffer.Length, $"[{typeof(T)}] Expect client parent buffer length IS replicated when `{m_SendForChildrenTestCase}`!");
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, clientParentEntityComponentEnabled, $"[{typeof(T)}] Expect client parent buffer enable bit IS replicated when `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(kBakedBufferSize, clientParentBuffer.Length, $"[{typeof(T)}] Expect client parent buffer length NOT replicated when `{m_SendForChildrenTestCase}`, so expect it will use default client buffer length!");
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, clientParentEntityComponentEnabled, $"[{typeof(T)}] Expect client parent buffer enable bit NOT replicated when `{m_SendForChildrenTestCase}`!");
                }

                for (int j = 0; j < serverParentBuffer.Length; ++j)
                {
                    var serverValue = serverParentBuffer[j];
                    var clientValue = clientParentBuffer[j];

                    var expectedBufferValue = m_IsValidatingBakedValues ? kDefaultValueIfNotReplicated : ((j + 1) * 1000 + m_ExpectedValueIfReplicated);
                    Assert.AreEqual(expectedBufferValue, serverValue.GetValue(), $"[{typeof(T)}] Expect server parent value is written [{i}]");
                    if (expectValuesReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                    {
                        Assert.AreEqual(expectedBufferValue, clientValue.GetValue(), $"[{typeof(T)}] Expect client parent value [{i}] IS replicated when `{m_SendForChildrenTestCase}`!");
                    }
                    else
                    {
                        Assert.AreEqual(kDefaultValueIfNotReplicated, clientValue.GetValue(), $"[{typeof(T)}] Expect client parent value [{i}] NOT replicated when `{m_SendForChildrenTestCase}`!");
                    }
                }

                // Children:
                if (IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false))
                {
                    Assert.AreEqual(m_ExpectedServerBufferSize, clientChildBuffer.Length, $"[{typeof(T)}] Expect client child buffer length IS replicated when `{m_SendForChildrenTestCase}`!");
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, clientChildEntityComponentEnabled, $"[{typeof(T)}] Expect client child buffer enable bit IS replicated when `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(kBakedBufferSize, clientChildBuffer.Length, $"[{typeof(T)}] Expect client child buffer length NOT replicated when `{m_SendForChildrenTestCase}`, so expect it will use the default client buffer length!");
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, clientChildEntityComponentEnabled, $"[{typeof(T)}] Expect client child buffer enable bit NOT replicated when `{m_SendForChildrenTestCase}`!");
                }
                for (int j = 0; j < serverChildBuffer.Length; ++j)
                {
                    var serverValue = serverChildBuffer[j];
                    var clientValue = clientChildBuffer[j];

                    var expectedBufferValue = m_IsValidatingBakedValues ? kDefaultValueIfNotReplicated : ((j + 1) * 1000 + m_ExpectedValueIfReplicated);
                    Assert.AreEqual(expectedBufferValue, serverValue.GetValue(), $"[{typeof(T)}] Expect client child value is written [{i}]!");

                    if (IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false))
                    {
                        Assert.AreEqual(expectedBufferValue, clientValue.GetValue(), $"[{typeof(T)}] Expect client child entity buffer value [{i}] IS replicated when `{m_SendForChildrenTestCase}`!");
                    }
                    else
                    {
                        Assert.AreEqual(kDefaultValueIfNotReplicated, clientValue.GetValue(), $"[{typeof(T)}] client parent value [{i}] NOT replicated when `{m_SendForChildrenTestCase}`!");
                    }
                }
            }
        }

        private void VerifyLinkedComponentValues<T>(bool expectValueReplicated, bool expectEnabledReplicated, bool? forceChildReplication = default)
            where T : unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            VerifyLinkedComponentEnabled<T>(expectValueReplicated, expectEnabledReplicated, forceChildReplication);

            var type = ComponentType.ReadOnly<TopLevelGhostEntity>();
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(type);
            using var clientEntities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(m_ServerEntities.Length, clientEntities.Length,  $"[{typeof(T)}] Expect client has entities!");

            for (int i = 0; i < clientEntities.Length; i++)
            {
                var clientEntity = clientEntities[i];

                Assert.IsTrue(m_TestWorld.ClientWorlds[0].EntityManager.HasComponent<LinkedEntityGroup>(clientEntity));

                var clientEntityGroup = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntity, true);
                Assert.AreEqual(2, clientEntityGroup.Length, "Client entity count should always be correct.");

                var clientRootValue = m_TestWorld.ClientWorlds[0].EntityManager.GetComponentData<T>(clientEntityGroup[0].Value).GetValue();
                var clientChildValue = m_TestWorld.ClientWorlds[0].EntityManager.GetComponentData<T>(clientEntityGroup[1].Value).GetValue();
                if (expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedValueIfReplicated, clientRootValue, $"[{typeof(T)}] Expected that value on component on root entity [{i}] IS replicated correctly when using this `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(kDefaultValueIfNotReplicated, clientRootValue, $"[{typeof(T)}] Expected that value on component on root entity [{i}] is NOT replicated by default (via this `{m_SendForChildrenTestCase}`)!");
                }

                if (forceChildReplication ?? expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false))
                {
                    Assert.AreEqual(m_ExpectedValueIfReplicated, clientChildValue, $"[{typeof(T)}] Expected that value on component on child entity [{i}] IS replicated when using this `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(kDefaultValueIfNotReplicated, clientChildValue, $"[{typeof(T)}] Expected that value on component on child entity [{i}] is NOT replicated by default (via this `{m_SendForChildrenTestCase}`)!");
                }
            }

            ValidateChangeMaskForComponent<T>(forceChildReplication ?? expectValueReplicated | expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false), false);
        }

        void VerifyLinkedComponentValueOnChild<T>(bool expectValueReplicated, bool expectEnabledReplicated)
            where T : unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            VerifyLinkedComponentEnabledOnChild<T>(expectValueReplicated, expectEnabledReplicated);

            var type = ComponentType.ReadOnly<TopLevelGhostEntity>();
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(type);
            using var clientEntities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(m_ServerEntities.Length, clientEntities.Length,  $"[{typeof(T)}] Expect client has entities!");

            for (int i = 0; i < clientEntities.Length; i++)
            {
                var clientEntity = clientEntities[i];

                Assert.IsTrue(m_TestWorld.ClientWorlds[0].EntityManager.HasComponent<LinkedEntityGroup>(clientEntity));

                var clientEntityGroup = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntity, true);
                Assert.AreEqual(2, clientEntityGroup.Length, "Client entity count should always be correct.");

                // This method is exclusively to test behaviour of children.

                var value = m_TestWorld.ClientWorlds[0].EntityManager.GetComponentData<T>(clientEntityGroup[1].Value).GetValue();
                if (expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false))
                {
                    Assert.AreEqual(m_ExpectedValueIfReplicated, value, $"[{typeof(T)}] Expected that value on component on child entity [{i}] IS replicated when using this `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(kDefaultValueIfNotReplicated, value, $"[{typeof(T)}] Expected that value on component on child entity [{i}] is NOT replicated by default (via this `{m_SendForChildrenTestCase}`)!");
                }
            }
        }

        private void VerifyLinkedComponentEnabled<T>(bool expectValueReplicated, bool expectEnabledReplicated, bool? forceChildReplication = default)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            var type = ComponentType.ReadOnly<TopLevelGhostEntity>();
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(type);
            using var clientEntities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(m_ServerEntities.Length, clientEntities.Length, $"[{typeof(T)}] Client has entity with TopLevelGhostEntity.");

            for (int i = 0; i < clientEntities.Length; i++)
            {
                var clientEntity = clientEntities[i];

                Assert.IsTrue(m_TestWorld.ClientWorlds[0].EntityManager.HasComponent<LinkedEntityGroup>(clientEntity), $"[{typeof(T)}] Client has entities with the LinkedEntityGroup.");

                var clientEntityGroup = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntity, true);
                Assert.AreEqual(2, clientEntityGroup.Length, $"[{typeof(T)}] Entities in the LinkedEntityGroup!");

                var rootEntityEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntityGroup[0].Value);
                if (expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, rootEntityEnabled, $"[{typeof(T)}] Expected that the enable-bit on component on root entity [{i}] is replicated when using `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, rootEntityEnabled, $"[{typeof(T)}] Expected that the enable-bit on component on root entity [{i}] is NOT replicated by default when using `{m_SendForChildrenTestCase}`!");
                }

                var childEntityEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntityGroup[1].Value);
                if (forceChildReplication ?? expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false))
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, childEntityEnabled, $"[{typeof(T)}] Expected that the enable-bit on component on child entity [{i}] is replicated when using `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, childEntityEnabled, $"[{typeof(T)}] Expected that the enable-bit on component on child entity [{i}] is NOT replicated by default when using `{m_SendForChildrenTestCase}`!");
                }
            }

            ValidateChangeMaskForComponent<T>(forceChildReplication ?? expectValueReplicated|expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false), false);
        }

        private void VerifyLinkedComponentEnabledOnChild<T>(bool expectValueReplicated, bool expectEnabledReplicated)
            where T : unmanaged, IComponentData, IEnableableComponent
        {
            var type = ComponentType.ReadOnly<TopLevelGhostEntity>();
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(type);
            using var clientEntities = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(m_ServerEntities.Length, clientEntities.Length, $"[{typeof(T)}] Client has entity with TopLevelGhostEntity.");

            for (int i = 0; i < clientEntities.Length; i++)
            {
                var clientEntity = clientEntities[i];

                Assert.IsTrue(m_TestWorld.ClientWorlds[0].EntityManager.HasComponent<LinkedEntityGroup>(clientEntity), $"[{typeof(T)}] Client has entities with the LinkedEntityGroup.");

                var clientEntityGroup = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<LinkedEntityGroup>(clientEntity, true);
                Assert.AreEqual(2, clientEntityGroup.Length, $"[{typeof(T)}] Entities in the LinkedEntityGroup!");

                // This method is exclusively to test behaviour of children.

                var childEntityEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntityGroup[1].Value);
                if (expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false))
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, childEntityEnabled, $"[{typeof(T)}] Expected that the enable-bit on component ONLY on child entity [{i}] is replicated when using `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, childEntityEnabled, $"[{typeof(T)}] Expected that the enable-bit on component ONLY on child entity [{i}] is NOT replicated by default when using `{m_SendForChildrenTestCase}`!");
                }
            }

            ValidateChangeMaskForComponent<T>(expectValueReplicated | expectEnabledReplicated  && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, false), false);
        }

        private void VerifyComponentValues<T>(bool expectValueReplicated, bool expectEnabledReplicated) where T: unmanaged, IComponentData, IEnableableComponent, IComponentValue
        {
            VerifyFlagComponentEnabledBit<T>(expectValueReplicated, expectEnabledReplicated);

            var builder = new EntityQueryBuilder(Allocator.Temp)
                .WithAll<T>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState);
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(builder);
            using var clientEntitiesWithoutFiltering = query.ToEntityArray(Allocator.Temp);
            Assert.AreEqual(m_ServerEntities.Length, clientEntitiesWithoutFiltering.Length, $"[{typeof(T)}] Expect client has entities!");

            for (int i = 0; i < clientEntitiesWithoutFiltering.Length; i++)
            {
                var serverEntity = m_ServerEntities[i];
                var clientEntity = clientEntitiesWithoutFiltering[i];

                var isServerEnabled = m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(serverEntity);
                var isClientEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntity);
                var serverValue = m_TestWorld.ServerWorld.EntityManager.GetComponentData<T>(serverEntity).GetValue();
                var clientValue = m_TestWorld.ClientWorlds[0].EntityManager.GetComponentData<T>(clientEntity).GetValue();
                Assert.AreEqual(m_ExpectedEnabledIfReplicated, isServerEnabled, $"[{typeof(T)}] Test expects server enable bit [{i}] to still be same!");
                Assert.AreEqual(m_ExpectedValueIfReplicated, serverValue, $"[{typeof(T)}] Test expects server value [{i}] to still be same!");

                if (expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, isClientEnabled, $"[{typeof(T)}] Test expects client enable bit [{i}] IS replicated when using `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, isClientEnabled, $"[{typeof(T)}] Test expects client enable bit [{i}] NOT replicated when using `{m_SendForChildrenTestCase}`!");
                }
                if (expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    // Note that values are replicated even if the component is disabled!
                    Assert.AreEqual(m_ExpectedValueIfReplicated, clientValue, $"[{typeof(T)}] Test expects client value [{i}] IS replicated when using `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(kDefaultValueIfNotReplicated, clientValue, $"[{typeof(T)}] Test expects client value [{i}] NOT replicated when using `{m_SendForChildrenTestCase}`!");
                }
            }
        }

        private void ValidateChangeMaskForComponent<T>(bool isExpectedToBeReplicated, bool isRoot)
            where T : unmanaged, IComponentData
        {
            var componentType = ComponentType.ReadOnly<T>();
            ValidateChangeMask(isExpectedToBeReplicated, componentType, isRoot);
        }
        private void ValidateChangeMaskForBuffer<T>(bool isExpectedToBeReplicated, bool isRoot)
            where T : unmanaged, IBufferElementData
        {
            var componentType = ComponentType.ReadOnly<T>();
            ValidateChangeMask(isExpectedToBeReplicated, componentType, isRoot);
        }

        /// <summary> Tests how Change Filtering works in the <see cref="GhostUpdateSystem"/>.</summary>
        private void ValidateChangeMask(bool isExpectedToBeReplicated, ComponentType componentType, bool isRoot)
        {
            if (m_IsFirstRun) // On the first run, there will be inconsistencies. Not worth trying to handle.
                return;

            FixedList32Bytes<ComponentType> componentTypeSet = default;
            componentTypeSet.Add(componentType);
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll(ref componentTypeSet).WithOptions(EntityQueryOptions.IgnoreComponentEnabledState);
            var clientEm = m_TestWorld.ClientWorlds[0].EntityManager;
            using var query = clientEm.CreateEntityQuery(builder);
            var chunks = query.ToArchetypeChunkArray(Allocator.Temp);
            for (var chunkIdx = 0; chunkIdx < chunks.Length; chunkIdx++)
            {
                var chunk = chunks[chunkIdx];
                // If isRoot, only check parent entities, and vice versa.
                if (chunk.Has<LinkedEntityGroup>() != isRoot)
                    continue;
                var dynamicComponentTypeHandle = clientEm.GetDynamicComponentTypeHandle(componentType);
                var componentChangeVersionInChunk = chunk.GetChangeVersion(ref dynamicComponentTypeHandle);
                var didChangeSinceLastVerifyCall = ChangeVersionUtility.DidChange(componentChangeVersionInChunk, m_LastGlobalSystemVersion);

                if (m_ExpectChangeFilterToChange && isExpectedToBeReplicated)
                    Assert.IsTrue(didChangeSinceLastVerifyCall, $"[{componentType}] [Chunk:{chunkIdx}] Expected this component's change version to be updated, but it was not! {componentChangeVersionInChunk} vs {m_LastGlobalSystemVersion}. Implies a bug in GhostUpdateSystem Change Filtering.");
                else if (m_ExpectChangeFilterToChange)
                    Assert.IsFalse(didChangeSinceLastVerifyCall, $"[{componentType}] [Chunk:{chunkIdx}] We'd expected this component's change version to be updated, but it's not replicated, so it SHOULDN'T be changed! {componentChangeVersionInChunk} vs {m_LastGlobalSystemVersion}. Implies a bug in GhostUpdateSystem Change Filtering.");
                else
                    Assert.IsFalse(didChangeSinceLastVerifyCall, $"[{componentType}] [Chunk:{chunkIdx}] We did not modify this component (nor it's enabled flag), so it SHOULDN'T be changed! {componentChangeVersionInChunk} vs {m_LastGlobalSystemVersion}. Implies a bug in GhostUpdateSystem Change Filtering.");
            }
        }

        private void VerifyFlagComponentEnabledBit<T>(bool expectValueReplicated, bool expectEnabledReplicated) where T : unmanaged, IComponentData, IEnableableComponent
        {
            var type = ComponentType.ReadOnly<T>();
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(type);
            using var clientEntities = query.ToEntityArray(Allocator.Temp);
            var clientEntitiesWithoutFilteringLength = query.CalculateEntityCountWithoutFiltering();
            Assert.AreEqual(m_ServerEntities.Length, clientEntitiesWithoutFilteringLength, $"[{typeof(T)}] Expect client has entities!");

            for (int i = 0; i < clientEntities.Length; i++)
            {
                var serverEntity = m_ServerEntities[i];
                var clientEntity = clientEntities[i];

                var isServerEnabled = m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(serverEntity);
                var isClientEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntity);
                Assert.AreEqual(m_ExpectedEnabledIfReplicated, isServerEnabled, $"[{typeof(T)}] Expect flag component server enabled bit is correct.");

                if (expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, isClientEnabled, $"{typeof(T)} Expected client enabled bit IS replicated.");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, isClientEnabled, $"{typeof(T)} Expected client enabled bit is NOT replicated.");
                }
            }

            ValidateChangeMaskForComponent<T>(expectEnabledReplicated | expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true), true);
        }

        NativeArray<ArchetypeChunk> chunkArray;

        private void VerifyBufferValues<T>(bool expectValueReplicated, bool expectEnabledReplicated) where T: unmanaged, IBufferElementData, IEnableableComponent, IComponentValue
        {
            var builder = new EntityQueryBuilder(Allocator.Temp).WithAll<T>().WithOptions(EntityQueryOptions.IgnoreComponentEnabledState);
            using var query = m_TestWorld.ClientWorlds[0].EntityManager.CreateEntityQuery(builder);
            using var clientEntities = query.ToEntityArray(Allocator.Temp);
            var totalEntities = query.CalculateEntityCountWithoutFiltering();
            Assert.AreEqual(totalEntities, clientEntities.Length, $"[{typeof(T)}] Client entity count should ALWAYS be correct, regardless of setting: `{m_SendForChildrenTestCase}`!");

            for (int i = 0; i < clientEntities.Length; i++)
            {
                var serverEntity = m_ServerEntities[i];
                var clientEntity = clientEntities[i];

                var isServerEnabled = m_TestWorld.ServerWorld.EntityManager.IsComponentEnabled<T>(serverEntity);
                var isClientEnabled = m_TestWorld.ClientWorlds[0].EntityManager.IsComponentEnabled<T>(clientEntity);
                var serverBuffer = m_TestWorld.ServerWorld.EntityManager.GetBuffer<T>(serverEntity, true);
                var clientBuffer = m_TestWorld.ClientWorlds[0].EntityManager.GetBuffer<T>(clientEntity, true);

                Assert.AreEqual(m_ExpectedServerBufferSize, serverBuffer.Length, $"[{typeof(T)}] server buffer length");
                Assert.AreEqual(m_ExpectedEnabledIfReplicated, isServerEnabled, $"[{typeof(T)}] server enable bit");

                if (expectEnabledReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedEnabledIfReplicated, isClientEnabled, $"[{typeof(T)}] Client enable bit IS replicated when `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(m_ExpectedEnabledIfNotReplicated, isClientEnabled, $"[{typeof(T)}] Client enable bit is NOT replicated when `{m_SendForChildrenTestCase}`!");
                }
                if (expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                {
                    Assert.AreEqual(m_ExpectedServerBufferSize, clientBuffer.Length, $"[{typeof(T)}] Expect client buffer length IS replicated when `{m_SendForChildrenTestCase}`!");
                }
                else
                {
                    Assert.AreEqual(kBakedBufferSize, clientBuffer.Length, $"[{typeof(T)}] Expect client buffer length should NOT be replicated when `{m_SendForChildrenTestCase}`, thus should be the default CLIENT value");
                }

                for (int j = 0; j < serverBuffer.Length; ++j)
                {
                    var serverValue = serverBuffer[j];
                    var clientValue = clientBuffer[j];

                    var expectedBufferValue = m_IsValidatingBakedValues ? kDefaultValueIfNotReplicated : ((j + 1) * 1000 + m_ExpectedValueIfReplicated);
                    Assert.AreEqual(expectedBufferValue, serverValue.GetValue(), $"[{typeof(T)}] Expect server buffer value [{i}]");

                    if (expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true))
                    {
                        Assert.AreEqual(expectedBufferValue, clientValue.GetValue(), $"[{typeof(T)}] Expect client buffer value [{i}] IS replicated when `{m_SendForChildrenTestCase}`!");
                    }
                    else
                    {
                        Assert.AreEqual(kDefaultValueIfNotReplicated, clientValue.GetValue(), $"[{typeof(T)}] Expect client buffer value [{i}] is NOT replicated when `{m_SendForChildrenTestCase}`!");
                    }
                }
            }

            ValidateChangeMaskForBuffer<T>(expectEnabledReplicated | expectValueReplicated && IsExpectedToBeReplicated<T>(m_SendForChildrenTestCase, true), true);
        }

        void SetGhostValues(int value, bool enabled = false)
        {
            m_ExpectedServerBufferSize = kWrittenServerBufferSize;
            m_IsValidatingBakedValues = false;

            Assert.IsTrue(m_ServerEntities.IsCreated);
            switch (m_Type)
            {
                case GhostTypeConverter.GhostTypes.EnableableComponent:
                    SetComponentValues<EnableableComponent>(value, enabled);
                    SetComponentValues<EnableableComponentWithNonGhostField>(value, enabled);
                    SetComponentEnabled<EnableableFlagComponent>(enabled);
                    SetComponentValues<ReplicatedFieldWithNonReplicatedEnableableComponent>(value, enabled);
                    SetComponentValues<ReplicatedEnableableComponentWithNonReplicatedField>(value, enabled);
                    SetComponentValues<ComponentWithReplicatedVariant>(value, enabled);
                    SetComponentValues<ComponentWithDontSendChildrenVariant>(value, enabled);
                    SetComponentValues<ComponentWithNonReplicatedVariant>(value, enabled);
                    SetComponentEnabled<NeverReplicatedEnableableFlagComponent>(enabled);
                    break;
                case GhostTypeConverter.GhostTypes.MultipleEnableableComponent:
                    SetComponentValues<EnableableComponent_0>(value, enabled);
                    SetComponentValues<EnableableComponent_1>(value, enabled);
                    SetComponentValues<EnableableComponent_2>(value, enabled);
                    SetComponentValues<EnableableComponent_3>(value, enabled);
                    SetComponentValues<EnableableComponent_4>(value, enabled);
                    SetComponentValues<EnableableComponent_5>(value, enabled);
                    SetComponentValues<EnableableComponent_6>(value, enabled);
                    SetComponentValues<EnableableComponent_7>(value, enabled);
                    SetComponentValues<EnableableComponent_8>(value, enabled);
                    SetComponentValues<EnableableComponent_9>(value, enabled);
                    SetComponentValues<EnableableComponent_10>(value, enabled);
                    SetComponentValues<EnableableComponent_11>(value, enabled);
                    SetComponentValues<EnableableComponent_12>(value, enabled);
                    SetComponentValues<EnableableComponent_13>(value, enabled);
                    SetComponentValues<EnableableComponent_14>(value, enabled);
                    SetComponentValues<EnableableComponent_15>(value, enabled);
                    SetComponentValues<EnableableComponent_16>(value, enabled);
                    SetComponentValues<EnableableComponent_17>(value, enabled);
                    SetComponentValues<EnableableComponent_18>(value, enabled);
                    SetComponentValues<EnableableComponent_19>(value, enabled);
                    SetComponentValues<EnableableComponent_20>(value, enabled);
                    SetComponentValues<EnableableComponent_21>(value, enabled);
                    SetComponentValues<EnableableComponent_22>(value, enabled);
                    SetComponentValues<EnableableComponent_23>(value, enabled);
                    SetComponentValues<EnableableComponent_24>(value, enabled);
                    SetComponentValues<EnableableComponent_25>(value, enabled);
                    SetComponentValues<EnableableComponent_26>(value, enabled);
                    SetComponentValues<EnableableComponent_27>(value, enabled);
                    SetComponentValues<EnableableComponent_28>(value, enabled);
                    SetComponentValues<EnableableComponent_29>(value, enabled);
                    SetComponentValues<EnableableComponent_30>(value, enabled);
                    SetComponentValues<EnableableComponent_31>(value, enabled);
                    SetComponentValues<EnableableComponent_32>(value, enabled);
                    break;
                case GhostTypeConverter.GhostTypes.EnableableBuffer:
                    SetBufferValues<EnableableBuffer>(value, enabled);
                    break;
                case GhostTypeConverter.GhostTypes.MultipleEnableableBuffer:
                    SetBufferValues<EnableableBuffer_0>(value, enabled);
                    SetBufferValues<EnableableBuffer_1>(value, enabled);
                    SetBufferValues<EnableableBuffer_2>(value, enabled);
                    SetBufferValues<EnableableBuffer_3>(value, enabled);
                    SetBufferValues<EnableableBuffer_4>(value, enabled);
                    SetBufferValues<EnableableBuffer_5>(value, enabled);
                    SetBufferValues<EnableableBuffer_6>(value, enabled);
                    SetBufferValues<EnableableBuffer_7>(value, enabled);
                    SetBufferValues<EnableableBuffer_8>(value, enabled);
                    SetBufferValues<EnableableBuffer_9>(value, enabled);
                    SetBufferValues<EnableableBuffer_10>(value, enabled);
                    SetBufferValues<EnableableBuffer_11>(value, enabled);
                    SetBufferValues<EnableableBuffer_12>(value, enabled);
                    SetBufferValues<EnableableBuffer_13>(value, enabled);
                    SetBufferValues<EnableableBuffer_14>(value, enabled);
                    SetBufferValues<EnableableBuffer_15>(value, enabled);
                    SetBufferValues<EnableableBuffer_16>(value, enabled);
                    SetBufferValues<EnableableBuffer_17>(value, enabled);
                    SetBufferValues<EnableableBuffer_18>(value, enabled);
                    SetBufferValues<EnableableBuffer_19>(value, enabled);
                    SetBufferValues<EnableableBuffer_20>(value, enabled);
                    SetBufferValues<EnableableBuffer_21>(value, enabled);
                    SetBufferValues<EnableableBuffer_22>(value, enabled);
                    SetBufferValues<EnableableBuffer_23>(value, enabled);
                    SetBufferValues<EnableableBuffer_24>(value, enabled);
                    SetBufferValues<EnableableBuffer_25>(value, enabled);
                    SetBufferValues<EnableableBuffer_26>(value, enabled);
                    SetBufferValues<EnableableBuffer_27>(value, enabled);
                    SetBufferValues<EnableableBuffer_28>(value, enabled);
                    SetBufferValues<EnableableBuffer_29>(value, enabled);
                    SetBufferValues<EnableableBuffer_30>(value, enabled);
                    SetBufferValues<EnableableBuffer_31>(value, enabled);
                    SetBufferValues<EnableableBuffer_32>(value, enabled);
                    break;
                case GhostTypeConverter.GhostTypes.ChildComponent:
                    SetLinkedComponentValues<EnableableComponent>(value, enabled);
                    SetLinkedComponentValues<EnableableComponentWithNonGhostField>(value, enabled);
                    SetLinkedComponentEnabled<EnableableFlagComponent>(enabled);
                    SetLinkedComponentValues<ReplicatedFieldWithNonReplicatedEnableableComponent>(value, enabled);
                    SetLinkedComponentValues<ReplicatedEnableableComponentWithNonReplicatedField>(value, enabled);
                    SetLinkedComponentValues<ComponentWithReplicatedVariant>(value, enabled);
                    SetLinkedComponentValues<ComponentWithDontSendChildrenVariant>(value, enabled);
                    SetLinkedComponentValues<ComponentWithNonReplicatedVariant>(value, enabled);
                    SetLinkedComponentEnabled<NeverReplicatedEnableableFlagComponent>(enabled);
                    SetLinkedComponentEnabledOnlyOnChildren<ChildOnlyComponent_1>(enabled);
                    SetLinkedComponentEnabledOnlyOnChildren<ChildOnlyComponent_2>(enabled);
                    SetLinkedComponentValueOnlyOnChildren<ChildOnlyComponent_3>(value, enabled);
                    SetLinkedComponentValueOnlyOnChildren<ChildOnlyComponent_4>(value, enabled);
                    break;
                case GhostTypeConverter.GhostTypes.ChildBufferComponent:
                    SetLinkedBufferValues<EnableableBuffer>(value, enabled);
                    break;
                case GhostTypeConverter.GhostTypes.GhostGroup:
                    SetGhostGroupValues<EnableableComponent>(value, enabled);
                    SetGhostGroupValues<EnableableComponentWithNonGhostField>(value, enabled);
                    SetGhostGroupEnabled<EnableableFlagComponent>(enabled);
                    SetGhostGroupValues<ReplicatedFieldWithNonReplicatedEnableableComponent>(value, enabled);
                    SetGhostGroupValues<ReplicatedEnableableComponentWithNonReplicatedField>(value, enabled);
                    SetGhostGroupValues<ComponentWithReplicatedVariant>(value, enabled);
                    SetGhostGroupValues<ComponentWithDontSendChildrenVariant>(value, enabled);
                    SetGhostGroupValues<ComponentWithNonReplicatedVariant>(value, enabled);
                    SetGhostGroupEnabled<NeverReplicatedEnableableFlagComponent>(enabled);
                    break;
                default:
                    Assert.True(true);
                    break;
            }
        }

        void VerifyGhostValues(int value, bool enabled, SendForChildrenTestCase sendForChildrenTestCase, EnabledBitBakedValue enabledBitBakedValue)
        {
            Assert.IsTrue(m_ServerEntities.IsCreated);
            m_ExpectedValueIfReplicated = value;
            m_ExpectedEnabledIfReplicated = enabled;
            m_ExpectedEnabledIfNotReplicated = GhostTypeConverter.BakedEnabledBitValue(enabledBitBakedValue);
            m_SendForChildrenTestCase = sendForChildrenTestCase;
            switch (m_Type)
            {
                case GhostTypeConverter.GhostTypes.EnableableComponent:
                    VerifyComponentValues<EnableableComponent>(true, true);
                    VerifyComponentValues<EnableableComponentWithNonGhostField>(true, true);
                    VerifyFlagComponentEnabledBit<EnableableFlagComponent>(false, true);
                    VerifyComponentValues<ReplicatedFieldWithNonReplicatedEnableableComponent>(true, false);
                    VerifyComponentValues<ReplicatedEnableableComponentWithNonReplicatedField>(false, true);
                    VerifyComponentValues<ComponentWithReplicatedVariant>(true, true);
                    VerifyComponentValues<ComponentWithDontSendChildrenVariant>(true, true);
                    VerifyComponentValues<ComponentWithNonReplicatedVariant>(false, false);
                    VerifyFlagComponentEnabledBit<NeverReplicatedEnableableFlagComponent>(false, false);
                    break;
                case GhostTypeConverter.GhostTypes.MultipleEnableableComponent:
                    VerifyComponentValues<EnableableComponent_1>(true, true);
                    VerifyComponentValues<EnableableComponent_2>(true, true);
                    VerifyComponentValues<EnableableComponent_3>(true, true);
                    VerifyComponentValues<EnableableComponent_4>(true, true);
                    VerifyComponentValues<EnableableComponent_5>(true, true);
                    VerifyComponentValues<EnableableComponent_6>(true, true);
                    VerifyComponentValues<EnableableComponent_7>(true, true);
                    VerifyComponentValues<EnableableComponent_8>(true, true);
                    VerifyComponentValues<EnableableComponent_9>(true, true);
                    VerifyComponentValues<EnableableComponent_10>(true, true);
                    VerifyComponentValues<EnableableComponent_11>(true, true);
                    VerifyComponentValues<EnableableComponent_12>(true, true);
                    VerifyComponentValues<EnableableComponent_13>(true, true);
                    VerifyComponentValues<EnableableComponent_14>(true, true);
                    VerifyComponentValues<EnableableComponent_15>(true, true);
                    VerifyComponentValues<EnableableComponent_16>(true, true);
                    VerifyComponentValues<EnableableComponent_17>(true, true);
                    VerifyComponentValues<EnableableComponent_18>(true, true);
                    VerifyComponentValues<EnableableComponent_19>(true, true);
                    VerifyComponentValues<EnableableComponent_20>(true, true);
                    VerifyComponentValues<EnableableComponent_21>(true, true);
                    VerifyComponentValues<EnableableComponent_22>(true, true);
                    VerifyComponentValues<EnableableComponent_23>(true, true);
                    VerifyComponentValues<EnableableComponent_24>(true, true);
                    VerifyComponentValues<EnableableComponent_25>(true, true);
                    VerifyComponentValues<EnableableComponent_26>(true, true);
                    VerifyComponentValues<EnableableComponent_27>(true, true);
                    VerifyComponentValues<EnableableComponent_28>(true, true);
                    VerifyComponentValues<EnableableComponent_29>(true, true);
                    VerifyComponentValues<EnableableComponent_30>(true, true);
                    VerifyComponentValues<EnableableComponent_31>(true, true);
                    VerifyComponentValues<EnableableComponent_32>(true, true);
                    break;
                case GhostTypeConverter.GhostTypes.EnableableBuffer:
                    VerifyBufferValues<EnableableBuffer>(true, true);
                    break;
                case GhostTypeConverter.GhostTypes.MultipleEnableableBuffer:
                    VerifyBufferValues<EnableableBuffer_0>(true, true);
                    VerifyBufferValues<EnableableBuffer_1>(true, true);
                    VerifyBufferValues<EnableableBuffer_2>(true, true);
                    VerifyBufferValues<EnableableBuffer_3>(true, true);
                    VerifyBufferValues<EnableableBuffer_4>(true, true);
                    VerifyBufferValues<EnableableBuffer_5>(true, true);
                    VerifyBufferValues<EnableableBuffer_6>(true, true);
                    VerifyBufferValues<EnableableBuffer_7>(true, true);
                    VerifyBufferValues<EnableableBuffer_8>(true, true);
                    VerifyBufferValues<EnableableBuffer_9>(true, true);
                    VerifyBufferValues<EnableableBuffer_10>(true, true);
                    VerifyBufferValues<EnableableBuffer_11>(true, true);
                    VerifyBufferValues<EnableableBuffer_12>(true, true);
                    VerifyBufferValues<EnableableBuffer_13>(true, true);
                    VerifyBufferValues<EnableableBuffer_14>(true, true);
                    VerifyBufferValues<EnableableBuffer_15>(true, true);
                    VerifyBufferValues<EnableableBuffer_16>(true, true);
                    VerifyBufferValues<EnableableBuffer_17>(true, true);
                    VerifyBufferValues<EnableableBuffer_18>(true, true);
                    VerifyBufferValues<EnableableBuffer_19>(true, true);
                    VerifyBufferValues<EnableableBuffer_20>(true, true);
                    VerifyBufferValues<EnableableBuffer_21>(true, true);
                    VerifyBufferValues<EnableableBuffer_22>(true, true);
                    VerifyBufferValues<EnableableBuffer_23>(true, true);
                    VerifyBufferValues<EnableableBuffer_24>(true, true);
                    VerifyBufferValues<EnableableBuffer_25>(true, true);
                    VerifyBufferValues<EnableableBuffer_26>(true, true);
                    VerifyBufferValues<EnableableBuffer_27>(true, true);
                    VerifyBufferValues<EnableableBuffer_28>(true, true);
                    VerifyBufferValues<EnableableBuffer_29>(true, true);
                    VerifyBufferValues<EnableableBuffer_30>(true, true);
                    VerifyBufferValues<EnableableBuffer_31>(true, true);
                    VerifyBufferValues<EnableableBuffer_32>(true, true);
                    break;
                case GhostTypeConverter.GhostTypes.ChildComponent:
                    VerifyLinkedComponentValues<EnableableComponent>(true, true);
                    VerifyLinkedComponentValues<EnableableComponentWithNonGhostField>(true, true);
                    VerifyLinkedComponentEnabled<EnableableFlagComponent>(false, true);
                    VerifyLinkedComponentValues<ReplicatedFieldWithNonReplicatedEnableableComponent>(true, false);
                    VerifyLinkedComponentValues<ReplicatedEnableableComponentWithNonReplicatedField>(false, true);
                    // We override variants for these two, so cannot test their "default variants" without massive complications.
                    VerifyLinkedComponentValues<ComponentWithReplicatedVariant>(true, true, ComponentWithReplicatedVariant.ExpectChildReplicated(m_SendForChildrenTestCase));
                    VerifyLinkedComponentEnabled<ComponentWithNonReplicatedVariant>(false, ComponentWithNonReplicatedVariant.ExpectReplicate(m_SendForChildrenTestCase));
                    // Note: We don't test the component on the root here.
                    VerifyLinkedComponentValues<ComponentWithDontSendChildrenVariant>(ComponentWithDontSendChildrenVariant.ExpectReplicate(m_SendForChildrenTestCase), ComponentWithDontSendChildrenVariant.ExpectReplicate(m_SendForChildrenTestCase), ComponentWithDontSendChildrenVariant.ExpectChildReplicate(m_SendForChildrenTestCase));
                    VerifyLinkedComponentEnabled<NeverReplicatedEnableableFlagComponent>(false, false);

                    VerifyLinkedComponentEnabledOnChild<ChildOnlyComponent_1>(false, true);
                    VerifyLinkedComponentEnabledOnChild<ChildOnlyComponent_2>(false, false);
                    VerifyLinkedComponentValueOnChild<ChildOnlyComponent_3>(true, true);
                    VerifyLinkedComponentValueOnChild<ChildOnlyComponent_4>(true, false);
                    break;
                case GhostTypeConverter.GhostTypes.ChildBufferComponent:
                    VerifyLinkedBufferValues<EnableableBuffer>(true, true);
                    break;
                case GhostTypeConverter.GhostTypes.GhostGroup:
                    // GhostGroup implies all of these are root entities! I.e. No children to worry about, so `_sendForChildrenTestCase` is ignored.
                    VerifyGhostGroupValues<EnableableComponent>(true, true);
                    VerifyGhostGroupValues<EnableableComponentWithNonGhostField>(true, true);
                    VerifyGhostGroupEnabledBits<EnableableFlagComponent>(false, true);
                    VerifyGhostGroupValues<ReplicatedFieldWithNonReplicatedEnableableComponent>(true, false);
                    VerifyGhostGroupValues<ReplicatedEnableableComponentWithNonReplicatedField>(false, true);
                    VerifyGhostGroupValues<ComponentWithReplicatedVariant>(true, true);
                    VerifyGhostGroupValues<ComponentWithDontSendChildrenVariant>(ComponentWithDontSendChildrenVariant.ExpectReplicate(m_SendForChildrenTestCase), ComponentWithDontSendChildrenVariant.ExpectReplicate(m_SendForChildrenTestCase));
                    VerifyGhostGroupValues<ComponentWithNonReplicatedVariant>(ComponentWithNonReplicatedVariant.ExpectReplicate(m_SendForChildrenTestCase), ComponentWithNonReplicatedVariant.ExpectReplicate(m_SendForChildrenTestCase));
                    VerifyGhostGroupEnabledBits<NeverReplicatedEnableableFlagComponent>(false, false);
                    // TODO - Ghost groups cannot have children, right?
                    break;
                default:
                    Assert.Fail();
                    break;
            }
        }


        public const int kDefaultValueIfNotReplicated = -33;
        public const int kDefaultValueForNonGhostFields = -9205;

        const int kWrittenServerBufferSize = 13;
        internal const int kBakedBufferSize = 29;

        NetCodeTestWorld m_TestWorld;
        NativeArray<Entity> m_ServerEntities;
        GhostTypeConverter.GhostTypes m_Type;
        private int m_ExpectedServerBufferSize;
        int m_ExpectedValueIfReplicated;
        bool m_ExpectedEnabledIfReplicated;
        bool m_ExpectedEnabledIfNotReplicated;
        SendForChildrenTestCase m_SendForChildrenTestCase;
        bool m_IsValidatingBakedValues;
        bool m_ExpectChangeFilterToChange;
        uint m_LastGlobalSystemVersion;
        private bool m_IsFirstRun;

        enum GhostFlags : int
        {
            None = 0,
            StaticOptimization = 1 << 0,
            PreSerialize = 1 << 2
        };

        void RunTest(int numClients, GhostTypeConverter.GhostTypes type, int entityCount, GhostFlags flags, SendForChildrenTestCase sendForChildrenTestCase, PredictionSetting predictionSetting, EnabledBitBakedValue enabledBitBakedValue)
        {
            // Create worlds:
            switch (sendForChildrenTestCase)
            {
                case SendForChildrenTestCase.YesViaExplicitVariantRule:
                    m_TestWorld.TestSpecificAdditionalSystems.Add(typeof(ForceSerializeSystem));
                    break;
                case SendForChildrenTestCase.YesViaExplicitVariantOnlyAllowChildrenToReplicateRule:
                    m_TestWorld.TestSpecificAdditionalSystems.Add(typeof(ForceSerializeOnlyChildrenSystem));
                    break;
                case SendForChildrenTestCase.NoViaExplicitDontSerializeVariantRule:
                    m_TestWorld.TestSpecificAdditionalSystems.Add(typeof(ForceDontSerializeSystem));
                    break;
            }

            m_TestWorld.Bootstrap(true);

            // Create ghosts:
            var prefabCount = 1;
            this.m_Type = type;
            if (type == GhostTypeConverter.GhostTypes.GhostGroup)
            {
                prefabCount = 2;
            }

            GameObject[] objects = new GameObject[prefabCount];
            var objectsToAddInspectionsTo = new List<GameObject>(8);
            for (int i = 0; i < prefabCount; i++)
            {
                if (type == GhostTypeConverter.GhostTypes.GhostGroup)
                {
                    objects[i] = new GameObject("ParentGhost");
                    objects[i].AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeConverter(type, enabledBitBakedValue);
                    i++;
                    objects[i] = new GameObject("ChildGhost");
                    objects[i].AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeConverter(type, enabledBitBakedValue);

                    continue;
                }

                objects[i] = new GameObject("Root");
                objects[i].AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeConverter(type, enabledBitBakedValue);

                if (type == GhostTypeConverter.GhostTypes.ChildComponent)
                {
                    var child = new GameObject("ChildComp");
                    child.transform.parent = objects[i].transform;
                    child.AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeConverter(type, enabledBitBakedValue);
                    objectsToAddInspectionsTo.Add(child);
                }
                else if (type == GhostTypeConverter.GhostTypes.ChildBufferComponent)
                {
                    var child = new GameObject("ChildBuffer");
                    child.transform.parent = objects[i].transform;
                    child.AddComponent<TestNetCodeAuthoring>().Converter = new GhostTypeConverter(type, enabledBitBakedValue);
                    objectsToAddInspectionsTo.Add(child);
                }
            }

            objectsToAddInspectionsTo.AddRange(objects);
            if (sendForChildrenTestCase == SendForChildrenTestCase.YesViaInspectionComponentOverride)
            {
                var optionalOverrides = BuildComponentOverridesForComponents();
                foreach (var go in objectsToAddInspectionsTo)
                {
                    var ghostAuthoringInspectionComponent = go.AddComponent<GhostAuthoringInspectionComponent>();
                    foreach (var componentOverride in optionalOverrides)
                    {
                        ref var @override = ref ghostAuthoringInspectionComponent.AddComponentOverrideRaw();
                        @override = componentOverride;
                        @override.EntityIndex = default;
                    }
                }
            }

            var ghostConfig = objects[0].AddComponent<GhostAuthoringComponent>();
            ghostConfig.DefaultGhostMode = predictionSetting == PredictionSetting.WithPredictedEntities ? GhostMode.Predicted : GhostMode.Interpolated;
            ghostConfig.SupportedGhostModes = GhostModeMask.All;

            if ((flags & GhostFlags.StaticOptimization) == GhostFlags.StaticOptimization)
            {
                ghostConfig.OptimizationMode = GhostOptimizationMode.Static;
            }

            if ((flags & GhostFlags.PreSerialize) == GhostFlags.PreSerialize)
            {
                ghostConfig.UsePreSerialization = true;
            }

            Assert.IsTrue(m_TestWorld.CreateGhostCollection(objects));
            m_TestWorld.CreateWorlds(true, numClients);

            entityCount *= prefabCount;
            m_ServerEntities = new NativeArray<Entity>(entityCount, Allocator.Persistent);

            var step = objects.Length;
            for (int i = 0; i < entityCount; i += step)
            {
                for (int j = 0; j < step; j++)
                {
                    m_ServerEntities[i + j] = m_TestWorld.SpawnOnServer(objects[j]);
                }
            }

            if (type == GhostTypeConverter.GhostTypes.GhostGroup)
            {
                for (int i = 0; i < entityCount; i += 2)
                {
                    m_TestWorld.ServerWorld.EntityManager.GetBuffer<GhostGroup>(m_ServerEntities[i]).Add(new GhostGroup {Value = m_ServerEntities[i + 1]});
                }
            }

            if (type == GhostTypeConverter.GhostTypes.ChildComponent)
            {
                foreach (var entity in m_ServerEntities)
                {
                    Assert.IsTrue(m_TestWorld.ServerWorld.EntityManager.HasComponent<LinkedEntityGroup>(entity));
                }
            }

            // Connect and make sure the connection could be established
            Assert.IsTrue(m_TestWorld.Connect(frameTime, 4));

            // Go in-game
            m_TestWorld.GoInGame();

            // Perform test:
            {
                m_IsFirstRun = true;
                m_ExpectChangeFilterToChange = true;

                ValidateBakedValues(enabledBitBakedValue, sendForChildrenTestCase, type);

                void SingleTest(int value, bool enabled)
                {
                    SetGhostValues(value, enabled);
                    m_LastGlobalSystemVersion = m_TestWorld.ClientWorlds[0].EntityManager.GlobalSystemVersion;
                    TickMultipleFrames(GetNumTicksToReplicateGhostTypes(type));
                    VerifyGhostValues(value, enabled, sendForChildrenTestCase, enabledBitBakedValue);
                }

                SingleTest(-999, false);
                m_IsFirstRun = false;
                SingleTest(999, true);
            }

            // Testing Change Filtering: Expecting no change beyond this point!
            m_ExpectChangeFilterToChange = false;
            m_LastGlobalSystemVersion = m_TestWorld.ClientWorlds[0].EntityManager.GlobalSystemVersion;
            TickMultipleFrames(15);
            VerifyGhostValues(999, true, sendForChildrenTestCase, enabledBitBakedValue);
        }

        /// <summary>
        /// To test whether or not a ghosts baked enabled bit status is properly respected, we need to:
        /// 1. CREATE the entities.
        /// 2. WAIT for them to be spawned on the client.
        /// 3. VERIFY that the baked value and enabled-bit matches the baked values on the prefab.
        /// </summary>
        private void ValidateBakedValues(EnabledBitBakedValue enabledBitBakedValue, SendForChildrenTestCase sendForChildrenTestCase, GhostTypeConverter.GhostTypes type)
        {
            if (GhostTypeConverter.WaitForClientEntitiesToSpawn(enabledBitBakedValue))
            {
                m_IsValidatingBakedValues = true;
                m_LastGlobalSystemVersion = m_TestWorld.ClientWorlds[0].EntityManager.GlobalSystemVersion;
                m_ExpectedServerBufferSize = kBakedBufferSize; // We haven't written to the server buffers yet.
                TickMultipleFrames(GetNumTicksToReplicateGhostTypes(type));
                VerifyGhostValues(kDefaultValueIfNotReplicated, GhostTypeConverter.BakedEnabledBitValue(enabledBitBakedValue), sendForChildrenTestCase, enabledBitBakedValue);
            }
        }

        [SetUp]
        public void SetupTestsForEnableableBits()
        {
            m_TestWorld = new NetCodeTestWorld();
        }

        [TearDown]
        public void TearDownTestsForEnableableBits()
        {
            if (m_ServerEntities.IsCreated)
                m_ServerEntities.Dispose();
            if (chunkArray.IsCreated)
                chunkArray.Dispose();
            m_TestWorld.Dispose();
        }

        [Test]
        public void GhostsAreSerializedWithEnabledBits([Values]PredictionSetting predictionSetting,[Values]GhostTypeConverter.GhostTypes type, [Values(1, 8)]int count, [Values]SendForChildrenTestCase sendForChildrenTestCase)
        {
            RunTest(1, type, count, GhostFlags.None, sendForChildrenTestCase, predictionSetting, EnabledBitBakedValue.StartDisabledAndWriteImmediately);
        }

        [DisableAutoCreation]
        partial class ForceSerializeSystem : DefaultVariantSystemBase
        {
            protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
            {
                var typesToOverride = GhostTypeConverter.FetchAllTestComponentTypesRequiringSendRuleOverride();
                foreach (var tuple in typesToOverride)
                {
                    defaultVariants.Add(tuple.Item1, Rule.ForAll(tuple.Item2 ?? tuple.Item1));
                }
            }
        }

        [DisableAutoCreation]
        partial class ForceSerializeOnlyChildrenSystem : DefaultVariantSystemBase
        {
            protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
            {
                var typesToOverride = GhostTypeConverter.FetchAllTestComponentTypesRequiringSendRuleOverride();
                foreach (var tuple in typesToOverride)
                {
                    defaultVariants.Add(tuple.Item1, Rule.Unique(typeof(DontSerializeVariant), tuple.Item2 ?? tuple.Item1));
                }
            }
        }
        [DisableAutoCreation]
        partial class ForceDontSerializeSystem : DefaultVariantSystemBase
        {
            protected override void RegisterDefaultVariants(Dictionary<ComponentType, Rule> defaultVariants)
            {
                var typesToOverride = GhostTypeConverter.FetchAllTestComponentTypesRequiringSendRuleOverride();
                foreach (var tuple in typesToOverride)
                {
                    defaultVariants.Add(tuple.Item1, Rule.ForAll(typeof(DontSerializeVariant)));
                }
            }
        }

        static GhostAuthoringInspectionComponent.ComponentOverride[] BuildComponentOverridesForComponents()
        {
            var testTypes = GhostTypeConverter.FetchAllTestComponentTypesRequiringSendRuleOverride();
            var overrides = testTypes
                .Select(x =>
                {
                    var componentTypeFullName = x.Item1.FullName;
                    var variantTypeName = x.Item2?.FullName ?? componentTypeFullName;
                    return new GhostAuthoringInspectionComponent.ComponentOverride
                    {
                        FullTypeName = componentTypeFullName,
                        PrefabType = GhostPrefabType.All,
                        SendTypeOptimization = GhostSendType.AllClients,
                        VariantHash = GhostVariantsUtility.UncheckedVariantHashNBC(variantTypeName, componentTypeFullName),
                    };
                }).ToArray();
            return overrides;
        }

        [Test]
        public void GhostsAreSerializedWithEnabledBits_PreSerialize([Values]GhostTypeConverter.GhostTypes type, [Values(1, 8)]int count,
            [Values]SendForChildrenTestCase sendForChildrenTestCase, [Values]EnabledBitBakedValue enabledBitBakedValue)
        {
            RunTest(1, type, count, GhostFlags.PreSerialize, sendForChildrenTestCase, PredictionSetting.WithInterpolatedEntities, enabledBitBakedValue);
        }

        [Test]
        public void GhostsAreSerializedWithEnabledBits_StaticOptimize(
            [Values (GhostTypeConverter.GhostTypes.EnableableComponent, GhostTypeConverter.GhostTypes.EnableableBuffer,
                    GhostTypeConverter.GhostTypes.MultipleEnableableComponent, GhostTypeConverter.GhostTypes.MultipleEnableableBuffer)]
            GhostTypeConverter.GhostTypes type, [Values(1, 8)]int count, [Values]SendForChildrenTestCase sendForChildrenTestCase, [Values]EnabledBitBakedValue enabledBitBakedValue)
        {
            // Just making sure we dont run with groups or children as they do not support static optimization.
            if (type == GhostTypeConverter.GhostTypes.GhostGroup ||
                type == GhostTypeConverter.GhostTypes.ChildComponent ||
                type == GhostTypeConverter.GhostTypes.ChildBufferComponent)
                throw new InvalidOperationException($"StaticOptimization doesn't work with '{type}'! Test setup invalid!");

            RunTest(1, type, count, GhostFlags.StaticOptimization, sendForChildrenTestCase, PredictionSetting.WithInterpolatedEntities, enabledBitBakedValue);
        }

        internal static bool IsExpectedToBeReplicated<T>(SendForChildrenTestCase sendForChildrenTestCase, bool isRoot)
        {
            switch (sendForChildrenTestCase)
            {
                case SendForChildrenTestCase.YesViaExplicitVariantRule:
                case SendForChildrenTestCase.YesViaInspectionComponentOverride:
                    return true;
                case SendForChildrenTestCase.NoViaExplicitDontSerializeVariantRule:
                    return false;
                case SendForChildrenTestCase.Default:
                    return isRoot || HasSendForChildrenFlagOnAttribute<T>();
                case SendForChildrenTestCase.YesViaExplicitVariantOnlyAllowChildrenToReplicateRule:
                    return !isRoot;
                default:
                    throw new ArgumentOutOfRangeException(nameof(sendForChildrenTestCase), sendForChildrenTestCase, nameof(IsExpectedToBeReplicated));
            }
        }

        private static bool HasSendForChildrenFlagOnAttribute<T>()
        {
            var attribute = typeof(T).GetCustomAttribute(typeof(GhostComponentAttribute)) as GhostComponentAttribute;
            return attribute != null && attribute.SendDataForChildEntity;
        }

        /// <summary>Ensure the GhostUpdateSystem doesn't corrupt fields without the <see cref="GhostFieldAttribute"/>.</summary>
        public static void EnsureNonGhostFieldValueIsNotClobbered(int nonGhostField)
        {
            Assert.AreEqual(kDefaultValueForNonGhostFields, nonGhostField, $"Expecting `nonGhostField` has not been clobbered by changes to this component!");
        }
    }
}
