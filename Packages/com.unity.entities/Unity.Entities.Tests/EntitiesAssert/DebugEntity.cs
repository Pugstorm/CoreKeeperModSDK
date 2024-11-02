using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using Unity.Collections;

namespace Unity.Entities.Tests
{
    public class DebugEntity
    {
        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        readonly DebugComponent[] m_Components;

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public string Name { get; }

        public Entity Entity { get; }

        [DebuggerBrowsable(DebuggerBrowsableState.RootHidden)]
        public IReadOnlyList<DebugComponent> Components => m_Components;

        public DebugEntity(Entity entity, params DebugComponent[] components)
        {
            Entity = entity;
            Name = entity.ToString();
            m_Components = components;
        }

        public DebugEntity(EntityManager entityManager, Entity entity)
        {
            #if UNITY_EDITOR
            Name = entityManager.GetName(entity);
            #endif
            if (string.IsNullOrEmpty(Name))
                Name = entity.ToString();

            Entity = entity;

            using (var componentTypes = entityManager.GetComponentTypes(entity))
            {
                m_Components = new DebugComponent[componentTypes.Length];

                for (var i = 0; i < componentTypes.Length; ++i)
                    m_Components[i] = new DebugComponent(entityManager, entity, componentTypes[i]);
            }
        }

        public static List<DebugEntity> GetAllEntitiesWithSystems(EntityManager entityManager)
        {
            using (var entities = entityManager.GetAllEntities(Allocator.Temp, EntityManager.GetAllEntitiesOptions.IncludeSystems))
            {
                var debugEntities = new List<DebugEntity>(entities.Length);

                foreach (var entity in entities)
                    debugEntities.Add(new DebugEntity(entityManager, entity));

                // consider rando-sorting debugEntities if a certain command line flag is set to detect instabilities

                debugEntities.Sort((x, y) => x.Entity.Index.CompareTo(y.Entity.Index));

                return debugEntities;
            }
        }

        public static List<DebugEntity> GetAllEntities(EntityManager entityManager)
        {
            using (var entities = entityManager.GetAllEntities())
            {
                var debugEntities = new List<DebugEntity>(entities.Length);

                foreach (var entity in entities)
                    debugEntities.Add(new DebugEntity(entityManager, entity));

                // consider rando-sorting debugEntities if a certain command line flag is set to detect instabilities

                debugEntities.Sort((x, y) => x.Entity.Index.CompareTo(y.Entity.Index));

                return debugEntities;
            }
        }

        public override string ToString() => $"{Entity} {Name} ({m_Components.Length} components)";
    }

    public struct DebugComponent
    {
        public Type Type;

        // if IBufferElementData, this will be a object[] but Type will still be typeof(T)
        public object Data;

        public unsafe DebugComponent(EntityManager entityManager, Entity entity, ComponentType componentType)
        {
            Type = componentType.GetManagedType();
            Data = null;

            if (componentType.IsManagedComponent)
            {
                Data = entityManager.GetComponentObject<object>(entity, componentType);
            }
            else if (componentType.IsComponent)
            {
                if (componentType.IsZeroSized)
                    Data = Activator.CreateInstance(Type);
                else
                {
                    var dataPtr = entityManager.GetComponentDataRawRO(entity, componentType.TypeIndex);
                    // this doesn't work for structs that contain BlobAssetReferences
                    Data = Marshal.PtrToStructure((IntPtr)dataPtr, Type);
                }
            }
            else if (componentType.IsSharedComponent)
            {
                try
                {
                    Data = entityManager.GetSharedComponentData(entity, componentType.TypeIndex);
                }
                catch (Exception x) // currently triggered in dots runtime (see GetAllEntities_WithSharedTagEntity)
                {
                    Data = x;
                }
            }
            else if (componentType.IsBuffer)
            {
                var bufferPtr = (byte*)entityManager.GetBufferRawRO(entity, componentType.TypeIndex);
                var length = entityManager.GetBufferLength(entity, componentType.TypeIndex);
                var elementSize = Marshal.SizeOf(Type);

                var array = Array.CreateInstance(Type, length);
                Data = array;

                for (var i = 0; i < length; ++i)
                {
                    var elementPtr = bufferPtr + (elementSize * i);
                    array.SetValue(Marshal.PtrToStructure((IntPtr)elementPtr, Type), i);
                }
            }
            else
                throw new InvalidOperationException("Unsupported ECS data type");
        }

        public override string ToString() => ToString(-1);

        public string ToString(int maxDataLen)
        {
            string str;
            if (Type != null)
                str = Type.Name;
            else if (Data != null)
                str = Data.GetType().Name;
            else
                return "null";

            if (Data != null)
            {
                var dataType = Data.GetType();
                if (Type != null && !typeof(IBufferElementData).IsAssignableFrom(Type) && dataType != Type)
                    str += $"({dataType.Name})";

                if (Data is object[] objects)
                    str += $"=len:{objects.Length}";
                else if (Data is UnityEngine.Component component)
                    str += $"={component.gameObject.name}";
                else if (!dataType.IsValueType || !Equals(Data, Activator.CreateInstance(dataType)))
                {
                    var dataStr = Data.ToString();
                    if (dataStr != dataType.ToString()) // default ToString just returns full type name
                    {
                        if (maxDataLen >= 0 && dataStr.Length > maxDataLen)
                        {
                            if (maxDataLen > 3)
                                dataStr = dataStr.Substring(0, maxDataLen - 3) + "...";
                            else
                                dataStr = dataStr.Substring(0, maxDataLen);
                        }

                        str += $"={dataStr}";
                    }
                }
            }
            else
                str += "=null";

            return str;
        }
    }

    public static class DebugEntityExtensions
    {
        public static bool HasComponent<T>(this DebugEntity de) => IndexOfComponent<T>(de) != -1;

        public static int IndexOfComponent<T>(this DebugEntity de)
        {
            int idx = 0;
            foreach (var c in de.Components)
            {
                if (c.Type == typeof(T))
                    return idx;
                idx++;
            }

            return -1;
        }

        public static T GetComponent<T>(this DebugEntity de)
        {
            if (!TryGetComponent<T>(de, out var c))
                throw new Exception($"Could not find component {typeof(T)}");
            return c;
        }

        public static bool TryGetComponent<T>(this DebugEntity de, out T component)
        {
            int idx = de.IndexOfComponent<T>();
            if (idx == -1)
            {
                component = default;
                return false;
            }
            component = (T) de.Components[idx].Data;
            return true;
        }
    }
}
