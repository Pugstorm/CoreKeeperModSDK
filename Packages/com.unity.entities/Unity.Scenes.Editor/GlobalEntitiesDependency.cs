using System;
using UnityEngine;
using Hash128 = Unity.Entities.Hash128;

namespace Unity.Scenes
{
    // Used to force rebuilding of all entity scenes (subscenes)
    // When used, this creates an asset that is dependency on the entity scene importers
    // These assets should be committed with the project so that it is compatible with the cache server
    internal class GlobalEntitiesDependency : ScriptableObject
    {
        [SerializeField]
        internal Hash128 cacheGUID;
    }
}
