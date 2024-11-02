#if UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP
#define UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_EDITOR_WORLD
#endif

using System;
using System.Collections.Generic;
using System.Reflection;
using Unity.Assertions;
using UnityEngine;
using Unity.Collections;
using Unity.Profiling;
using UnityEngine.LowLevel;
using UnityEngine.PlayerLoop;
using System.Linq;

namespace Unity.Entities
{
    /// <summary>
    /// Utilities to help initialize the default ECS <see cref="World"/>.
    /// </summary>
    public static class DefaultWorldInitialization
    {
#pragma warning disable 0067 // unused variable
        /// <summary>
        /// Invoked after the default World is initialized.
        /// </summary>
        internal static event Action<World> DefaultWorldInitialized;

        /// <summary>
        /// Invoked after the Worlds are destroyed.
        /// </summary>
        internal static event Action DefaultWorldDestroyed;
#pragma warning restore 0067 // unused variable

        static bool s_UnloadOrPlayModeChangeShutdownRegistered = false;

        /// <summary>
        /// Destroys Editor World when entering Play Mode without Domain Reload.
        /// RuntimeInitializeOnLoadMethod is called before the new scene is loaded, before Awake and OnEnable of MonoBehaviour.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void CleanupWorldBeforeSceneLoad()
        {
            DomainUnloadOrPlayModeChangeShutdown();
        }

        /// <summary>
        /// Ensures the current World destruction on shutdown or when entering/exiting Play Mode or Domain Reload.
        /// 1) When switching to Play Mode Editor World (if created) has to be destroyed:
        ///     - after the current scene objects are destroyed and OnDisable/Destroy are called,
        ///     - before game scene is loaded and Awake/OnEnable are called.
        /// 2) When switching to Edit Mode Game World has to be destroyed:
        ///     - after the current scene objects are destroyed and OnDisable/Destroy are called,
        ///     - before backup scene is loaded and Awake/OnEnable are called.
        /// 3) When Unloading Domain (as well as Editor/Player exit) Editor or Game World has to be destroyed:
        ///     - after OnDisable/OnBeforeSerialize are called,
        ///     - before AppDomain.DomainUnload.
        /// Point 1) is covered by RuntimeInitializeOnLoadMethod attribute.
        /// For points 2) and 3) there are no entry point in the Unity API and they have to be handled by a proxy MonoBehaviour
        /// which in OnDisable can drive the World cleanup for both Exit Play Mode and Domain Unload.
        /// </summary>
        static void RegisterUnloadOrPlayModeChangeShutdown()
        {
            if (s_UnloadOrPlayModeChangeShutdownRegistered)
                return;

            var go = new GameObject { hideFlags = HideFlags.HideInHierarchy };
            if (Application.isPlaying)
                UnityEngine.Object.DontDestroyOnLoad(go);
            else
                go.hideFlags = HideFlags.HideAndDontSave;

            go.AddComponent<DefaultWorldInitializationProxy>().IsActive = true;

            RuntimeApplication.RegisterFrameUpdateToCurrentPlayerLoop();

            s_UnloadOrPlayModeChangeShutdownRegistered = true;
        }

        internal static void DomainUnloadOrPlayModeChangeShutdown()
        {
            if (!s_UnloadOrPlayModeChangeShutdownRegistered)
                return;

            var playerLoop = PlayerLoop.GetCurrentPlayerLoop();
            foreach (var w in World.s_AllWorlds)
                ScriptBehaviourUpdateOrder.RemoveWorldFromPlayerLoop(w, ref playerLoop);
            PlayerLoop.SetPlayerLoop(playerLoop);

            RuntimeApplication.UnregisterFrameUpdateToCurrentPlayerLoop();

            World.DisposeAllWorlds();

#if!ENTITY_STORE_V1
            EntityComponentStore.s_entityStore.Data.Dispose();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            EntitiesJournaling.Shutdown();
#endif

#if ENABLE_PROFILER
            EntitiesProfiler.Shutdown();
#endif

            s_UnloadOrPlayModeChangeShutdownRegistered = false;

            DefaultWorldDestroyed?.Invoke();
        }

