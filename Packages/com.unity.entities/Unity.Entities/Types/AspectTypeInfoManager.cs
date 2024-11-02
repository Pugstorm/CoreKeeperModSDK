using System;
using System.Linq;
using System.Reflection;
using Unity.Burst;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;

namespace Unity.Entities
{
    struct AspectTypeInfoManager
    {
        struct AspectTypeInfoTag { };
        static readonly SharedStatic<AspectTypeInfo> s_AspectTypeInfo = SharedStatic<AspectTypeInfo>.GetOrCreate<AspectTypeInfoTag>();
        static UnsafeParallelMultiHashMap<ulong, AspectType> m_ArchetypeAspectMap;

        private delegate void AddComponentRequirementsTo(ref UnsafeList<ComponentType> all);

        static void InitializeAspectTypeInfo()
        {
            var aspectTypes = TypeManager.GetAllAspectTypes();
            if (aspectTypes == null || !aspectTypes.Any())
                return;

            s_AspectTypeInfo.Data = new AspectTypeInfo
            {
                AspectTypes = new UnsafeList<AspectType>(aspectTypes.Length, Allocator.Persistent),
                AspectRequiredComponents = new UnsafeParallelMultiHashMap<AspectType, ComponentType>(aspectTypes.Length, Allocator.Persistent),
                AspectExcludedComponents = new UnsafeParallelMultiHashMap<AspectType, ComponentType>(aspectTypes.Length, Allocator.Persistent)
            };

            for (var i = 0; i < aspectTypes.Length; ++i)
            {
                var aspectType = AspectType.FromTypeIndex(i);
                var aspectManagedType = aspectType.GetManagedType();

                var addComponentRequirementsTo = (AddComponentRequirementsTo)Delegate.CreateDelegate(
                    typeof(AddComponentRequirementsTo),
                    Activator.CreateInstance(aspectManagedType),
                    aspectManagedType.GetMethod("AddComponentRequirementsTo", BindingFlags.Public | BindingFlags.Instance));

                var all = new UnsafeList<ComponentType>(8, Allocator.Temp);
                var any = new UnsafeList<ComponentType>(8, Allocator.Temp);
                var none = new UnsafeList<ComponentType>(8, Allocator.Temp);
                var disabled = new UnsafeList<ComponentType>(8, Allocator.Temp);
                var absent = new UnsafeList<ComponentType>(8, Allocator.Temp);
                var present = new UnsafeList<ComponentType>(8, Allocator.Temp);
                addComponentRequirementsTo.Invoke(ref all);

                for (var j = 0; j != all.Length; ++j)
                    s_AspectTypeInfo.Data.AspectRequiredComponents.Add(aspectType, all[j]);
                for (var j = 0; j != none.Length; ++j)
                    s_AspectTypeInfo.Data.AspectExcludedComponents.Add(aspectType, none[j]);

                all.Dispose();
                any.Dispose();
                none.Dispose();
                disabled.Dispose();
                absent.Dispose();
                present.Dispose();

                s_AspectTypeInfo.Data.AspectTypes.AddNoResize(aspectType);
            }
        }

        internal static void Dispose()
        {
            s_AspectTypeInfo.Data.Dispose();
            if (m_ArchetypeAspectMap.IsCreated)
                m_ArchetypeAspectMap.Dispose();
        }

        internal static NativeArray<AspectType> GetAspectTypesFromEntity(World world, Entity entity)
        {
            if (s_AspectTypeInfo.Data.AspectTypes.Length == 0)
                InitializeAspectTypeInfo();

            var archetype = world.EntityManager.GetChunk(entity).Archetype;
            var aspectTypes = new NativeList<AspectType>(16, Allocator.Temp);
            if (!m_ArchetypeAspectMap.IsCreated)
                m_ArchetypeAspectMap = new UnsafeParallelMultiHashMap<ulong, AspectType>(16, Allocator.Persistent);

            // If mapping already exists, return those values.
            if (m_ArchetypeAspectMap.ContainsKey(archetype.StableHash))
            {
                foreach (var at in m_ArchetypeAspectMap.GetValuesForKey(archetype.StableHash))
                    aspectTypes.Add(at);
            }
            else
            {
                // Otherwise, build up the mapping.
                var componentTypes = archetype.GetComponentTypes();
                foreach (var at in s_AspectTypeInfo.Data.AspectTypes)
                {
                    if (HasAnyQueryComponents(at) && ContainAllRequiredComponents(componentTypes, at) && !ContainAnyExcludeComponents(componentTypes, at))
                    {
                        m_ArchetypeAspectMap.Add(archetype.StableHash, at);
                        aspectTypes.Add(at);
                    }
                }

                componentTypes.Dispose();
            }

            var result = aspectTypes.ToArray(Allocator.Temp);
            aspectTypes.Dispose();
            return result;
        }

        static bool HasAnyQueryComponents(AspectType aspectType)
        {
            using var enumerator = s_AspectTypeInfo.Data.AspectRequiredComponents.GetValuesForKey(aspectType);
            return enumerator.MoveNext();
        }

        static bool ContainAllRequiredComponents(NativeArray<ComponentType> componentTypes, AspectType aspectType)
        {
            foreach (var comp in s_AspectTypeInfo.Data.AspectRequiredComponents.GetValuesForKey(aspectType))
            {
                if (!componentTypes.Contains(comp))
                    return false;
            }

            return true;
        }

        static bool ContainAnyExcludeComponents(NativeArray<ComponentType> componentTypes, AspectType aspectType)
        {
            foreach (var comp in s_AspectTypeInfo.Data.AspectExcludedComponents.GetValuesForKey(aspectType))
            {
                if (componentTypes.Contains(comp))
                    return true;
            }

            return false;
        }
    }
}
