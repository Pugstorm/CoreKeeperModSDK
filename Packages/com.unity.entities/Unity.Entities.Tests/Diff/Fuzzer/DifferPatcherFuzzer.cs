#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using UnityEngine;
using Random = Unity.Mathematics.Random;
using CommandGenerator = Unity.Entities.Tests.Fuzzer.FuzzerCommandGenerator<Unity.Entities.Tests.Fuzzer.DifferPatcherFuzzer>;
using ICommand = Unity.Entities.Tests.Fuzzer.IFuzzerCommand<Unity.Entities.Tests.Fuzzer.DifferPatcherFuzzer>;

namespace Unity.Entities.Tests.Fuzzer
{
    class DifferPatcherFuzzer : IFuzzer
    {
        public World SourceWorld { get; }
        private World DestinationWorld { get; }

        private EntityManagerDifferOptions _differOptions = EntityManagerDifferOptions.IncludeForwardChangeSet | EntityManagerDifferOptions.ValidateUniqueEntityGuid;
        private BlobAssetCache _srcBlobAssets = new BlobAssetCache(Allocator.Persistent);
        private BlobAssetCache _dstBlobAssets = new BlobAssetCache(Allocator.Persistent);
        private EntityDiffer.CachedComponentChanges _cachedComponentChanges = new EntityDiffer.CachedComponentChanges(1024);
        internal int NextId;

        // For every entity, we record its "parent" LinkedEntityGroup. That means that with this current setup, every
        // entity can only be in a single LinkedEntityGroup.
        internal readonly DictionaryForKeySampling<Entity, Entity> LinkedEntityParent = new DictionaryForKeySampling<Entity, Entity>();
        internal readonly HashSetForSampling<EntityGuid> LinkedEntityRoots = new HashSetForSampling<EntityGuid>();
        internal readonly DictionaryForKeySampling<EntityGuid, Entity> AliveEntities = new DictionaryForKeySampling<EntityGuid, Entity>();
        private static readonly EntityQueryDesc Query;

        static DifferPatcherFuzzer()
        {
            TypeManager.Initialize();
            Query = new EntityQueryDesc
            {
                All = new ComponentType[] { typeof(EntityGuid) },
                Options = EntityQueryOptions.IncludeDisabledEntities | EntityQueryOptions.IncludePrefab
            };
            WeightedGenerators = new List<(CommandGenerator Generator, int Weight)>
            {
                (DifferPatcherFuzzerCommands.CreateEntity, 10),
                (DifferPatcherFuzzerCommands.DestroyEntity, 8),
                (DifferPatcherFuzzerCommands.CreateLinkedEntityGroup, 10),
                (DifferPatcherFuzzerCommands.RemoveLinkedEntityGroup, 8),
                (DifferPatcherFuzzerCommands.AddToLinkedEntityGroup, 10),
                (DifferPatcherFuzzerCommands.RemoveFromLinkedEntityGroup, 8),
                (Fuzzer.ValidationCommandGenerator<DifferPatcherFuzzer>(), 2)
            };
            Generators = WeightedGenerators.Select(t => t.Item1).ToList();
        }

        public DifferPatcherFuzzer()
        {
            SourceWorld = new World("Source");
            SourceWorld.UpdateAllocatorEnableBlockFree = true;
            DestinationWorld = new World("Destination");
            DestinationWorld.UpdateAllocatorEnableBlockFree = true;
        }

        public void Validate()
        {
            var srcEm = SourceWorld.EntityManager;
            var dstEm = DestinationWorld.EntityManager;

            using (var changes = EntityDiffer.GetChanges(ref _cachedComponentChanges, srcEm, dstEm, _differOptions, Query, _srcBlobAssets, SourceWorld.UpdateAllocator.ToAllocator))
            {
                EntityPatcher.ApplyChangeSet(dstEm, changes.ForwardChangeSet);
            }

            using (var changes = EntityDiffer.GetChanges(ref _cachedComponentChanges, dstEm, srcEm, _differOptions, Query, _dstBlobAssets, DestinationWorld.UpdateAllocator.ToAllocator))
            {
                if (!changes.ForwardChangeSet.HasChangesIncludeNames(true))
                    return;
                // TODO: Format the entity changeset nicely and print it out
                throw new Exception("Diff is not zero!");
            }
        }

