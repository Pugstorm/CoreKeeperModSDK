#if !UNITY_DISABLE_MANAGED_COMPONENTS
using System;
using UnityEngine;
using UnityObject = UnityEngine.Object;

namespace Unity.Entities
{
    class CompanionReference : IComponentData, IEquatable<CompanionReference>, IDisposable
    {
        public UnityObjectRef<GameObject> Companion;

        public bool Equals(CompanionReference other)
        {
            return Companion == other.Companion;
        }

        public override int GetHashCode()
        {
            return Companion.GetHashCode();
        }

        public void Dispose()
        {
            CompanionLink.DestroyObject(Companion.Id.instanceId);
        }
    }

    struct CompanionLinkTransform : IComponentData
    {
        public UnityObjectRef<Transform> CompanionTransform;
    }

    struct CompanionLink : IComponentData
    {
        public UnityObjectRef<GameObject> Companion;

        public static void DestroyObject(int instanceID)
        {
            var unityObject = Resources.InstanceIDToObject(instanceID);
#if UNITY_EDITOR
            if (Application.isPlaying)
                UnityObject.Destroy(unityObject);
            else
                UnityObject.DestroyImmediate(unityObject);
#else
            UnityObject.Destroy(unityObject);
#endif
        }
    }
}

#endif
