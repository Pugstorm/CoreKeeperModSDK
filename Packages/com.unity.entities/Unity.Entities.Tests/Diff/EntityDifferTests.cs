using System;
using System.Text.RegularExpressions;
using NUnit.Framework;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;
using UnityEngine.TestTools;

namespace Unity.Entities.Tests
{
    [TestFixture]
    sealed class EntityDifferTests : EntityDifferTestFixture
    {
        [Test]
        public void EntityDiffer_GetChanges_NoChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }
            }
        }

        // https://unity3d.atlassian.net/browse/DOTSR-1435
        /// <summary>
        /// Generates a change set over the world and efficiently updates the internal shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithFastForward()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData));

                var entityGuid = CreateEntityGuid();

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                    EntityManagerDifferOptions.IncludeReverseChangeSet |
                    EntityManagerDifferOptions.FastForwardShadowWorld;

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // Forward changes is all changes needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);

                    // Reverse changes is all changes needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                }

                // The inner shadow world was updated during the last call which means no new changes should be found.
                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }
            }
        }

        /// <summary>
        /// Generates a change set over the world without updating the shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithoutFastForward()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                    EntityManagerDifferOptions.IncludeReverseChangeSet;

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // ForwardChanges defines all operations needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);

                    // ReverseChanges defines all operations needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                }

                // Since we did not fast forward the inner shadow world. We should be able to generate the exact same changes again.
                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);

                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_IncrementalChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Mutate some component data.
                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 10 });

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the one data change.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // The ForwardChangeSet will contain a set value 10
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);

                    // The ReverseChangeSet will contain a set value 9
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                }

                SrcEntityManager.DestroyEntity(entity);
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ReverseChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                }
            }
        }


        // This test checks that if there are multiple entities per archetype and multiple archetypes that each
        // entity is added to their own archetype, and that the correct components are added to the archetype
        [Test] public void EntityDiffer_GetChanges_CreateArchetypeEntity_MultipleEntities()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                NativeArray<Entity> entities = new NativeArray<Entity>(new []
                {
                    SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData)),
                    SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData)),
                    SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData2)),
                    SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData2))
                }, Allocator.Temp);

                SrcEntityManager.SetComponentData(entities[0], CreateEntityGuid());
                SrcEntityManager.SetComponentData(entities[1], CreateEntityGuid());
                SrcEntityManager.SetComponentData(entities[2], CreateEntityGuid());
                SrcEntityManager.SetComponentData(entities[3], CreateEntityGuid());

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // The ForwardChangeSet will contain a set value 10
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(4, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(8, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);

                    Assert.AreEqual(2, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.IsTrue(changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Contains(TypeManager.GetTypeIndex(typeof(EcsTestData))));
                    Assert.AreEqual(2, changes.ForwardChangeSet.AddArchetypes[1].EntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddArchetypes[1].TypeIndices.Length); // +1 due to Simulate
                    Assert.IsTrue(changes.ForwardChangeSet.AddArchetypes[1].TypeIndices.Contains(TypeManager.GetTypeIndex(typeof(EcsTestData2))));
                }

                SrcEntityManager.DestroyEntity(entities);
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(4, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(4, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(8, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);

                    Assert.AreEqual(2, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(2, changes.ReverseChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ReverseChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.IsTrue(changes.ReverseChangeSet.AddArchetypes[0].TypeIndices.Contains(TypeManager.GetTypeIndex(typeof(EcsTestData))));
                    Assert.AreEqual(2, changes.ReverseChangeSet.AddArchetypes[1].EntityCount);
                    Assert.AreEqual(3, changes.ReverseChangeSet.AddArchetypes[1].TypeIndices.Length); // +1 due to Simulate
                    Assert.IsTrue(changes.ReverseChangeSet.AddArchetypes[1].TypeIndices.Contains(TypeManager.GetTypeIndex(typeof(EcsTestData2))));
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetDefaultSharedComponentData_NoChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestSharedComp));
                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetSharedComponentManaged(entity, default(EcsTestSharedComp));
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetSharedComponents.Length);
                }

                SrcEntityManager.SetSharedComponentManaged(entity, new EcsTestSharedComp { value = 1});
                SrcEntityManager.SetSharedComponentManaged(entity, default(EcsTestSharedComp));
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.HasForwardChangeSet);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetSharedComponentData_IncrementalChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestSharedComp));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetSharedComponentManaged(entity, new EcsTestSharedComp { value = 2 });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(3, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetSharedComponents.Length);
                }
            }
        }

        [Test]
        public unsafe void EntityDiffer_GetChanges_RefCounts_SharedComponents()
        {
            int RefCount1 = 0;
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestSharedCompWithRefCount));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetSharedComponentManaged(entity, new EcsTestSharedCompWithRefCount(&RefCount1));

                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, RefCount1);
                }

                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(3, RefCount1);
                }

                Assert.AreEqual(2, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, RefCount1);
                }

                Assert.AreEqual(2, RefCount1);

                SrcEntityManager.RemoveComponent<EcsTestSharedCompWithRefCount>(entity);

                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(1, RefCount1);
                }

                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.IncludeReverseChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, RefCount1);
                }

                Assert.AreEqual(1, RefCount1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(0, RefCount1);
                }
            }
            Assert.AreEqual(0, RefCount1);
        }


        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetEnableableComponent_IncrementalChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestEmptyEnableable1), typeof(EcsTestEmptyEnableable2), typeof(EcsTestDataEnableable), typeof(EcsTestDataEnableable2));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentEnabled<EcsTestEmptyEnableable1>(entity, true);
                SrcEntityManager.SetComponentEnabled<EcsTestEmptyEnableable2>(entity, false);
                SrcEntityManager.SetComponentEnabled<EcsTestDataEnableable>(entity, true);
                SrcEntityManager.SetComponentEnabled<EcsTestDataEnableable2>(entity, false);

                var entityGuidTypeIndex = TypeManager.GetTypeIndex<EntityGuid>();
                var empty1TypeIndex = TypeManager.GetTypeIndex<EcsTestEmptyEnableable1>(); // empty component
                var empty2TypeIndex = TypeManager.GetTypeIndex<EcsTestEmptyEnableable2>(); // empty component
                var component1TypeIndex = TypeManager.GetTypeIndex<EcsTestDataEnableable>(); // non-empty component
                var component2TypeIndex = TypeManager.GetTypeIndex<EcsTestDataEnableable2>(); // non-empty component

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(6, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(4, changes.ForwardChangeSet.SetComponents.Length); // Enabled to true are ignored in the differ

                    var packedComponents = changes.ForwardChangeSet.TypeHashes;
                    foreach (var setComponent in changes.ForwardChangeSet.SetComponents)
                    {
                        var componentHash = packedComponents[setComponent.Component.PackedTypeIndex];
                        var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(componentHash.StableTypeHash);
                        if (typeIndex == entityGuidTypeIndex)
                        {
                            Assert.AreEqual(-1, setComponent.Enabled); // Is not enableable
                        }
                        else if (typeIndex == empty2TypeIndex)
                        {
                            Assert.AreEqual(0, setComponent.Enabled); // Set to false
                        }
                        else if (typeIndex == component1TypeIndex)
                        {
                            Assert.AreEqual(-1, setComponent.Enabled); // Set to true, but enableable bit is ignored
                        }
                        else if (typeIndex == component2TypeIndex)
                        {
                            Assert.AreEqual(0, setComponent.Enabled); // Set to false
                        }
                        else
                        {
                            // Simulate and EcsTestEnableableComp1 are empty and set to true, so are ignored when being added
                            throw new ArgumentException("There should be not other components in the changeset");
                        }
                    }
                }

                SrcEntityManager.SetComponentEnabled<EcsTestEmptyEnableable1>(entity, false); // empty component
                SrcEntityManager.SetComponentEnabled<EcsTestDataEnableable>(entity, false); // non-empty component
                SrcEntityManager.SetComponentData(entity, new EcsTestDataEnableable2{value0 = 1}); // Changes to component, but no enableable bit change

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(3, changes.ForwardChangeSet.SetComponents.Length);

                    var packedComponents = changes.ForwardChangeSet.TypeHashes;
                    foreach (var setComponent in changes.ForwardChangeSet.SetComponents)
                    {
                        var componentHash = packedComponents[setComponent.Component.PackedTypeIndex];
                        var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(componentHash.StableTypeHash);
                        if (typeIndex == empty1TypeIndex)
                        {
                            Assert.AreEqual(0, setComponent.Enabled);// Set to false
                        }
                        else if (typeIndex == component1TypeIndex)
                        {
                            Assert.AreEqual(0, setComponent.Enabled);// Set to false
                        }
                        else if (typeIndex == component2TypeIndex)
                        {
                            Assert.AreEqual(-1, setComponent.Enabled); // No enableable changes
                        }
                        else
                        {
                            throw new ArgumentException("There should be not other components in the changeset");
                        }
                    }
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetEnableableBuffer_IncrementalChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestEnableableBuffer1), typeof(EcsTestEnableableBuffer2));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentEnabled<EcsTestEnableableBuffer1>(entity, true);
                SrcEntityManager.SetComponentEnabled<EcsTestEnableableBuffer2>(entity, false);

                var entityGuidTypeIndex = TypeManager.GetTypeIndex<EntityGuid>();
                var buffer1TypeIndex = TypeManager.GetTypeIndex<EcsTestEnableableBuffer1>();
                var buffer2TypeIndex = TypeManager.GetTypeIndex<EcsTestEnableableBuffer2>();

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);// Enabled to true are ignored in the differ

                    var packedComponents = changes.ForwardChangeSet.TypeHashes;
                    foreach (var setComponent in changes.ForwardChangeSet.SetComponents)
                    {
                        var componentHash = packedComponents[setComponent.Component.PackedTypeIndex];
                        var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(componentHash.StableTypeHash);
                        if (typeIndex == entityGuidTypeIndex)
                        {
                            Assert.AreEqual(-1, setComponent.Enabled); // Is not enableable
                        }
                        else if (typeIndex == buffer2TypeIndex)
                        {
                            Assert.AreEqual(0, setComponent.Enabled); // Set to false
                        }
                        else
                        {
                            // Simulate and EcsTestEnableableBuffer1 are set to true, so are ignored when being added
                            throw new ArgumentException("There should be not other components in the changeset");
                        }
                    }
                }


                SrcEntityManager.SetComponentEnabled<EcsTestEnableableBuffer1>(entity, false);
                var buffer = SrcEntityManager.GetBuffer<EcsTestEnableableBuffer2>(entity, false); // Changes to buffer, but no enableable bit change
                buffer.Add(new EcsTestEnableableBuffer2 {Value = 1});

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);

                    var packedComponents = changes.ForwardChangeSet.TypeHashes;
                    foreach (var setComponent in changes.ForwardChangeSet.SetComponents)
                    {
                        var componentHash = packedComponents[setComponent.Component.PackedTypeIndex];
                        var typeIndex = TypeManager.GetTypeIndexFromStableTypeHash(componentHash.StableTypeHash);
                        if (typeIndex == buffer1TypeIndex)
                        {
                            Assert.AreEqual(0, setComponent.Enabled);// Set to false
                        }
                        else if (typeIndex == buffer2TypeIndex)
                        {
                            Assert.AreEqual(-1, setComponent.Enabled); // No enableable changes
                        }
                        else
                        {
                            throw new ArgumentException("There should be not other components in the changeset");
                        }
                    }
                }
            }
        }

        [Test]
        public void EntityDifferDetectsNameChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData));
                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetName(entity, "Old Name");

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                }

                SrcEntityManager.SetName(entity, "New Name");

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    #if !DOTS_DISABLE_DEBUG_NAMES
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.NameChangedCount);
                    #else
                    Assert.IsFalse(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.NameChangedCount);
                    #endif
                }
            }
        }

        [Test]
        [TestCase(EntityManagerDifferOptions.Default, TestName = nameof(EntityDiffer_GetChanges_EntityReferenceChange_DependsOnGUID) + "_Default")]
        [TestCase(EntityManagerDifferOptions.Default | EntityManagerDifferOptions.UseReferentialEquality, TestName = nameof(EntityDiffer_GetChanges_EntityReferenceChange_DependsOnGUID) + "_ReferentialEquality")]
        public void EntityDiffer_GetChanges_EntityReferenceChange_DependsOnGUID(EntityManagerDifferOptions options)
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                // Setup two entities, entity1 and entity2, and have the entity2 refer to entity1.
                var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity1Guid = CreateEntityGuid();
                SrcEntityManager.SetComponentData(entity1, entity1Guid);

                var entity2 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataEntity));
                SrcEntityManager.SetComponentData(entity2, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity2, new EcsTestDataEntity
                {
                    value1 = entity1
                });

                // apply the first changes, that's not what we're testing
                differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator);
                // Destroy entity1 and recreate it.
                SrcEntityManager.DestroyEntity(entity1);
                entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                SrcEntityManager.SetComponentData(entity1, entity1Guid);

                SrcEntityManager.SetComponentData(entity2, new EcsTestDataEntity
                {
                    value1 = entity1
                });

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    if ((options & EntityManagerDifferOptions.UseReferentialEquality) != 0)
                    {
                        // if we check for referential equivalence, there are no changes because the entity reference
                        // points to an entity that has the same GUID
                        Assert.IsFalse(changes.HasForwardChangeSet);
                    }
                    else
                    {
                        // otherwise, we detect that a component was changed, but interestingly enough do not register
                        // that a new entity was created.
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                        Assert.AreEqual(1, changes.ForwardChangeSet.EntityReferenceChanges.Length);
                    }
                }

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.HasForwardChangeSet);
                }
            }
        }

