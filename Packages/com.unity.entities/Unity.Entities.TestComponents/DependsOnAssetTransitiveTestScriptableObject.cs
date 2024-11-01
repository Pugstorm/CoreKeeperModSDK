using UnityEngine;

namespace Unity.Entities.Tests
{
    //[CreateAssetMenu(fileName = "Data", menuName = "ScriptableObjects/SpawnManagerScriptableObject", order = 1)]
    public class DependsOnAssetTransitiveTestScriptableObject : ScriptableObject
    { 
        public int SelfValue;
    }
}
