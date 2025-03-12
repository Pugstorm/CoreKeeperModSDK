using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Unity.Physics.Authoring
{
    interface ITagNames
    {
        IReadOnlyList<string> TagNames { get; }
    }

    /// <summary>
    /// Custom Physics Body Tag Names.
    ///
    /// A scriptable object used for assigning names to <see cref="CustomPhysicsBodyTags"/> and saving them.
    /// </summary>
    [CreateAssetMenu(menuName = "Unity Physics/Custom Physics Body Tag Names", fileName = "Custom Physics Body Tag Names", order = 505)]
    [HelpURL(HelpURLs.CustomPhysicsBodyTagNames)]
    public sealed class CustomPhysicsBodyTagNames : ScriptableObject, ITagNames
    {
        CustomPhysicsBodyTagNames() {}

        /// <summary>
        ///     The names corresponding to the eight tags in a <see cref="CustomPhysicsBodyTags"/> instance.
        /// </summary>
        public IReadOnlyList<string> TagNames => m_TagNames;
        [SerializeField]
        string[] m_TagNames = Enumerable.Range(0, 8).Select(i => string.Empty).ToArray();

        void OnValidate()
        {
            if (m_TagNames.Length != 8)
                Array.Resize(ref m_TagNames, 8);
        }
    }
}
