using JetBrains.Annotations;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    [UsedImplicitly]
    class LazyLoadReferencePropertyInspector<T> : PropertyInspector<LazyLoadReference<T>>, IExperimentalInspector
        where T : UnityEngine.Object
    {
        ObjectField m_ObjectField;

        public override VisualElement Build()
        {
            var lazyLoadReferenceField = Resources.Templates.LazyLoadReference.Clone();

            var labelElement = lazyLoadReferenceField.Q<Label>("label");
            labelElement.text = DisplayName;

            m_ObjectField = lazyLoadReferenceField.Q<ObjectField>("input");
            m_ObjectField.objectType = typeof(T);
            m_ObjectField.value = Target.asset;
            m_ObjectField.RegisterValueChangedCallback(changeEvent =>
            {
                Target = new LazyLoadReference<T> { asset = (T)changeEvent.newValue };
                NotifyChanged();
            });

            return lazyLoadReferenceField;
        }

        public override void Update()
        {
            if (m_ObjectField.value != Target.asset)
            {
                m_ObjectField.SetValueWithoutNotify(Target.asset);
            }
        }
    }
}