#if !DOTS_DISABLE_DEBUG_NAMES
// TODO - rewrite this test now that the assumption that entity indices are identical in both worlds
// has been invalidated
#if ENTITY_STORE_V1
        [Test]
        [TestCase(100)]
        public void EntityDiffer_GetChanges_EntityReferenceChange_DependsOnGUID_NameByIndexAligned(int entityCount)
        {
            const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                    EntityManagerDifferOptions.IncludeReverseChangeSet |
                    EntityManagerDifferOptions.FastForwardShadowWorld;

            var entityArray1 = new NativeArray<Entity>(entityCount, Allocator.Temp);
            var entityGuidArray1 = new NativeArray<EntityGuid>(entityCount, Allocator.Temp);
            var entityArray2 = new NativeArray<Entity>(entityCount, Allocator.Temp);
            var entityGuidArray2 = new NativeArray<EntityGuid>(entityCount, Allocator.Temp);

            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.Temp))
            {
                for (int i = 0; i < entityCount; i++)
                {
                    // Setup two entities, entity1 and entity2, and have the entity2 refer to entity1.
                    var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    var entity1Guid = CreateEntityGuid();
                    SrcEntityManager.SetComponentData(entity1, entity1Guid);
                    entityArray1[i] = entity1;
                    entityGuidArray1[i] = entity1Guid;

                    var entity2 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataEntity));
                    var entity2Guid = CreateEntityGuid();
                    SrcEntityManager.SetComponentData(entity2, entity2Guid);
                    SrcEntityManager.SetComponentData(entity2, new EcsTestDataEntity
                    {
                        value1 = entity1
                    });
                    entityArray2[i] = entity2;
                    entityGuidArray2[i] = entity2Guid;
                }

                // apply the first changes, this is not what we're testing
                differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator);

                for (int i = 0; i < entityCount / 2; i++)
                {
                    // Destroy entity1 and recreate it.
                    SrcEntityManager.DestroyEntity(entityArray1[i]);
                    entityArray1[i] = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    SrcEntityManager.SetComponentData(entityArray1[i], entityGuidArray1[i]);

                    SrcEntityManager.SetComponentData(entityArray2[i], new EcsTestDataEntity
                    {
                        value1 = entityArray1[i]
                    });
                }

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    var ShadowEntityManager = differ.ShadowEntityManager;

                    // Confirm that two worlds' have the same entity capacity
                    Assert.AreEqual(SrcEntityManager.EntityCapacity, ShadowEntityManager.EntityCapacity);
                    for(int i = 0; i < SrcEntityManager.EntityCapacity; i++)
                    {
                        var srcEntity = SrcEntityManager.GetEntityByEntityIndex(i);
                        var srcNameIndex = SrcEntityManager.GetNameIndexByEntityIndex(i);

                        var dstEntity = ShadowEntityManager.GetEntityByEntityIndex(i);
                        var dstNameIndex = ShadowEntityManager.GetNameIndexByEntityIndex(i);

                        // After fast forward shadow world, the EntityInChunkByEntity of the shawdow world and
                        // incremental convertion world line up with the same EntityGuid.  And the NameByEnity
                        // of the shawdow world and incremental convertion world also line up.
                        if (SrcEntityManager.Exists(srcEntity))
                        {
                            Assert.IsTrue(ShadowEntityManager.Exists(dstEntity));
                            Assert.AreEqual(SrcEntityManager.GetComponentData<EntityGuid>(srcEntity), ShadowEntityManager.GetComponentData<EntityGuid>(dstEntity));
                            Assert.AreEqual(srcNameIndex, dstNameIndex);
                        }
                    }
                }
            }

            entityArray1.Dispose();
            entityGuidArray1.Dispose();
            entityArray2.Dispose();
            entityGuidArray2.Dispose();
        }
