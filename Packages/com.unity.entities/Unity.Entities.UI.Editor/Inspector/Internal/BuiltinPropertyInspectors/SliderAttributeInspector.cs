using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    abstract class
        SliderAttributeInspectorBase<TSlider, TFieldType, TValue> : BaseFieldAttributeInspector<TSlider, TFieldType, TValue, RangeAttribute>
        where TSlider : BaseSlider<TFieldType>, new()
        where TFieldType : IComparable<TFieldType>
    {
        protected abstract float LowValue { get; }
        protected abstract float HighValue { get; }

        FloatField m_ValueField;

        public override VisualElement Build()
        {
            base.Build();
            var range = DrawerAttribute;
            var lowValue = Mathf.Max(range.min, LowValue);
            var highValue = Mathf.Min(range.max, HighValue);
            m_Field.lowValue = TypeConversion.Convert<float, TFieldType>(ref lowValue);
            m_Field.highValue = TypeConversion.Convert<float, TFieldType>(ref highValue);

            var root = new VisualElement();
            root.Add(m_Field);
            m_Field.style.flexGrow = 1;
            m_ValueField = new FloatField
            {
                name = Name,
                label = null,
                tooltip = Tooltip,
            };
            m_ValueField.formatString = "0.##";
            m_ValueField.RegisterValueChangedCallback(OnChanged);
            m_ValueField.style.flexGrow = 0;
            m_ValueField.style.width = 50;
            root.Add(m_ValueField);
            root.style.flexDirection = FlexDirection.Row;
            return root;
        }

        void OnChanged(ChangeEvent<float> evt)
        {
            var clampedValue = Mathf.Clamp(evt.newValue, DrawerAttribute.min, DrawerAttribute.max);
            var value = TypeConversion.Convert<float, TValue>(ref clampedValue);
            if (!EqualityComparer<TValue>.Default.Equals(value, Target))
            {
                Target = value;
                Update();
            }
        }

        public override void Update()
        {
            var target = Target;
            var value = TypeConversion.Convert<TValue, float>(ref target);
            if (value != m_ValueField.value)
            {
                m_ValueField.SetValueWithoutNotify(value);
            }
        }
    }

    [UsedImplicitly]
    sealed class SByteSliderAttributeInspector : SliderAttributeInspectorBase<SliderInt, int, sbyte>
    {
        protected override float LowValue { get; } = sbyte.MinValue;
        protected override float HighValue { get; } = sbyte.MaxValue;
    }

    [UsedImplicitly]
    sealed class ByteSliderAttributeInspector : SliderAttributeInspectorBase<SliderInt, int, byte>
    {
        protected override float LowValue { get; } = byte.MinValue;
        protected override float HighValue { get; } = byte.MaxValue;
    }

    [UsedImplicitly]
    sealed class ShortSliderAttributeInspector : SliderAttributeInspectorBase<SliderInt, int, short>
    {
        protected override float LowValue { get; } = short.MinValue;
        protected override float HighValue { get; } = short.MaxValue;
    }

    [UsedImplicitly]
    sealed class UShortSliderAttributeInspector : SliderAttributeInspectorBase<SliderInt, int, ushort>
    {
        protected override float LowValue { get; } = ushort.MinValue;
        protected override float HighValue { get; } = ushort.MaxValue;
    }

    [UsedImplicitly]
    sealed class SliderIntAttributeInspector : SliderAttributeInspectorBase<SliderInt, int, int>
    {
        protected override float LowValue { get; } = int.MinValue;
        protected override float HighValue { get; } = int.MaxValue;
    }

    [UsedImplicitly]
    class SliderAttributeInspector : SliderAttributeInspectorBase<Slider, float, float>
    {
        protected override float LowValue { get; } = float.MinValue;
        protected override float HighValue { get; } = float.MaxValue;
    }

    [UsedImplicitly]
    class LongSliderAttributeInspector : SliderAttributeInspectorBase<Slider, float, long>
    {
        protected override float LowValue { get; } = long.MinValue;
        protected override float HighValue { get; } = long.MaxValue;
    }

    [UsedImplicitly]
    class ULongSliderAttributeInspector : SliderAttributeInspectorBase<Slider, float, ulong>
    {
        protected override float LowValue { get; } = ulong.MinValue;
        protected override float HighValue { get; } = ulong.MaxValue;
    }

    [UsedImplicitly]
    class UIntSliderAttributeInspector : SliderAttributeInspectorBase<Slider, float, uint>
    {
        protected override float LowValue { get; } = uint.MinValue;
        protected override float HighValue { get; } = uint.MaxValue;
    }

    [UsedImplicitly]
    class DoubleSliderAttributeInspector : SliderAttributeInspectorBase<Slider, float, double>
    {
        protected override float LowValue { get; } = float.MinValue;
        protected override float HighValue { get; } = float.MaxValue;
    }
}
