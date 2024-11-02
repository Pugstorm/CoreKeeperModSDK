using System;
using System.Collections.Generic;
using Unity.Profiling;
using UnityEditor;
using System.Reflection;
#if UNITY_EDITOR
using Unity.Entities.Build;
#endif
using Unity.Entities.Conversion;

namespace Unity.Entities
{
    class AssemblyData
    {
        public bool Enabled;
        public bool IsUnityAssembly;
    }

    /// <summary>
    ///  A mapping of UnityEngine.Component (represented by TypeManager.GetTypeIndex) => array of bakers that need to execute to bake the authoring data.
    /// </summary>
    internal struct BakerDataUtility
    {
        static Dictionary<TypeIndex, BakerData[]>             _IndexToBakerInstances;
        internal static Dictionary<Assembly, AssemblyData>    _BakersByAssembly;

        static string unityAssembly = "Unity.";
        static string unityEngineAssembly = "UnityEngine.";

        static ProfilerMarker s_RegisterBakers = new ProfilerMarker("Baking.RegisterBakers");
        static ProfilerMarker s_SortBakers = new ProfilerMarker("Baking.SortBakers");

        public struct BakerData
        {
            public ProfilerMarker   Profiler;
            public IBaker           Baker;
            public AssemblyData     AssemblyData;
            /// <summary>
            /// The number of authoring components compatible with this baker.
            /// </summary>
            /// <remarks>
            /// Bakers usually handle one authoring component type. If the bakers are decorated with the
            /// <see cref="BakeDerivedTypesAttribute"/> attribute, they are also applied on the authoring components
            /// derived from the base type. In this case, the base baker should be evaluated before any bakers defined
            /// for derived types.
            /// Unity guarantees this order by sorting the bakers based on the number of components handled by each baker,
            /// with bakers handling more components (hence handling base types) being evaluated first.
            /// </remarks>
            public int              CompatibleComponentCount;
            public bool             ForceBakingForDisabledComponents;
        }

#if UNITY_EDITOR
        public static void ApplyAssemblyFilter(BakingSystemFilterSettings filter)
        {
            foreach (var assemblyEntry in _BakersByAssembly)
            {
                assemblyEntry.Value.Enabled = (filter == null || !filter.IsAssemblyExcluded(assemblyEntry.Key));
            }
        }
#endif

        public static BakerData[] GetBakers(TypeIndex typeIndex)
        {
            if (_IndexToBakerInstances == null || !_IndexToBakerInstances.TryGetValue(typeIndex, out var bakerData))
                return null;

            return bakerData;
        }

        //Method used by EntityBehaviour in motion
        internal static int GetBakerIndexInArray(TypeIndex authoringTypeIndex, Type bakerType)
        {
            if (_IndexToBakerInstances == null || !_IndexToBakerInstances.TryGetValue(authoringTypeIndex, out var bakerData))
                return 0;
            for (int i = 0; i < bakerData.Length; i++)
            {
                var data = bakerData[i];
                if (data.GetType() == bakerType)
                    return i;
            }
            return 0;
        }

        public static void Initialize()
        {
            if (_IndexToBakerInstances != null)
                return;

            _IndexToBakerInstances = new Dictionary<TypeIndex, BakerData[]>();
            _BakersByAssembly = new Dictionary<Assembly, AssemblyData>();

#if UNITY_EDITOR
            using (s_RegisterBakers.Auto())
            {
                AddBakers(typeof(GameObjectBaker));
                AddBakers(typeof(Baker<>));
            }

            using (s_SortBakers.Auto())
            {
                // Sort the bakers so that the bakers for base authoring components are evaluated before bakers for derived types.
                // This guarantees that the type hierarchy chain authoring components is respected, regardless of the order in which
                // the bakers are defined in code.
                foreach (var bakers in _IndexToBakerInstances.Values)
                {
                    Array.Sort(bakers, (a, b) => b.CompatibleComponentCount.CompareTo(a.CompatibleComponentCount));
                }
            }
#endif
        }

        static void AddBakers(Type baseBakerType)
        {
#if UNITY_EDITOR
            foreach (var goBakerType in TypeCache.GetTypesDerivedFrom(baseBakerType))
            {
                if (!goBakerType.IsAbstract && !goBakerType.IsDefined(typeof(DisableAutoCreationAttribute)))
                {
                    try
                    {
                        AddBaker(goBakerType);
                    }
                    catch (Exception e)
                    {
                        Debug.LogException(e);
                    }
                }
            }
#endif // UNITY_EDITOR
        }

