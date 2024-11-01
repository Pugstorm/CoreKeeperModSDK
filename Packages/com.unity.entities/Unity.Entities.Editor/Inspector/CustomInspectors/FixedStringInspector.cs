using System;
using JetBrains.Annotations;
using Unity.Collections;
using Unity.Entities.UI;
using UnityEngine.UIElements;

namespace Unity.Entities.Editor.Inspectors
{
    abstract class FixedStringInspectorBase<T> : PropertyInspector<T>
        where T : struct, IEquatable<T>
    {
        TextField m_TextField;
        T m_LastValue;

        protected abstract int MaxLength { get; }
        protected abstract string Value { get; set; }

        public override VisualElement Build()
        {
            m_TextField = new TextField(DisplayName)
            {
                isDelayed = true
            };

            if (IsReadOnly)
            {
                m_TextField.SetEnabled(true);
            }

            m_TextField.RegisterValueChangedCallback(value =>
            {
                m_LastValue = Target;
                Value = value.newValue;
            });

            m_TextField.maxLength = MaxLength;
            m_LastValue = Target;
            m_TextField.SetValueWithoutNotify(Value);

            InspectorUtility.AddRuntimeBar(m_TextField);
            return m_TextField;
        }

        public override void Update()
        {
            if (!m_LastValue.Equals(Target))
            {
                m_LastValue = Target;
                m_TextField.SetValueWithoutNotify(Value);
            }
        }
    }

    [UsedImplicitly]
    class FixedString32Inspector : FixedStringInspectorBase<FixedString32Bytes>
    {
        protected override int MaxLength => FixedString32Bytes.UTF8MaxLengthInBytes;

        protected override string Value
        {
            get => Target.ToString();
            set => Target = (FixedString32Bytes) value;
        }
    }

    [UsedImplicitly]
    class FixedString64Inspector : FixedStringInspectorBase<FixedString64Bytes>
    {
        protected override int MaxLength => FixedString64Bytes.UTF8MaxLengthInBytes;

        protected override string Value
        {
            get => Target.ToString();
            set => Target = (FixedString64Bytes) value;
        }
    }

    [UsedImplicitly]
    class FixedString128Inspector : FixedStringInspectorBase<FixedString128Bytes>
    {
        protected override int MaxLength => FixedString128Bytes.UTF8MaxLengthInBytes;

        protected override string Value
        {
            get => Target.ToString();
            set => Target = (FixedString128Bytes) value;
        }
    }

    [UsedImplicitly]
    class FixedString512Inspector : FixedStringInspectorBase<FixedString512Bytes>
    {
        protected override int MaxLength => FixedString512Bytes.UTF8MaxLengthInBytes;

        protected override string Value
        {
            get => Target.ToString();
            set => Target = (FixedString512Bytes) value;
        }
    }

    [UsedImplicitly]
    class FixedString4096Inspector : FixedStringInspectorBase<FixedString4096Bytes>
    {
        protected override int MaxLength => FixedString4096Bytes.UTF8MaxLengthInBytes;

        protected override string Value
        {
            get => Target.ToString();
            set => Target = (FixedString4096Bytes) value;
        }
    }
}