        /// <summary>
        /// Initializes the default world or runs ICustomBootstrap if one is available.
        /// </summary>
        /// <param name="defaultWorldName">The name of the world that will be created. Unless there is a custom bootstrap.</param>
        /// <param name="editorWorld">Editor worlds by default only include systems with [WorldSystemFilter(WorldSystemFilterFlags.Editor)]. If editorWorld is true, ICustomBootstrap will not be used.</param>
        /// <returns>The initialized <see cref="World"/> object.</returns>
        public static World Initialize(string defaultWorldName, bool editorWorld = false)
        {
            using var marker = new ProfilerMarker("Create World & Systems").Auto();

            RegisterUnloadOrPlayModeChangeShutdown();

#if ENABLE_PROFILER
            EntitiesProfiler.Initialize();
#endif

#if (UNITY_EDITOR || DEVELOPMENT_BUILD) && !DISABLE_ENTITIES_JOURNALING
            EntitiesJournaling.Initialize();
#endif

            if (!editorWorld)
            {
                var bootStrap = CreateBootStrap();
                if (bootStrap != null && bootStrap.Initialize(defaultWorldName))
                {
                    Assert.IsTrue(World.DefaultGameObjectInjectionWorld != null,
                        $"ICustomBootstrap.Initialize() implementation failed to set " +
                        $"World.DefaultGameObjectInjectionWorld, despite returning true " +
                        $"(indicating the World has been properly initialized)");
                    return World.DefaultGameObjectInjectionWorld;
                }
            }
            var world = new World(defaultWorldName, editorWorld ? WorldFlags.Editor : WorldFlags.Game);

            World.DefaultGameObjectInjectionWorld = world;

            AddSystemToRootLevelSystemGroupsInternal(world, GetAllSystemTypeIndices(WorldSystemFilterFlags.Default, editorWorld));

            ScriptBehaviourUpdateOrder.AppendWorldToCurrentPlayerLoop(world);

            DefaultWorldInitialized?.Invoke(world);
            return world;
        }

        /// <summary>
        /// Adds the collection of systems to the world by injecting them into the root level system groups
        /// (InitializationSystemGroup, SimulationSystemGroup and PresentationSystemGroup). Prefer the version that
        /// takes SystemTypeIndex's as an argument to avoid unnecessary reflection.
        /// </summary>
        /// <param name="world">The World in which the root-level system groups should be created.</param>
        /// <param name="systemTypes">The system types to create and add.</param>
        public static void AddSystemsToRootLevelSystemGroups(World world, IEnumerable<Type> systemTypes)
        {
            var systemTypeIndices = new NativeList<SystemTypeIndex>(16, Allocator.Temp);
            foreach (var t in systemTypes)
            {
                systemTypeIndices.Add(TypeManager.GetSystemTypeIndex(t));
            }
            AddSystemToRootLevelSystemGroupsInternal(world, systemTypeIndices);
        }

        /// <summary>
        /// Adds the collection of systems to the world by injecting them into the root level system groups
        /// (InitializationSystemGroup, SimulationSystemGroup and PresentationSystemGroup). Prefer the version that
        /// takes SystemTypeIndex's as an argument to avoid unnecessary reflection.
        /// </summary>
        /// <param name="world">The World in which the root-level system groups should be created.</param>
        /// <param name="systemTypes">The system types to create and add.</param>
        public static void AddSystemsToRootLevelSystemGroups(World world, IReadOnlyList<Type> systemTypes)
        {
            var systemTypeIndices = new NativeList<SystemTypeIndex>(16, Allocator.Temp);
            foreach (var t in systemTypes)
            {
                systemTypeIndices.Add(TypeManager.GetSystemTypeIndex(t));
            }
            AddSystemToRootLevelSystemGroupsInternal(world, systemTypeIndices);
        }

        /// <summary>
        /// Adds the collection of systems to the world by injecting them into the root level system groups
        /// (InitializationSystemGroup, SimulationSystemGroup and PresentationSystemGroup). Prefer the version that
        /// takes SystemTypeIndex's as an argument to avoid unnecessary reflection.
        /// </summary>
        /// <param name="world">The World in which the root-level system groups should be created.</param>
        /// <param name="systemTypes">The system types to create and add.</param>
        public static void AddSystemsToRootLevelSystemGroups(World world, params Type[] systemTypes)
        {
            var indices = new NativeList<SystemTypeIndex>(systemTypes.Length, Allocator.Temp);
            for (int i = 0; i < systemTypes.Length; i++)
            {
                indices.Add(TypeManager.GetSystemTypeIndex(systemTypes[i]));
            }
            AddSystemToRootLevelSystemGroupsInternal(world, indices);
        }

        /// <summary>
        /// Adds the collection of systems to the world by injecting them into the root level system groups
        /// (InitializationSystemGroup, SimulationSystemGroup and PresentationSystemGroup). This version avoids
        /// unnecessary reflection.
        /// </summary>
        /// <param name="world">The World in which the root-level system groups should be created.</param>
        /// <param name="systemTypes">The system types to create and add.</param>
        public static void AddSystemsToRootLevelSystemGroups(World world, NativeList<SystemTypeIndex> systemTypes)
        {
            AddSystemToRootLevelSystemGroupsInternal(world, systemTypes);
        }