        static void AddBaker(Type type)
        {
            var baker = (IBaker) Activator.CreateInstance(type);
            var authoringType = baker.GetAuthoringType();
            int compatibleComponentCount = 1;

            // If BakeDerivedTypesAttribute is used in the baker,
            // try find derived types that are present in TypeManager
            if (type.IsDefined(typeof(BakeDerivedTypesAttribute)))
            {
#if UNITY_EDITOR
                // In the editor we can use TypeCache as an accelerator
                var types = TypeCache.GetTypesDerivedFrom(authoringType);
                compatibleComponentCount += types.Count;
                foreach (var compatibleType in types)
                {
                    if (TypeManager.TryGetTypeIndex(compatibleType, out var typeIndex))
                    {
                        AddBaker(type, baker, typeIndex, compatibleComponentCount);
                    }
                }
#else
                // At runtime we need to make a linear search for derived classes
                var list = new List<(System.Type type, IBaker baker, TypeIndex typeIndex)>();
                var typeCount = TypeManager.GetTypeCount();
                for (var i = 0; i < typeCount; ++i)
                {
                    var compatibleType = TypeManager.GetType(new TypeIndex { Value = i });
                    if (authoringType.IsAssignableFrom(compatibleType))
                    {
                        compatibleComponentCount++;
                        var typeIndex = TypeManager.GetTypeIndex(compatibleType);
                        list.Add((type, (IBaker) Activator.CreateInstance(type), typeIndex));
                    }
                }
                foreach (var t in list)
                {
                    AddBaker(t.type, t.baker, t.typeIndex, compatibleComponentCount);
                }
#endif
            }
            // Try find with the authoring type in the TypeManager
            if (TypeManager.TryGetTypeIndex(authoringType, out var authoringTypeIndex))
            {
                AddBaker(type, baker, authoringTypeIndex, compatibleComponentCount);
            }
        }

        static void AddBaker(Type type, IBaker baker, TypeIndex typeIndex, int compatibleComponentCount)
        {
            var bakerIndex = 0;
            if (!_IndexToBakerInstances.TryGetValue(typeIndex, out var bakers))
            {
                bakers = new BakerData[1];
            }
            else
            {
                bakerIndex = bakers.Length;
                Array.Resize(ref bakers, bakerIndex + 1);
            }

            if (!_BakersByAssembly.TryGetValue(type.Assembly, out var assemblyData))
            {
                assemblyData = new AssemblyData();
                assemblyData.Enabled = true;
                assemblyData.IsUnityAssembly = type.Assembly.GetName().Name.StartsWith(unityAssembly)
                    || type.Assembly.GetName().Name.StartsWith(unityEngineAssembly);
                _BakersByAssembly.Add(type.Assembly, assemblyData);
            }

            bakers[bakerIndex] = new BakerData
            {
                Baker = baker,
                Profiler = new ProfilerMarker(baker.GetType().Name),
                AssemblyData = assemblyData,
                CompatibleComponentCount = compatibleComponentCount,
                ForceBakingForDisabledComponents = type.IsDefined(typeof(ForceBakingOnDisabledComponentsAttribute))
            };

            _IndexToBakerInstances[typeIndex] = bakers;
        }

        /// <summary>
        /// Overrides the global list of bakers either adding new ones or replacing old ones.
        /// This is used for tests. Always make sure to dispose to revert the global state back to what it was.
        /// </summary>
        internal struct OverrideBakers : IDisposable
        {
            Dictionary<TypeIndex, BakerData[]>     OldBakers;

            public OverrideBakers(bool replaceExistingBakers, params Type[] bakerTypes)
            {
                Initialize();

                OldBakers = _IndexToBakerInstances;

                if (replaceExistingBakers)
                    _IndexToBakerInstances = new Dictionary<TypeIndex, BakerData[]>(OldBakers.Count);
                else
                    _IndexToBakerInstances = new Dictionary<TypeIndex, BakerData[]>(OldBakers);

                foreach (var type in bakerTypes)
                    AddBaker(type);
            }

            public void Dispose()
            {
                _IndexToBakerInstances = OldBakers;
            }
        }
    }
}
