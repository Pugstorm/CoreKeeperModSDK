using System;
using UnityEngine.ResourceManagement.Util;
using UnityEngine.Serialization;

namespace UnityEngine.AddressableAssets.ResourceLocators
{
    /// <summary>
    /// Serializable location data.  This is used for the locations of the content catalogs.
    /// </summary>
    [Serializable]
    public class ResourceLocationData
    {
        [FormerlySerializedAs("m_keys")]
        [SerializeField]
        string[] m_Keys;

        /// <summary>
        /// The collection of keys for this location.
        /// </summary>
        public string[] Keys
        {
            get { return m_Keys; }
        }

        [FormerlySerializedAs("m_internalId")]
        [SerializeField]
        string m_InternalId;

        /// <summary>
        /// The internal id.
        /// </summary>
        public string InternalId
        {
            get { return m_InternalId; }
        }

        [FormerlySerializedAs("m_provider")]
        [SerializeField]
        string m_Provider;

        /// <summary>
        /// The provider id.
        /// </summary>
        public string Provider
        {
            get { return m_Provider; }
        }

        [FormerlySerializedAs("m_dependencies")]
        [SerializeField]
        string[] m_Dependencies;

        /// <summary>
        /// The collection of dependencies for this location.
        /// </summary>
        public string[] Dependencies
        {
            get { return m_Dependencies; }
        }

        [SerializeField]
        SerializedType m_ResourceType;

        /// <summary>
        /// The type of the resource for the location.
        /// </summary>
        public Type ResourceType
        {
            get { return m_ResourceType.Value; }
        }

        [SerializeField]
        byte[] SerializedData;

        object _Data;

        /// <summary>
        /// The optional arbitrary data stored along with location
        /// </summary>
        public object Data
        {
            get
            {
                if (_Data == null)
                {
                    if (SerializedData == null || SerializedData.Length <= 0)
                        return null;
                    _Data = Utility.SerializationUtilities.ReadObjectFromByteArray(SerializedData, 0);
                }

                return _Data;
            }
            set
            {
                var tmp = new System.Collections.Generic.List<byte>();
                Utility.SerializationUtilities.WriteObjectToByteList(value, tmp);
                SerializedData = tmp.ToArray();
            }
        }

        /// <summary>
        /// Construct a new ResourceLocationData object.
        /// </summary>
        /// <param name="keys">Array of keys for the location.  This must contain at least one item.</param>
        /// <param name="id">The internal id.</param>
        /// <param name="provider">The provider id.</param>
        /// <param name="t">The resource object type.</param>
        /// <param name="dependencies">Optional array of dependencies.</param>
        public ResourceLocationData(string[] keys, string id, Type provider, Type t, string[] dependencies = null)
        {
            m_Keys = keys;
            m_InternalId = id;
            m_Provider = provider == null ? "" : provider.FullName;
            m_Dependencies = dependencies == null ? new string[0] : dependencies;
            m_ResourceType = new SerializedType() {Value = t};
        }
    }
}
