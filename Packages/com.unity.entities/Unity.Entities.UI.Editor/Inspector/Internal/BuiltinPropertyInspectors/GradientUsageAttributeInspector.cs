using JetBrains.Annotations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    [UsedImplicitly]
    class GradientUsageAttributeInspector : BaseFieldAttributeInspector<GradientField, Gradient, GradientUsageAttribute>
    {
        public override VisualElement Build()
        {
            var element = base.Build();
            // GradientField.hdr is not yet supported.
//            var usage = GetAttribute<GradientUsageAttribute>();
//            m_Field.hdr = usage.hdr;
            return element;
        }
    }
}