#endif
#endif

        [Test]
        [TestCase(20)]
        public void EntityDiffer_GetChanges_EntityReferenceChange_DependsOnGUID_PrintSummary(int entityCount)
        {
            const int groupsCount = 3;
            const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet;

            var entityArray1 = new NativeArray<Entity>(groupsCount * entityCount, Allocator.Temp);
            var entityGuidArray1 = new NativeArray<EntityGuid>(groupsCount * entityCount, Allocator.Temp);
            var entityNameArray1 = new NativeArray<FixedString64Bytes>(groupsCount * entityCount, Allocator.Temp);
            var entityArray2 = new NativeArray<Entity>(groupsCount * entityCount, Allocator.Temp);
            var entityGuidArray2 = new NativeArray<EntityGuid>(groupsCount * entityCount, Allocator.Temp);
            var entityNameArray2 = new NativeArray<FixedString64Bytes>(groupsCount * entityCount, Allocator.Temp);

            using (var differ = new EntityManagerDiffer(SrcEntityManager, Allocator.Temp))
            {
                for (int i = 0; i < (groupsCount - 1) * entityCount; i++)
                {
                    // Setup two entities, entity1 and entity2, and have the entity2 refer to entity1.
                    var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    var entity1Guid = CreateEntityGuid();
                    var entity1Name = new FixedString64Bytes("entityName1_" + i.ToString());
                    SrcEntityManager.SetComponentData(entity1, entity1Guid);
                    SrcEntityManager.SetName(entity1, entity1Name);
                    entityArray1[i] = entity1;
                    entityGuidArray1[i] = entity1Guid;
                    entityNameArray1[i] = entity1Name;

                    var entity2 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataEntity));
                    var entity2Guid = CreateEntityGuid();
                    var entity2Name = new FixedString64Bytes("entityName2_" + i.ToString());
                    SrcEntityManager.SetComponentData(entity2, entity2Guid);
                    SrcEntityManager.SetComponentData(entity2, new EcsTestDataEntity
                    {
                        value1 = entity1
                    });
                    SrcEntityManager.SetName(entity2, entity2Name);
                    entityArray2[i] = entity2;
                    entityGuidArray2[i] = entity2Guid;
                    entityNameArray1[i] = entity2Name;
                }

                // apply the first changes, this is not what we're testing
                var tempChanges = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator);

                // Destroy some entities and recreate them
                for (int i = 0; i < entityCount; i++)
                {
                    // Destroy entity1 and recreate it.
                    SrcEntityManager.DestroyEntity(entityArray1[i]);
                    entityArray1[i] = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    SrcEntityManager.SetComponentData(entityArray1[i], entityGuidArray1[i]);
                    var entity1Name = new FixedString64Bytes("entityName1_new_" + i.ToString());
                    SrcEntityManager.SetName(entityArray1[i], entity1Name);
                    entityNameArray1[i] = entity1Name;

                    SrcEntityManager.SetComponentData(entityArray2[i], new EcsTestDataEntity
                    {
                        value1 = entityArray1[i]
                    });
                }

                // Change some entities' names
                for (int i = entityCount; i < (3 * entityCount / 2); i++)
                {
                    var entity1Name = new FixedString64Bytes("entityName1_new_" + i.ToString());
                    SrcEntityManager.SetName(entityArray1[i], entity1Name);
                    entityNameArray1[i] = entity1Name;
                }

                // Delete some entities
                for (int i = (3 * entityCount / 2); i < 2 * entityCount; i++)
                {
                    SrcEntityManager.DestroyEntity(entityArray1[i]);
                }

                // Create more entities
                for (int i = 2 * entityCount; i < 3 * entityCount; i++)
                {
                    // Setup two entities, entity1 and entity2, and have the entity2 refer to entity1.
                    var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    var entity1Guid = CreateEntityGuid();
                    var entity1Name = new FixedString64Bytes("entityName1_" + i.ToString());
                    SrcEntityManager.SetComponentData(entity1, entity1Guid);
                    SrcEntityManager.SetName(entity1, entity1Name);
                    entityArray1[i] = entity1;
                    entityGuidArray1[i] = entity1Guid;
                    entityNameArray1[i] = entity1Name;

                    var entity2 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataEntity));
                    var entity2Guid = CreateEntityGuid();
                    var entity2Name = new FixedString64Bytes("entityName2_" + i.ToString());
                    SrcEntityManager.SetComponentData(entity2, entity2Guid);
                    SrcEntityManager.SetComponentData(entity2, new EcsTestDataEntity
                    {
                        value1 = entity1
                    });
                    SrcEntityManager.SetName(entity2, entity2Name);
                    entityArray2[i] = entity2;
                    entityGuidArray2[i] = entity2Guid;
                    entityNameArray1[i] = entity2Name;
                }

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    var ShadowEntityManager = differ.ShadowEntityManager;