        public static readonly List<(CommandGenerator Generator, int Weight)> WeightedGenerators;

        public static readonly List<CommandGenerator> Generators;

        public void Dispose()
        {
            _srcBlobAssets.Dispose();
            _dstBlobAssets.Dispose();
            SourceWorld?.Dispose();
            DestinationWorld?.Dispose();
            _cachedComponentChanges.Dispose();
        }

        internal class DictionaryForKeySampling<K, V>
        {
            public readonly Dictionary<K, V> Dictionary;
            public readonly List<K> Keys;

            public DictionaryForKeySampling()
            {
                Dictionary = new Dictionary<K, V>();
                Keys = new List<K>();
            }

            public V this[K key] => Dictionary[key];
            public int Count => Dictionary.Count;

            public void Add(K key, V value)
            {
                if (!Dictionary.ContainsKey(key))
                    Keys.Add(key);
                Dictionary.Add(key, value);
            }

            public bool TryGetValue(K key, out V value) => Dictionary.TryGetValue(key, out value);
            public bool ContainsKey(K key) => Dictionary.ContainsKey(key);

            public void Remove(K key)
            {
                Dictionary.Remove(key);
                Keys.Remove(key);
            }

            public K SampleKey(ref Random rng) => Keys[rng.NextInt(0, Keys.Count)];
        }

        internal class HashSetForSampling<K>
        {
            public readonly HashSet<K> HashSet;
            public readonly List<K> Keys;

            public HashSetForSampling()
            {
                HashSet = new HashSet<K>();
                Keys = new List<K>();
            }

            public int Count => HashSet.Count;

            public bool Contains(K key) => HashSet.Contains(key);

            public void Add(K key)
            {
                if (HashSet.Add(key))
                    Keys.Add(key);
            }

            public void Remove(K key)
            {
                HashSet.Remove(key);
                Keys.Remove(key);
            }

            public K Sample(ref Random rng) => Keys[rng.NextInt(0, Keys.Count)];
        }
    }

    static class DifferPatcherFuzzerCommands
    {
        delegate T SampleCommand<T>(DifferPatcherFuzzer state, ref Random rng);
        static CommandGenerator MakeCommand<T>(string commandId, SampleCommand<ICommand> sampler) where T : ICommand
            => new CommandGenerator
            {
                Id = commandId,
                DeserializeCommand = str => JsonUtility.FromJson<T>(str),
                SampleCommand = (DifferPatcherFuzzer state, ref Random rng, out string serializedCommand) =>
                {
                    var cmd = sampler(state, ref rng);
                    serializedCommand = cmd == null ? default : JsonUtility.ToJson(cmd);
                    return cmd;
                }
            };

        static void RemoveFromLinkedEntityGroupBuffer(DynamicBuffer<LinkedEntityGroup> linkedEntityGroup, Entity e)
        {
            for (int i = linkedEntityGroup.Length - 1; i >= 0; i--)
            {
                if (linkedEntityGroup[i].Value == e)
                    linkedEntityGroup.RemoveAtSwapBack(i);
            }
        }

        ///////////////////////////////////////////
        /// Create Entity
        ///////////////////////////////////////////
        struct CreateEntityCommand : ICommand
        {
            public EntityGuid Guid;
            public void ApplyCommand(DifferPatcherFuzzer state)
            {
                var e = state.SourceWorld.EntityManager.CreateEntity();
                state.SourceWorld.EntityManager.AddComponentData(e, Guid);
                state.AliveEntities.Add(Guid, e);
            }
        }

        public static readonly CommandGenerator CreateEntity = MakeCommand<CreateEntityCommand>("CreateEntity",
            (DifferPatcherFuzzer state, ref Random rng) =>
            {
                int id = state.NextId++;
                return new CreateEntityCommand {Guid = new EntityGuid(id, 0, 0, 0)};
            });

