using UnityEngine;

namespace Unity.Physics.Authoring
{
    /// <summary>
    /// An authoring component to tag the physics colliders used by this entity as unique. It is intended to be used with
    /// Built-In Physics Collider Components. Any collider that is present on a GameObject with this component will be
    /// flagged as unique during the baking process. A unique collider will not share a BlobAssetReference&lt;Collider&gt; with
    /// any other collider.
    /// </summary>
    [Icon(k_IconPath)]
    [AddComponentMenu("Entities/Physics/Force Unique Collider")]
    [HelpURL(HelpURLs.ForceUniqueColliderAuthoring)]
    [DisallowMultipleComponent]
    public class ForceUniqueColliderAuthoring : MonoBehaviour
    {
        const string k_IconPath = "Packages/com.unity.physics/Unity.Physics.Editor/Editor Default Resources/Icons/d_BoxCollider@64.png";
    }
}