#if ENTITY_STORE_V1
                    // Confirm that two worlds' have the same entity capacity
                    Assert.AreEqual(SrcEntityManager.EntityCapacity, ShadowEntityManager.EntityCapacity);
#endif

                    var summaryString = EntityChangeSetFormatter.PrintSummary(changes.ForwardChangeSet, SrcEntityManager);
                }
            }

            entityArray1.Dispose();
            entityGuidArray1.Dispose();
            entityNameArray1.Dispose();
            entityArray2.Dispose();
            entityGuidArray2.Dispose();
            entityNameArray2.Dispose();
        }


        [Test]
        [TestCase(EntityManagerDifferOptions.Default, TestName = nameof(EntityDiffer_GetChanges_EntityReferenceChange_WithDynamicBuffer_DependsOnGUID) + "_Default")]
        [TestCase(EntityManagerDifferOptions.Default | EntityManagerDifferOptions.UseReferentialEquality, TestName = nameof(EntityDiffer_GetChanges_EntityReferenceChange_WithDynamicBuffer_DependsOnGUID) + "_ReferentialEquality")]
        public void EntityDiffer_GetChanges_EntityReferenceChange_WithDynamicBuffer_DependsOnGUID(EntityManagerDifferOptions options)
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                // Setup three entities, entity1a, entity1b and entity2, and have the entity2 refer to the other two.
                var entity1a = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                SrcEntityManager.SetComponentData(entity1a, CreateEntityGuid());

                var entity1b = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity1bGuid = CreateEntityGuid();
                SrcEntityManager.SetComponentData(entity1b, entity1bGuid);

                var entity2 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                SrcEntityManager.SetComponentData(entity2, CreateEntityGuid());
                var buf = SrcEntityManager.AddBuffer<EcsComplexEntityRefElement>(entity2);
                buf.Add(new EcsComplexEntityRefElement {Entity = entity1a});
                buf.Add(new EcsComplexEntityRefElement {Entity = entity1b});

                // apply the first changes, that's not what we're testing
                differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator);
                // Destroy entity1b and recreate it.
                SrcEntityManager.DestroyEntity(entity1b);
                entity1b = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                SrcEntityManager.SetComponentData(entity1b, entity1bGuid);
                buf = SrcEntityManager.GetBuffer<EcsComplexEntityRefElement>(entity2);
                buf[1] = new EcsComplexEntityRefElement {Entity = entity1b};

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    if ((options & EntityManagerDifferOptions.UseReferentialEquality) != 0)
                    {
                        // If we check for referential equivalence, there are no changes because the entity reference
                        // points to an entity that has the same GUID
                        Assert.IsFalse(changes.HasForwardChangeSet);
                    }
                    else
                    {
                        // Otherwise, we detect that a component was changed, but interestingly enough do not register
                        // that a new entity was created. There are two reference changes because one change in the
                        // buffer registers all elements as changed.
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                        Assert.AreEqual(2, changes.ForwardChangeSet.EntityReferenceChanges.Length);
                    }
                }

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.HasForwardChangeSet);
                }
            }
        }

        [Test]
        [TestCase(EntityManagerDifferOptions.Default, TestName = nameof(EntityDiffer_GetChanges_BlobAssetReferenceChange_DependsOnHash) + "_Default")]
        [TestCase(EntityManagerDifferOptions.Default | EntityManagerDifferOptions.UseReferentialEquality, TestName = nameof(EntityDiffer_GetChanges_BlobAssetReferenceChange_DependsOnHash) + "_ReferentialEquality")]
        public void EntityDiffer_GetChanges_BlobAssetReferenceChange_DependsOnHash(EntityManagerDifferOptions options)
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            using (var blobAssetReference0 = BlobAssetReference<int>.Create(10))
            using (var blobAssetReference1 = BlobAssetReference<int>.Create(10))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference0
                });

                // apply the first changes, that's not what we're testing
                differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator);

                SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference1
                });
                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    if ((options & EntityManagerDifferOptions.UseReferentialEquality) != 0)
                    {
                        // if we check for referential equivalence, there are no changes because the blob asset
                        // reference points to a blob that is identical.
                        Assert.IsFalse(changes.HasForwardChangeSet);
                    }
                    else
                    {
                        // otherwise, we detect that a component was changed
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                        Assert.AreEqual(1, changes.ForwardChangeSet.BlobAssetReferenceChanges.Length);
                    }
                }

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.HasForwardChangeSet);
                }
            }
        }

        [Test]
        [TestCase(EntityManagerDifferOptions.Default, TestName = nameof(EntityDiffer_GetChanges_BlobAssetReferenceChange_WithDynamicBuffer_DependsOnHash) + "_Default")]
        [TestCase(EntityManagerDifferOptions.Default | EntityManagerDifferOptions.UseReferentialEquality, TestName = nameof(EntityDiffer_GetChanges_BlobAssetReferenceChange_WithDynamicBuffer_DependsOnHash) + "_ReferentialEquality")]
        public void EntityDiffer_GetChanges_BlobAssetReferenceChange_WithDynamicBuffer_DependsOnHash(EntityManagerDifferOptions options)
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            using (var blobAssetReference0 = BlobAssetReference<int>.Create(10))
            using (var blobAssetReference1 = BlobAssetReference<int>.Create(10))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                var buf = SrcEntityManager.AddBuffer<EcsTestDataBlobAssetElement>(entity);
                buf.Add(new EcsTestDataBlobAssetElement {blobElement = blobAssetReference0});
                buf.Add(new EcsTestDataBlobAssetElement {blobElement = blobAssetReference1});

                // apply the first changes, that's not what we're testing
                differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator);

                buf = SrcEntityManager.GetBuffer<EcsTestDataBlobAssetElement>(entity);
                buf[1] = new EcsTestDataBlobAssetElement { blobElement = blobAssetReference0};
                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    if ((options & EntityManagerDifferOptions.UseReferentialEquality) != 0)
                    {
                        // if we check for referential equivalence, there are no changes because the blob asset
                        // reference points to a blob that is identical.
                        Assert.IsFalse(changes.HasForwardChangeSet);
                    }
                    else
                    {
                        // otherwise, we detect that a component was changed
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                        Assert.AreEqual(2, changes.ForwardChangeSet.BlobAssetReferenceChanges.Length);
                    }
                }

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.HasForwardChangeSet);
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        [Test]
        public unsafe void EntityDiffer_GetChanges_Clones_And_Disposes_ManagedComponents()
        {
            var managedRefComponent = new EcsTestManagedCompWithRefCount();
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestManagedCompWithRefCount));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, managedRefComponent);

                Assert.AreEqual(1, managedRefComponent.RefCount);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, managedRefComponent.RefCount);
                }

                Assert.AreEqual(1, managedRefComponent.RefCount);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(3, managedRefComponent.RefCount);
                }

                Assert.AreEqual(2, managedRefComponent.RefCount);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, managedRefComponent.RefCount);
                }

                Assert.AreEqual(2, managedRefComponent.RefCount);

                SrcEntityManager.RemoveComponent<EcsTestManagedCompWithRefCount>(entity);

                Assert.AreEqual(1, managedRefComponent.RefCount);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(1, managedRefComponent.RefCount);
                }

                Assert.AreEqual(1, managedRefComponent.RefCount);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.IncludeReverseChangeSet, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(2, managedRefComponent.RefCount);
                }

                Assert.AreEqual(1, managedRefComponent.RefCount);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.FastForwardShadowWorld, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.AreEqual(0, managedRefComponent.RefCount);
                }
            }
            Assert.AreEqual(0, managedRefComponent.RefCount);
        }

