using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    abstract class BaseFieldPropertyInspector<TField, TFieldValue, TValue> : PropertyInspector<TValue>
        where TField : BaseField<TFieldValue>, new()
    {
        protected TField m_Field;

        public override VisualElement Build()
        {
            m_Field = new TField
            {
                name = Name,
                label = DisplayName,
                tooltip = Tooltip,
                bindingPath = "."
            };
            return m_Field;
        }
    }

    abstract class BaseFieldPropertyInspector<TField, TValue> : BaseFieldPropertyInspector<TField, TValue, TValue>
        where TField : BaseField<TValue>, new()
    {
    }

    abstract class BaseFieldAttributeInspector<TField, TFieldValue, TValue, TAttribute> : PropertyInspector<TValue, TAttribute>
        where TField : BaseField<TFieldValue>, new()
        where TAttribute : UnityEngine.PropertyAttribute
    {
        protected TField m_Field;

        public override VisualElement Build()
        {
            m_Field = new TField
            {
                name = Name,
                label = DisplayName,
                tooltip = Tooltip,
                bindingPath = "."
            };
            return m_Field;
        }
    }

    abstract class BaseFieldAttributeInspector<TField, TValue, TAttribute> : BaseFieldAttributeInspector<TField, TValue, TValue, TAttribute>
        where TField : BaseField<TValue>, new()
        where TAttribute : UnityEngine.PropertyAttribute
    {
    }
}
