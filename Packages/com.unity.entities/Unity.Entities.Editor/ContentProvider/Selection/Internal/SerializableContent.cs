using System;
using Unity.Serialization.Json;
using UnityEngine;

namespace Unity.Entities.UI
{
    [Serializable]
    class SerializableContent
    {
        [SerializeField, HideInInspector] string m_Data;

        [NonSerialized] public ContentProvider Provider;

        public string Name => Provider?.Name;

        public void Load()
        {
            if (string.IsNullOrEmpty(m_Data))
                return;

            if (!JsonSerialization.TryFromJsonOverride(m_Data, ref Provider, out var events))
            {
                foreach (var exception in events.Exceptions)
                {
                    Debug.LogException((Exception) exception.Payload);
                }

                foreach (var warnings in events.Warnings)
                {
                    Debug.LogWarning(warnings.Payload);
                }

                foreach (var logs in events.Logs)
                {
                    Debug.Log(logs.Payload);
                }
            }
        }

        public void Save()
        {
            m_Data = null != Provider
                ? JsonSerialization.ToJson(Provider)
                : string.Empty;
        }
    }
}