#endif

        [Test]
        public void EntityDiffer_GetChanges_DuplicateEntityGuidThrows()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                void GetChanges()
                {
                    using (differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator)) {}
                }

                var entityGuid0 = CreateEntityGuid();
                var entityGuid1 = CreateEntityGuid();
                var entityGuid2 = CreateEntityGuid();
                var entityGuid3 = CreateEntityGuid();

                var entity0 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity2 = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                var entity3 = SrcEntityManager.CreateEntity(typeof(EntityGuid));

                SrcEntityManager.SetComponentData(entity0, entityGuid0);
                SrcEntityManager.SetComponentData(entity1, entityGuid1);
                SrcEntityManager.SetComponentData(entity2, entityGuid2);
                SrcEntityManager.SetComponentData(entity3, entityGuid3);

                Assert.DoesNotThrow(GetChanges);

                SrcEntityManager.SetComponentData(entity1, entityGuid0);
                SrcEntityManager.SetComponentData(entity2, entityGuid0);
                SrcEntityManager.SetComponentData(entity3, entityGuid0);

                var regexMain = new Regex($"DuplicateEntityGuidException");
                LogAssert.Expect(LogType.Exception, regexMain);

                var dup0 = Assert.Throws<DuplicateEntityGuidException>(GetChanges).DuplicateEntityGuids;
                Assert.That(dup0, Is.EquivalentTo(new[] { new DuplicateEntityGuid(entityGuid0, 3) }));

                SrcEntityManager.SetComponentData(entity0, entityGuid1);
                SrcEntityManager.SetComponentData(entity3, entityGuid1);

                LogAssert.Expect(LogType.Exception, regexMain);
                var dup1 = Assert.Throws<DuplicateEntityGuidException>(GetChanges).DuplicateEntityGuids;
                Assert.That(dup1, Is.EquivalentTo(new[] { new DuplicateEntityGuid(entityGuid0, 1), new DuplicateEntityGuid(entityGuid1, 1) }));
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_IgnoresCleanup()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity();
                SrcEntityManager.AddComponentData(entity, entityGuid);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator)) {}

                SrcEntityManager.AddComponentData(entity, new EcsCleanup1 {Value = 9});
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }

                SrcEntityManager.SetComponentData(entity, new EcsCleanup1 {Value = 10});
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }

                // NOTE: the cleanup component being copied to shadow world is not required by the public API.
                //       This is simply defining the expected internal behaviour.

                using var query = differ.ShadowEntityManager.CreateEntityQuery(typeof(EcsCleanup1));
                Assert.AreEqual(10, query.GetSingleton<EcsCleanup1>().Value);
            }
        }

        [Test]
        public void EntityDiffer_AddingZeroSizeComponentToWholeChunk()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                for (int i = 0; i != 10; i++)
                {
                    var entityGuid = CreateEntityGuid();
                    var entity = SrcEntityManager.CreateEntity();
                    SrcEntityManager.AddComponentData(entity, entityGuid);
                }

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator)) {}

                SrcEntityManager.AddSharedComponentManaged(SrcEntityManager.UniversalQuery, new SharedData1(9));

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.AnyChanges);
                    Assert.AreEqual(10, changes.ForwardChangeSet.AddComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_LinkedEntityGroup_AdditionsAreDetected()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var aGuid = CreateEntityGuid();
                var a = SrcEntityManager.CreateEntity();
                SrcEntityManager.AddComponentData(a, aGuid);
                SrcEntityManager.AddBuffer<LinkedEntityGroup>(a).Add(a);
                var bGuid = CreateEntityGuid();
                var b = SrcEntityManager.CreateEntity();
                SrcEntityManager.AddComponentData(b, bGuid);
                SrcEntityManager.AddBuffer<LinkedEntityGroup>(b).Add(b);

                using (differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator)) {}

                SrcEntityManager.GetBuffer<LinkedEntityGroup>(a).Add(b);
                SrcEntityManager.GetBuffer<LinkedEntityGroup>(b).Add(a);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.AnyChanges);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.LinkedEntityGroupRemovals.Length);
                    Assert.AreEqual(2, changes.ForwardChangeSet.LinkedEntityGroupAdditions.Length);
                }
            }
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        /// <summary>
        /// Generates a change set over the world and efficiently updates the internal shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithFastForward_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                var entityGuid = CreateEntityGuid();

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                    EntityManagerDifferOptions.IncludeReverseChangeSet |
                    EntityManagerDifferOptions.FastForwardShadowWorld;

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // Forward changes is all changes needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);

                    // Reverse changes is all changes needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetManagedComponents.Length);
                }

                // The inner shadow world was updated during the last call which means no new changes should be found.
                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsFalse(changes.AnyChanges);
                }
            }
        }

        /// <summary>
        /// Generates a change set over the world without updating the shadow world.
        /// </summary>
        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_WithoutFastForward_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                const EntityManagerDifferOptions options = EntityManagerDifferOptions.IncludeForwardChangeSet |
                    EntityManagerDifferOptions.IncludeReverseChangeSet;

                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // ForwardChanges defines all operations needed to go from the shadow state to the current state.
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);

                    // ReverseChanges defines all operations needed to go from the current state back to the last shadow state. (i.e. Undo)
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetManagedComponents.Length);
                }

                // Since we did not fast forward the inner shadow world. We should be able to generate the exact same changes again.
                using (var changes = differ.GetChanges(options, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);

                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(1, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetComponentData_IncrementalChanges_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Mutate some component data.
                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 10 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeOtherString" });

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the two data changes.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // The ForwardChangeSet will contain a set value 10 and set value "SomeString"
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);

                    // The ReverseChangeSet will contain a set value 9 and set value "SomeOtherString"
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                }

                SrcEntityManager.DestroyEntity(entity);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ReverseChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        public void SetComponentData_WithManagedComponent_IsDetectedBy_GetChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Only mutate managed component.
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeOtherString" });

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the two data changes.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // The ForwardChangeSet will contain a set value 10 and set value "SomeString"
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);

                    // The ReverseChangeSet will contain a set value 9 and set value "SomeOtherString"
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                }

                SrcEntityManager.DestroyEntity(entity);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ReverseChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        public void GetComponentData_WithManagedComponent_IsDetectedBy_GetChanges()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entityGuid = CreateEntityGuid();
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestData), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, entityGuid);
                SrcEntityManager.SetComponentData(entity, new EcsTestData { value = 9 });
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.AnyChanges);
                }

                // Only mutate managed component.
                SrcEntityManager.GetComponentData<EcsTestManagedComponent>(entity).value = "SomeOtherString";

                // The entityGuid value is the same so it should not be picked up during change tracking.
                // We should only see the two data changes.
                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    // The ForwardChangeSet will contain a set value 10 and set value "SomeString"
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(0, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.AddArchetypes.Length);

                    // The ReverseChangeSet will contain a set value 9 and set value "SomeOtherString"
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddComponents.Length);
                    Assert.AreEqual(0, changes.ReverseChangeSet.AddArchetypes.Length);
                }

                SrcEntityManager.DestroyEntity(entity);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.SetManagedComponents.Length);
                    Assert.AreEqual(0, changes.ForwardChangeSet.RemoveComponents.Length);

                    // In this case the ReverseChangeSet should describe how to get this entity back in it's entirety
                    Assert.IsTrue(changes.HasReverseChangeSet);
                    Assert.AreEqual(0, changes.ReverseChangeSet.DestroyedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ReverseChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(2, changes.ReverseChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ReverseChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_CreateEntityAndSetSharedComponentData_IncrementalChanges_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestSharedComp), typeof(EcsTestManagedComponent));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestManagedComponent { value = "SomeString" });
                SrcEntityManager.SetSharedComponentManaged(entity, new EcsTestSharedComp { value = 2 });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    Assert.AreEqual(1, changes.ForwardChangeSet.CreatedEntityCount);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.AddArchetypes[0].EntityCount);
                    Assert.AreEqual(4, changes.ForwardChangeSet.AddArchetypes[0].TypeIndices.Length); // +1 due to Simulate
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetSharedComponents.Length);
                    Assert.AreEqual(1, changes.ForwardChangeSet.SetManagedComponents.Length);
                }
            }
        }

        [Test]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReference0 = BlobAssetReference<int>.Create(10);

                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference0
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    var forward = changes.ForwardChangeSet;

                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.DestroyedBlobAssets.Length, Is.EqualTo(0));

                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));

                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }

                blobAssetReference0.Dispose();

                var blobAssetReference1 = BlobAssetReference<int>.Create(20);

                SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference1
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);

                    var forward = changes.ForwardChangeSet;
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(20));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));

                    Assert.IsTrue(changes.HasReverseChangeSet);

                    var reverse = changes.ReverseChangeSet;
                    Assert.That(reverse.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)reverse.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                    Assert.That(reverse.BlobAssetReferenceChanges[0].Value, Is.EqualTo(reverse.CreatedBlobAssets[0].Hash));
                }

                blobAssetReference1.Dispose();
            }
        }

        [Test]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent_SameContentHash()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReference0 = BlobAssetReference<int>.Create(10);
                var blobAssetReference1 = BlobAssetReference<int>.Create(10);

                var entity0 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));

                SrcEntityManager.SetComponentData(entity0, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity0, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference0
                });

                var entity1 = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));

                SrcEntityManager.SetComponentData(entity1, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity1, new EcsTestDataBlobAssetRef
                {
                    value = blobAssetReference1
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    var forward = changes.ForwardChangeSet;

                    // Only one blob asset is created
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.DestroyedBlobAssets.Length, Is.EqualTo(0));

                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(2));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));
                    Assert.That(forward.BlobAssetReferenceChanges[1].Value, Is.EqualTo(forward.CreatedBlobAssets[0].Hash));

                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }

                SrcEntityManager.DestroyEntity(entity1);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.That(changes.ForwardChangeSet.DestroyedBlobAssets.Length, Is.EqualTo(0));
                }

                SrcEntityManager.DestroyEntity(entity0);

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.That(changes.ForwardChangeSet.DestroyedBlobAssets.Length, Is.EqualTo(1));
                }

                blobAssetReference0.Dispose();
                blobAssetReference1.Dispose();
            }
        }

        [Test]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent_SharedAsset()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReference = BlobAssetReference<int>.Create(10);

                try
                {
                    for (var i = 0; i < 100; i++)
                    {
                        var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));
                        SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                        SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef { value = blobAssetReference });
                    }

                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                        Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                    }
                }
                finally
                {
                    blobAssetReference.Dispose();
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_BlobAssets_SetComponent_Null()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(EcsTestDataBlobAssetRef));
                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);
                    var forward = changes.ForwardChangeSet;
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges[0].Value, Is.EqualTo((ulong)0));
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(0));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(0));
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_BlobAssets_SetBuffer()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReferences = new NativeArray<BlobAssetReference<int>>(100, Allocator.Temp);

                for (var i = 0; i < blobAssetReferences.Length; i++)
                {
                    blobAssetReferences[i] = BlobAssetReference<int>.Create(i);
                }

                try
                {
                    var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid));
                    SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                    var buffer = SrcEntityManager.AddBuffer<EcsTestDataBlobAssetElement>(entity);

                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i] });
                    }

                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int) * 100));
                    }
                }
                finally
                {
                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        blobAssetReferences[i].Dispose();
                    }

                    blobAssetReferences.Dispose();
                }
            }
        }

        [Test]
        public void EntityDiffer_GetChanges_BlobAssets_SetBuffer_MultipleEntities()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReferences = new NativeArray<BlobAssetReference<int>>(100, Allocator.Temp);

                for (var i = 0; i < blobAssetReferences.Length; i++)
                {
                    blobAssetReferences[i] = BlobAssetReference<int>.Create(i);
                }

                for (var i = 0; i < blobAssetReferences.Length; i += 4)
                {
                    var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid));

                    SrcEntityManager.SetComponentData(entity, CreateEntityGuid());

                    var buffer = SrcEntityManager.AddBuffer<EcsTestDataBlobAssetElement>(entity);

                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 0] });
                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 1] });
                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 2] });
                    buffer.Add(new EcsTestDataBlobAssetElement { blobElement = blobAssetReferences[i + 3] });
                }

                try
                {
                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int) * 100));
                    }
                }
                finally
                {
                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        blobAssetReferences[i].Dispose();
                    }

                    blobAssetReferences.Dispose();
                }
            }
        }

        // TODO: This test doesn't appear to make much sense. The problem of diffing when the memory order is different makes sense
        // but the validation with TypeMemoryOrder isn't so clear, and the magic values can change when we modify the type hash code, thus changing the order
        // this requires the magic values to be updated in this test every time. This test needs some re-thinking.
        /*
        [Test]
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SetComponent_TypeMemoryOrdering()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReferences = new NativeArray<BlobAssetReference<int>>(100, Allocator.Temp);

                for (var i = 0; i < blobAssetReferences.Length; i++)
                {
                    // Construct the archetype in an order which will NOT match the memory order.
                    var archetype = SrcEntityManager.CreateArchetype(
                        typeof(EntityGuid),
                        typeof(EcsTestDataBlobAssetRef),
                        typeof(EcsTestData4));

                    // Validate the assumption that the archetype is created in this way.
                    Assert.That(archetype.Archetype->TypeMemoryOrder[0], Is.EqualTo(0));
                    Assert.That(archetype.Archetype->TypeMemoryOrder[1], Is.EqualTo(2));
                    Assert.That(archetype.Archetype->TypeMemoryOrder[2], Is.EqualTo(3));

                    // Validate the component sizes are different
                    Assert.AreNotEqual(UnsafeUtility.SizeOf<EcsTestDataBlobAssetRef>(), UnsafeUtility.SizeOf<EcsTestData4>());

                    var entity = SrcEntityManager.CreateEntity(archetype);

                    blobAssetReferences[i] = BlobAssetReference<int>.Create(i);

                    SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                    SrcEntityManager.SetComponentData(entity, new EcsTestData4());
                    SrcEntityManager.SetComponentData(entity, new EcsTestDataBlobAssetRef
                    {
                        value = blobAssetReferences[i]
                    });
                }

                try
                {
                    using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, Allocator.Temp))
                    {
                        Assert.IsTrue(changes.HasForwardChangeSet);
                        var forward = changes.ForwardChangeSet;
                        Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(100));
                        Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int) * 100));
                    }
                }
                finally
                {
                    for (var i = 0; i < blobAssetReferences.Length; i++)
                    {
                        blobAssetReferences[i].Dispose();
                    }

                    blobAssetReferences.Dispose();
                }
            }
        }
        */

        class ManagedComponentWithBlobAssetRef : IComponentData
        {
            public BlobAssetReference<int> Value;
        }

        struct SharedComponentWithBlobAssetRef : ISharedComponentData
        {
            public BlobAssetReference<int> Value;
        }

        [Test]
