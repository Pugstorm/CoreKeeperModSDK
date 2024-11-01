using JetBrains.Annotations;
using Unity.Properties;
using UnityEngine;
using UnityEngine.UIElements;

namespace Unity.Entities.UI
{
    abstract class MinAttributeInspectorBase<TElement, TFieldValue, TValue, TAttribute> : BaseFieldAttributeInspector<TElement, TFieldValue, TValue, TAttribute>
        where TElement : BaseField<TFieldValue>, new()
        where TAttribute : PropertyAttribute
    {
        float m_MinValue;

        public override VisualElement Build()
        {
            base.Build();
            m_Field.bindingPath = string.Empty;
            RegisterValueChangedCallback();
            m_MinValue = GetMinValue();
            return m_Field;
        }

        protected abstract float GetMinValue();

        void RegisterValueChangedCallback()
        {
            m_Field.RegisterValueChangedCallback(evt =>
            {
                var input = m_Field as TextInputBaseField<TFieldValue>;
                if (null != input)
                {
                    input.isDelayed = false;
                }
                OnChanged(evt);
                Update();
                if (null != input)
                {
                    input.isDelayed = IsDelayed;
                }
            });
        }

        void OnChanged(ChangeEvent<TFieldValue> evt)
        {
            var newValue = evt.newValue;
            if (!TypeConversion.TryConvert(ref newValue, out float convertedNewValue))
                return;

            var max = Mathf.Max(convertedNewValue, m_MinValue);
            if (TypeConversion.TryConvert(ref max, out TValue value))
            {
                Target = value;
            }
        }

        public override void Update()
        {
            var target = Target;
            if (TypeConversion.TryConvert(ref target, out TFieldValue value) && !value.Equals(m_Field.value))
            {
                m_Field.SetValueWithoutNotify(value);
            }
        }
    }

    abstract class MinValueAttributeInspector<TElement, TFieldValue, TValue> : MinAttributeInspectorBase<TElement, TFieldValue, TValue, MinValueAttribute>
        where TElement : BaseField<TFieldValue>, new()
    {
        protected override float GetMinValue()
            => GetAttribute<MinValueAttribute>().Min;
    }

    abstract class MinAttributeInspector<TElement, TFieldValue, TValue> : MinAttributeInspectorBase<TElement, TFieldValue, TValue, MinAttribute>
        where TElement : BaseField<TFieldValue>, new()
    {
        protected override float GetMinValue()
            => GetAttribute<MinAttribute>().min;
    }

    [UsedImplicitly] class MinFieldSByteAttributeInspector : MinAttributeInspector<IntegerField, int, sbyte> { }
    [UsedImplicitly] class MinFieldByteAttributeInspector : MinAttributeInspector<IntegerField, int, byte> { }
    [UsedImplicitly] class MinFieldShortAttributeInspector : MinAttributeInspector<IntegerField, int, short> { }
    [UsedImplicitly] class MinFieldUShortAttributeInspector : MinAttributeInspector<IntegerField, int, ushort> { }
    [UsedImplicitly] class MinFieldIntAttributeInspector : MinAttributeInspector<IntegerField, int, int> { }
    [UsedImplicitly] class MinFieldUIntAttributeInspector : MinAttributeInspector<LongField, long, uint> { }
    [UsedImplicitly] class MinFieldLongAttributeInspector : MinAttributeInspector<LongField, long, long> { }
    [UsedImplicitly] class MinFieldULongAttributeInspector : MinAttributeInspector<FloatField, float, ulong> { }
    [UsedImplicitly] class MinFieldFloatAttributeInspector : MinAttributeInspector<FloatField, float, float> { }
    [UsedImplicitly] class MinFieldDoubleAttributeInspector : MinAttributeInspector<DoubleField, double, double> { }

    [UsedImplicitly] class MinFieldSByteValueAttributeInspector : MinValueAttributeInspector<IntegerField, int, sbyte> { }
    [UsedImplicitly] class MinFieldByteValueAttributeInspector : MinValueAttributeInspector<IntegerField, int, byte> { }
    [UsedImplicitly] class MinFieldShortValueAttributeInspector : MinValueAttributeInspector<IntegerField, int, short> { }
    [UsedImplicitly] class MinFieldUShortValueAttributeInspector : MinValueAttributeInspector<IntegerField, int, ushort> { }
    [UsedImplicitly] class MinFieldIntValueAttributeInspector : MinValueAttributeInspector<IntegerField, int, int> { }
    [UsedImplicitly] class MinFieldUIntValueAttributeInspector : MinValueAttributeInspector<LongField, long, uint> { }
    [UsedImplicitly] class MinFieldLongValueAttributeInspector : MinValueAttributeInspector<LongField, long, long> { }
    [UsedImplicitly] class MinFieldULongValueAttributeInspector : MinValueAttributeInspector<FloatField, float, ulong> { }
    [UsedImplicitly] class MinFieldFloatValueAttributeInspector : MinValueAttributeInspector<FloatField, float, float> { }
    [UsedImplicitly] class MinFieldDoubleValueAttributeInspector : MinValueAttributeInspector<DoubleField, double, double> { }
}