        ///////////////////////////////////////////
        /// Destroy Entity
        ///////////////////////////////////////////
        struct DestroyEntityCommand : ICommand
        {
            public EntityGuid Guid;
            public void ApplyCommand(DifferPatcherFuzzer state)
            {
                var e = state.AliveEntities[Guid];
                var em = state.SourceWorld.EntityManager;
                if (state.LinkedEntityParent.TryGetValue(e, out var parent))
                    RemoveFromLinkedEntityGroupBuffer(em.GetBuffer<LinkedEntityGroup>(parent), e);

                if (em.HasComponent<LinkedEntityGroup>(e))
                {
                    var linkedEntities = em.GetBuffer<LinkedEntityGroup>(e);
                    for (int i = 0; i < linkedEntities.Length; i++)
                    {
                        var otherEntity = linkedEntities[i].Value;
                        var otherGuid = em.GetComponentData<EntityGuid>(otherEntity);
                        state.AliveEntities.Remove(otherGuid);
                        state.LinkedEntityRoots.Remove(otherGuid);
                    }
                }
                state.SourceWorld.EntityManager.DestroyEntity(e);
                state.AliveEntities.Remove(Guid);
                state.LinkedEntityRoots.Remove(Guid);
            }
        }

        public static readonly CommandGenerator DestroyEntity = MakeCommand<DestroyEntityCommand>("DestroyEntity",
            (DifferPatcherFuzzer state, ref Random rng) =>
            {
                if (state.AliveEntities.Count == 0)
                    return null;

                var guid = state.AliveEntities.SampleKey(ref rng);
                return new DestroyEntityCommand {Guid = guid};
            });

        ///////////////////////////////////////////
        /// Create Linked Entity Group
        ///////////////////////////////////////////
        struct CreateLinkedEntityGroupCommand : ICommand
        {
            public EntityGuid Guid;
            public void ApplyCommand(DifferPatcherFuzzer state)
            {
                var e = state.AliveEntities[Guid];
                if (state.SourceWorld.EntityManager.HasComponent<LinkedEntityGroup>(e))
                    throw new InvalidOperationException();
                var l = state.SourceWorld.EntityManager.AddBuffer<LinkedEntityGroup>(e);
                l.Add(e);
                state.LinkedEntityRoots.Add(Guid);
            }
        }

        public static readonly CommandGenerator CreateLinkedEntityGroup = MakeCommand<CreateLinkedEntityGroupCommand>(
            "CreateLinkedEntityGroup",
            (DifferPatcherFuzzer state, ref Random rng) =>
            {
                if (state.AliveEntities.Count == 0 || state.AliveEntities.Count == state.LinkedEntityRoots.Count)
                    return null;

                const int maxAttempts = 10;
                for (int a = 0; a < maxAttempts; a++)
                {
                    var guid = state.AliveEntities.SampleKey(ref rng);
                    var e = state.AliveEntities[guid];
                    if (!state.LinkedEntityRoots.Contains(guid) && !state.LinkedEntityParent.ContainsKey(e))
                    {
                        return new CreateLinkedEntityGroupCommand {Guid = guid};
                    }
                }

                return null;
            });

        ///////////////////////////////////////////
        /// Remove Linked Entity Group
        ///////////////////////////////////////////
        struct RemoveLinkedEntityGroupCommand : ICommand
        {
            public EntityGuid Guid;
            public void ApplyCommand(DifferPatcherFuzzer state)
            {
                var e = state.AliveEntities[Guid];
                var linkedEntities = state.SourceWorld.EntityManager.GetBuffer<LinkedEntityGroup>(e);
                for (int i = 0; i < linkedEntities.Length; i++)
                    state.LinkedEntityParent.Remove(linkedEntities[i].Value);
                state.SourceWorld.EntityManager.RemoveComponent<LinkedEntityGroup>(e);
                state.LinkedEntityRoots.Remove(Guid);
            }
        }