#if ENABLE_IL2CPP
        [Ignore("DOTS-7524 - \"System.ExecutionEngineException : An unresolved indirect call lookup failed\" is thrown when executed with an IL2CPP build")]
#endif
        public unsafe void EntityDiffer_GetChanges_BlobAssets_ManagedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReference0 = BlobAssetReference<int>.Create(10);

                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(ManagedComponentWithBlobAssetRef));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new ManagedComponentWithBlobAssetRef
                {
                    Value = blobAssetReference0
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);

                    var forward = changes.ForwardChangeSet;
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.DestroyedBlobAssets.Length, Is.EqualTo(0));
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }

                var blobAssetReference1 = BlobAssetReference<int>.Create(20);

                SrcEntityManager.SetComponentData(entity, new ManagedComponentWithBlobAssetRef
                {
                    Value = blobAssetReference1
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);

                    var forward = changes.ForwardChangeSet;
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(20));

                    Assert.IsTrue(changes.HasReverseChangeSet);

                    var reverse = changes.ReverseChangeSet;
                    Assert.That(reverse.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)reverse.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }

                blobAssetReference0.Dispose();
                blobAssetReference1.Dispose();
            }
        }

        [Test]
#if ENABLE_IL2CPP
        [Ignore("DOTS-7524 - \"System.ExecutionEngineException : An unresolved indirect call lookup failed\" is thrown when executed with an IL2CPP build")]