        /// <summary>
        /// This internal interface is used when adding systems to the default world to identify the root groups in your
        /// setup. They will then be skipped when we try to find the parent of each system (because they don't need a
        /// parent).
        /// </summary>
        internal interface IIdentifyRootGroups
        {
            bool IsRootGroup(SystemTypeIndex type);
        }

        struct DefaultRootGroups : IIdentifyRootGroups
        {
            public bool IsRootGroup(SystemTypeIndex type) =>
                type == TypeManager.GetSystemTypeIndex<InitializationSystemGroup>() ||
                type == TypeManager.GetSystemTypeIndex<SimulationSystemGroup>() ||
                type == TypeManager.GetSystemTypeIndex<PresentationSystemGroup>();
        }

        internal static unsafe void AddSystemToRootLevelSystemGroupsInternal<T>(World world, NativeList<SystemTypeIndex> systemTypesOrig, ComponentSystemGroup defaultGroup, T rootGroups)
            where T : struct, IIdentifyRootGroups
        {
            var managedTypes = new List<SystemTypeIndex>();
            var unmanagedTypes = new List<SystemTypeIndex>();

            foreach (var stype in systemTypesOrig)
            {
                if (!TypeManager.IsSystemTypeIndex(stype))
                    throw new InvalidOperationException("Bad type");
                if (stype.IsManaged)
                    managedTypes.Add(stype);
                else
                    unmanagedTypes.Add(stype);
            }

            var allSystemHandlesToAdd = world.GetOrCreateSystemsAndLogException(systemTypesOrig, Allocator.Temp);

            // Add systems to their groups, based on the [UpdateInGroup] attribute.
            for (int i=0; i<systemTypesOrig.Length; i++)
            {
                SystemHandle system = allSystemHandlesToAdd[i];

                // Skip the built-in root-level system groups
                if (rootGroups.IsRootGroup(systemTypesOrig[i]))
                {
                    continue;
                }

                var updateInGroupAttributes = TypeManager.GetSystemAttributes(systemTypesOrig[i],
                    TypeManager.SystemAttributeKind.UpdateInGroup);
                if (updateInGroupAttributes.Length == 0)
                {
                    defaultGroup.AddSystemToUpdateList(system);
                }

                foreach (var attr in updateInGroupAttributes)
                {
                    var group = FindGroup(world, systemTypesOrig[i], attr);
                    if (group != null)
                    {
                        group.AddSystemToUpdateList(system);
                    }
                }
            }
        }

        internal static void AddSystemToRootLevelSystemGroupsInternal(World world, NativeList<SystemTypeIndex> systemTypesOrig)
        {
            using var marker = new ProfilerMarker("AddSystems").Auto();

            var initializationSystemGroup = world.GetOrCreateSystemManaged<InitializationSystemGroup>();
            var simulationSystemGroup = world.GetOrCreateSystemManaged<SimulationSystemGroup>();
            var presentationSystemGroup = world.GetOrCreateSystemManaged<PresentationSystemGroup>();

            AddSystemToRootLevelSystemGroupsInternal(world, systemTypesOrig, simulationSystemGroup, new DefaultRootGroups());

            // Update player loop
            initializationSystemGroup.SortSystems();
            simulationSystemGroup.SortSystems();
            presentationSystemGroup.SortSystems();
        }

        private static ComponentSystemGroup FindGroup(World world, SystemTypeIndex systemType, TypeManager.SystemAttribute attr)
        {
            var groupTypeIndex = attr.TargetSystemTypeIndex;

            if (!TypeManager.IsSystemTypeIndex(groupTypeIndex) || !groupTypeIndex.IsGroup)
            {
                throw new InvalidOperationException($"Invalid [{nameof(UpdateInGroupAttribute)}] attribute for {systemType}: target group must be derived from {nameof(ComponentSystemGroup)}.");
            }
            if ((attr.Flags & TypeManager.SystemAttribute.kOrderFirstFlag) != 0 && (attr.Flags & TypeManager.SystemAttribute.kOrderLastFlag) != 0)
            {
                throw new InvalidOperationException($"The system {systemType} can not specify both OrderFirst=true and OrderLast=true in its [{nameof(UpdateInGroupAttribute)}] attribute.");
            }

            var groupSys = world.GetExistingSystemManaged(groupTypeIndex);
            if (groupSys == null)
            {
                // Warn against unexpected behaviour combining DisableAutoCreation and UpdateInGroup
                var parentDisableAutoCreation = TypeManager.GetSystemAttributes(groupTypeIndex, TypeManager.SystemAttributeKind.DisableAutoCreation).Length > 0;
                var name = TypeManager.GetSystemName(groupTypeIndex);
                if (parentDisableAutoCreation)
                {
                    Debug.LogWarning($"A system {systemType} wants to execute in {name} but this group has [{nameof(DisableAutoCreationAttribute)}] and {systemType} does not. The system will not be added to any group and thus not update.");
                }
                else
                {
                    Debug.LogWarning(
                        $"A system {systemType} could not be added to group {name}, because the group was not created in the world {world.Name}. Fix these errors before continuing. The system will not be added to any group and thus not update.");
                }
            }

            return groupSys as ComponentSystemGroup;
        }

