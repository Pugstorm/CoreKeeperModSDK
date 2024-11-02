using System;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.Tests
{
    public class AssetReferenceTests : ECSTestsFixture
    {
        struct SharedComponentWithAssetReference : ISharedComponentData , IEquatable<SharedComponentWithAssetReference>
        {
            public TextAsset Target;

            public bool Equals(SharedComponentWithAssetReference other)
            {
                return Target == other.Target;
            }

            public override int GetHashCode()
            {
                return ReferenceEquals(Target, null) ? 0 : Target.GetHashCode();
            }
        }

        [Test]
        public void SharedComponents_ReferencingAssets_PreventUnloadBy_UnloadUnusedAssets()
        {
            var e = m_Manager.CreateEntity();
            var sharedComponent = new SharedComponentWithAssetReference {Target = new TextAsset()};
            m_Manager.AddSharedComponentManaged(e, sharedComponent);
            sharedComponent.Target = null;
            EditorUtility.UnloadUnusedAssetsImmediate();
            Assert.IsFalse(m_Manager.GetSharedComponentManaged<SharedComponentWithAssetReference>(e).Target == null);
        }

#if !UNITY_DISABLE_MANAGED_COMPONENTS
        class ManagedComponentWithAssetReference : IComponentData
        {
            public TextAsset Target;
        }

        [Test]
        public void ManagedComponents_ReferencingAssets_PreventUnloadBy_UnloadUnusedAssets()
        {
            var e = m_Manager.CreateEntity();
            var managedComponent = new ManagedComponentWithAssetReference {Target = new TextAsset()};
            m_Manager.AddComponentData(e, managedComponent);
            managedComponent = null;
            EditorUtility.UnloadUnusedAssetsImmediate();
            Assert.IsFalse(m_Manager.GetComponentData<ManagedComponentWithAssetReference>(e).Target == null);
        }
#endif
    }
}
