using Unity.Entities.UI;
using UnityEditor;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    [CustomEditor(typeof(InspectorContent), false)]
    class InspectorContentEditor : UnityEditor.Editor
    {
        class UpdateBinding : IBinding
        {
            public DisplayContent DisplayContent;
            public UnityEditor.Editor Editor;

            void IBinding.PreUpdate() { }

            void IBinding.Update()
            {
                DisplayContent.Update();
                if (!DisplayContent.IsValid)
                {
                    DestroyImmediate(Editor);
                }

                // We are saving here because we want to store the data inside the scriptable object window so that it
                // survives domain reloads (global selection is not persisted across Unity sessions).
                DisplayContent.Content.Save();
            }

            void IBinding.Release() { }
        }

        internal InspectorContent Target => (InspectorContent) target;
        internal DisplayContent DisplayContent;
        internal BindableElement m_Root;

        // Invoked by the Unity update loop
        protected override void OnHeaderGUI()
        {
            // Intentionally left empty.
        }

        //Invoked by the Unity loop
        public override bool UseDefaultMargins() => DisplayContent.InspectionContext.UseDefaultMargins;

        // Invoked by the Unity update loop
        public override VisualElement CreateInspectorGUI()
        {
            DisplayContent = new DisplayContent(Target.Content)
            {
                InspectionContext =
                {
                    ApplyInspectorStyling = Target.Parameters.ApplyInspectorStyling,
                    UseDefaultMargins = Target.Parameters.UseDefaultMargins
                }
            };

            var content = DisplayContent.CreateGUI();
            DisplayContent.Content.Load();
            DisplayContent.Update();

            m_Root = new BindableElement();
            m_Root.Add(content);
            m_Root.binding = new UpdateBinding
            {
                DisplayContent = DisplayContent,
                Editor = this
            };
            return m_Root;
        }
    }
}