        /// <summary>
        /// Can be called when in edit mode in the editor to initialize a the default world.
        /// </summary>
        public static void DefaultLazyEditModeInitialize()
        {
#if UNITY_EDITOR
            if (World.DefaultGameObjectInjectionWorld == null)
            {
                // * OnDisable (Serialize monobehaviours in temporary backup)
                // * unload domain
                // * load new domain
                // * OnEnable (Deserialize monobehaviours in temporary backup)
                // * mark entered playmode / load scene
                // * OnDisable / OnDestroy
                // * OnEnable (Loading object from scene...)
                if (UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    // We are just gonna ignore this enter playmode reload.
                    // Can't see a situation where it would be useful to create something inbetween.
                    // But we really need to solve this at the root. The execution order is kind if crazy.
                }
                else
                {
#if !UNITY_DISABLE_AUTOMATIC_SYSTEM_BOOTSTRAP_EDITOR_WORLD
                    Initialize("Editor World", true);
#endif
                }
            }
#endif
        }

        /// <summary>
        /// Calculates a list of all systems filtered with WorldSystemFilterFlags, [DisableAutoCreation] etc. Prefer
        /// GetAllSystemTypeIndices where possible to avoid extra reflection.
        /// </summary>
        /// <param name="filterFlags">The filter flags to search for.</param>
        /// <param name="requireExecuteInEditor">Optionally require that [WorldSystemFilter(WorldSystemFilterFlags.Editor)] is present on the system. This is used when creating edit mode worlds.</param>
        /// <returns>The list of filtered systems</returns>
        public static IReadOnlyList<Type> GetAllSystems(WorldSystemFilterFlags filterFlags, bool requireExecuteInEditor = false)
        {
            using var marker = new ProfilerMarker("GetAllSystems").Auto();
            var indices = GetAllSystemTypeIndices(filterFlags, requireExecuteInEditor);
            var ret = new List<Type>();
            for (int i = 0; i < indices.Length; i++)
            {
                ret.Add(TypeManager.GetSystemType(indices[i]));
            }

            return ret;
        }

        /// <summary>
        /// Calculates a list of all systems filtered with WorldSystemFilterFlags, [DisableAutoCreation] etc.
        /// Prefer this over GetAllSystems if possible, to avoid extra reflection usage.
        /// </summary>
        /// <param name="filterFlags">The filter flags to search for.</param>
        /// <param name="requireExecuteInEditor">Optionally require that [WorldSystemFilter(WorldSystemFilterFlags.Editor)] is present on the system. This is used when creating edit mode worlds.</param>
        /// <returns>The list of filtered systems</returns>
        public static NativeList<SystemTypeIndex> GetAllSystemTypeIndices(WorldSystemFilterFlags filterFlags, bool requireExecuteInEditor = false)
        {
            return TypeManager.GetSystemTypeIndices(filterFlags, requireExecuteInEditor ? WorldSystemFilterFlags.Editor : 0);
        }

        static ICustomBootstrap CreateBootStrap()
        {
            var bootstrapTypes = TypeManager.GetTypesDerivedFrom(typeof(ICustomBootstrap));
            Type selectedType = null;

            foreach (var bootType in bootstrapTypes)
            {
                if (bootType.IsAbstract || bootType.ContainsGenericParameters)
                    continue;

                if (selectedType == null)
                    selectedType = bootType;
                else if (selectedType.IsAssignableFrom(bootType))
                    selectedType = bootType;
                else if (!bootType.IsAssignableFrom(selectedType))
                    Debug.LogError("Multiple custom ICustomBootstrap specified, ignoring " + bootType);
            }
            ICustomBootstrap bootstrap = null;
            if (selectedType != null)
                bootstrap = Activator.CreateInstance(selectedType) as ICustomBootstrap;

            return bootstrap;
        }
    }
}
