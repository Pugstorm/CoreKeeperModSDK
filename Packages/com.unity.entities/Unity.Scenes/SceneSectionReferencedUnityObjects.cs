using System;
using Unity.Entities;
using Unity.Entities.Serialization;

namespace Unity.Scenes
{
    [Serializable]
    internal struct SceneSectionReferencedUnityObjects : ISharedComponentData, IEquatable<SceneSectionReferencedUnityObjects>
#if !UNITY_EDITOR
        , IRefCounted
#endif
    {
        private UntypedWeakReferenceId _sceneBundleHandles;

        public SceneSectionReferencedUnityObjects(UntypedWeakReferenceId bundles)
        {
            _sceneBundleHandles = bundles;
        }

#if !UNITY_EDITOR
        public void Release() => Entities.Content.RuntimeContentManager.ReleaseObjectAsync(_sceneBundleHandles);
        public void Retain() => Entities.Content.RuntimeContentManager.LoadObjectAsync(_sceneBundleHandles);
#endif

        public bool Equals(SceneSectionReferencedUnityObjects other) => _sceneBundleHandles.Equals(other._sceneBundleHandles);
        public override int GetHashCode() => _sceneBundleHandles.GetHashCode();
    }
}