#endif
        public unsafe void EntityDiffer_GetChanges_BlobAssets_SharedComponents()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var blobAssetReference0 = BlobAssetReference<int>.Create(10);

                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(SharedComponentWithBlobAssetRef));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetSharedComponentManaged(entity, new SharedComponentWithBlobAssetRef
                {
                    Value = blobAssetReference0
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);

                    var forward = changes.ForwardChangeSet;

                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.DestroyedBlobAssets.Length, Is.EqualTo(0));
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }

                // IMPORTANT There is a known issue here.
                // We need to allocate the new blob BEFORE de-allocating the old one. Otherwise the newly allocated blob will actually have the same memory address.
                // If this happens setting the component data will not correctly bump the global system version since it detects no change.
                var blobAssetReference1 = BlobAssetReference<int>.Create(20);
                blobAssetReference0.Dispose();

                SrcEntityManager.SetSharedComponentManaged(entity, new SharedComponentWithBlobAssetRef
                {
                    Value = blobAssetReference1
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);

                    var forward = changes.ForwardChangeSet;
                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)forward.BlobAssetData.GetUnsafePtr(), Is.EqualTo(20));

                    Assert.IsTrue(changes.HasReverseChangeSet);

                    var reverse = changes.ReverseChangeSet;
                    Assert.That(reverse.CreatedBlobAssets.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetReferenceChanges.Length, Is.EqualTo(1));
                    Assert.That(reverse.BlobAssetData.Length, Is.EqualTo(sizeof(int)));
                    Assert.That(*(int*)reverse.BlobAssetData.GetUnsafePtr(), Is.EqualTo(10));
                }

                blobAssetReference1.Dispose();
            }
        }

        class ManagedComponentWithScriptableObject: IComponentData
        {
            public ScriptableObjectWithBlobAssetRef Value;
        }

        class ScriptableObjectWithBlobAssetRef : UnityEngine.ScriptableObject
        {
            public BlobAssetReference<int> Value;
        }

        [Test]
        public void EntityDiffer_GetChanges_BlobAssets_ScriptableObject()
        {
            using (var differ = new EntityManagerDiffer(SrcEntityManager, SrcWorld.UpdateAllocator.ToAllocator))
            {
                var scriptableObject = UnityEngine.ScriptableObject.CreateInstance<ScriptableObjectWithBlobAssetRef>();
                scriptableObject.Value = BlobAssetReference<int>.Create(10);

                var entity = SrcEntityManager.CreateEntity(typeof(EntityGuid), typeof(ManagedComponentWithScriptableObject));

                SrcEntityManager.SetComponentData(entity, CreateEntityGuid());
                SrcEntityManager.SetComponentData(entity, new ManagedComponentWithScriptableObject
                {
                    Value = scriptableObject
                });

                using (var changes = differ.GetChanges(EntityManagerDifferOptions.Default, SrcWorld.UpdateAllocator.ToAllocator))
                {
                    Assert.IsTrue(changes.HasForwardChangeSet);

                    var forward = changes.ForwardChangeSet;

                    Assert.That(forward.CreatedBlobAssets.Length, Is.EqualTo(0));
                    Assert.That(forward.DestroyedBlobAssets.Length, Is.EqualTo(0));
                    Assert.That(forward.BlobAssetReferenceChanges.Length, Is.EqualTo(0));
                    Assert.That(forward.BlobAssetData.Length, Is.EqualTo(0));
                }

                scriptableObject.Value.Dispose();
                UnityEngine.Object.DestroyImmediate(scriptableObject);
            }
        }
