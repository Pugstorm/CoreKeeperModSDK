using PugMod;
using Unity.Entities;
using Unity.Transforms;
using UnityEngine;

public class EnemySpawner : IMod
{
    private float timer;
    
    public void EarlyInit()
    {
    }

    public void Init()
    {
        timer = 10f;
    }

    public void Shutdown()
    {
    }

    public void ModObjectLoaded(Object obj)
    {
    }

    public void Update()
    {
        if (API.Client.LocalPlayer == null)
        {
            timer = 10f;
            return;
        }
        
        timer -= Time.deltaTime;
        if (timer < 0)
        {
            timer = 30f;
            var pos = API.Client.LocalPlayer.transform.position - API.Rendering.RenderOffset;
            API.Server.InstantiateObject((int)API.Authoring.GetObjectID("Enemy1"), 0, pos);
        }
    }
}