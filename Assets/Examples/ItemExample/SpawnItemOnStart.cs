using PugMod;
using Unity.Mathematics;
using UnityEngine;

public class SpawnItemOnStart : IMod
{
    private bool hasSpawned = false;
    private float timer = 0;
    
    public void EarlyInit()
    {
    }

    public void Init()
    {
    }

    public void Shutdown()
    {
    }

    public void ModObjectLoaded(Object obj)
    {
    }

    public void Update()
    {
        if (API.Server.World == null || hasSpawned)
        {
            return;
        }
        
        // Wait a bit until the world is loaded
        timer += Time.deltaTime;
        if (timer < 5)
        {
            return;
        }

        hasSpawned = true;
        Debug.Log("Spawn Sword1");
        API.Server.DropObject((int)API.Authoring.GetObjectID("Sword1"), 0, 250, float3.zero);
    }
}
