using Unity.Entities.UI;
using UnityEditor;
using UnityEngine;

namespace Unity.Entities.UI
{
    sealed class InspectorContent : ScriptableObject
    {
        public static InspectorContent Show(ContentProvider provider, InspectorContentParameters parameters)
        {
            var dynamicContent = CreateInstance<InspectorContent>();
            dynamicContent.SetContent(new SerializableContent {Provider = provider}, parameters);
            Selection.activeObject = dynamicContent;
            return dynamicContent;
        }

        [SerializeField] SerializableContent m_Content;

        [SerializeField] InspectorContentParameters m_Parameters;
        public SerializableContent Content => m_Content;
        public InspectorContentParameters Parameters => m_Parameters;

        void SetContent(SerializableContent content, InspectorContentParameters parameters)
        {
            m_Parameters = parameters;
            m_Content = content;
        }

        public void ResetContent()
        {
            SetContent(m_Content, m_Parameters);
        }
    }
}
