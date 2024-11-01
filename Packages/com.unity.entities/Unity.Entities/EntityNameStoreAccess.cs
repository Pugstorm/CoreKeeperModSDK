using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Entities;
using UnityEngine;

#if !ENTITY_STORE_V1 && !DOTS_DISABLE_DEBUG_NAMES
internal unsafe struct EntityNameStoreAccessData
{
    public ulong m_NameChangeBitsSequenceNum;
}

internal unsafe struct EntityNameStoreAccess : IDisposable
{
    public const ulong InitialNameChangeBitsSequenceNum = 1;
    public ulong NameChangeBitsSequenceNum => m_Data->m_NameChangeBitsSequenceNum;

    [NativeDisableUnsafePtrRestriction]
    private EntityComponentStore* m_EntityComponentStore;
    private UnsafeHashSet<Entity> m_EntitiesNameSet;
    [NativeDisableUnsafePtrRestriction]
    private EntityNameStoreAccessData* m_Data;

    public EntityNameStoreAccess(EntityComponentStore* componentStore)
    {
        m_EntityComponentStore = componentStore;
        m_Data = Memory.Unmanaged.Allocate<EntityNameStoreAccessData>(Allocator.Persistent);
        m_Data->m_NameChangeBitsSequenceNum = InitialNameChangeBitsSequenceNum;
        m_EntitiesNameSet = new UnsafeHashSet<Entity>(1000, Allocator.Persistent);
    }

    public bool IsCreated => m_Data != null;

    public void Dispose()
    {
        Memory.Unmanaged.Free(m_Data, Allocator.Persistent);
        m_EntitiesNameSet.Dispose();

        m_Data = null;
        m_EntityComponentStore = null;
    }

    public ulong IncNameChangeBitsVersion()
    {
        m_Data->m_NameChangeBitsSequenceNum++;
        return m_Data->m_NameChangeBitsSequenceNum;
    }

    public void SetNameChangeBitsVersion(ulong nameChangeBitsVersion)
    {
        m_Data->m_NameChangeBitsSequenceNum = nameChangeBitsVersion;
    }

    public EntityName GetEntityNameByEntityIndex(int index)
    {
        return EntityComponentStore.s_entityStore.Data.GetEntityName(index);
    }

    public EntityName GetEntityName(Entity entity)
    {
        return EntityComponentStore.s_entityStore.Data.GetEntityName(entity);
    }

    public void SetEntityName(Entity entity, EntityName name)
    {
        EntityComponentStore.s_entityStore.Data.SetEntityName(entity, name);
    }

    public int CountEntitiesWithNamesSet()
    {
        return m_EntitiesNameSet.Count;
    }

    public void ResetEntitiesWithNamesSet()
    {
        m_EntitiesNameSet.Clear();
    }

    public void AddEntityWithNameSet(Entity entity)
    {
        m_EntitiesNameSet.Add(entity);
    }

    public void RemoveEntityWithNameSet(Entity entity)
    {
        m_EntitiesNameSet.Remove(entity);
    }

    public void RemoveEntityWithNameSet(Entity* entities, int count)
    {
        for (int i = 0; i < count; ++i)
        {
            m_EntitiesNameSet.Remove(entities[i]);
        }
    }

    public UnsafeHashSet<Entity>.ReadOnly GetEntityWithNameSetRO()
    {
        return m_EntitiesNameSet.AsReadOnly();
    }
}
#endif