#endif // !UNITY_DISABLE_MANAGED_COMPONENTS

        [Test]
        public void EntityDiffer_GatherLinkedEntityGroupChanges_DetectsSimpleChanges()
        {
            var a = new EntityGuid(1, 0, 0, 0);
            var b = new EntityGuid(2, 0, 0, 0);
            var before = new NativeList<EntityGuid>(1, SrcWorld.UpdateAllocator.ToAllocator);
            var after = new NativeList<EntityGuid>(2, SrcWorld.UpdateAllocator.ToAllocator);
            var additions = new NativeList<LinkedEntityGroupChange>(16, SrcWorld.UpdateAllocator.ToAllocator);
            var removals = new NativeList<LinkedEntityGroupChange>(16, SrcWorld.UpdateAllocator.ToAllocator);
            {
                before.Add(a);
                after.Add(a);
                after.Add(b);

                // detect an addition...
                EntityDiffer.GatherLinkedEntityGroupChanges(default, before.AsArray(), after.AsArray(), ref additions, ref removals);
                Assert.AreEqual(0, removals.Length);
                Assert.AreEqual(1, additions.Length);
                Assert.AreEqual(b, additions[0].ChildEntityGuid);
                additions.Clear();
                removals.Clear();

                // ...or a removal in reverse.
                EntityDiffer.GatherLinkedEntityGroupChanges(default, after.AsArray(), before.AsArray(), ref additions, ref removals);
                Assert.AreEqual(0, additions.Length);
                Assert.AreEqual(1, removals.Length);
                Assert.AreEqual(b, removals[0].ChildEntityGuid);
                additions.Clear();
                removals.Clear();

                // now the same with the second range swapped around
                after[0] = b;
                after[1] = a;

                EntityDiffer.GatherLinkedEntityGroupChanges(default, before.AsArray(), after.AsArray(), ref additions, ref removals);
                Assert.AreEqual(0, removals.Length);
                Assert.AreEqual(1, additions.Length);
                Assert.AreEqual(b, additions[0].ChildEntityGuid);
                additions.Clear();
                removals.Clear();

                EntityDiffer.GatherLinkedEntityGroupChanges(default, after.AsArray(), before.AsArray(), ref additions, ref removals);
                Assert.AreEqual(0, additions.Length);
                Assert.AreEqual(1, removals.Length);
                Assert.AreEqual(b, removals[0].ChildEntityGuid);
                additions.Clear();
                removals.Clear();

                // and now with an empty first range
                before.Clear();

                EntityDiffer.GatherLinkedEntityGroupChanges(default, before.AsArray(), after.AsArray(), ref additions, ref removals);
                Assert.AreEqual(0, removals.Length);
                Assert.AreEqual(2, additions.Length);
                Assert.AreEqual(a, additions[0].ChildEntityGuid);
                Assert.AreEqual(b, additions[1].ChildEntityGuid);
                additions.Clear();
                removals.Clear();

                EntityDiffer.GatherLinkedEntityGroupChanges(default, after.AsArray(), before.AsArray(), ref additions, ref removals);
                Assert.AreEqual(0, additions.Length);
                Assert.AreEqual(2, removals.Length);
                Assert.AreEqual(a, removals[0].ChildEntityGuid);
                Assert.AreEqual(b, removals[1].ChildEntityGuid);
            }
        }

        [Test]
        public void EntityDiffer_GatherLinkedEntityGroupChanges_DetectsCombinedChanges()
        {
            var a = new EntityGuid(1, 0, 0, 0);
            var b = new EntityGuid(2, 0, 0, 0);
            var c = new EntityGuid(3, 0, 0, 0);
            var additions = new NativeList<LinkedEntityGroupChange>(16, SrcWorld.UpdateAllocator.ToAllocator);
            var removals = new NativeList<LinkedEntityGroupChange>(16, SrcWorld.UpdateAllocator.ToAllocator);
            var before = new NativeList<EntityGuid>(1, SrcWorld.UpdateAllocator.ToAllocator)
            {
                a, b
            };
            var after = new NativeList<EntityGuid>(2, SrcWorld.UpdateAllocator.ToAllocator)
            {
                b, c
            };
            using (before)
            using (after)
            {
                EntityDiffer.GatherLinkedEntityGroupChanges(default, before.AsArray(), after.AsArray(), ref additions, ref removals);
                Assert.AreEqual(1, removals.Length);
                Assert.AreEqual(1, additions.Length);
                Assert.AreEqual(c, additions[0].ChildEntityGuid);
                Assert.AreEqual(a, removals[0].ChildEntityGuid);
                additions.Clear();
                removals.Clear();

                EntityDiffer.GatherLinkedEntityGroupChanges(default, after.AsArray(), before.AsArray(), ref additions, ref removals);
                Assert.AreEqual(1, additions.Length);
                Assert.AreEqual(1, removals.Length);
                Assert.AreEqual(a, additions[0].ChildEntityGuid);
                Assert.AreEqual(c, removals[0].ChildEntityGuid);
            }
        }
    }
}