        public static readonly CommandGenerator RemoveLinkedEntityGroup = MakeCommand<RemoveLinkedEntityGroupCommand>(
            "RemoveLinkedEntityGroup",
            (DifferPatcherFuzzer state, ref Random rng) =>
            {
                if (state.LinkedEntityRoots.Count == 0)
                    return null;

                EntityGuid guid = state.LinkedEntityRoots.Sample(ref rng);
                return new RemoveLinkedEntityGroupCommand {Guid = guid};
            });

        ///////////////////////////////////////////
        /// Add To Linked Entity Group
        ///////////////////////////////////////////
        struct AddToLinkedEntityGroupCommand : ICommand
        {
            public EntityGuid ToAdd;
            public EntityGuid AddTo;
            public void ApplyCommand(DifferPatcherFuzzer state)
            {
                var addTo = state.AliveEntities[AddTo];
                var toAdd = state.AliveEntities[ToAdd];
                var l = state.SourceWorld.EntityManager.GetBuffer<LinkedEntityGroup>(addTo);
                l.Add(toAdd);
                state.LinkedEntityParent.Add(toAdd, addTo);
            }
        }

        public static readonly CommandGenerator AddToLinkedEntityGroup = MakeCommand<AddToLinkedEntityGroupCommand>(
            "AddToLinkedEntityGroup",
            (DifferPatcherFuzzer state, ref Random rng) =>
            {
                if (state.LinkedEntityParent.Count + state.LinkedEntityRoots.Count == state.AliveEntities.Count ||
                    state.LinkedEntityRoots.Count == 0)
                    return null;

                const int maxAttempts = 10;
                for (int a = 0; a < maxAttempts; a++)
                {
                    // find something that has no linked entity group
                    var toAdd = state.AliveEntities.SampleKey(ref rng);
                    var toAddEntity = state.AliveEntities[toAdd];
                    if (state.LinkedEntityParent.ContainsKey(toAddEntity) ||
                        state.LinkedEntityRoots.Contains(toAdd))
                        continue;
                    return new AddToLinkedEntityGroupCommand
                    {
                        AddTo = state.LinkedEntityRoots.Sample(ref rng),
                        ToAdd = toAdd
                    };
                }

                return null;
            });

        ///////////////////////////////////////////
        /// Remove From Linked Entity Group
        ///////////////////////////////////////////
        struct RemoveFromLinkedEntityGroupCommand : ICommand
        {
            public EntityGuid ToRemove;
            public EntityGuid RemoveFrom;
            public void ApplyCommand(DifferPatcherFuzzer state)
            {
                var removeFrom = state.AliveEntities[RemoveFrom];
                var toRemove = state.AliveEntities[ToRemove];
                var l = state.SourceWorld.EntityManager.GetBuffer<LinkedEntityGroup>(removeFrom);
                RemoveFromLinkedEntityGroupBuffer(l, toRemove);
                state.LinkedEntityParent.Remove(toRemove);
            }
        }

        public static readonly CommandGenerator RemoveFromLinkedEntityGroup =
            MakeCommand<RemoveFromLinkedEntityGroupCommand>(
                "RemoveFromLinkedEntityGroup",
                (DifferPatcherFuzzer state, ref Random rng) =>
                {
                    if (state.LinkedEntityRoots.Count == 0)
                        return null;

                    var em = state.SourceWorld.EntityManager;
                    const int maxAttempts = 10;
                    for (int a = 0; a < maxAttempts; a++)
                    {
                        var removeFrom = state.LinkedEntityRoots.Sample(ref rng);
                        var root = state.AliveEntities[removeFrom];
                        var linkedEntities = em.GetBuffer<LinkedEntityGroup>(root);
                        if (linkedEntities.Length <= 1)
                            continue;
                        var other = linkedEntities[rng.NextInt(1, linkedEntities.Length)].Value;
                        return new RemoveFromLinkedEntityGroupCommand
                        {
                            RemoveFrom = removeFrom,
                            ToRemove = em.GetComponentData<EntityGuid>(other)
                        };
                    }
                    return null;
                });
    }
}
#endif
